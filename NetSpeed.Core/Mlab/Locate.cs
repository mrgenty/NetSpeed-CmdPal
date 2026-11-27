using System.Net;
using System.Text.Json;

namespace NetSpeed.Core.Mlab;

/// <summary>One server returned by Locate v2, with ready-to-use, token-bearing URLs.</summary>
public sealed record MlabTarget(
    string Machine,
    string Hostname,
    string? City,
    string? Country,
    Uri DownloadUrl,
    Uri UploadUrl);

/// <summary>Parses Locate v2 "nearest" answers.</summary>
public static class LocateParser
{
    /// <summary>
    /// Returns the usable targets in Locate order. Results lacking either URL, or with a
    /// non-wss URL, are skipped.
    /// </summary>
    public static IReadOnlyList<MlabTarget> Parse(string json, MlabProtocol protocol)
    {
        ArgumentNullException.ThrowIfNull(protocol);

        LocateResponse? response;
        try
        {
            response = JsonSerializer.Deserialize(json, MlabJsonContext.Default.LocateResponse);
        }
        catch (JsonException ex)
        {
            throw new SpeedTestException(SpeedTestErrorKind.LocateFailed, "M-Lab Locate returned invalid JSON.", ex);
        }

        List<MlabTarget> targets = [];
        foreach (LocateResult result in response?.Results ?? [])
        {
            if (result.Urls is null
                || !result.Urls.TryGetValue(protocol.DownloadKey, out string? download)
                || !result.Urls.TryGetValue(protocol.UploadKey, out string? upload)
                || !Uri.TryCreate(download, UriKind.Absolute, out Uri? downloadUri)
                || !Uri.TryCreate(upload, UriKind.Absolute, out Uri? uploadUri)
                || downloadUri.Scheme != "wss"
                || uploadUri.Scheme != "wss")
            {
                continue;
            }

            string host = !string.IsNullOrWhiteSpace(result.Hostname) ? result.Hostname : downloadUri.Host;
            string machine = !string.IsNullOrWhiteSpace(result.Machine) ? result.Machine : host;
            targets.Add(new MlabTarget(machine, host, result.Location?.City, result.Location?.Country, downloadUri, uploadUri));
        }

        return targets;
    }
}

/// <summary>Minimal Locate v2 client (https://locate.measurementlab.net/v2/nearest/{service}).</summary>
public sealed class LocateClient
{
    public static readonly Uri BaseUri = new("https://locate.measurementlab.net/v2/nearest/");

    private readonly HttpClient _http;
    private readonly string _userAgent;

    public LocateClient(HttpClient http, string userAgent)
    {
        _http = http ?? throw new ArgumentNullException(nameof(http));
        _userAgent = userAgent;
    }

    public async Task<IReadOnlyList<MlabTarget>> NearestAsync(MlabProtocol protocol, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(protocol);

        using HttpRequestMessage request = new(HttpMethod.Get, new Uri(BaseUri, protocol.LocateService));
        request.Headers.TryAddWithoutValidation("User-Agent", _userAgent);
        request.Headers.TryAddWithoutValidation("Accept", "application/json");

        HttpResponseMessage response;
        try
        {
            response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        }
        catch (HttpRequestException ex)
        {
            throw new SpeedTestException(SpeedTestErrorKind.Network, "Could not reach M-Lab (locate.measurementlab.net). Check your Internet connection.", ex);
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            throw new SpeedTestException(SpeedTestErrorKind.Timeout, "M-Lab Locate did not answer in time.", ex);
        }

        using (response)
        {
            return await InterpretAsync(response, protocol, cancellationToken).ConfigureAwait(false);
        }
    }

    internal static async Task<IReadOnlyList<MlabTarget>> InterpretAsync(HttpResponseMessage response, MlabProtocol protocol, CancellationToken cancellationToken)
    {
        // ndt7 spec, "Server discovery": 200 = success, 204 = no capacity, anything else = failure.
        if (response.StatusCode == HttpStatusCode.NoContent)
        {
            throw new SpeedTestException(SpeedTestErrorKind.NoCapacity, "M-Lab has no free test capacity right now. Please try again in a few minutes.");
        }

        if (response.StatusCode != HttpStatusCode.OK)
        {
            throw new SpeedTestException(
                SpeedTestErrorKind.LocateFailed,
                $"M-Lab Locate answered HTTP {(int)response.StatusCode} for {protocol.LocateService}.");
        }

        string json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        IReadOnlyList<MlabTarget> targets = LocateParser.Parse(json, protocol);
        if (targets.Count == 0)
        {
            throw new SpeedTestException(SpeedTestErrorKind.LocateFailed, $"M-Lab Locate returned no usable {protocol.DisplayName} server.");
        }

        return targets;
    }
}
