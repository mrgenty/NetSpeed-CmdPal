using System.Globalization;
using System.Text;

namespace NetSpeed.Core;

public static class ResultFormatter
{
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    public static string Mbps(double value) => value.ToString(value >= 100 ? "0" : "0.0", Invariant);

    public static string Ms(double? value) => value is double v ? v.ToString("0.0", Invariant) + " ms" : "n/a";

    public static string Bytes(long bytes)
    {
        const double kib = 1024d;
        const double mib = kib * 1024d;
        const double gib = mib * 1024d;
        return bytes switch
        {
            >= (long)gib => (bytes / gib).ToString("0.00", Invariant) + " GiB",
            >= (long)mib => (bytes / mib).ToString("0.0", Invariant) + " MiB",
            >= (long)kib => (bytes / kib).ToString("0.0", Invariant) + " KiB",
            _ => bytes.ToString(Invariant) + " B",
        };
    }

    public static string ServerPlace(SpeedResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        string place = string.Join(", ", new[] { result.ServerCity, result.ServerCountry }.Where(p => !string.IsNullOrWhiteSpace(p)));
        return string.IsNullOrEmpty(place) ? result.ServerMachine : place;
    }

    /// <summary>Plain-text summary for the clipboard. The client IP address is deliberately omitted.</summary>
    public static string ToClipboardText(SpeedResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        StringBuilder sb = new();
        sb.AppendLine(Invariant, $"NetSpeed result — {result.Timestamp.ToString("yyyy-MM-dd HH:mm:ss zzz", Invariant)}");
        sb.AppendLine(Invariant, $"Download: {Mbps(result.DownloadMbps)} Mbps (full-phase average {Mbps(result.DownloadAverageMbps)} Mbps)");
        sb.AppendLine(Invariant, $"Upload: {Mbps(result.UploadMbps)} Mbps (full-phase average {Mbps(result.UploadAverageMbps)} Mbps)");
        sb.AppendLine(Invariant, $"Idle latency: {Ms(result.IdleLatencyMs)} • Jitter: {Ms(result.JitterMs)}");
        sb.AppendLine(Invariant, $"Loaded latency: download {Ms(result.DownloadLoadedLatencyMs)} • upload {Ms(result.UploadLoadedLatencyMs)}");
        if (result.BufferbloatMs is double bloat)
        {
            sb.AppendLine(Invariant, $"Bufferbloat: +{bloat.ToString("0", Invariant)} ms ({result.BufferbloatGrade})");
        }

        sb.AppendLine(Invariant, $"Server: {result.ServerMachine} ({ServerPlace(result)})");
        sb.AppendLine(Invariant, $"Engine: {result.EngineName} {result.Protocol}, {result.Streams} stream(s)");
        sb.Append(Invariant, $"Data: received {Bytes(result.BytesReceived)}, sent {Bytes(result.BytesSent)}");
        return sb.ToString();
    }
}
