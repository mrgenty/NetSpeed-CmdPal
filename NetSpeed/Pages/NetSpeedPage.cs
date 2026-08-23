using System.Globalization;
using System.Threading;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace NetSpeed;

internal sealed partial class NetSpeedPage : ListPage
{
    private static readonly CultureInfo DisplayCulture = CultureInfo.InvariantCulture;

    private readonly SpeedTestService _speedTest = new();
    private IListItem[] _items;
    private int _testInProgress;

    public NetSpeedPage()
    {
        Icon = IconHelpers.FromRelativePath("Assets\\StoreLogo.png");
        Title = "NetSpeed";
        Name = "Open";
        _items = BuildIdleItems();
    }

    public override IListItem[] GetItems() => _items;

    private void RequestRun()
    {
        _ = RunAsync();
    }

    private async Task RunAsync()
    {
        if (Interlocked.Exchange(ref _testInProgress, 1) == 1)
        {
            return;
        }

        try
        {
            _items = BuildRunningItems();
            RaiseItemsChanged();

            LibreSpeedResult result = await _speedTest.RunAsync().ConfigureAwait(false);
            _items = BuildResultItems(result);
        }
        catch (TimeoutException ex)
        {
            _items = BuildErrorItems("Speed test timed out", ex.Message);
        }
        catch (FileNotFoundException ex)
        {
            _items = BuildErrorItems("LibreSpeed component missing", ex.Message);
        }
        catch (Exception ex)
        {
            _items = BuildErrorItems("Speed test failed", FriendlyMessage(ex.Message));
        }
        finally
        {
            Volatile.Write(ref _testInProgress, 0);
            RaiseItemsChanged();
        }
    }

    private IListItem[] BuildIdleItems() =>
    [
        new ListItem(new RunSpeedTestCommand(RequestRun))
        {
            Title = "▶ Run speed test",
            Subtitle = "Measures ping, jitter, download and upload using the fastest available LibreSpeed server",
        },
        new ListItem(new NoOpCommand())
        {
            Title = "Privacy-first",
            Subtitle = "No NetSpeed telemetry and no result sharing. Only traffic required by the speed test is generated.",
        },
        new ListItem(new OpenUrlCommand(SpeedTestService.LibreSpeedWebsite))
        {
            Title = "LibreSpeed",
            Subtitle = "Open librespeed.org",
        },
        new ListItem(new OpenUrlCommand(SpeedTestService.DeveloperWebsite))
        {
            Title = "Developer: Michele Gentilini",
            Subtitle = "genty.me",
        },
    ];

    private IListItem[] BuildRunningItems() =>
    [
        new ListItem(new NoOpCommand())
        {
            Title = "⏱ Speed test in progress…",
            Subtitle = "Selecting the fastest server, then testing download and upload. This can take around 20–30 seconds.",
        },
        new ListItem(new NoOpCommand())
        {
            Title = "Please keep this page open",
            Subtitle = "NetSpeed is using LibreSpeed public test infrastructure.",
        },
    ];

    private IListItem[] BuildResultItems(LibreSpeedResult r)
    {
        string serverName = string.IsNullOrWhiteSpace(r.Server.Name) ? "LibreSpeed server" : r.Server.Name;
        string serverHost = GetHost(r.Server.Url);
        string organization = string.IsNullOrWhiteSpace(r.Client.Organization) ? "ISP unavailable" : r.Client.Organization;
        string location = BuildLocation(r.Client);
        string testedAt = r.Timestamp == default
            ? DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm:ss", DisplayCulture)
            : r.Timestamp.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss", DisplayCulture);

        return
        [
            new ListItem(new NoOpCommand())
            {
                Title = $"↓ {N(r.Download)} Mbps",
                Subtitle = "Download",
            },
            new ListItem(new NoOpCommand())
            {
                Title = $"↑ {N(r.Upload)} Mbps",
                Subtitle = "Upload",
            },
            new ListItem(new NoOpCommand())
            {
                Title = $"Latency {N(r.Ping)} ms",
                Subtitle = $"Jitter {N(r.Jitter)} ms",
            },
            new ListItem(new NoOpCommand())
            {
                Title = serverName,
                Subtitle = string.IsNullOrWhiteSpace(serverHost) ? "Selected LibreSpeed server" : $"Server: {serverHost}",
            },
            new ListItem(new NoOpCommand())
            {
                Title = organization,
                Subtitle = string.IsNullOrWhiteSpace(location) ? "Client network" : location,
            },
            new ListItem(new NoOpCommand())
            {
                Title = $"Transferred {FormatBytes(r.BytesReceived + r.BytesSent)}",
                Subtitle = $"Received {FormatBytes(r.BytesReceived)} • Sent {FormatBytes(r.BytesSent)} • {testedAt}",
            },
            new ListItem(new RunSpeedTestCommand(RequestRun))
            {
                Title = "↻ Run again",
                Subtitle = "Start a new speed test",
            },
            new ListItem(new OpenUrlCommand(SpeedTestService.LibreSpeedCliWebsite))
            {
                Title = "About the test engine",
                Subtitle = "LibreSpeed CLI 1.0.13 • LGPL-3.0",
            },
            new ListItem(new OpenUrlCommand(SpeedTestService.DeveloperWebsite))
            {
                Title = "Developer: Michele Gentilini",
                Subtitle = "genty.me",
            },
        ];
    }

    private IListItem[] BuildErrorItems(string title, string detail) =>
    [
        new ListItem(new NoOpCommand())
        {
            Title = $"⚠ {title}",
            Subtitle = detail,
        },
        new ListItem(new RunSpeedTestCommand(RequestRun))
        {
            Title = "↻ Try again",
            Subtitle = "Start a new speed test",
        },
        new ListItem(new OpenUrlCommand(SpeedTestService.LibreSpeedWebsite))
        {
            Title = "Check LibreSpeed",
            Subtitle = "Open librespeed.org",
        },
    ];

    private static string N(double value) => value.ToString("0.0", DisplayCulture);

    private static string GetHost(string? url)
    {
        return Uri.TryCreate(url, UriKind.Absolute, out Uri? uri) ? uri.Host : string.Empty;
    }

    private static string BuildLocation(LibreSpeedClient client)
    {
        string[] parts = [client.City ?? string.Empty, client.Region ?? string.Empty, client.Country ?? string.Empty];
        return string.Join(", ", parts.Where(static part => !string.IsNullOrWhiteSpace(part)).Distinct(StringComparer.OrdinalIgnoreCase));
    }

    private static string FormatBytes(ulong bytes)
    {
        const double kib = 1024d;
        const double mib = kib * 1024d;
        const double gib = mib * 1024d;

        return bytes switch
        {
            >= (ulong)gib => $"{bytes / gib:0.00} GiB",
            >= (ulong)mib => $"{bytes / mib:0.0} MiB",
            >= (ulong)kib => $"{bytes / kib:0.0} KiB",
            _ => $"{bytes} B",
        };
    }

    private static string FriendlyMessage(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return "Unknown error.";
        }

        string singleLine = message.Replace('\r', ' ').Replace('\n', ' ').Trim();
        return singleLine.Length <= 220 ? singleLine : singleLine[..217] + "…";
    }
}
