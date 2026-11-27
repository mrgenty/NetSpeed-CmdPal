using System.Diagnostics;
using System.Net;
using System.Net.Sockets;

namespace NetSpeed.Core.Measurement;

/// <summary>
/// Measures round-trip time as the duration of a TCP handshake (SYN → SYN/ACK) to the test
/// server's HTTPS port. No payload is sent; the socket is closed right after connecting.
/// DNS is resolved once beforehand so it does not count.
/// </summary>
public static class TcpPinger
{
    public static async Task<IPAddress?> ResolveAsync(string host, CancellationToken cancellationToken)
    {
        try
        {
            IPAddress[] addresses = await Dns.GetHostAddressesAsync(host, cancellationToken).ConfigureAwait(false);
            return addresses.Length > 0 ? addresses[0] : null;
        }
        catch (SocketException)
        {
            return null;
        }
    }

    /// <returns>RTT in milliseconds, or null if the connection failed or timed out.</returns>
    public static async Task<double?> PingAsync(IPAddress address, int port, TimeSpan timeout, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(address);

        using Socket socket = new(address.AddressFamily, SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
        using CancellationTokenSource timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(timeout);

        long start = Stopwatch.GetTimestamp();
        try
        {
            await socket.ConnectAsync(new IPEndPoint(address, port), timeoutCts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return null;
        }
        catch (SocketException)
        {
            return null;
        }

        return Stopwatch.GetElapsedTime(start).TotalMilliseconds;
    }

    /// <summary>Runs <paramref name="count"/> sequential pings spaced by <paramref name="interval"/>.</summary>
    public static async Task<List<double>> PingSeriesAsync(IPAddress address, int port, int count, TimeSpan interval, CancellationToken cancellationToken)
    {
        List<double> samples = [];
        for (int i = 0; i < count; i++)
        {
            if (i > 0)
            {
                await Task.Delay(interval, cancellationToken).ConfigureAwait(false);
            }

            if (await PingAsync(address, port, TimeSpan.FromSeconds(2), cancellationToken).ConfigureAwait(false) is double rtt)
            {
                samples.Add(rtt);
            }
        }

        return samples;
    }
}
