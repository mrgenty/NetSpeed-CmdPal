using System.Text;

namespace NetSpeed.Core.Mlab;

public static class TestUriBuilder
{
    /// <summary>
    /// Appends query parameters to a Locate URL. Existing keys (e.g. access_token) are kept and
    /// never duplicated (ndt7 spec: clients MUST NOT send duplicate keys or keys without a value).
    /// Parameters with an empty value are skipped.
    /// </summary>
    public static Uri AddQuery(Uri baseUri, IEnumerable<KeyValuePair<string, string>> parameters)
    {
        ArgumentNullException.ThrowIfNull(baseUri);
        ArgumentNullException.ThrowIfNull(parameters);

        string query = baseUri.Query.TrimStart('?');
        HashSet<string> existing = new(StringComparer.Ordinal);
        foreach (string part in query.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            int eq = part.IndexOf('=', StringComparison.Ordinal);
            existing.Add(Uri.UnescapeDataString(eq < 0 ? part : part[..eq]));
        }

        StringBuilder builder = new(query);
        foreach ((string key, string value) in parameters)
        {
            if (string.IsNullOrEmpty(key) || string.IsNullOrEmpty(value) || !existing.Add(key))
            {
                continue;
            }

            if (builder.Length > 0)
            {
                builder.Append('&');
            }

            builder.Append(Uri.EscapeDataString(key)).Append('=').Append(Uri.EscapeDataString(value));
        }

        UriBuilder result = new(baseUri) { Query = builder.ToString() };
        return result.Uri;
    }
}
