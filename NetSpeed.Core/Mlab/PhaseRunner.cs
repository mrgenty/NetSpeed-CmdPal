using System.Buffers;
using System.Diagnostics;
using System.Net;
using System.Net.WebSockets;
using System.Security.Cryptography;
using NetSpeed.Core.Measurement;

namespace NetSpeed.Core.Mlab;

/// <summary>Thrown when no stream could connect to a server, so the caller can try the next one.</summary>
internal sealed class ServerUnavailableException : Exception
{
    public ServerUnavailableException()
    {
    }

    public ServerUnavailableException(string message)
        : base(message)
    {
    }

    public ServerUnavailableException(string message, Exception? innerException)
        : base(message, innerException)
    {
    }
}

internal sealed class PhaseOptions
{
    public required MlabProtocol Protocol { get; init; }

    public required TestDirection Direction { get; init; }

    public required Uri Url { get; init; }

    public required int Streams { get; init; }

    public required TimeSpan Duration { get; init; }

    public required double WarmupSeconds { get; init; }

    public required string UserAgent { get; init; }

    public required IReadOnlyList<KeyValuePair<string, string>> Query { get; init; }

    public required TimeSpan ConnectTimeout { get; init; }

    /// <summary>When set, TCP-handshake pings run during the phase to measure loaded latency.</summary>
    public IPAddress? PingAddress { get; init; }

    public int PingPort { get; init; } = 443;
}

internal sealed class PhaseResult
{
    public double SteadyMbps { get; init; }

    public double AverageMbps { get; init; }

    public long Bytes { get; init; }

    public int StreamsConnected { get; init; }

    /// <summary>TCP-handshake RTTs (ms) measured during the phase, after the first second.</summary>
    public required List<double> LoadedPingsMs { get; init; }

    /// <summary>Server-side smoothed RTT samples (ms) from TCP_INFO.</summary>
    public required List<double> ServerRttMs { get; init; }

    public double? ServerMinRttMs { get; init; }

    public string? ClientAddress { get; init; }

    public required List<string> Uuids { get; init; }
}

/// <summary>Runs one download or upload phase over one or more WebSocket streams.</summary>
internal static class PhaseRunner
{
    private const int ReceiveBufferSize = 1 << 20;
    private const int MaxTextMessageSize = 1 << 20;
    private static readonly TimeSpan SampleInterval = TimeSpan.FromMilliseconds(100);
    private static readonly TimeSpan PingInterval = TimeSpan.FromMilliseconds(250);
    private static readonly TimeSpan GracePeriod = TimeSpan.FromSeconds(3);
    private static readonly byte[] Payload = CreatePayload();

    /// <param name="onSample">Called about every 100 ms with (phase fraction 0..1, current Mbps).</param>
    public static async Task<PhaseResult> RunAsync(PhaseOptions options, Action<double, double>? onSample, CancellationToken cancellationToken)
    {
        Uri url = TestUriBuilder.AddQuery(options.Url, options.Query);
        int streamCount = options.Protocol.SupportsMultipleStreams ? Math.Max(1, options.Streams) : 1;

        // The phase clock starts when the first stream connects. Each stream starts consuming
        // data as soon as its own handshake completes: the server starts its timed test at
        // upgrade, so waiting for slower streams would distort the measurement.
        Stopwatch clock = Stopwatch.StartNew();
        PhaseClock phase = new(clock);
        using CancellationTokenSource deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(options.ConnectTimeout + options.Duration + GracePeriod);

        StreamCounters[] counters = [.. Enumerable.Range(0, streamCount).Select(_ => new StreamCounters())];
        Task<Exception?>[] streams = [.. counters.Select(c => RunStreamAsync(url, options, c, phase, deadline.Token, cancellationToken))];
        Task all = Task.WhenAll(streams);

        List<(double Seconds, double Rtt)> pings = [];
        using CancellationTokenSource pingCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        Task pingTask = options.PingAddress is null
            ? Task.CompletedTask
            : PingLoopAsync(options.PingAddress, options.PingPort, phase, pings, pingCts.Token);

        // Upload: the server's application counters are authoritative (ndt7 spec recommends
        // server measurements for upload); client-side counters include socket buffers.
        ThroughputTracker clientTracker = new(options.WarmupSeconds);
        ThroughputTracker serverTracker = new(options.WarmupSeconds);
        bool useServer = options.Direction == TestDirection.Upload;

        while (!all.IsCompleted)
        {
            await Task.WhenAny(all, Task.Delay(SampleInterval, cancellationToken)).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            if (phase.Started)
            {
                Sample(phase, counters, clientTracker, serverTracker);
                ThroughputTracker live = useServer && serverTracker.TotalBytes > 0 ? serverTracker : clientTracker;
                onSample?.Invoke(Math.Min(1, phase.Seconds / options.Duration.TotalSeconds), live.CurrentMbps());
            }
        }

        Exception?[] errors = await Task.WhenAll(streams).ConfigureAwait(false);
        await pingCts.CancelAsync().ConfigureAwait(false);
        try
        {
            await pingTask.ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
        }

        int connected = counters.Count(c => c.Connected);
        string what = options.Direction.ToString().ToLowerInvariant();
        if (connected == 0)
        {
            Exception? first = errors.FirstOrDefault(e => e is not null);
            throw new ServerUnavailableException($"Server refused the {what} test: {first?.Message}", first);
        }

        Sample(phase, counters, clientTracker, serverTracker);
        ThroughputTracker final = useServer && counters.Any(c => c.ServerMessages > 0) ? serverTracker : clientTracker;
        if (final.TotalBytes <= 0)
        {
            // Connected, then closed or reset before any payload: not a valid 0 Mbps result.
            throw new ServerUnavailableException($"The server closed the {what} test before any data was transferred.");
        }

        List<double> serverRtt = [];
        foreach (StreamCounters c in counters)
        {
            serverRtt.AddRange(c.RttSnapshot());
        }

        long? minRttUs = counters.Select(c => c.MinRttMicroseconds).Where(v => v is > 0).Min();

        return new PhaseResult
        {
            SteadyMbps = final.SteadyMbps,
            AverageMbps = final.AverageMbps,
            Bytes = final.TotalBytes,
            StreamsConnected = connected,
            LoadedPingsMs = [.. pings.Where(p => p.Seconds >= 1.0).Select(p => p.Rtt)],
            ServerRttMs = serverRtt,
            ServerMinRttMs = minRttUs / 1000d,
            ClientAddress = counters.Select(c => c.ClientAddress).FirstOrDefault(a => a is not null),
            Uuids = [.. counters.Select(c => c.Uuid).OfType<string>().Distinct(StringComparer.Ordinal)],
        };
    }

    private static void Sample(PhaseClock phase, StreamCounters[] counters, ThroughputTracker client, ThroughputTracker server)
    {
        double t = phase.Seconds;
        client.Add(t, counters.Sum(c => Interlocked.Read(ref c.ClientBytes)));
        server.Add(t, counters.Sum(c => Interlocked.Read(ref c.ServerAppBytes)));
    }

    /// <summary>Connects one stream and, as soon as it is up, runs its download or upload loop.</summary>
    /// <returns>The connection error, or null when the stream connected.</returns>
    private static async Task<Exception?> RunStreamAsync(Uri url, PhaseOptions options, StreamCounters counters, PhaseClock phase, CancellationToken deadline, CancellationToken userToken)
    {
        (ClientWebSocket? socket, Exception? error) = await ConnectAsync(url, options, userToken).ConfigureAwait(false);
        if (socket is null)
        {
            return error;
        }

        using (socket)
        {
            counters.Connected = true;
            phase.MarkStarted();
            if (options.Direction == TestDirection.Download)
            {
                await ReceiveLoopAsync(socket, counters, options, closeOnServerClose: true, deadline, userToken).ConfigureAwait(false);
            }
            else
            {
                await UploadAsync(socket, counters, options, phase.Clock.Elapsed, phase.Clock, deadline, userToken).ConfigureAwait(false);
            }
        }

        return null;
    }

    private static async Task<(ClientWebSocket? Socket, Exception? Error)> ConnectAsync(Uri url, PhaseOptions options, CancellationToken cancellationToken)
    {
        ClientWebSocket socket = new();
        socket.Options.AddSubProtocol(options.Protocol.SubProtocol);
        socket.Options.SetRequestHeader("User-Agent", options.UserAgent);
        socket.Options.KeepAliveInterval = TimeSpan.Zero; // Server pings are answered automatically.
        socket.Options.CollectHttpResponseDetails = true;

        using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(options.ConnectTimeout);
        try
        {
            await socket.ConnectAsync(url, timeout.Token).ConfigureAwait(false);
            if (!string.Equals(socket.SubProtocol, options.Protocol.SubProtocol, StringComparison.Ordinal))
            {
                socket.Abort();
                socket.Dispose();
                return (null, new WebSocketException("The server did not accept the " + options.Protocol.SubProtocol + " subprotocol."));
            }

            return (socket, null);
        }
        catch (Exception ex) when (ex is WebSocketException or HttpRequestException or IOException
            || (ex is OperationCanceledException && !cancellationToken.IsCancellationRequested))
        {
            Exception error = socket.HttpStatusCode is HttpStatusCode status and not 0 and not HttpStatusCode.SwitchingProtocols
                ? new WebSocketException($"HTTP {(int)status}", ex)
                : ex is OperationCanceledException ? new TimeoutException("Connection timed out.", ex) : ex;
            socket.Dispose();
            return (null, error);
        }
    }

    /// <summary>
    /// Reads binary (download payload) and text (measurement) messages until the server closes,
    /// the deadline expires, or the connection drops. Abrupt endings are tolerated, as the ndt7
    /// spec recommends; only user cancellation is propagated.
    /// </summary>
    private static async Task ReceiveLoopAsync(
        ClientWebSocket socket,
        StreamCounters counters,
        PhaseOptions options,
        bool closeOnServerClose,
        CancellationToken deadline,
        CancellationToken userToken)
    {
        byte[] buffer = ArrayPool<byte>.Shared.Rent(ReceiveBufferSize);
        ArrayBufferWriter<byte> text = new();
        bool overflow = false;
        try
        {
            while (true)
            {
                ValueWebSocketReceiveResult result = await socket.ReceiveAsync(buffer.AsMemory(), deadline).ConfigureAwait(false);
                switch (result.MessageType)
                {
                    case WebSocketMessageType.Close:
                        if (closeOnServerClose)
                        {
                            await CloseQuietlyAsync(socket).ConfigureAwait(false);
                        }

                        return;

                    case WebSocketMessageType.Binary:
                        if (options.Direction == TestDirection.Download)
                        {
                            Interlocked.Add(ref counters.ClientBytes, result.Count);
                        }

                        break;

                    case WebSocketMessageType.Text:
                        if (!overflow && text.WrittenCount + result.Count <= MaxTextMessageSize)
                        {
                            text.Write(buffer.AsSpan(0, result.Count));
                        }
                        else
                        {
                            overflow = true;
                        }

                        if (result.EndOfMessage)
                        {
                            if (!overflow)
                            {
                                counters.OnMeasurement(options.Protocol.Kind, options.Direction, text.WrittenSpan);
                            }

                            text.Clear();
                            overflow = false;
                        }

                        break;
                }
            }
        }
        catch (OperationCanceledException) when (!userToken.IsCancellationRequested)
        {
            // Deadline reached: the phase is over.
        }
        catch (Exception ex) when (ex is WebSocketException or IOException)
        {
            // Abrupt EOF/RST: keep what was measured.
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    private static async Task UploadAsync(
        ClientWebSocket socket,
        StreamCounters counters,
        PhaseOptions options,
        TimeSpan streamStart,
        Stopwatch clock,
        CancellationToken deadline,
        CancellationToken userToken)
    {
        // The receive loop only reads server measurements; this method owns sending, including Close.
        Task receive = ReceiveLoopAsync(socket, counters, options, closeOnServerClose: false, deadline, userToken);
        int size = options.Protocol.InitialMessageSize;
        long queued = 0;
        try
        {
            // Each stream uploads for the full duration from its own connection time.
            while (socket.State == WebSocketState.Open && !receive.IsCompleted && clock.Elapsed - streamStart < options.Duration)
            {
                await socket.SendAsync(Payload.AsMemory(0, size), WebSocketMessageType.Binary, endOfMessage: true, deadline).ConfigureAwait(false);
                queued += size;
                Interlocked.Add(ref counters.ClientBytes, size);
                size = MessageSizer.Next(size, queued, options.Protocol.MaxMessageSize);
            }
        }
        catch (OperationCanceledException) when (!userToken.IsCancellationRequested)
        {
        }
        catch (Exception ex) when (ex is WebSocketException or IOException)
        {
        }

        await CloseQuietlyAsync(socket).ConfigureAwait(false);
        await receive.ConfigureAwait(false);
    }

    private static async Task CloseQuietlyAsync(ClientWebSocket socket)
    {
        if (socket.State is not (WebSocketState.Open or WebSocketState.CloseReceived))
        {
            return;
        }

        using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(2));
        try
        {
            await socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "Done", timeout.Token).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is WebSocketException or IOException or OperationCanceledException)
        {
        }
    }

    private static async Task PingLoopAsync(IPAddress address, int port, PhaseClock phase, List<(double Seconds, double Rtt)> pings, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            double at = phase.Seconds; // 0 until a stream is up; such samples are discarded.
            if (await TcpPinger.PingAsync(address, port, TimeSpan.FromSeconds(2), cancellationToken).ConfigureAwait(false) is double rtt)
            {
                lock (pings)
                {
                    pings.Add((at, rtt));
                }
            }

            await Task.Delay(PingInterval, cancellationToken).ConfigureAwait(false);
        }
    }

    private static byte[] CreatePayload()
    {
        // ndt7 spec: binary messages SHOULD contain random data.
        byte[] data = new byte[1 << 20];
        RandomNumberGenerator.Fill(data);
        return data;
    }

    /// <summary>Phase time measured from the moment the first stream connected.</summary>
    private sealed class PhaseClock(Stopwatch clock)
    {
        private long _originTicks = -1;

        public Stopwatch Clock => clock;

        public bool Started => Interlocked.Read(ref _originTicks) >= 0;

        public double Seconds
        {
            get
            {
                long origin = Interlocked.Read(ref _originTicks);
                return origin < 0 ? 0 : TimeSpan.FromTicks(clock.Elapsed.Ticks - origin).TotalSeconds;
            }
        }

        public void MarkStarted() => Interlocked.CompareExchange(ref _originTicks, clock.Elapsed.Ticks, -1);
    }

    private sealed class StreamCounters
    {
        public long ClientBytes;
        public long ServerAppBytes;
        private readonly List<double> _rttMs = [];

        public bool Connected { get; set; }

        public int ServerMessages { get; private set; }

        public long? MinRttMicroseconds { get; private set; }

        public string? ClientAddress { get; private set; }

        public string? Uuid { get; private set; }

        public void OnMeasurement(MlabProtocolKind kind, TestDirection direction, ReadOnlySpan<byte> json)
        {
            if (!MeasurementParser.TryParse(kind, direction, json, out ServerMeasurement m))
            {
                return;
            }

            ServerMessages++;
            if (m.AppBytes is long bytes && bytes > Interlocked.Read(ref ServerAppBytes))
            {
                Interlocked.Exchange(ref ServerAppBytes, bytes);
            }

            if (m.RttMicroseconds is long rtt and > 0)
            {
                lock (_rttMs)
                {
                    _rttMs.Add(rtt / 1000d);
                }
            }

            if (m.MinRttMicroseconds is long min and > 0 && (MinRttMicroseconds is null || min < MinRttMicroseconds))
            {
                MinRttMicroseconds = min;
            }

            ClientAddress ??= m.ClientAddress;
            Uuid ??= m.Uuid;
        }

        public double[] RttSnapshot()
        {
            lock (_rttMs)
            {
                return [.. _rttMs];
            }
        }
    }
}
