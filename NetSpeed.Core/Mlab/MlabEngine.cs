using System.Globalization;
using System.Net;
using System.Runtime.InteropServices;
using NetSpeed.Core.Measurement;

namespace NetSpeed.Core.Mlab;

public sealed class MlabEngineOptions
{
    /// <summary>Identifies the client to M-Lab (client_name query parameter and User-Agent).</summary>
    public string ClientName { get; init; } = "netspeed-cmdpal";

    public string ClientVersion { get; init; } = "0.0.0";

    /// <summary>Parallel TCP streams for MSAK. ndt7 always uses one.</summary>
    public int Streams { get; init; } = 4;

    /// <summary>Duration of each of the download and upload phases.</summary>
    public TimeSpan PhaseDuration { get; init; } = TimeSpan.FromSeconds(10);

    /// <summary>Initial seconds of each phase excluded from the headline (steady-state) value.</summary>
    public double WarmupSeconds { get; init; } = 2.0;

    public TimeSpan ConnectTimeout { get; init; } = TimeSpan.FromSeconds(10);

    /// <summary>TCP-handshake samples taken before the test for idle latency and jitter.</summary>
    public int IdlePingCount { get; init; } = 10;

    public string UserAgent => $"{ClientName}/{ClientVersion} (+https://github.com/mrgenty/NetSpeed-CmdPal)";
}

/// <summary>
/// M-Lab engine: Locate v2 → MSAK throughput1 (multi-stream); falls back to ndt7 when MSAK has
/// no capacity or every MSAK server refuses the connection.
/// </summary>
public sealed class MlabEngine : ISpeedEngine
{
    private const int HttpsPort = 443;
    private const string LibraryName = "netspeed-core";

    private readonly LocateClient _locate;
    private readonly MlabEngineOptions _options;

    public MlabEngine(HttpClient http, MlabEngineOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _locate = new LocateClient(http, options.UserAgent);
    }

    public string Name => "M-Lab";

    public async Task<SpeedResult> RunAsync(IProgress<SpeedProgress>? progress, CancellationToken cancellationToken)
    {
        SpeedTestException? lastError = null;
        foreach (MlabProtocol protocol in new[] { MlabProtocol.Msak, MlabProtocol.Ndt7 })
        {
            progress?.Report(new SpeedProgress(SpeedPhase.Locating, 0.01, null, $"Finding the nearest M-Lab server ({protocol.DisplayName})…"));

            IReadOnlyList<MlabTarget> targets;
            try
            {
                targets = await _locate.NearestAsync(protocol, cancellationToken).ConfigureAwait(false);
            }
            catch (SpeedTestException ex) when (ex.Kind is SpeedTestErrorKind.NoCapacity or SpeedTestErrorKind.LocateFailed)
            {
                lastError = ex;
                continue;
            }

            progress?.Report(new SpeedProgress(SpeedPhase.Locating, 0.03, null, "Picking the closest server…"));
            foreach ((MlabTarget target, IPAddress? address) in await RankAsync(targets, cancellationToken).ConfigureAwait(false))
            {
                try
                {
                    return await RunOnTargetAsync(protocol, target, address, progress, cancellationToken).ConfigureAwait(false);
                }
                catch (ServerUnavailableException ex)
                {
                    lastError = new SpeedTestException(SpeedTestErrorKind.AllServersRejected, ex.Message, ex);
                }
            }
        }

        throw lastError is { Kind: SpeedTestErrorKind.AllServersRejected }
            ? new SpeedTestException(SpeedTestErrorKind.AllServersRejected, "All nearby M-Lab servers are busy or refused the test. Please try again later.", lastError)
            : lastError ?? new SpeedTestException(SpeedTestErrorKind.LocateFailed, "No M-Lab server available.");
    }

    internal static IReadOnlyList<KeyValuePair<string, string>> BuildQuery(MlabProtocol protocol, MlabEngineOptions options, int streams)
    {
        List<KeyValuePair<string, string>> query = [];
        if (protocol.Kind == MlabProtocolKind.Msak)
        {
            // Required by msak-server; duration is in milliseconds.
            query.Add(new("streams", streams.ToString(CultureInfo.InvariantCulture)));
            query.Add(new("duration", ((long)options.PhaseDuration.TotalMilliseconds).ToString(CultureInfo.InvariantCulture)));
        }

        query.Add(new("client_name", options.ClientName));
        query.Add(new("client_version", options.ClientVersion));
        query.Add(new("client_library_name", LibraryName));
        query.Add(new("client_library_version", options.ClientVersion));
        query.Add(new("client_os", OperatingSystem.IsWindows() ? "windows" : RuntimeInformation.OSDescription));
        query.Add(new("client_arch", RuntimeInformation.OSArchitecture.ToString().ToLowerInvariant()));
        return query;
    }

    private static async Task<IReadOnlyList<(MlabTarget Target, IPAddress? Address)>> RankAsync(IReadOnlyList<MlabTarget> targets, CancellationToken cancellationToken)
    {
        (MlabTarget Target, IPAddress? Address, double? Rtt)[] probes = await Task.WhenAll(targets.Select(async target =>
        {
            IPAddress? address = await TcpPinger.ResolveAsync(target.Hostname, cancellationToken).ConfigureAwait(false);
            double? rtt = null;
            if (address is not null)
            {
                List<double> samples = await TcpPinger.PingSeriesAsync(address, HttpsPort, 3, TimeSpan.FromMilliseconds(50), cancellationToken).ConfigureAwait(false);
                rtt = LatencyStats.Median(samples);
            }

            return (target, address, rtt);
        })).ConfigureAwait(false);

        return ServerRanker.Rank(
            [.. probes.Select(p => (p.Target, p.Address))],
            [.. probes.Select(p => p.Rtt)]);
    }

    private async Task<SpeedResult> RunOnTargetAsync(MlabProtocol protocol, MlabTarget target, IPAddress? address, IProgress<SpeedProgress>? progress, CancellationToken cancellationToken)
    {
        string where = FormatPlace(target);
        int streams = protocol.SupportsMultipleStreams ? _options.Streams : 1;
        string detail = $"{protocol.DisplayName} • {streams} stream{(streams == 1 ? string.Empty : "s")} • {where}";

        progress?.Report(new SpeedProgress(SpeedPhase.Latency, 0.05, null, $"Measuring idle latency • {where}"));
        List<double> idle = address is null
            ? []
            : await TcpPinger.PingSeriesAsync(address, HttpsPort, _options.IdlePingCount, TimeSpan.FromMilliseconds(100), cancellationToken).ConfigureAwait(false);

        IReadOnlyList<KeyValuePair<string, string>> query = BuildQuery(protocol, _options, streams);

        PhaseResult download = await PhaseRunner.RunAsync(
            Phase(protocol, TestDirection.Download, target.DownloadUrl, streams, query, address),
            (fraction, mbps) => progress?.Report(new SpeedProgress(SpeedPhase.Download, 0.10 + (0.45 * fraction), mbps, detail)),
            cancellationToken).ConfigureAwait(false);

        PhaseResult upload;
        try
        {
            upload = await PhaseRunner.RunAsync(
                Phase(protocol, TestDirection.Upload, target.UploadUrl, streams, query, address),
                (fraction, mbps) => progress?.Report(new SpeedProgress(SpeedPhase.Upload, 0.55 + (0.45 * fraction), mbps, detail)),
                cancellationToken).ConfigureAwait(false);
        }
        catch (ServerUnavailableException ex)
        {
            // Download already ran on this server: do not silently switch servers mid-test.
            throw new SpeedTestException(SpeedTestErrorKind.Protocol, "The upload test could not start: " + ex.Message, ex);
        }

        bool pinged = idle.Count >= 3;
        double? idleLatency = pinged ? LatencyStats.Median(idle) : download.ServerMinRttMs;
        double? downLoaded = download.LoadedPingsMs.Count >= 3 ? LatencyStats.Median(download.LoadedPingsMs) : LatencyStats.Median(download.ServerRttMs);
        double? upLoaded = upload.LoadedPingsMs.Count >= 3 ? LatencyStats.Median(upload.LoadedPingsMs) : null;

        return new SpeedResult
        {
            EngineName = Name,
            Protocol = protocol.DisplayName,
            Timestamp = DateTimeOffset.Now,
            DownloadMbps = download.SteadyMbps,
            DownloadAverageMbps = download.AverageMbps,
            UploadMbps = upload.SteadyMbps,
            UploadAverageMbps = upload.AverageMbps,
            IdleLatencyMs = idleLatency,
            JitterMs = pinged ? LatencyStats.Jitter(idle) : null,
            DownloadLoadedLatencyMs = downLoaded,
            UploadLoadedLatencyMs = upLoaded,
            LatencyMethod = pinged ? "TCP handshake to the test server" : "Server-side TCP_INFO RTT (handshake probes unavailable)",
            ServerMachine = target.Machine,
            ServerCity = target.City,
            ServerCountry = target.Country,
            ClientAddress = download.ClientAddress ?? upload.ClientAddress,
            BytesReceived = download.Bytes,
            BytesSent = upload.Bytes,
            Streams = Math.Max(download.StreamsConnected, upload.StreamsConnected),
        };
    }

    private PhaseOptions Phase(MlabProtocol protocol, TestDirection direction, Uri url, int streams, IReadOnlyList<KeyValuePair<string, string>> query, IPAddress? address) => new()
    {
        Protocol = protocol,
        Direction = direction,
        Url = url,
        Streams = streams,
        Duration = _options.PhaseDuration,
        WarmupSeconds = _options.WarmupSeconds,
        UserAgent = _options.UserAgent,
        Query = query,
        ConnectTimeout = _options.ConnectTimeout,
        PingAddress = address,
        PingPort = HttpsPort,
    };

    private static string FormatPlace(MlabTarget target)
    {
        string place = string.Join(", ", new[] { target.City, target.Country }.Where(p => !string.IsNullOrWhiteSpace(p)));
        return string.IsNullOrEmpty(place) ? target.Machine : place;
    }
}
