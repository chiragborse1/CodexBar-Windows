using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CodexBar.Windows;

internal sealed record UpdateInfo(
    string TagName,
    string Url,
    bool Prerelease,
    bool IsNewer);

internal static class UpdateChecker
{
    private const string ReleasesUrl = "https://api.github.com/repos/chiragborse1/CodexBar-Windows/releases";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public static async Task<UpdateInfo?> CheckAsync(CancellationToken cancellationToken)
    {
        using var client = new HttpClient();
        client.DefaultRequestHeaders.UserAgent.ParseAdd($"{AppInfo.DisplayName}/{AppInfo.Version}");

        using var response = await client.GetAsync(ReleasesUrl, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        var releases = await JsonSerializer.DeserializeAsync<List<GitHubRelease>>(
            stream,
            JsonOptions,
            cancellationToken).ConfigureAwait(false);

        var release = releases?
            .Where(item => !item.Draft && !string.IsNullOrWhiteSpace(item.TagName) && !string.IsNullOrWhiteSpace(item.HtmlUrl))
            .OrderByDescending(item => item.PublishedAt ?? item.CreatedAt ?? DateTimeOffset.MinValue)
            .FirstOrDefault();
        if (release is null)
        {
            return null;
        }

        return new UpdateInfo(
            release.TagName,
            release.HtmlUrl,
            release.Prerelease,
            VersionComparer.IsNewer(release.TagName, AppInfo.Version));
    }

    private sealed class GitHubRelease
    {
        [JsonPropertyName("tag_name")]
        public string TagName { get; set; } = "";

        [JsonPropertyName("html_url")]
        public string HtmlUrl { get; set; } = "";

        [JsonPropertyName("draft")]
        public bool Draft { get; set; }

        [JsonPropertyName("prerelease")]
        public bool Prerelease { get; set; }

        [JsonPropertyName("created_at")]
        public DateTimeOffset? CreatedAt { get; set; }

        [JsonPropertyName("published_at")]
        public DateTimeOffset? PublishedAt { get; set; }
    }
}

internal static class VersionComparer
{
    public static bool IsNewer(string candidate, string current)
    {
        var candidateVersion = Parse(candidate);
        var currentVersion = Parse(current);
        if (candidateVersion is null || currentVersion is null)
        {
            return !string.Equals(candidate.Trim(), current.Trim(), StringComparison.OrdinalIgnoreCase);
        }

        return candidateVersion.Value.CompareTo(currentVersion.Value) > 0;
    }

    private static ComparableVersion? Parse(string value)
    {
        var cleaned = value.Trim();
        if (cleaned.StartsWith("v", StringComparison.OrdinalIgnoreCase))
        {
            cleaned = cleaned[1..];
        }

        var dash = cleaned.IndexOf('-');
        var prerelease = dash >= 0 ? cleaned[(dash + 1)..] : "";
        var numeric = dash >= 0 ? cleaned[..dash] : cleaned;
        var parts = numeric.Split('.');
        if (parts.Length == 0 || parts.Any(part => !int.TryParse(part, out _)))
        {
            return null;
        }

        var numbers = parts.Select(int.Parse).Concat(Enumerable.Repeat(0, 3)).Take(3).ToArray();
        var prereleaseRank = PrereleaseRank(prerelease);
        return new ComparableVersion(numbers[0], numbers[1], numbers[2], prereleaseRank);
    }

    private static int PrereleaseRank(string prerelease)
    {
        if (string.IsNullOrWhiteSpace(prerelease))
        {
            return int.MaxValue;
        }

        var parts = prerelease.Split('.', '-');
        if (parts.Length > 0 && int.TryParse(parts[^1], out var number))
        {
            return number;
        }

        return 0;
    }

    private readonly record struct ComparableVersion(int Major, int Minor, int Patch, int PrereleaseRank)
        : IComparable<ComparableVersion>
    {
        public int CompareTo(ComparableVersion other)
        {
            var major = Major.CompareTo(other.Major);
            if (major != 0) { return major; }

            var minor = Minor.CompareTo(other.Minor);
            if (minor != 0) { return minor; }

            var patch = Patch.CompareTo(other.Patch);
            if (patch != 0) { return patch; }

            return PrereleaseRank.CompareTo(other.PrereleaseRank);
        }
    }
}
