using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text;
using NetSpeed.Core.Mlab;

namespace NetSpeed.Core.Tests;

/// <summary>
/// Exercises the real WebSocket code against a tiny local fake server (no Internet, no M-Lab).
/// The fake server imitates msak-server: it requires the subprotocol, streams data or reads it,
/// sends JSON measurements and closes the connection when the duration expires.
/// </summary>
public sealed class PhaseRunnerLoopbackTests : IAsyncDisposable
{
    private readonly HttpListener _listener = new();
    private readonly CancellationTokenSource _stop = new();
    private readonly Task _serverLoop;
    private readonly string _prefix;
    private int _connections;
    private long _uploadBytesReceivedByServer;

    public PhaseRunnerLoopbackTests()
    {
        int port = FreePort();
        _prefix = $"http://localhost:{port}/";
        _listener.Prefixes.Add(_prefix);
        _listener.Start();
        _serverLoop = Task.Run(AcceptLoopAsync);
    }

    public async ValueTask DisposeAsync()
    {
        await _stop.CancelAsync();
        _listener.Stop();
        _listener.Close();
        try
        {
            await _serverLoop;
        }
        catch (Exception ex) when (ex is HttpListenerException or ObjectDisposedException or OperationCanceledException)
        {
        }

        _stop.Dispose();
    }

    [Fact]
    public async Task Download_over_several_streams_counts_bytes_and_reads_measurements()
    {
        List<double> live = [];
        PhaseResult result = await PhaseRunner.RunAsync(
            Options(TestDirection.Download, "throughput/v1/download", streams: 3),
            (_, mbps) => live.Add(mbps),
            CancellationToken.None);

        Assert.Equal(3, result.StreamsConnected);
        Assert.Equal(3, Volatile.Read(ref _connections));
        Assert.True(result.Bytes > 1_000_000, $"bytes={result.Bytes}");
        Assert.True(result.AverageMbps > 1);
        Assert.True(result.SteadyMbps > 1);
        Assert.NotEmpty(live);
        Assert.Equal("127.0.0.1", result.ClientAddress);
        Assert.Equal(5.0, result.ServerMinRttMs);
        Assert.Contains(6.0, result.ServerRttMs);
        Assert.Equal(3, result.Uuids.Count);
    }

    [Fact]
    public async Task Upload_uses_server_reported_bytes()
    {
        PhaseResult result = await PhaseRunner.RunAsync(
            Options(TestDirection.Upload, "throughput/v1/upload", streams: 2),
            null,
            CancellationToken.None);

        Assert.Equal(2, result.StreamsConnected);
        Assert.True(result.Bytes > 100_000, $"bytes={result.Bytes}");

        // The phase result must come from the server's counters, never exceed what the server read.
        Assert.True(result.Bytes <= Interlocked.Read(ref _uploadBytesReceivedByServer));
    }

    [Fact]
    public async Task Wrong_subprotocol_means_server_unavailable()
    {
        PhaseOptions options = Options(TestDirection.Download, "reject", streams: 2);
        await Assert.ThrowsAsync<ServerUnavailableException>(() => PhaseRunner.RunAsync(options, null, CancellationToken.None));
    }

    [Fact]
    public async Task User_cancellation_propagates()
    {
        using CancellationTokenSource cts = new(TimeSpan.FromMilliseconds(400));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => PhaseRunner.RunAsync(
            Options(TestDirection.Download, "throughput/v1/download", streams: 1, seconds: 5),
            null,
            cts.Token));
    }

    private PhaseOptions Options(TestDirection direction, string path, int streams, double seconds = 1.2) => new()
    {
        Protocol = MlabProtocol.Msak,
        Direction = direction,
        Url = new Uri(_prefix.Replace("http://", "ws://", StringComparison.Ordinal) + path + "?access_token=test"),
        Streams = streams,
        Duration = TimeSpan.FromSeconds(seconds),
        WarmupSeconds = 0.2,
        UserAgent = "netspeed-tests/1.0",
        Query = [new("streams", streams.ToString(System.Globalization.CultureInfo.InvariantCulture)), new("duration", "1200")],
        ConnectTimeout = TimeSpan.FromSeconds(5),
    };

    private static int FreePort()
    {
        using TcpListener l = new(IPAddress.Loopback, 0);
        l.Start();
        return ((IPEndPoint)l.LocalEndpoint).Port;
    }

    private async Task AcceptLoopAsync()
    {
        while (!_stop.IsCancellationRequested)
        {
            HttpListenerContext context = await _listener.GetContextAsync();
            _ = Task.Run(() => HandleAsync(context));
        }
    }

    private async Task HandleAsync(HttpListenerContext context)
    {
        string path = context.Request.Url!.AbsolutePath;
        string? requested = context.Request.Headers["Sec-WebSocket-Protocol"];
        if (!context.Request.IsWebSocketRequest || path == "/reject" || requested != MlabProtocol.Msak.SubProtocol
            || context.Request.QueryString["access_token"] != "test" || context.Request.QueryString["streams"] is null)
        {
            context.Response.StatusCode = 400;
            context.Response.Close();
            return;
        }

        HttpListenerWebSocketContext ws = await context.AcceptWebSocketAsync(MlabProtocol.Msak.SubProtocol);
        int id = Interlocked.Increment(ref _connections);
        double seconds = int.Parse(context.Request.QueryString["duration"]!, System.Globalization.CultureInfo.InvariantCulture) / 1000d;
        using WebSocket socket = ws.WebSocket;
        try
        {
            if (path.EndsWith("/download", StringComparison.Ordinal))
            {
                await ServeDownloadAsync(socket, id, seconds);
            }
            else
            {
                await ServeUploadAsync(socket, id, seconds);
            }
        }
        catch (Exception ex) when (ex is WebSocketException or HttpListenerException or OperationCanceledException or ObjectDisposedException)
        {
        }
    }

    private static async Task ServeDownloadAsync(WebSocket socket, int id, double seconds)
    {
        byte[] chunk = new byte[64 * 1024];
        long sent = 0;
        DateTime start = DateTime.UtcNow;
        DateTime nextMeasurement = start;
        Task drain = DrainAsync(socket); // reads the client's close frame
        while ((DateTime.UtcNow - start).TotalSeconds < seconds)
        {
            await socket.SendAsync(chunk, WebSocketMessageType.Binary, true, CancellationToken.None);
            sent += chunk.Length;
            if (DateTime.UtcNow >= nextMeasurement)
            {
                await SendMeasurementAsync(socket, id, sent, 0, (DateTime.UtcNow - start).TotalMicroseconds);
                nextMeasurement = DateTime.UtcNow.AddMilliseconds(250);
            }

            await Task.Delay(2);
        }

        await SendMeasurementAsync(socket, id, sent, 0, (DateTime.UtcNow - start).TotalMicroseconds);
        await socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "Done sending", CancellationToken.None);
        await drain;
    }

    private async Task ServeUploadAsync(WebSocket socket, int id, double seconds)
    {
        long received = 0;
        DateTime start = DateTime.UtcNow;
        byte[] buffer = new byte[1 << 20];
        using CancellationTokenSource done = new();

        Task reporter = Task.Run(async () =>
        {
            while (!done.IsCancellationRequested && (DateTime.UtcNow - start).TotalSeconds < seconds)
            {
                await Task.Delay(250);
                await SendMeasurementAsync(socket, id, 0, Interlocked.Read(ref received), (DateTime.UtcNow - start).TotalMicroseconds);
            }

            if (socket.State == WebSocketState.Open)
            {
                await SendMeasurementAsync(socket, id, 0, Interlocked.Read(ref received), (DateTime.UtcNow - start).TotalMicroseconds);
                await socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "Done", CancellationToken.None);
            }
        });

        while (true)
        {
            WebSocketReceiveResult r = await socket.ReceiveAsync(buffer, CancellationToken.None);
            if (r.MessageType == WebSocketMessageType.Close)
            {
                break;
            }

            Interlocked.Add(ref received, r.Count);
            Interlocked.Add(ref _uploadBytesReceivedByServer, r.Count); // all streams together
        }

        await done.CancelAsync();
        try
        {
            await reporter;
        }
        catch (Exception ex) when (ex is WebSocketException or ObjectDisposedException)
        {
        }
    }

    private static async Task DrainAsync(WebSocket socket)
    {
        byte[] buffer = new byte[1024];
        try
        {
            while (socket.State is WebSocketState.Open or WebSocketState.CloseSent)
            {
                WebSocketReceiveResult r = await socket.ReceiveAsync(buffer, CancellationToken.None);
                if (r.MessageType == WebSocketMessageType.Close)
                {
                    return;
                }
            }
        }
        catch (WebSocketException)
        {
        }
    }

    private static readonly SemaphoreSlim SendLock = new(1, 1);

    private static async Task SendMeasurementAsync(WebSocket socket, int id, long bytesSent, long bytesReceived, double elapsedUs)
    {
        string json = string.Create(
            System.Globalization.CultureInfo.InvariantCulture,
            $"{{\"UUID\":\"fake-{id}\",\"RemoteAddr\":\"127.0.0.1:5{id:000}\",\"Application\":{{\"BytesSent\":{bytesSent},\"BytesReceived\":{bytesReceived}}},\"ElapsedTime\":{(long)elapsedUs},\"TCPInfo\":{{\"MinRTT\":5000,\"RTT\":6000}}}}");
        await SendLock.WaitAsync();
        try
        {
            await socket.SendAsync(Encoding.UTF8.GetBytes(json), WebSocketMessageType.Text, true, CancellationToken.None);
        }
        finally
        {
            SendLock.Release();
        }
    }
}
