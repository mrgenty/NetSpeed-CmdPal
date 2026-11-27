using System.Text.Json.Serialization;

namespace NetSpeed.Core.Mlab;

// ---- Locate v2 ----------------------------------------------------------

public sealed class LocateResponse
{
    [JsonPropertyName("results")]
    public List<LocateResult>? Results { get; set; }
}

public sealed class LocateResult
{
    [JsonPropertyName("machine")]
    public string? Machine { get; set; }

    [JsonPropertyName("hostname")]
    public string? Hostname { get; set; }

    [JsonPropertyName("location")]
    public LocateLocation? Location { get; set; }

    [JsonPropertyName("urls")]
    public Dictionary<string, string>? Urls { get; set; }
}

public sealed class LocateLocation
{
    [JsonPropertyName("city")]
    public string? City { get; set; }

    [JsonPropertyName("country")]
    public string? Country { get; set; }
}

// ---- Server measurement messages -----------------------------------------

/// <summary>TCP_INFO subset shared by ndt7 and MSAK (field names match the wire JSON).</summary>
public sealed class TcpInfoMessage
{
    public long? BusyTime { get; set; }

    public long? BytesAcked { get; set; }

    public long? BytesReceived { get; set; }

    public long? BytesSent { get; set; }

    public long? BytesRetrans { get; set; }

    public long? ElapsedTime { get; set; }

    /// <summary>Minimum RTT seen by the server kernel, microseconds.</summary>
    public long? MinRTT { get; set; }

    /// <summary>Smoothed RTT, microseconds.</summary>
    public long? RTT { get; set; }

    public long? RTTVar { get; set; }
}

/// <summary>MSAK throughput1 WireMeasurement (pkg/throughput1/model).</summary>
public sealed class MsakWireMeasurement
{
    public string? CC { get; set; }

    public string? UUID { get; set; }

    public string? LocalAddr { get; set; }

    /// <summary>Client address as seen by the server ("ip:port").</summary>
    public string? RemoteAddr { get; set; }

    public MsakByteCounters? Application { get; set; }

    public MsakByteCounters? Network { get; set; }

    /// <summary>Microseconds since the stream started.</summary>
    public long? ElapsedTime { get; set; }

    public TcpInfoMessage? TCPInfo { get; set; }
}

public sealed class MsakByteCounters
{
    public long? BytesSent { get; set; }

    public long? BytesReceived { get; set; }
}

/// <summary>ndt7 measurement message (spec v0.11.0, "Measurement message").</summary>
public sealed class Ndt7Measurement
{
    public Ndt7AppInfo? AppInfo { get; set; }

    public Ndt7ConnectionInfo? ConnectionInfo { get; set; }

    public string? Origin { get; set; }

    public string? Test { get; set; }

    public TcpInfoMessage? TCPInfo { get; set; }
}

public sealed class Ndt7AppInfo
{
    public long? ElapsedTime { get; set; }

    public long? NumBytes { get; set; }
}

public sealed class Ndt7ConnectionInfo
{
    public string? Client { get; set; }

    public string? Server { get; set; }

    public string? UUID { get; set; }
}

[JsonSourceGenerationOptions(PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(LocateResponse))]
[JsonSerializable(typeof(MsakWireMeasurement))]
[JsonSerializable(typeof(Ndt7Measurement))]
internal sealed partial class MlabJsonContext : JsonSerializerContext
{
}
