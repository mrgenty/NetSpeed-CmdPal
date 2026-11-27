namespace NetSpeed.Core.Measurement;

public static class LatencyStats
{
    public static double? Median(IReadOnlyCollection<double> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        if (values.Count == 0)
        {
            return null;
        }

        double[] sorted = [.. values];
        Array.Sort(sorted);
        int mid = sorted.Length / 2;
        return sorted.Length % 2 == 1 ? sorted[mid] : (sorted[mid - 1] + sorted[mid]) / 2d;
    }

    /// <summary>
    /// Jitter as the mean absolute difference between consecutive samples (the definition
    /// used by most consumer speed tests). Needs at least two samples.
    /// </summary>
    public static double? Jitter(IReadOnlyList<double> samples)
    {
        ArgumentNullException.ThrowIfNull(samples);
        if (samples.Count < 2)
        {
            return null;
        }

        double sum = 0;
        for (int i = 1; i < samples.Count; i++)
        {
            sum += Math.Abs(samples[i] - samples[i - 1]);
        }

        return sum / (samples.Count - 1);
    }
}

public static class ServerRanker
{
    /// <summary>
    /// Orders items by measured RTT (ascending). Items without a measurement keep their
    /// original (Locate) order after all measured ones. The sort is stable.
    /// </summary>
    public static IReadOnlyList<T> Rank<T>(IReadOnlyList<T> items, IReadOnlyList<double?> rttMs)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(rttMs);
        if (items.Count != rttMs.Count)
        {
            throw new ArgumentException("Items and RTTs must have the same length.", nameof(rttMs));
        }

        return [.. Enumerable.Range(0, items.Count)
            .OrderBy(i => rttMs[i].HasValue ? 0 : 1)
            .ThenBy(i => rttMs[i] ?? 0)
            .ThenBy(i => i)
            .Select(i => items[i])];
    }
}

public static class MessageSizer
{
    /// <summary>
    /// Upload message scaling from the ndt7 spec appendix (also used by MSAK): double the
    /// message when it is no larger than 1/16 of the bytes queued so far, up to <paramref name="max"/>.
    /// </summary>
    public static int Next(int current, long totalQueued, int max)
    {
        if (current >= max)
        {
            return max;
        }

        return current <= totalQueued / 16 ? Math.Min(current * 2, max) : current;
    }
}
