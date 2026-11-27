using System.Text.Json;
using System.Text.Json.Serialization;

namespace NetSpeed.Core;

public sealed class LocalState
{
    /// <summary>Version of the data-policy notice the user accepted; 0 = never.</summary>
    public int ConsentVersion { get; set; }

    public DateTimeOffset? ConsentedAt { get; set; }

    /// <summary>Start times of recent tests, for the local fair-use limit.</summary>
    public List<DateTimeOffset> RecentTests { get; set; } = [];
}

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(LocalState))]
internal sealed partial class LocalStateJsonContext : JsonSerializerContext
{
}

/// <summary>
/// Persists the user's consent to the M-Lab data policy and enforces the M-Lab AUP limit for
/// interactive users (no more than 40 tests per day, applied as a rolling 24-hour window).
/// Everything stays on this device.
/// </summary>
public sealed class LocalStateStore
{
    /// <summary>Bump when the consent text changes materially, to ask again.</summary>
    public const int CurrentConsentVersion = 1;

    public const int MaxTestsPerDay = 40;

    private static readonly TimeSpan Window = TimeSpan.FromHours(24);

    private readonly string _path;
    private readonly TimeProvider _time;
    private readonly Lock _gate = new();
    private LocalState _state;

    public LocalStateStore(string path, TimeProvider? timeProvider = null)
    {
        _path = path ?? throw new ArgumentNullException(nameof(path));
        _time = timeProvider ?? TimeProvider.System;
        _state = Load(path);
    }

    /// <summary>Default location: %LOCALAPPDATA%\NetSpeed\state.json (virtualised per package under MSIX).</summary>
    public static string DefaultPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NetSpeed", "state.json");

    public bool HasConsent
    {
        get
        {
            lock (_gate)
            {
                return _state.ConsentVersion >= CurrentConsentVersion;
            }
        }
    }

    public void GrantConsent()
    {
        lock (_gate)
        {
            _state.ConsentVersion = CurrentConsentVersion;
            _state.ConsentedAt = _time.GetUtcNow();
            Save();
        }
    }

    public void RevokeConsent()
    {
        lock (_gate)
        {
            _state.ConsentVersion = 0;
            _state.ConsentedAt = null;
            Save();
        }
    }

    /// <summary>Tests still allowed in the current 24-hour window.</summary>
    public int RemainingTests
    {
        get
        {
            lock (_gate)
            {
                Prune();
                return Math.Max(0, MaxTestsPerDay - _state.RecentTests.Count);
            }
        }
    }

    /// <summary>Records a test start if the limit allows it.</summary>
    public bool TryRegisterTest()
    {
        lock (_gate)
        {
            Prune();
            if (_state.RecentTests.Count >= MaxTestsPerDay)
            {
                return false;
            }

            _state.RecentTests.Add(_time.GetUtcNow());
            Save();
            return true;
        }
    }

    private void Prune()
    {
        DateTimeOffset cutoff = _time.GetUtcNow() - Window;
        _state.RecentTests.RemoveAll(t => t <= cutoff);
    }

    private static LocalState Load(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                using FileStream stream = File.OpenRead(path);
                return JsonSerializer.Deserialize(stream, LocalStateJsonContext.Default.LocalState) ?? new LocalState();
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            // A corrupt or unreadable file means "no consent yet": the user will simply be asked again.
        }

        return new LocalState();
    }

    private void Save()
    {
        try
        {
            string? directory = Path.GetDirectoryName(_path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            string temp = _path + ".tmp";
            File.WriteAllText(temp, JsonSerializer.Serialize(_state, LocalStateJsonContext.Default.LocalState));
            File.Move(temp, _path, overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Best effort: the in-memory state still applies for this session.
        }
    }
}
