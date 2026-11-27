using System.Text.Json;

namespace NetSpeed.Core.Mlab;

/// <summary>Server measurement normalised across ndt7 and MSAK.</summary>
/// <param name="AppBytes">Application-level bytes moved in the test direction, as counted by the server.</param>
/// <param name="ElapsedMicroseconds">Server-side elapsed time.</param>
/// <param name="MinRttMicroseconds">TCP_INFO MinRTT.</param>
/// <param name="RttMicroseconds">TCP_INFO smoothed RTT.</param>
/// <param name="ClientAddress">Client address as seen by the server, without port.</param>
/// <param name="Uuid">M-Lab measurement UUID, when present.</param>
public readonly record struct ServerMeasurement(
    long? AppBytes,
    long? ElapsedMicroseconds,
    long? MinRttMicroseconds,
    long? RttMicroseconds,
    string? ClientAddress,
    string? Uuid);

public static class MeasurementParser
{
    /// <summary>Parses a textual WebSocket message. Returns false for malformed input.</summary>
    public static bool TryParse(MlabProtocolKind kind, TestDirection direction, ReadOnlySpan<byte> utf8Json, out ServerMeasurement measurement)
    {
        measurement = default;
        try
        {
            if (kind == MlabProtocolKind.Msak)
            {
                MsakWireMeasurement? m = JsonSerializer.Deserialize(utf8Json, MlabJsonContext.Default.MsakWireMeasurement);
                if (m is null)
                {
                    return false;
                }

                // Download: the server is the sender. Upload: the server is the receiver.
                long? appBytes = direction == TestDirection.Download ? m.Application?.BytesSent : m.Application?.BytesReceived;
                measurement = new ServerMeasurement(
                    appBytes,
                    m.ElapsedTime,
                    m.TCPInfo?.MinRTT,
                    m.TCPInfo?.RTT,
                    AddressHelper.StripPort(m.RemoteAddr),
                    m.UUID);
                return true;
            }

            Ndt7Measurement? n = JsonSerializer.Deserialize(utf8Json, MlabJsonContext.Default.Ndt7Measurement);
            if (n is null)
            {
                return false;
            }

            measurement = new ServerMeasurement(
                n.AppInfo?.NumBytes,
                n.AppInfo?.ElapsedTime ?? n.TCPInfo?.ElapsedTime,
                n.TCPInfo?.MinRTT,
                n.TCPInfo?.RTT,
                AddressHelper.StripPort(n.ConnectionInfo?.Client),
                n.ConnectionInfo?.UUID);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}

public static class AddressHelper
{
    /// <summary>"1.2.3.4:5678" → "1.2.3.4"; "[2001:db8::1]:443" → "2001:db8::1".</summary>
    public static string? StripPort(string? endpoint)
    {
        if (string.IsNullOrWhiteSpace(endpoint))
        {
            return null;
        }

        string value = endpoint.Trim();
        if (value.StartsWith('['))
        {
            int close = value.IndexOf(']', StringComparison.Ordinal);
            return close > 1 ? value[1..close] : value;
        }

        int colon = value.LastIndexOf(':');

        // More than one colon without brackets: a bare IPv6 address, leave it alone.
        if (colon > 0 && value.IndexOf(':', StringComparison.Ordinal) == colon)
        {
            return value[..colon];
        }

        return value;
    }
}
