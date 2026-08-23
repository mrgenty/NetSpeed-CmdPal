using System.Diagnostics;
using System.Text.Json;

namespace NetSpeed;

internal sealed class SpeedTestService
{
    public const string LibreSpeedWebsite = "https://librespeed.org/";
    public const string LibreSpeedCliWebsite = "https://github.com/librespeed/speedtest-cli";
    public const string DeveloperWebsite = "https://genty.me/";

    private static readonly TimeSpan OverallTimeout = TimeSpan.FromSeconds(75);

    public async Task<LibreSpeedResult> RunAsync()
    {
        string executable = Path.Combine(AppContext.BaseDirectory, "ThirdParty", "LibreSpeed", "librespeed-cli.exe");
        if (!File.Exists(executable))
        {
            throw new FileNotFoundException(
                "LibreSpeed CLI is missing from the package. Rebuild after running scripts/Prepare-LibreSpeed.ps1.",
                executable);
        }

        ProcessStartInfo startInfo = new()
        {
            FileName = executable,
            Arguments = "--json --no-icmp --duration 10 --concurrent 4 --timeout 15",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = Path.GetDirectoryName(executable)!,
        };

        using Process process = new() { StartInfo = startInfo };
        if (!process.Start())
        {
            throw new InvalidOperationException("Unable to start LibreSpeed CLI.");
        }

        Task<string> stdoutTask = process.StandardOutput.ReadToEndAsync();
        Task<string> stderrTask = process.StandardError.ReadToEndAsync();
        using CancellationTokenSource timeout = new(OverallTimeout);

        try
        {
            await process.WaitForExitAsync(timeout.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            TryKill(process);
            throw new TimeoutException("The speed test exceeded 75 seconds and was stopped.");
        }

        string stdout = (await stdoutTask.ConfigureAwait(false)).Trim();
        string stderr = (await stderrTask.ConfigureAwait(false)).Trim();

        if (process.ExitCode != 0)
        {
            string detail = string.IsNullOrWhiteSpace(stderr) ? "LibreSpeed CLI returned a non-zero exit code." : stderr;
            throw new InvalidOperationException(detail);
        }

        if (string.IsNullOrWhiteSpace(stdout))
        {
            throw new InvalidOperationException("LibreSpeed CLI returned an empty result.");
        }

        LibreSpeedResult? result;
        try
        {
            result = JsonSerializer.Deserialize(stdout, NetSpeedJsonContext.Default.LibreSpeedResult);
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException("LibreSpeed CLI returned invalid JSON.", ex);
        }

        return result ?? throw new InvalidOperationException("LibreSpeed CLI returned no usable result.");
    }

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch
        {
            // Best effort only: the process may have already exited.
        }
    }
}
