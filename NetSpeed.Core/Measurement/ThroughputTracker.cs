namespace NetSpeed.Core.Measurement;

/// <summary>
/// Turns cumulative byte counters sampled over time into throughput figures.
/// Samples are (seconds since the phase started, total bytes so far); the implicit first
/// sample is (0, 0).
/// </summary>
public sealed class ThroughputTracker
{
    /// <summary>Minimum steady-state window; shorter windows fall back to the full average.</summary>
    public const double MinSteadySeconds = 1.0;

    private readonly List<Sample> _samples = [];
    private readonly double _warmupSeconds;

    /// <param name="warmupSeconds">
    /// Initial part of the phase excluded from <see cref="SteadyMbps"/> (TCP slow start and
    /// stream ramp-up).
    /// </param>
    public ThroughputTracker(double warmupSeconds)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(warmupSeconds);
        _warmupSeconds = warmupSeconds;
    }

    public int Count => _samples.Count;

    public long TotalBytes => _samples.Count == 0 ? 0 : _samples[^1].Bytes;

    public double ElapsedSeconds => _samples.Count == 0 ? 0 : _samples[^1].Seconds;

    /// <summary>
    /// Average over the whole phase, ramp-up included. The phase ends at the first sample that
    /// reached the final byte count: idle time spent waiting for the close handshake is excluded.
    /// </summary>
    public double AverageMbps
    {
        get
        {
            if (_samples.Count == 0)
            {
                return 0;
            }

            Sample end = _samples[EndIndex()];
            return Mbps(end.Bytes, end.Seconds);
        }
    }

    /// <summary>
    /// Throughput after the warm-up: bytes moved between the first sample at or after the
    /// warm-up and the end of data transfer, divided by that time. Falls back to
    /// <see cref="AverageMbps"/> when less than <see cref="MinSteadySeconds"/> remain.
    /// </summary>
    public double SteadyMbps
    {
        get
        {
            if (_samples.Count < 2)
            {
                return AverageMbps;
            }

            Sample last = _samples[EndIndex()];
            int start = _samples.FindIndex(s => s.Seconds >= _warmupSeconds);
            if (start < 0 || last.Seconds - _samples[start].Seconds < MinSteadySeconds)
            {
                return AverageMbps;
            }

            Sample first = _samples[start];
            return Mbps(last.Bytes - first.Bytes, last.Seconds - first.Seconds);
        }
    }

    /// <summary>Adds a sample. Non-increasing times are ignored; byte counters never go backwards.</summary>
    public void Add(double seconds, long totalBytes)
    {
        if (double.IsNaN(seconds) || seconds <= 0)
        {
            return;
        }

        if (_samples.Count > 0)
        {
            Sample last = _samples[^1];
            if (seconds <= last.Seconds)
            {
                return;
            }

            totalBytes = Math.Max(totalBytes, last.Bytes);
        }

        _samples.Add(new Sample(seconds, Math.Max(0, totalBytes)));
    }

    /// <summary>Throughput over the most recent <paramref name="windowSeconds"/>.</summary>
    public double CurrentMbps(double windowSeconds = 1.0)
    {
        if (_samples.Count == 0)
        {
            return 0;
        }

        Sample last = _samples[^1];
        Sample reference = new(0, 0);
        for (int i = _samples.Count - 2; i >= 0; i--)
        {
            reference = _samples[i];
            if (last.Seconds - reference.Seconds >= windowSeconds)
            {
                break;
            }

            if (i == 0)
            {
                // Not enough history yet: measure from the start of the phase.
                reference = new Sample(0, 0);
            }
        }

        return Mbps(last.Bytes - reference.Bytes, last.Seconds - reference.Seconds);
    }

    /// <summary>Index of the first sample that reached the final byte count (end of data flow).</summary>
    private int EndIndex()
    {
        long total = _samples[^1].Bytes;
        int index = _samples.Count - 1;
        while (index > 0 && _samples[index - 1].Bytes == total)
        {
            index--;
        }

        return index;
    }

    public static double Mbps(long bytes, double seconds) =>
        seconds <= 0 ? 0 : bytes * 8d / seconds / 1_000_000d;

    private readonly record struct Sample(double Seconds, long Bytes);
}
