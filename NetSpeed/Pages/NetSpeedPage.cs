using System.Diagnostics;
using System.Globalization;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using NetSpeed.Core;
using NetSpeed.Core.Mlab;

namespace NetSpeed;

internal sealed partial class NetSpeedPage : ListPage
{
    public const string Version = "0.2.0";
    public const string DeveloperWebsite = "https://genty.me/";
    public const string MlabWebsite = "https://www.measurementlab.net/";
    public const string MlabPrivacyPolicy = "https://www.measurementlab.net/privacy/";
    public const string MlabAup = "https://www.measurementlab.net/aup/";
    public const string ProjectPrivacy = "https://github.com/mrgenty/NetSpeed-CmdPal/blob/main/PRIVACY.md";

    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;
    private static readonly TimeSpan OverallTimeout = TimeSpan.FromSeconds(90);
    private static readonly TimeSpan UiRefreshInterval = TimeSpan.FromMilliseconds(250);
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(15) };

    private readonly LocalStateStore _state = new(LocalStateStore.DefaultPath);
#pragma warning disable CA1859 // The page depends on the engine abstraction on purpose.
    private readonly ISpeedEngine _engine = new MlabEngine(Http, new MlabEngineOptions { ClientVersion = Version });
#pragma warning restore CA1859
    private readonly Lock _progressGate = new();
    private IListItem[] _items;
    private int _testInProgress;
    private CancellationTokenSource? _cts;
    private long _lastRefreshTicks;

    public NetSpeedPage()
    {
        Icon = IconHelpers.FromRelativePath("Assets\\StoreLogo.png");
        Title = "NetSpeed";
        Name = "Open";
        _items = BuildIdleItems();
    }

    public override IListItem[] GetItems() => Volatile.Read(ref _items);

    private void SetItems(IListItem[] items)
    {
        Volatile.Write(ref _items, items);
        RaiseItemsChanged();
    }

    private void RequestRun()
    {
        if (!_state.HasConsent)
        {
            SetItems(BuildConsentItems());
            return;
        }

        _ = RunAsync();
    }

    private void AcceptAndRun()
    {
        _state.GrantConsent();
        _ = RunAsync();
    }

    private void RevokeConsent()
    {
        _state.RevokeConsent();
        SetItems(BuildIdleItems());
    }

    private void Cancel() => _cts?.Cancel();

    private async Task RunAsync()
    {
        if (Interlocked.Exchange(ref _testInProgress, 1) == 1)
        {
            return;
        }

        try
        {
            if (!_state.TryRegisterTest())
            {
                SetItems(BuildErrorItems(
                    "Daily test limit reached",
                    $"M-Lab's fair-use policy allows up to {LocalStateStore.MaxTestsPerDay} tests per day. Please try again later."));
                return;
            }

            using CancellationTokenSource cts = new(OverallTimeout);
            _cts = cts;
            IsLoading = true;
            SetItems(BuildRunningItems(new SpeedProgress(SpeedPhase.Locating, 0, null, "Contacting M-Lab…")));

            InlineProgress progress = new(OnProgress);
            try
            {
                SpeedResult result = await Task.Run(() => _engine.RunAsync(progress, cts.Token), cts.Token).ConfigureAwait(false);
                SetItems(BuildResultItems(result));
            }
            catch (OperationCanceledException) when (cts.IsCancellationRequested)
            {
                bool timedOut = cts.Token.IsCancellationRequested && !_userCancelled;
                SetItems(timedOut
                    ? BuildErrorItems("Speed test timed out", $"The test did not finish within {OverallTimeout.TotalSeconds:0} seconds and was stopped.")
                    : BuildIdleItems("Speed test cancelled."));
            }
            catch (SpeedTestException ex)
            {
                SetItems(BuildErrorItems(ErrorTitle(ex.Kind), FriendlyMessage(ex.Message)));
            }
            catch (Exception ex) when (ex is HttpRequestException or System.Net.Sockets.SocketException or IOException)
            {
                SetItems(BuildErrorItems("Network error", FriendlyMessage(ex.Message)));
            }
#pragma warning disable CA1031 // Last line of defence: never crash the extension host.
            catch (Exception ex)
#pragma warning restore CA1031
            {
                Debug.WriteLine(ex);
                SetItems(BuildErrorItems("Speed test failed", FriendlyMessage(ex.Message)));
            }
        }
        finally
        {
            _cts = null;
            _userCancelled = false;
            IsLoading = false;
            Volatile.Write(ref _testInProgress, 0);
        }
    }

    private bool _userCancelled;

    private void UserCancel()
    {
        _userCancelled = true;
        Cancel();
    }

    private void OnProgress(SpeedProgress p)
    {
        // Throttle UI refreshes; always show phase changes.
        long now = Stopwatch.GetTimestamp();
        lock (_progressGate)
        {
            if (p.CurrentMbps is not null && Stopwatch.GetElapsedTime(_lastRefreshTicks, now) < UiRefreshInterval)
            {
                return;
            }

            _lastRefreshTicks = now;
        }

        if (Volatile.Read(ref _testInProgress) == 1)
        {
            SetItems(BuildRunningItems(p));
        }
    }

    // ---- Item builders ----------------------------------------------------

    private IListItem[] BuildIdleItems(string? notice = null)
    {
        List<IListItem> items =
        [
            new ListItem(new ActionCommand("Run speed test", RequestRun))
            {
                Title = "▶ Run speed test",
                Subtitle = notice ?? "Download, upload, latency, jitter and bufferbloat via Measurement Lab (M-Lab)",
            },
            new ListItem(new OpenUrlCommand(MlabWebsite))
            {
                Title = "Powered by Measurement Lab (M-Lab)",
                Subtitle = "Open, non-commercial Internet measurement platform • results are published as open data",
            },
        ];

        if (_state.HasConsent)
        {
            items.Add(new ListItem(new ActionCommand("Withdraw consent", RevokeConsent))
            {
                Title = "Withdraw data-policy consent",
                Subtitle = "NetSpeed will ask again before the next test",
            });
        }

        items.Add(new ListItem(new OpenUrlCommand(DeveloperWebsite))
        {
            Title = "Developer: g_",
            Subtitle = "genty.me",
        });
        return [.. items];
    }

    private IListItem[] BuildConsentItems() =>
    [
        new ListItem(new NoOpCommand())
        {
            Title = "Before your first test: your result becomes public data",
            Subtitle = "Tests run on M-Lab servers. M-Lab publishes every result, including your public IP address, as open data.",
        },
        new ListItem(new ActionCommand("Agree and run", AcceptAndRun))
        {
            Title = "✓ I agree — run the speed test",
            Subtitle = "You accept M-Lab's privacy policy and acceptable use policy. Your choice is saved on this device only.",
        },
        new ListItem(new OpenUrlCommand(MlabPrivacyPolicy))
        {
            Title = "Read M-Lab privacy policy",
            Subtitle = MlabPrivacyPolicy,
        },
        new ListItem(new OpenUrlCommand(MlabAup))
        {
            Title = "Read M-Lab acceptable use policy",
            Subtitle = MlabAup,
        },
        new ListItem(new OpenUrlCommand(ProjectPrivacy))
        {
            Title = "Read NetSpeed privacy notice",
            Subtitle = "What NetSpeed itself stores (nothing leaves your PC except the test traffic)",
        },
        new ListItem(new ActionCommand("Not now", () => SetItems(BuildIdleItems("No test was run."))))
        {
            Title = "Not now",
            Subtitle = "Go back without testing",
        },
    ];

    private IListItem[] BuildRunningItems(SpeedProgress p)
    {
        string percent = (Math.Clamp(p.Fraction, 0, 1) * 100).ToString("0", Invariant) + "%";
        string title = p.Phase switch
        {
            SpeedPhase.Download => $"↓ {ResultFormatter.Mbps(p.CurrentMbps ?? 0)} Mbps",
            SpeedPhase.Upload => $"↑ {ResultFormatter.Mbps(p.CurrentMbps ?? 0)} Mbps",
            SpeedPhase.Latency => "⏱ Measuring latency…",
            _ => "⏱ Preparing speed test…",
        };
        string phase = p.Phase switch
        {
            SpeedPhase.Download => "Download",
            SpeedPhase.Upload => "Upload",
            SpeedPhase.Latency => "Latency",
            _ => "Locating server",
        };

        return
        [
            new ListItem(new NoOpCommand())
            {
                Title = title,
                Subtitle = $"{phase} • {percent}" + (string.IsNullOrEmpty(p.Detail) ? string.Empty : $" • {p.Detail}"),
            },
            new ListItem(new ActionCommand("Cancel", UserCancel))
            {
                Title = "✕ Cancel test",
                Subtitle = "Stop the test now",
            },
        ];
    }

    private IListItem[] BuildResultItems(SpeedResult r)
    {
        string latencySubtitle = r.JitterMs is double jitter
            ? $"Idle latency • jitter {ResultFormatter.Ms(jitter)} • {r.LatencyMethod}"
            : $"Idle latency • {r.LatencyMethod}";

        List<IListItem> items =
        [
            new ListItem(new NoOpCommand())
            {
                Title = $"↓ {ResultFormatter.Mbps(r.DownloadMbps)} Mbps",
                Subtitle = $"Download (after ramp-up) • full-phase average {ResultFormatter.Mbps(r.DownloadAverageMbps)} Mbps",
            },
            new ListItem(new NoOpCommand())
            {
                Title = $"↑ {ResultFormatter.Mbps(r.UploadMbps)} Mbps",
                Subtitle = $"Upload (after ramp-up) • full-phase average {ResultFormatter.Mbps(r.UploadAverageMbps)} Mbps",
            },
            new ListItem(new NoOpCommand())
            {
                Title = $"Latency {ResultFormatter.Ms(r.IdleLatencyMs)}",
                Subtitle = latencySubtitle,
            },
        ];

        if (r.DownloadLoadedLatencyMs is not null || r.UploadLoadedLatencyMs is not null)
        {
            string bloat = r.BufferbloatMs is double b
                ? $"Bufferbloat +{b.ToString("0", Invariant)} ms ({r.BufferbloatGrade})"
                : "Bufferbloat n/a";
            items.Add(new ListItem(new NoOpCommand())
            {
                Title = bloat,
                Subtitle = $"Latency under load: download {ResultFormatter.Ms(r.DownloadLoadedLatencyMs)} • upload {ResultFormatter.Ms(r.UploadLoadedLatencyMs)}",
            });
        }

        items.Add(new ListItem(new CopyTextCommand(ResultFormatter.ToClipboardText(r)))
        {
            Title = "Copy result",
            Subtitle = "Copy a plain-text summary (your IP address is not included)",
        });
        items.Add(new ListItem(new NoOpCommand())
        {
            Title = $"Server: {ResultFormatter.ServerPlace(r)}",
            Subtitle = $"{r.ServerMachine} • {r.EngineName} {r.Protocol} • {r.Streams} stream{(r.Streams == 1 ? string.Empty : "s")}",
        });
        items.Add(new ListItem(new NoOpCommand())
        {
            Title = $"Transferred {ResultFormatter.Bytes(r.BytesReceived + r.BytesSent)}",
            Subtitle = $"Received {ResultFormatter.Bytes(r.BytesReceived)} • sent {ResultFormatter.Bytes(r.BytesSent)} • "
                + r.Timestamp.ToString("yyyy-MM-dd HH:mm:ss", Invariant)
                + (r.ClientAddress is null ? string.Empty : $" • your IP {r.ClientAddress}"),
        });
        items.Add(new ListItem(new ActionCommand("Run again", RequestRun))
        {
            Title = "↻ Run again",
            Subtitle = $"{_state.RemainingTests} of {LocalStateStore.MaxTestsPerDay} tests left in the last 24 hours (M-Lab fair use)",
        });
        items.Add(new ListItem(new OpenUrlCommand(MlabWebsite))
        {
            Title = "About Measurement Lab",
            Subtitle = "This result is published by M-Lab as open data",
        });
        items.Add(new ListItem(new OpenUrlCommand(DeveloperWebsite))
        {
            Title = "Developer: g_",
            Subtitle = "genty.me",
        });
        return [.. items];
    }

    private IListItem[] BuildErrorItems(string title, string detail) =>
    [
        new ListItem(new NoOpCommand())
        {
            Title = $"⚠ {title}",
            Subtitle = detail,
        },
        new ListItem(new ActionCommand("Try again", RequestRun))
        {
            Title = "↻ Try again",
            Subtitle = "Start a new speed test",
        },
        new ListItem(new OpenUrlCommand(MlabWebsite))
        {
            Title = "Measurement Lab status and information",
            Subtitle = "measurementlab.net",
        },
    ];

    private static string ErrorTitle(SpeedTestErrorKind kind) => kind switch
    {
        SpeedTestErrorKind.NoCapacity => "M-Lab is at capacity",
        SpeedTestErrorKind.AllServersRejected => "Servers are busy",
        SpeedTestErrorKind.LocateFailed => "No test server available",
        SpeedTestErrorKind.Timeout => "Speed test timed out",
        SpeedTestErrorKind.Network => "Network error",
        SpeedTestErrorKind.RateLimited => "Daily test limit reached",
        _ => "Speed test failed",
    };

    private static string FriendlyMessage(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return "Unknown error.";
        }

        string singleLine = message.Replace('\r', ' ').Replace('\n', ' ').Trim();
        return singleLine.Length <= 220 ? singleLine : singleLine[..217] + "…";
    }

    /// <summary>
    /// Reports synchronously on the engine's thread. <see cref="Progress{T}"/> would post to the
    /// thread pool (no synchronization context in the COM server) and could reorder updates.
    /// </summary>
    private sealed class InlineProgress(Action<SpeedProgress> handler) : IProgress<SpeedProgress>
    {
        public void Report(SpeedProgress value) => handler(value);
    }
}
