using System.Globalization;
using System.Net;
using System.Text.Json;

namespace PkgViewer.Infrastructure;

/// <summary>Result of an update check: the newest release tag, its page, and the release notes.</summary>
internal sealed record UpdateCheck(string Latest, string PageUrl, string Notes)
{
    public bool IsNewerThan(string current)
    {
        int? comparison = UpdateChecker.CompareVersions(current, Latest);
        return comparison is < 0;
    }
}

/// <summary>
/// Lightweight GitHub Releases check for PkgViewer. Reads the latest published release (drafts and
/// prereleases are excluded by the API) and compares its tag with the running assembly version.
/// </summary>
internal static class UpdateChecker
{
    public const string RepositoryUrl = "https://github.com/pearlxcore/PkgViewer";
    private const string LatestReleaseApiUrl = "https://api.github.com/repos/pearlxcore/PkgViewer/releases/latest";

    /// <summary>The running assembly version, for example "1.2.0".</summary>
    public static string AppVersion()
    {
        Version? version = typeof(UpdateChecker).Assembly.GetName().Version;
        if (version is null) return "0.0.0";
        return version.Build >= 0
            ? $"{version.Major}.{version.Minor}.{version.Build}"
            : $"{version.Major}.{version.Minor}";
    }

    /// <summary>Returns the latest release, or null when the repository has no published release.</summary>
    public static async Task<UpdateCheck?> CheckAsync(CancellationToken cancellationToken = default)
    {
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("PkgViewer");

        string json;
        try
        {
            json = await client.GetStringAsync(LatestReleaseApiUrl, cancellationToken).ConfigureAwait(false);
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return null; // no published release yet
        }

        using JsonDocument document = JsonDocument.Parse(json);
        string tag = document.RootElement.TryGetProperty("tag_name", out JsonElement tagElement)
            ? tagElement.GetString() ?? string.Empty
            : string.Empty;
        string page = document.RootElement.TryGetProperty("html_url", out JsonElement urlElement)
            ? urlElement.GetString() ?? RepositoryUrl
            : RepositoryUrl;
        string notes = document.RootElement.TryGetProperty("body", out JsonElement bodyElement)
            ? bodyElement.GetString() ?? string.Empty
            : string.Empty;
        string latest = tag.TrimStart('v', 'V').Trim();
        return latest.Length == 0 ? null : new UpdateCheck(latest, page, notes.Trim());
    }

    /// <summary>Numeric dotted comparison; null when either side cannot be parsed.</summary>
    public static int? CompareVersions(string? left, string? right)
    {
        long[]? a = ParseVersion(left);
        long[]? b = ParseVersion(right);
        if (a is null || b is null) return null;
        int length = Math.Max(a.Length, b.Length);
        for (int i = 0; i < length; i++)
        {
            long x = i < a.Length ? a[i] : 0;
            long y = i < b.Length ? b[i] : 0;
            if (x != y) return x < y ? -1 : 1;
        }
        return 0;
    }

    private static long[]? ParseVersion(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var parts = new List<long>();
        foreach (string piece in value.Split('.', StringSplitOptions.RemoveEmptyEntries))
        {
            int digits = 0;
            while (digits < piece.Length && char.IsDigit(piece[digits])) digits++;
            if (digits == 0) break;
            if (!long.TryParse(piece[..digits], NumberStyles.None, CultureInfo.InvariantCulture, out long number))
                return null;
            parts.Add(number);
        }
        return parts.Count > 0 ? parts.ToArray() : null;
    }
}
