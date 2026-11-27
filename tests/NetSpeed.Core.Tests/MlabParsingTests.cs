using System.Net;
using System.Text;
using NetSpeed.Core.Mlab;

namespace NetSpeed.Core.Tests;

public class LocateParserTests
{
    // Shape of a real Locate v2 answer for msak/throughput1 (tokens replaced, "&" escaped as in the wire JSON).
    private const string MsakJson = """
    {
      "results": [
        {
          "machine": "mlab3-mil06.mlab-oti.measurement-lab.org",
          "hostname": "msak-mlab3-mil06.mlab-oti.measurement-lab.org",
          "location": { "city": "Milan", "country": "IT" },
          "urls": {
            "ws:///throughput/v1/download": "ws://msak-mlab3-mil06.mlab-oti.measurement-lab.org/throughput/v1/download?access_token=T1&index=0",
            "ws:///throughput/v1/upload": "ws://msak-mlab3-mil06.mlab-oti.measurement-lab.org/throughput/v1/upload?access_token=T1&index=0",
            "wss:///throughput/v1/download": "wss://msak-mlab3-mil06.mlab-oti.measurement-lab.org/throughput/v1/download?access_token=T1&index=0&locate_version=v2",
            "wss:///throughput/v1/upload": "wss://msak-mlab3-mil06.mlab-oti.measurement-lab.org/throughput/v1/upload?access_token=T1&index=0&locate_version=v2"
          }
        },
        {
          "machine": "broken-1",
          "urls": { "wss:///throughput/v1/download": "wss://x/throughput/v1/download" }
        },
        {
          "machine": "insecure-1",
          "urls": {
            "wss:///throughput/v1/download": "ws://y/throughput/v1/download",
            "wss:///throughput/v1/upload": "ws://y/throughput/v1/upload"
          }
        },
        {
          "machine": "mlab2-mil07.mlab-oti.measurement-lab.org",
          "urls": {
            "wss:///throughput/v1/download": "wss://msak-mlab2-mil07.example/throughput/v1/download?access_token=T2",
            "wss:///throughput/v1/upload": "wss://msak-mlab2-mil07.example/throughput/v1/upload?access_token=T2"
          }
        }
      ]
    }
    """;

    [Fact]
    public void Parses_wss_targets_in_order_and_skips_incomplete_or_insecure()
    {
        IReadOnlyList<MlabTarget> targets = LocateParser.Parse(MsakJson, MlabProtocol.Msak);

        Assert.Equal(2, targets.Count);
        MlabTarget first = targets[0];
        Assert.Equal("mlab3-mil06.mlab-oti.measurement-lab.org", first.Machine);
        Assert.Equal("msak-mlab3-mil06.mlab-oti.measurement-lab.org", first.Hostname);
        Assert.Equal("Milan", first.City);
        Assert.Equal("IT", first.Country);
        Assert.Equal("wss", first.DownloadUrl.Scheme);
        Assert.Equal("/throughput/v1/upload", first.UploadUrl.AbsolutePath);
        Assert.Contains("access_token=T1&index=0", first.DownloadUrl.Query, StringComparison.Ordinal);

        // Hostname missing → taken from the URL.
        Assert.Equal("msak-mlab2-mil07.example", targets[1].Hostname);
        Assert.Null(targets[1].City);
    }

    [Fact]
    public void Ndt7_keys_are_used_for_ndt7()
    {
        const string json = """
        {"results":[{"machine":"m","hostname":"ndt-m","urls":{
          "wss:///ndt/v7/download":"wss://ndt-m/ndt/v7/download?access_token=A",
          "wss:///ndt/v7/upload":"wss://ndt-m/ndt/v7/upload?access_token=A"}}]}
        """;
        Assert.Single(LocateParser.Parse(json, MlabProtocol.Ndt7));
        Assert.Empty(LocateParser.Parse(json, MlabProtocol.Msak));
    }

    [Fact]
    public void Invalid_json_is_a_locate_failure()
    {
        SpeedTestException ex = Assert.Throws<SpeedTestException>(() => LocateParser.Parse("{not json", MlabProtocol.Msak));
        Assert.Equal(SpeedTestErrorKind.LocateFailed, ex.Kind);
    }

    [Fact]
    public void Empty_results_parse_to_empty_list()
    {
        Assert.Empty(LocateParser.Parse("{}", MlabProtocol.Msak));
        Assert.Empty(LocateParser.Parse("""{"results":[]}""", MlabProtocol.Msak));
    }

    [Theory]
    [InlineData(HttpStatusCode.NoContent, SpeedTestErrorKind.NoCapacity)]
    [InlineData(HttpStatusCode.ServiceUnavailable, SpeedTestErrorKind.LocateFailed)]
    [InlineData(HttpStatusCode.TooManyRequests, SpeedTestErrorKind.LocateFailed)]
    public async Task Non_200_statuses_map_to_errors(HttpStatusCode status, SpeedTestErrorKind kind)
    {
        using HttpResponseMessage response = new(status);
        SpeedTestException ex = await Assert.ThrowsAsync<SpeedTestException>(
            () => LocateClient.InterpretAsync(response, MlabProtocol.Msak, CancellationToken.None));
        Assert.Equal(kind, ex.Kind);
    }

    [Fact]
    public async Task Ok_without_usable_results_is_a_locate_failure()
    {
        using HttpResponseMessage response = new(HttpStatusCode.OK) { Content = new StringContent("""{"results":[]}""") };
        SpeedTestException ex = await Assert.ThrowsAsync<SpeedTestException>(
            () => LocateClient.InterpretAsync(response, MlabProtocol.Msak, CancellationToken.None));
        Assert.Equal(SpeedTestErrorKind.LocateFailed, ex.Kind);
    }

    [Fact]
    public async Task Ok_with_results_returns_targets()
    {
        using HttpResponseMessage response = new(HttpStatusCode.OK) { Content = new StringContent(MsakJson) };
        IReadOnlyList<MlabTarget> targets = await LocateClient.InterpretAsync(response, MlabProtocol.Msak, CancellationToken.None);
        Assert.Equal(2, targets.Count);
    }
}

public class MeasurementParserTests
{
    // Field set observed from a real msak-server download measurement (values shortened).
    private const string MsakMessage = """
    {"UUID":"mlab3-mil06_1700000000_000000000001","LocalAddr":"[2001:db8::2]:443","RemoteAddr":"203.0.113.7:51544",
     "Application":{"BytesSent":68170045,"BytesReceived":120},"Network":{"BytesSent":68300000,"BytesReceived":900},
     "ElapsedTime":3025520,
     "BBRInfo":{"BW":1,"MinRTT":2},
     "TCPInfo":{"State":1,"RTT":38905,"RTTVar":800,"MinRTT":38423,"BytesAcked":67000000,"BytesReceived":900,"ElapsedTime":3025000,"SndCwnd":1000}}
    """;

    // ndt7 spec v0.11.0 example message.
    private const string Ndt7Message = """
    {"AppInfo":{"ElapsedTime":1234,"NumBytes":5678},
     "ConnectionInfo":{"Client":"1.2.3.4:5678","Server":"[::1]:2345","UUID":"abc","StartTime":"2019-07-16T15:26:05.987748459-04:00"},
     "Origin":"server","Test":"download",
     "TCPInfo":{"BusyTime":1,"BytesAcked":2,"BytesReceived":3,"BytesSent":4,"BytesRetrans":5,"ElapsedTime":6,"MinRTT":7000,"RTT":9000,"RTTVar":10,"RWndLimited":11,"SndBufLimited":12}}
    """;

    [Fact]
    public void Msak_download_uses_server_bytes_sent()
    {
        Assert.True(MeasurementParser.TryParse(MlabProtocolKind.Msak, TestDirection.Download, Encoding.UTF8.GetBytes(MsakMessage), out ServerMeasurement m));
        Assert.Equal(68170045, m.AppBytes);
        Assert.Equal(3025520, m.ElapsedMicroseconds);
        Assert.Equal(38423, m.MinRttMicroseconds);
        Assert.Equal(38905, m.RttMicroseconds);
        Assert.Equal("203.0.113.7", m.ClientAddress);
        Assert.Equal("mlab3-mil06_1700000000_000000000001", m.Uuid);
    }

    [Fact]
    public void Msak_upload_uses_server_bytes_received()
    {
        Assert.True(MeasurementParser.TryParse(MlabProtocolKind.Msak, TestDirection.Upload, Encoding.UTF8.GetBytes(MsakMessage), out ServerMeasurement m));
        Assert.Equal(120, m.AppBytes);
    }

    [Fact]
    public void Ndt7_spec_example_parses()
    {
        Assert.True(MeasurementParser.TryParse(MlabProtocolKind.Ndt7, TestDirection.Download, Encoding.UTF8.GetBytes(Ndt7Message), out ServerMeasurement m));
        Assert.Equal(5678, m.AppBytes);
        Assert.Equal(1234, m.ElapsedMicroseconds);
        Assert.Equal(7000, m.MinRttMicroseconds);
        Assert.Equal(9000, m.RttMicroseconds);
        Assert.Equal("1.2.3.4", m.ClientAddress);
        Assert.Equal("abc", m.Uuid);
    }

    [Fact]
    public void Missing_fields_are_null_not_errors()
    {
        Assert.True(MeasurementParser.TryParse(MlabProtocolKind.Ndt7, TestDirection.Upload, "{}"u8, out ServerMeasurement m));
        Assert.Null(m.AppBytes);
        Assert.Null(m.RttMicroseconds);
        Assert.Null(m.ClientAddress);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not json")]
    [InlineData("{\"ElapsedTime\":\"x\",\"AppInfo\":{\"NumBytes\":\"text\"}}")]
    public void Malformed_messages_are_rejected(string json)
    {
        Assert.False(MeasurementParser.TryParse(MlabProtocolKind.Ndt7, TestDirection.Download, Encoding.UTF8.GetBytes(json), out _));
        Assert.False(MeasurementParser.TryParse(MlabProtocolKind.Msak, TestDirection.Download, Encoding.UTF8.GetBytes(json), out _));
    }

    [Theory]
    [InlineData("1.2.3.4:5678", "1.2.3.4")]
    [InlineData("[2001:db8::1]:443", "2001:db8::1")]
    [InlineData("2001:db8::1", "2001:db8::1")]
    [InlineData("1.2.3.4", "1.2.3.4")]
    [InlineData("", null)]
    [InlineData(null, null)]
    public void StripPort(string? input, string? expected)
    {
        Assert.Equal(expected, AddressHelper.StripPort(input));
    }
}

public class TestUriBuilderTests
{
    [Fact]
    public void Appends_parameters_and_keeps_token()
    {
        Uri baseUri = new("wss://host/throughput/v1/download?access_token=abc.def&index=0");
        Uri uri = TestUriBuilder.AddQuery(baseUri, [new("streams", "4"), new("client_name", "netspeed cmdpal")]);
        Assert.Equal("?access_token=abc.def&index=0&streams=4&client_name=netspeed%20cmdpal", uri.Query);
        Assert.Equal("wss", uri.Scheme);
        Assert.Equal(443, uri.Port);
    }

    [Fact]
    public void Never_duplicates_or_sends_empty_values()
    {
        Uri baseUri = new("wss://host/ndt/v7/upload?access_token=abc");
        Uri uri = TestUriBuilder.AddQuery(baseUri, [new("access_token", "evil"), new("a", "1"), new("a", "2"), new("empty", "")]);
        Assert.Equal("?access_token=abc&a=1", uri.Query);
    }

    [Fact]
    public void Works_without_existing_query()
    {
        Uri uri = TestUriBuilder.AddQuery(new Uri("ws://localhost:8080/x"), [new("k", "v")]);
        Assert.Equal("ws://localhost:8080/x?k=v", uri.ToString());
    }
}

public class BuildQueryTests
{
    [Fact]
    public void Msak_query_has_required_streams_and_duration_in_ms()
    {
        MlabEngineOptions options = new() { ClientName = "netspeed-cmdpal", ClientVersion = "0.2.0", PhaseDuration = TimeSpan.FromSeconds(10) };
        Dictionary<string, string> q = MlabEngine.BuildQuery(MlabProtocol.Msak, options, 4).ToDictionary(p => p.Key, p => p.Value);
        Assert.Equal("4", q["streams"]);
        Assert.Equal("10000", q["duration"]);
        Assert.Equal("netspeed-cmdpal", q["client_name"]);
        Assert.Equal("0.2.0", q["client_version"]);
    }

    [Fact]
    public void Ndt7_query_has_no_msak_options()
    {
        List<KeyValuePair<string, string>> q = [.. MlabEngine.BuildQuery(MlabProtocol.Ndt7, new MlabEngineOptions(), 1)];
        Assert.DoesNotContain(q, p => p.Key is "streams" or "duration");
        Assert.Equal(q.Count, q.Select(p => p.Key).Distinct().Count());
        Assert.All(q, p => Assert.False(string.IsNullOrEmpty(p.Value)));
    }

    [Fact]
    public void User_agent_identifies_the_client()
    {
        MlabEngineOptions options = new() { ClientName = "netspeed-cmdpal", ClientVersion = "0.2.0" };
        Assert.StartsWith("netspeed-cmdpal/0.2.0 ", options.UserAgent, StringComparison.Ordinal);
    }
}
