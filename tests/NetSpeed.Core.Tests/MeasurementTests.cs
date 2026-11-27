using NetSpeed.Core.Measurement;

namespace NetSpeed.Core.Tests;

public class ThroughputTrackerTests
{
    [Fact]
    public void Empty_tracker_reports_zero()
    {
        ThroughputTracker t = new(2);
        Assert.Equal(0, t.AverageMbps);
        Assert.Equal(0, t.SteadyMbps);
        Assert.Equal(0, t.CurrentMbps());
        Assert.Equal(0, t.TotalBytes);
    }

    [Fact]
    public void Constant_rate_gives_same_average_steady_and_current()
    {
        // 12.5 MB/s = 100 Mbps, sampled every 100 ms for 10 s.
        ThroughputTracker t = new(2);
        for (int i = 1; i <= 100; i++)
        {
            t.Add(i / 10d, i * 1_250_000L);
        }

        Assert.Equal(100, t.AverageMbps, 6);
        Assert.Equal(100, t.SteadyMbps, 6);
        Assert.Equal(100, t.CurrentMbps(), 6);
    }

    [Fact]
    public void Steady_excludes_ramp_up()
    {
        // 0-2 s: slow start at 10 Mbps; 2-10 s: 900 Mbps.
        ThroughputTracker t = new(2);
        long bytes = 0;
        for (int i = 1; i <= 100; i++)
        {
            double mbps = i <= 20 ? 10 : 900;
            bytes += (long)(mbps * 1_000_000 / 8 / 10);
            t.Add(i / 10d, bytes);
        }

        Assert.Equal(900, t.SteadyMbps, 3);
        Assert.True(t.AverageMbps < t.SteadyMbps);
        Assert.Equal(722, t.AverageMbps, 0); // (2 s × 10 + 8 s × 900) / 10 s
    }

    [Fact]
    public void Steady_falls_back_to_average_when_too_short()
    {
        ThroughputTracker t = new(2);
        t.Add(0.5, 1_000_000);
        t.Add(1.0, 2_000_000);
        t.Add(2.5, 5_000_000); // only 0 s after warm-up
        Assert.Equal(t.AverageMbps, t.SteadyMbps);
    }

    [Fact]
    public void Ignores_non_increasing_time_and_backwards_counters()
    {
        ThroughputTracker t = new(0);
        t.Add(1, 1000);
        t.Add(1, 5000);   // same time: ignored
        t.Add(0.5, 9000); // earlier: ignored
        t.Add(2, 500);    // counter went backwards: clamped to 1000
        Assert.Equal(2, t.Count);
        Assert.Equal(1000, t.TotalBytes);
    }

    [Fact]
    public void Current_uses_last_window_only()
    {
        ThroughputTracker t = new(0);
        t.Add(1, 1_250_000);    // 10 Mbps for 1 s
        t.Add(2, 13_750_000);   // then 100 Mbps
        Assert.Equal(100, t.CurrentMbps(1.0), 6);
    }

    [Fact]
    public void Current_with_short_history_measures_from_phase_start()
    {
        ThroughputTracker t = new(0);
        t.Add(0.2, 250_000);
        t.Add(0.4, 500_000);
        Assert.Equal(10, t.CurrentMbps(1.0), 6);
    }
}

public class LatencyStatsTests
{
    [Fact]
    public void Median_odd_even_empty()
    {
        Assert.Null(LatencyStats.Median([]));
        Assert.Equal(3, LatencyStats.Median([5, 1, 3]));
        Assert.Equal(2.5, LatencyStats.Median([4, 1, 2, 3]));
    }

    [Fact]
    public void Jitter_is_mean_absolute_consecutive_difference()
    {
        Assert.Null(LatencyStats.Jitter([10]));
        Assert.Equal(0, LatencyStats.Jitter([10, 10, 10]));
        // |12-10| + |9-12| + |13-9| = 2 + 3 + 4 = 9 → 9/3
        Assert.Equal(3, LatencyStats.Jitter([10, 12, 9, 13]));
    }
}

public class ServerRankerTests
{
    [Fact]
    public void Orders_by_rtt_and_keeps_unmeasured_last_in_original_order()
    {
        string[] servers = ["a", "b", "c", "d", "e"];
        double?[] rtts = [30, null, 12, null, 12];
        Assert.Equal(["c", "e", "a", "b", "d"], ServerRanker.Rank(servers, rtts));
    }

    [Fact]
    public void Mismatched_lengths_throw()
    {
        Assert.Throws<ArgumentException>(() => ServerRanker.Rank(["a"], [1, 2]));
    }
}

public class MessageSizerTests
{
    [Theory]
    [InlineData(8192, 0, 8192)]              // nothing queued yet
    [InlineData(8192, 16 * 8192, 16384)]     // exactly 1/16 → double
    [InlineData(8192, 16 * 8192 - 1, 8192)]  // just below
    [InlineData(1 << 20, long.MaxValue, 1 << 20)] // capped
    [InlineData(786432, 1L << 40, 1 << 20)]  // doubling would exceed cap
    public void Scales_as_in_ndt7_appendix(int current, long queued, int expected)
    {
        Assert.Equal(expected, MessageSizer.Next(current, queued, 1 << 20));
    }
}
