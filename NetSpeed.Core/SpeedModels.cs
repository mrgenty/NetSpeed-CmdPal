namespace NetSpeed.Core;

/// <summary>A speed-test engine. Implementations must honour cancellation and report progress.</summary>
public interface ISpeedEngine
{
    /// <summary>Human-readable engine name, e.g. "M-Lab".</summary>
    string Name { get; }

    Task<SpeedResult> RunAsync(IProgress<SpeedProgress>? progress, CancellationToken cancellationToken);
}

public enum SpeedPhase
{
    Locating,
    Latency,
    Download,
    Upload,
}

/// <summary>Live progress of a running test.</summary>
/// <param name="Phase">Current phase.</param>
/// <param name="Fraction">Overall progress, 0..1.</param>
/// <param name="CurrentMbps">Instantaneous throughput of the current phase, when measuring.</param>
/// <param name="Detail">Short status text (server, protocol, streams).</param>
public sealed record SpeedProgress(SpeedPhase Phase, double Fraction, double? CurrentMbps, string? Detail);

/// <summary>Engine-neutral result of a complete speed test.</summary>
public sealed class SpeedResult
{
    public required string EngineName { get; init; }

    /// <summary>Protocol actually used, e.g. "MSAK throughput1" or "ndt7".</summary>
    public required string Protocol { get; init; }

    public required DateTimeOffset Timestamp { get; init; }

    /// <summary>Download throughput after the TCP ramp-up (headline value).</summary>
    public double DownloadMbps { get; init; }

    /// <summary>Download throughput averaged over the whole phase, ramp-up included.</summary>
    public double DownloadAverageMbps { get; init; }

    public double UploadMbps { get; init; }

    public double UploadAverageMbps { get; init; }

    public double? IdleLatencyMs { get; init; }

    public double? JitterMs { get; init; }

    public double? DownloadLoadedLatencyMs { get; init; }

    public double? UploadLoadedLatencyMs { get; init; }

    /// <summary>How latency was measured, for transparency.</summary>
    public string? LatencyMethod { get; init; }

    public required string ServerMachine { get; init; }

    public string? ServerCity { get; init; }

    public string? ServerCountry { get; init; }

    /// <summary>Public client address as seen by the server, without port.</summary>
    public string? ClientAddress { get; init; }

    /// <summary>ISP name. M-Lab does not return it, so this is null for the M-Lab engine.</summary>
    public string? Isp { get; init; }

    /// <summary>Approximate client location. Not returned by M-Lab.</summary>
    public string? ClientLocation { get; init; }

    public long BytesReceived { get; init; }

    public long BytesSent { get; init; }

    public int Streams { get; init; }

    /// <summary>Extra latency added by load: worst loaded latency minus idle latency.</summary>
    public double? BufferbloatMs
    {
        get
        {
            if (IdleLatencyMs is not double idle)
            {
                return null;
            }

            double? loaded = (DownloadLoadedLatencyMs, UploadLoadedLatencyMs) switch
            {
                (double d, double u) => Math.Max(d, u),
                (double d, null) => d,
                (null, double u) => u,
                _ => null,
            };

            return loaded is double l ? Math.Max(0, l - idle) : null;
        }
    }

    public string? BufferbloatGrade => BufferbloatMs switch
    {
        null => null,
        < 30 => "Low",
        < 100 => "Moderate",
        _ => "High",
    };
}

public enum SpeedTestErrorKind
{
    /// <summary>M-Lab Locate returned 204: no capacity right now.</summary>
    NoCapacity,

    /// <summary>Locate v2 failed or returned an unusable answer.</summary>
    LocateFailed,

    /// <summary>Every candidate server refused the connection.</summary>
    AllServersRejected,

    Timeout,

    Network,

    Protocol,

    /// <summary>The local fair-use limit (M-Lab AUP: 40 tests/day) was reached.</summary>
    RateLimited,
}

public sealed class SpeedTestException : Exception
{
    public SpeedTestException()
    {
    }

    public SpeedTestException(string message)
        : base(message)
    {
    }

    public SpeedTestException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    public SpeedTestException(SpeedTestErrorKind kind, string message, Exception? innerException = null)
        : base(message, innerException)
    {
        Kind = kind;
    }

    public SpeedTestErrorKind Kind { get; } = SpeedTestErrorKind.Protocol;
}
