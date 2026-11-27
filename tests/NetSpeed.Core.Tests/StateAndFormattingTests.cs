namespace NetSpeed.Core.Tests;

internal sealed class FakeTime : TimeProvider
{
    public DateTimeOffset Now { get; set; } = new(2026, 11, 27, 12, 0, 0, TimeSpan.Zero);

    public override DateTimeOffset GetUtcNow() => Now;
}

public sealed class LocalStateStoreTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "netspeed-tests-" + Guid.NewGuid().ToString("N"));

    private string StatePath => Path.Combine(_dir, "sub", "state.json");

    public void Dispose()
    {
        if (Directory.Exists(_dir))
        {
            Directory.Delete(_dir, recursive: true);
        }
    }

    [Fact]
    public void Consent_is_absent_by_default_and_persists_once_granted()
    {
        FakeTime time = new();
        LocalStateStore store = new(StatePath, time);
        Assert.False(store.HasConsent);

        store.GrantConsent();
        Assert.True(store.HasConsent);
        Assert.True(File.Exists(StatePath));

        Assert.True(new LocalStateStore(StatePath, time).HasConsent);
    }

    [Fact]
    public void Revoke_clears_consent()
    {
        LocalStateStore store = new(StatePath, new FakeTime());
        store.GrantConsent();
        store.RevokeConsent();
        Assert.False(new LocalStateStore(StatePath, new FakeTime()).HasConsent);
    }

    [Fact]
    public void Corrupt_file_means_no_consent()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(StatePath)!);
        File.WriteAllText(StatePath, "{ broken");
        Assert.False(new LocalStateStore(StatePath, new FakeTime()).HasConsent);
    }

    [Fact]
    public void Old_consent_version_asks_again()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(StatePath)!);
        File.WriteAllText(StatePath, """{"ConsentVersion":0,"RecentTests":[]}""");
        Assert.False(new LocalStateStore(StatePath, new FakeTime()).HasConsent);
    }

    [Fact]
    public void Daily_limit_is_40_in_a_rolling_24_hours()
    {
        FakeTime time = new();
        LocalStateStore store = new(StatePath, time);

        for (int i = 0; i < LocalStateStore.MaxTestsPerDay; i++)
        {
            Assert.True(store.TryRegisterTest());
            time.Now = time.Now.AddMinutes(1);
        }

        Assert.Equal(0, store.RemainingTests);
        Assert.False(store.TryRegisterTest());

        // Survives a restart.
        Assert.False(new LocalStateStore(StatePath, time).TryRegisterTest());

        // 24 h after the first test, one slot frees up.
        time.Now = new DateTimeOffset(2026, 11, 28, 12, 0, 0, TimeSpan.Zero);
        Assert.Equal(1, store.RemainingTests);
        Assert.True(store.TryRegisterTest());
        Assert.False(store.TryRegisterTest());
    }
}

public class SpeedResultTests
{
    private static SpeedResult Make(double? idle, double? down, double? up) => new()
    {
        EngineName = "M-Lab",
        Protocol = "MSAK throughput1",
        Timestamp = new DateTimeOffset(2026, 11, 27, 10, 30, 0, TimeSpan.FromHours(1)),
        DownloadMbps = 943.21,
        DownloadAverageMbps = 870.4,
        UploadMbps = 48.76,
        UploadAverageMbps = 45.1,
        IdleLatencyMs = idle,
        JitterMs = 1.24,
        DownloadLoadedLatencyMs = down,
        UploadLoadedLatencyMs = up,
        ServerMachine = "mlab3-mil06.mlab-oti.measurement-lab.org",
        ServerCity = "Milan",
        ServerCountry = "IT",
        ClientAddress = "203.0.113.7",
        BytesReceived = 1_180_000_000,
        BytesSent = 60_000_000,
        Streams = 4,
    };

    [Theory]
    [InlineData(10.0, 25.0, 80.0, 70.0, "Moderate")]
    [InlineData(10.0, 25.0, null, 15.0, "Low")]
    [InlineData(10.0, 200.0, 20.0, 190.0, "High")]
    [InlineData(10.0, 5.0, null, 0.0, "Low")] // loaded below idle is clamped
    public void Bufferbloat_is_worst_loaded_minus_idle(double idle, double? down, double? up, double expected, string grade)
    {
        SpeedResult r = Make(idle, down, up);
        Assert.Equal(expected, r.BufferbloatMs!.Value, 6);
        Assert.Equal(grade, r.BufferbloatGrade);
    }

    [Fact]
    public void Bufferbloat_unknown_without_idle_or_loaded()
    {
        Assert.Null(Make(null, 30, 30).BufferbloatMs);
        Assert.Null(Make(10, null, null).BufferbloatMs);
        Assert.Null(Make(10, null, null).BufferbloatGrade);
    }

    [Fact]
    public void Clipboard_text_contains_results_but_not_ip()
    {
        string text = ResultFormatter.ToClipboardText(Make(12.3, 40, 90));
        Assert.Contains("Download: 943 Mbps", text, StringComparison.Ordinal);
        Assert.Contains("Upload: 48.8 Mbps", text, StringComparison.Ordinal);
        Assert.Contains("Idle latency: 12.3 ms • Jitter: 1.2 ms", text, StringComparison.Ordinal);
        Assert.Contains("Bufferbloat: +78 ms (Moderate)", text, StringComparison.Ordinal);
        Assert.Contains("Milan, IT", text, StringComparison.Ordinal);
        Assert.Contains("MSAK throughput1, 4 stream(s)", text, StringComparison.Ordinal);
        Assert.DoesNotContain("203.0.113.7", text, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(0L, "0 B")]
    [InlineData(1536L, "1.5 KiB")]
    [InlineData(1_180_000_000L, "1.10 GiB")]
    public void Bytes_formatting(long bytes, string expected)
    {
        Assert.Equal(expected, ResultFormatter.Bytes(bytes));
    }
}
