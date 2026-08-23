using System.Text.Json.Serialization;

namespace NetSpeed;

public sealed class LibreSpeedResult
{
    [JsonPropertyName("timestamp")]
    public DateTimeOffset Timestamp { get; set; }

    [JsonPropertyName("server")]
    public LibreSpeedServer Server { get; set; } = new();

    [JsonPropertyName("client")]
    public LibreSpeedClient Client { get; set; } = new();

    [JsonPropertyName("bytes_sent")]
    public ulong BytesSent { get; set; }

    [JsonPropertyName("bytes_received")]
    public ulong BytesReceived { get; set; }

    [JsonPropertyName("ping")]
    public double Ping { get; set; }

    [JsonPropertyName("jitter")]
    public double Jitter { get; set; }

    [JsonPropertyName("upload")]
    public double Upload { get; set; }

    [JsonPropertyName("download")]
    public double Download { get; set; }

    [JsonPropertyName("share")]
    public string? Share { get; set; }
}

public sealed class LibreSpeedServer
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("url")]
    public string? Url { get; set; }
}

public sealed class LibreSpeedClient
{
    [JsonPropertyName("ip")]
    public string? Ip { get; set; }

    [JsonPropertyName("hostname")]
    public string? Hostname { get; set; }

    [JsonPropertyName("city")]
    public string? City { get; set; }

    [JsonPropertyName("region")]
    public string? Region { get; set; }

    [JsonPropertyName("country")]
    public string? Country { get; set; }

    [JsonPropertyName("loc")]
    public string? Location { get; set; }

    [JsonPropertyName("org")]
    public string? Organization { get; set; }

    [JsonPropertyName("postal")]
    public string? Postal { get; set; }

    [JsonPropertyName("timezone")]
    public string? Timezone { get; set; }
}
