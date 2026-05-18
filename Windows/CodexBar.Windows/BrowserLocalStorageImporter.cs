using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace CodexBar.Windows;

internal static class BrowserLocalStorageImporter
{
    private static readonly string[] WindsurfKeys =
    [
        "devin_session_token",
        "devin_auth1_token",
        "devin_account_id",
        "devin_primary_org_id",
    ];

    public static Task<BrowserLocalStorageImportResult> ImportWindsurfAsync(
        string browserId,
        CancellationToken cancellationToken)
    {
        return Task.Run(() => ImportWindsurf(browserId, cancellationToken), cancellationToken);
    }

    private static BrowserLocalStorageImportResult ImportWindsurf(
        string browserId,
        CancellationToken cancellationToken)
    {
        var browser = BrowserCookieImporter.Browsers
            .FirstOrDefault(item => string.Equals(item.Id, browserId, StringComparison.OrdinalIgnoreCase));
        if (browser is null)
        {
            return BrowserLocalStorageImportResult.Failed("Unknown browser.");
        }

        if (browser.Kind != BrowserCookieBrowserKind.Chromium)
        {
            return BrowserLocalStorageImportResult.Failed($"{browser.DisplayName} localStorage import is not supported yet.");
        }

        if (!Directory.Exists(browser.UserDataDirectory))
        {
            return BrowserLocalStorageImportResult.Failed($"{browser.DisplayName} profile data was not found.");
        }

        foreach (var profile in DiscoverChromiumLocalStorageProfiles(browser))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var storage = ReadLocalStorage(profile.LevelDbPath, WindsurfKeys, cancellationToken);
            if (!WindsurfKeys.All(storage.ContainsKey))
            {
                continue;
            }

            var payload = JsonSerializer.Serialize(new Dictionary<string, string>
            {
                ["devin_session_token"] = storage["devin_session_token"],
                ["devin_auth1_token"] = storage["devin_auth1_token"],
                ["devin_account_id"] = storage["devin_account_id"],
                ["devin_primary_org_id"] = storage["devin_primary_org_id"],
            });

            return BrowserLocalStorageImportResult.Success(
                payload,
                $"{browser.DisplayName} {profile.DisplayName}",
                WindsurfKeys.Length);
        }

        return BrowserLocalStorageImportResult.Failed(
            $"No usable Windsurf session was found in {browser.DisplayName} localStorage.");
    }

    private static IEnumerable<BrowserLocalStorageProfile> DiscoverChromiumLocalStorageProfiles(BrowserCookieBrowser browser)
    {
        if (ResolveLevelDbPath(browser.UserDataDirectory) is { } rootLevelDbPath)
        {
            yield return new BrowserLocalStorageProfile(browser.UserDataDirectory, "Default", rootLevelDbPath);
        }

        foreach (var directory in Directory.EnumerateDirectories(browser.UserDataDirectory))
        {
            var name = Path.GetFileName(directory);
            if (string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            if (!IsChromiumProfileDirectory(name))
            {
                continue;
            }

            if (ResolveLevelDbPath(directory) is not { } levelDbPath)
            {
                continue;
            }

            yield return new BrowserLocalStorageProfile(directory, ProfileDisplayName(name), levelDbPath);
        }
    }

    private static string? ResolveLevelDbPath(string profileDirectory)
    {
        var levelDbPath = Path.Combine(profileDirectory, "Local Storage", "leveldb");
        return Directory.Exists(levelDbPath) ? levelDbPath : null;
    }

    private static bool IsChromiumProfileDirectory(string name) =>
        string.Equals(name, "Default", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(name, "Guest Profile", StringComparison.OrdinalIgnoreCase) ||
        name.StartsWith("Profile ", StringComparison.OrdinalIgnoreCase);

    private static string ProfileDisplayName(string name) =>
        string.Equals(name, "Default", StringComparison.OrdinalIgnoreCase) ? "Default" : name;

    private static Dictionary<string, string> ReadLocalStorage(
        string levelDbPath,
        IReadOnlyCollection<string> keys,
        CancellationToken cancellationToken)
    {
        var storage = new Dictionary<string, string>(StringComparer.Ordinal);
        var files = Directory.EnumerateFiles(levelDbPath)
            .Where(path =>
            {
                var ext = Path.GetExtension(path);
                return ext.Equals(".ldb", StringComparison.OrdinalIgnoreCase) ||
                    ext.Equals(".log", StringComparison.OrdinalIgnoreCase) ||
                    ext.Equals(".sst", StringComparison.OrdinalIgnoreCase);
            })
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .ToArray();

        foreach (var file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            byte[] data;
            try
            {
                using var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                using var memory = new MemoryStream();
                stream.CopyTo(memory);
                data = memory.ToArray();
            }
            catch
            {
                continue;
            }

            var texts = new[]
            {
                Encoding.UTF8.GetString(data),
                Encoding.Latin1.GetString(data),
            };

            foreach (var text in texts)
            {
                foreach (var key in keys.Where(key => !storage.ContainsKey(key)))
                {
                    if (ExtractStorageValue(text, key) is { } value)
                    {
                        storage[key] = value;
                    }
                }

                if (keys.All(storage.ContainsKey))
                {
                    return storage;
                }
            }
        }

        return storage;
    }

    private static string? ExtractStorageValue(string text, string key)
    {
        var index = 0;
        string? lastMatch = null;
        while (index < text.Length)
        {
            var keyIndex = text.IndexOf(key, index, StringComparison.Ordinal);
            if (keyIndex < 0)
            {
                break;
            }

            var start = keyIndex + key.Length;
            var length = Math.Min(2048, text.Length - start);
            if (length <= 0)
            {
                break;
            }

            var tail = text.Substring(start, length);
            var quoted = Regex.Match(tail, """["']((?:\\.|[^"'\\]){4,})["']""", RegexOptions.Singleline);
            if (quoted.Success)
            {
                var value = DecodeStorageValue(quoted.Value);
                if (IsLikelyStorageValue(value, key))
                {
                    lastMatch = value;
                }
            }
            else
            {
                var token = Regex.Match(tail, """[A-Za-z0-9][A-Za-z0-9._~+/=:-]{7,}""", RegexOptions.Singleline);
                if (token.Success)
                {
                    var value = DecodeStorageValue(token.Value);
                    if (IsLikelyStorageValue(value, key))
                    {
                        lastMatch = value;
                    }
                }
            }

            index = start;
        }

        return lastMatch;
    }

    private static string DecodeStorageValue(string value)
    {
        var trimmed = value.Trim();
        if (trimmed.Length >= 2 && trimmed.StartsWith('"') && trimmed.EndsWith('"'))
        {
            try
            {
                return JsonSerializer.Deserialize<string>(trimmed)?.Trim() ?? "";
            }
            catch
            {
                return trimmed.Trim('"').Trim();
            }
        }

        return trimmed.Trim('"', '\'').Trim();
    }

    private static bool IsLikelyStorageValue(string value, string key)
    {
        var trimmed = value.Trim();
        return trimmed.Length >= 4 &&
            !trimmed.Contains("windsurf.com", StringComparison.OrdinalIgnoreCase) &&
            !trimmed.Contains("Local Storage", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(trimmed, key, StringComparison.Ordinal);
    }

    private sealed record BrowserLocalStorageProfile(string DirectoryPath, string DisplayName, string LevelDbPath);
}

internal sealed record BrowserLocalStorageImportResult(
    bool Succeeded,
    string? SessionPayload,
    string SourceLabel,
    int ValueCount,
    string Message)
{
    public static BrowserLocalStorageImportResult Success(
        string sessionPayload,
        string sourceLabel,
        int valueCount) =>
        new(
            true,
            sessionPayload,
            sourceLabel,
            valueCount,
            $"Imported {valueCount} localStorage values from {sourceLabel}.");

    public static BrowserLocalStorageImportResult Failed(string message) =>
        new(false, null, "", 0, message);
}
