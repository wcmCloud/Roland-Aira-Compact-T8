using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text.Json;

namespace T8.Editor.Services;

/// <summary>A released version on GitHub: its tag, its version and its release page.</summary>
public sealed record ReleaseInfo(string Tag, ReleaseVersion Version, string Url);

public enum UpdateOutcome
{
    UpdateAvailable,
    UpToDate,
    Failed,
}

/// <summary>What a check found. <see cref="Release"/> is the latest release (when known); <see cref="Error"/> says why it failed.</summary>
public sealed record UpdateCheckResult(UpdateOutcome Outcome, ReleaseInfo? Release = null, string? Error = null, bool FromCache = false);

/// <summary>A release version: major.minor.patch, with an optional pre-release label (1.0.6-beta).</summary>
public sealed record ReleaseVersion(int Major, int Minor, int Patch, string? PreRelease = null) : IComparable<ReleaseVersion>
{
    public bool IsPreRelease => !string.IsNullOrEmpty(PreRelease);

    /// <summary>Parses "v1.0.6", "1.0.6", "1.0" or "v1.0.6-beta.1"; null for anything else.</summary>
    public static ReleaseVersion? Parse(string? tag)
    {
        if (string.IsNullOrWhiteSpace(tag))
            return null;
        var text = tag.Trim();
        if (text.StartsWith('v') || text.StartsWith('V'))
            text = text[1..];
        var plus = text.IndexOf('+'); // build metadata doesn't count
        if (plus >= 0)
            text = text[..plus];
        string? pre = null;
        var dash = text.IndexOf('-');
        if (dash >= 0)
        {
            pre = text[(dash + 1)..];
            text = text[..dash];
            if (pre.Length == 0)
                return null;
        }
        var parts = text.Split('.');
        if (parts.Length is < 2 or > 3 || parts.Any(p => p.Length == 0 || !p.All(char.IsAsciiDigit)))
            return null;
        if (!int.TryParse(parts[0], out var major) || !int.TryParse(parts[1], out var minor))
            return null;
        var patch = 0;
        if (parts.Length == 3 && !int.TryParse(parts[2], out patch))
            return null;
        return new ReleaseVersion(major, minor, patch, pre);
    }

    public static ReleaseVersion From(Version version) => new(version.Major, Math.Max(version.Minor, 0), Math.Max(version.Build, 0));

    /// <summary>Semantic version order: numbers first; a pre-release comes before its release.</summary>
    public int CompareTo(ReleaseVersion? other)
    {
        if (other is null)
            return 1;
        var byNumber = (Major, Minor, Patch).CompareTo((other.Major, other.Minor, other.Patch));
        if (byNumber != 0)
            return byNumber;
        return (IsPreRelease, other.IsPreRelease) switch
        {
            (false, false) => 0,
            (true, false) => -1,
            (false, true) => 1,
            _ => string.CompareOrdinal(PreRelease, other.PreRelease),
        };
    }

    public override string ToString() => IsPreRelease ? $"{Major}.{Minor}.{Patch}-{PreRelease}" : $"{Major}.{Minor}.{Patch}";
}

/// <summary>Where the last check's answer is kept, so the startup check asks GitHub at most once a day.</summary>
public sealed class UpdateCheckCache
{
    public DateTimeOffset? CheckedAt { get; set; }
    public string? LatestTag { get; set; }
    public string? LatestUrl { get; set; }

    public UpdateCheckCache Clone() => new() { CheckedAt = CheckedAt, LatestTag = LatestTag, LatestUrl = LatestUrl };
}

/// <summary>
/// Asks GitHub's public "latest release" endpoint whether there is a newer version. Sends a plain GET with only a
/// User-Agent (no personal data or identifiers), gives up after a short timeout, ignores drafts and pre-releases,
/// and keeps the answer for a day.
/// </summary>
public sealed class UpdateChecker(HttpClient http, ReleaseVersion current, Func<DateTimeOffset>? clock = null)
{
    public const string LatestReleaseUrl = "https://api.github.com/repos/wcmCloud/Roland-Aira-Compact-T8/releases/latest";
    public const string ReleasesPage = "https://github.com/wcmCloud/Roland-Aira-Compact-T8/releases";

    private readonly Func<DateTimeOffset> _clock = clock ?? (() => DateTimeOffset.UtcNow);

    public static TimeSpan CacheTime { get; } = TimeSpan.FromHours(24);

    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(8);

    public ReleaseVersion Current { get; } = current;

    /// <summary>
    /// Checks for a newer release. With <paramref name="cache"/> recent (under a day old) and <paramref name="useCache"/>
    /// on, uses it instead of asking GitHub. A successful answer is written into the cache.
    /// </summary>
    public async Task<UpdateCheckResult> CheckAsync(UpdateCheckCache cache, bool useCache, CancellationToken cancellationToken = default)
    {
        if (useCache && cache.CheckedAt is { } checkedAt && _clock() - checkedAt < CacheTime && cache.LatestTag is not null)
            return Compare(cache.LatestTag, cache.LatestUrl, fromCache: true);

        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(Timeout);
            using var request = new HttpRequestMessage(HttpMethod.Get, LatestReleaseUrl);
            request.Headers.UserAgent.Add(new ProductInfoHeaderValue("T8Editor", Current.ToString()));
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
            using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
            if (!response.IsSuccessStatusCode)
                return Failed(response.StatusCode switch
                {
                    System.Net.HttpStatusCode.Forbidden or System.Net.HttpStatusCode.TooManyRequests => "GitHub is limiting requests; try again later",
                    System.Net.HttpStatusCode.NotFound => "no release found",
                    _ => $"GitHub answered {(int)response.StatusCode}",
                });

            await using var stream = await response.Content.ReadAsStreamAsync(timeout.Token);
            using var json = await JsonDocument.ParseAsync(stream, cancellationToken: timeout.Token);
            var root = json.RootElement;
            if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("tag_name", out var tagElement) || tagElement.ValueKind != JsonValueKind.String)
                return Failed("unexpected answer from GitHub");
            var draft = root.TryGetProperty("draft", out var d) && d.ValueKind == JsonValueKind.True;
            var prerelease = root.TryGetProperty("prerelease", out var p) && p.ValueKind == JsonValueKind.True;
            var tag = tagElement.GetString()!;
            var url = root.TryGetProperty("html_url", out var u) && u.ValueKind == JsonValueKind.String ? u.GetString() : null;
            if (draft || prerelease)
                return new UpdateCheckResult(UpdateOutcome.UpToDate); // only full releases count

            var result = Compare(tag, url, fromCache: false);
            if (result.Outcome != UpdateOutcome.Failed)
            {
                cache.CheckedAt = _clock();
                cache.LatestTag = tag;
                cache.LatestUrl = result.Release?.Url;
            }
            return result;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Failed("GitHub didn't answer in time");
        }
        catch (HttpRequestException)
        {
            return Failed("no connection to GitHub");
        }
        catch (JsonException)
        {
            return Failed("unexpected answer from GitHub");
        }
    }

    private UpdateCheckResult Compare(string tag, string? url, bool fromCache)
    {
        var latest = ReleaseVersion.Parse(tag);
        if (latest is null)
            return Failed($"the latest release has an unknown version ({tag})");
        if (latest.IsPreRelease)
            return new UpdateCheckResult(UpdateOutcome.UpToDate, FromCache: fromCache);
        var release = new ReleaseInfo(tag, latest, IsGitHubUrl(url) ? url! : $"{ReleasesPage}/tag/{Uri.EscapeDataString(tag)}");
        return latest.CompareTo(Current) > 0
            ? new UpdateCheckResult(UpdateOutcome.UpdateAvailable, release, FromCache: fromCache)
            : new UpdateCheckResult(UpdateOutcome.UpToDate, release, FromCache: fromCache);
    }

    /// <summary>Only release pages on github.com are opened.</summary>
    private static bool IsGitHubUrl(string? url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps && uri.Host == "github.com";

    private static UpdateCheckResult Failed(string error) => new(UpdateOutcome.Failed, Error: error);
}

/// <summary>Opens a web page in the browser; behind an interface so tests can check what would be opened.</summary>
public interface IUrlOpener
{
    void Open(string url);
}

public sealed class BrowserUrlOpener : IUrlOpener
{
    public void Open(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch (Exception)
        {
            // No browser to open.
        }
    }
}
