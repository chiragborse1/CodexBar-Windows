using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace CodexBar.Windows;

internal static class BrowserCookieImporter
{
    public static readonly BrowserCookieBrowser[] Browsers =
    [
        new("edge", "Edge", Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Microsoft", "Edge", "User Data"),
            BrowserCookieBrowserKind.Chromium),
        new("chrome", "Chrome", Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Google", "Chrome", "User Data"),
            BrowserCookieBrowserKind.Chromium),
        new("brave", "Brave", Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "BraveSoftware", "Brave-Browser", "User Data"),
            BrowserCookieBrowserKind.Chromium),
        new("firefox", "Firefox", Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Mozilla", "Firefox", "Profiles"),
            BrowserCookieBrowserKind.Firefox),
    ];

    private static readonly BrowserCookieProviderRecipe[] Recipes =
    [
        new(
            "codex",
            ["chatgpt.com", "openai.com", "platform.openai.com"],
            AnyNames: ["__Secure-next-auth.session-token", "next-auth.session-token", "__Secure-authjs.session-token", "authjs.session-token"],
            AnyPrefixes: ["__Secure-next-auth.session-token.", "next-auth.session-token.", "__Secure-authjs.session-token.", "authjs.session-token."]),
        new("claude", ["claude.ai"], RequiredNames: ["sessionKey"]),
        new(
            "cursor",
            ["cursor.com", "www.cursor.com", "cursor.sh", "authenticator.cursor.sh"],
            AnyNames:
            [
                "WorkosCursorSessionToken",
                "__Secure-next-auth.session-token",
                "next-auth.session-token",
                "wos-session",
                "__Secure-wos-session",
                "authjs.session-token",
                "__Secure-authjs.session-token",
            ],
            AnyPrefixes: ["__Secure-next-auth.session-token.", "next-auth.session-token."]),
        new("opencode", ["opencode.ai", "app.opencode.ai"], AnyNames: ["auth", "__Host-auth"]),
        new("opencodego", ["opencode.ai", "app.opencode.ai"], AnyNames: ["auth", "__Host-auth"]),
        new(
            "alibaba",
            [
                "bailian-singapore-cs.alibabacloud.com",
                "bailian-cs.console.aliyun.com",
                "bailian-beijing-cs.aliyuncs.com",
                "modelstudio.console.alibabacloud.com",
                "bailian.console.aliyun.com",
                "free.aliyun.com",
                "account.aliyun.com",
                "signin.aliyun.com",
                "passport.alibabacloud.com",
                "console.alibabacloud.com",
                "console.aliyun.com",
                "alibabacloud.com",
                "aliyun.com",
            ],
            AnyNames: ["login_aliyunid_ticket", "login_aliyunid_pk", "login_current_pk", "login_aliyunid"]),
        new("factory", ["factory.ai", "app.factory.ai", "auth.factory.ai", "workos.com"], AllowAnyCookie: true),
        new(
            "minimax",
            ["platform.minimax.io", "openplatform.minimax.io", "minimax.io", "platform.minimaxi.com", "openplatform.minimaxi.com", "minimaxi.com"],
            AnyNames: ["HERTZ-SESSION"],
            AllowAnyCookie: true),
        new("manus", ["manus.im", "www.manus.im"], RequiredNames: ["session_id"]),
        new("kimi", ["www.kimi.com", "kimi.com"], RequiredNames: ["kimi-auth"]),
        new("amp", ["ampcode.com", "www.ampcode.com"], RequiredNames: ["session"]),
        new("augment", ["augmentcode.com", "app.augmentcode.com"], AllowAnyCookie: true),
        new(
            "ollama",
            ["ollama.com", "www.ollama.com"],
            AnyNames: ["session", "__Secure-session", "ollama_session", "__Host-ollama_session", "__Secure-next-auth.session-token", "next-auth.session-token"],
            AnyPrefixes: ["__Secure-next-auth.session-token.", "next-auth.session-token."]),
        new("perplexity", ["www.perplexity.ai", "perplexity.ai"], AnyNames:
            [
                "__Secure-authjs.session-token",
                "authjs.session-token",
                "__Secure-next-auth.session-token",
                "next-auth.session-token",
            ],
            AnyPrefixes:
            [
                "__Secure-authjs.session-token.",
                "authjs.session-token.",
                "__Secure-next-auth.session-token.",
                "next-auth.session-token.",
            ]),
        new("mimo", ["platform.xiaomimimo.com", "xiaomimimo.com"], RequiredNames: ["api-platform_serviceToken", "userId"]),
        new(
            "abacus",
            ["abacus.ai", "apps.abacus.ai"],
            AnyNames: ["sessionid", "session_id", "session_token", "auth_token", "access_token"],
            AnySubstrings: ["session", "auth", "sid", "jwt"]),
        new("mistral", ["mistral.ai", "admin.mistral.ai", "auth.mistral.ai"], AnyPrefixes: ["ory_session_"]),
        new("commandcode", ["commandcode.ai", "www.commandcode.ai"], AnyNames:
            [
                "__Host-better-auth.session_token",
                "__Secure-better-auth.session_token",
                "better-auth.session_token",
            ]),
        new("stepfun", ["platform.stepfun.com"], AnyNames: ["Oasis-Token", "Oasis-Webid", "INGRESSCOOKIE"]),
        new("grok", ["grok.com"], AnyNames: ["sso", "sso-rw"]),
    ];

    public static bool IsBrowserLikelyInstalled(string browserId) =>
        Browsers.FirstOrDefault(browser => string.Equals(browser.Id, browserId, StringComparison.OrdinalIgnoreCase))
            is { } browser &&
        Directory.Exists(browser.UserDataDirectory);

    public static Task<BrowserCookieImportResult> ImportAsync(
        string providerId,
        string browserId,
        CancellationToken cancellationToken)
    {
        return Task.Run(() => Import(providerId, browserId, cancellationToken), cancellationToken);
    }

    private static BrowserCookieImportResult Import(
        string providerId,
        string browserId,
        CancellationToken cancellationToken)
    {
        var recipe = RecipeFor(providerId);
        if (recipe is null)
        {
            return BrowserCookieImportResult.Failed($"Browser import is not configured for {ProviderCatalog.DisplayNameFor(providerId)}.");
        }

        var browser = Browsers.FirstOrDefault(item => string.Equals(item.Id, browserId, StringComparison.OrdinalIgnoreCase));
        if (browser is null)
        {
            return BrowserCookieImportResult.Failed("Unknown browser.");
        }

        if (!Directory.Exists(browser.UserDataDirectory))
        {
            return BrowserCookieImportResult.Failed($"{browser.DisplayName} profile data was not found.");
        }

        var masterKey = browser.Kind == BrowserCookieBrowserKind.Chromium
            ? ReadChromiumMasterKey(browser.UserDataDirectory)
            : null;
        var profiles = DiscoverProfiles(browser).ToArray();
        if (profiles.Length == 0)
        {
            return BrowserCookieImportResult.Failed($"{browser.DisplayName} has no readable cookie profiles.");
        }

        var failures = 0;
        foreach (var profile in profiles)
        {
            cancellationToken.ThrowIfCancellationRequested();

            BrowserProfileCookieResult profileResult;
            try
            {
                profileResult = browser.Kind == BrowserCookieBrowserKind.Firefox
                    ? ReadFirefoxProfileCookies(browser, profile, recipe)
                    : ReadChromiumProfileCookies(browser, profile, recipe, masterKey);
            }
            catch
            {
                failures++;
                continue;
            }

            if (profileResult.Cookies.Count == 0)
            {
                if (profileResult.DecryptionFailures > 0)
                {
                    failures += profileResult.DecryptionFailures;
                }
                continue;
            }

            var selected = SelectCookies(profileResult.Cookies, recipe).ToArray();
            if (!IsUsableSession(selected, recipe))
            {
                continue;
            }

            var header = BuildHeader(selected);
            if (string.IsNullOrWhiteSpace(header))
            {
                continue;
            }

            return BrowserCookieImportResult.Success(
                header,
                $"{browser.DisplayName} {profile.DisplayName}",
                selected.Length,
                profileResult.DecryptionFailures);
        }

        var suffix = failures > 0
            ? $" {failures} encrypted cookie value{(failures == 1 ? "" : "s")} could not be decrypted."
            : "";
        return BrowserCookieImportResult.Failed(
            $"No usable {ProviderCatalog.DisplayNameFor(providerId)} session cookies were found in {browser.DisplayName}.{suffix}");
    }

    private static BrowserCookieProviderRecipe? RecipeFor(string providerId) =>
        Recipes.FirstOrDefault(recipe => string.Equals(recipe.ProviderId, providerId, StringComparison.OrdinalIgnoreCase));

    private static IEnumerable<BrowserProfile> DiscoverProfiles(BrowserCookieBrowser browser) =>
        browser.Kind == BrowserCookieBrowserKind.Firefox
            ? DiscoverFirefoxProfiles(browser.UserDataDirectory)
            : DiscoverChromiumProfiles(browser.UserDataDirectory);

    private static IEnumerable<BrowserProfile> DiscoverChromiumProfiles(string userDataDirectory)
    {
        foreach (var directory in Directory.EnumerateDirectories(userDataDirectory))
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

            var cookiesPath = ResolveCookiesPath(directory);
            if (cookiesPath is null)
            {
                continue;
            }

            yield return new BrowserProfile(directory, ProfileDisplayName(name), cookiesPath);
        }
    }

    private static IEnumerable<BrowserProfile> DiscoverFirefoxProfiles(string profilesDirectory)
    {
        foreach (var directory in Directory.EnumerateDirectories(profilesDirectory))
        {
            var name = Path.GetFileName(directory);
            if (string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            var cookiesPath = Path.Combine(directory, "cookies.sqlite");
            if (!File.Exists(cookiesPath))
            {
                continue;
            }

            yield return new BrowserProfile(directory, FirefoxProfileDisplayName(name), cookiesPath);
        }
    }

    private static bool IsChromiumProfileDirectory(string name) =>
        string.Equals(name, "Default", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(name, "Guest Profile", StringComparison.OrdinalIgnoreCase) ||
        name.StartsWith("Profile ", StringComparison.OrdinalIgnoreCase);

    private static string ProfileDisplayName(string name) =>
        string.Equals(name, "Default", StringComparison.OrdinalIgnoreCase) ? "Default" : name;

    private static string FirefoxProfileDisplayName(string name)
    {
        var separator = name.IndexOf('.');
        if (separator >= 0 && separator < name.Length - 1)
        {
            return name[(separator + 1)..];
        }

        return name;
    }

    private static string? ResolveCookiesPath(string profileDirectory)
    {
        var networkPath = Path.Combine(profileDirectory, "Network", "Cookies");
        if (File.Exists(networkPath))
        {
            return networkPath;
        }

        var legacyPath = Path.Combine(profileDirectory, "Cookies");
        return File.Exists(legacyPath) ? legacyPath : null;
    }

    private static byte[]? ReadChromiumMasterKey(string userDataDirectory)
    {
        try
        {
            var localStatePath = Path.Combine(userDataDirectory, "Local State");
            if (!File.Exists(localStatePath))
            {
                return null;
            }

            using var document = JsonDocument.Parse(File.ReadAllText(localStatePath));
            if (!document.RootElement.TryGetProperty("os_crypt", out var osCrypt) ||
                !osCrypt.TryGetProperty("encrypted_key", out var encryptedKeyElement))
            {
                return null;
            }

            var encryptedKeyText = encryptedKeyElement.GetString();
            if (string.IsNullOrWhiteSpace(encryptedKeyText))
            {
                return null;
            }

            var encryptedKey = Convert.FromBase64String(encryptedKeyText);
            var dpapiPrefix = Encoding.ASCII.GetBytes("DPAPI");
            if (encryptedKey.Length > dpapiPrefix.Length &&
                encryptedKey.AsSpan(0, dpapiPrefix.Length).SequenceEqual(dpapiPrefix))
            {
                encryptedKey = encryptedKey[dpapiPrefix.Length..];
            }

            return ProtectedData.Unprotect(encryptedKey, optionalEntropy: null, DataProtectionScope.CurrentUser);
        }
        catch
        {
            return null;
        }
    }

    private static BrowserProfileCookieResult ReadChromiumProfileCookies(
        BrowserCookieBrowser browser,
        BrowserProfile profile,
        BrowserCookieProviderRecipe recipe,
        byte[]? masterKey)
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), AppInfo.DisplayName, "Cookies", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);

        try
        {
            var copiedDbPath = Path.Combine(tempDirectory, "Cookies");
            CopyCookieStore(profile.CookiesPath, copiedDbPath);

            var cookies = new List<BrowserCookieRecord>();
            var decryptionFailures = 0;

            using var connection = new SqliteConnection($"Data Source={copiedDbPath};Mode=ReadOnly");
            connection.Open();

            using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT host_key, name, path, value, encrypted_value, expires_utc, is_secure
                FROM cookies
                """;

            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                var host = ReadString(reader, 0);
                var name = ReadString(reader, 1);
                if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(name))
                {
                    continue;
                }

                if (!MatchesAnyDomain(host, recipe.Domains))
                {
                    continue;
                }

                var expiresUtc = reader.IsDBNull(5) ? 0 : reader.GetInt64(5);
                if (IsExpiredChromiumCookie(expiresUtc))
                {
                    continue;
                }

                var value = ReadString(reader, 3);
                if (string.IsNullOrEmpty(value))
                {
                    var encryptedValue = ReadBlob(reader, 4);
                    value = DecryptCookieValue(encryptedValue, masterKey);
                    if (string.IsNullOrEmpty(value) && encryptedValue is { Length: > 0 })
                    {
                        decryptionFailures++;
                        continue;
                    }
                }

                if (string.IsNullOrEmpty(value))
                {
                    continue;
                }

                cookies.Add(new BrowserCookieRecord(
                    Name: name,
                    Value: value,
                    Domain: NormalizeDomain(host),
                    Path: ReadString(reader, 2) ?? "/",
                    ExpiresUtc: expiresUtc,
                    IsSecure: !reader.IsDBNull(6) && reader.GetInt32(6) != 0,
                    SourceLabel: $"{browser.DisplayName} {profile.DisplayName}"));
            }

            return new BrowserProfileCookieResult(cookies, decryptionFailures);
        }
        finally
        {
            try
            {
                Directory.Delete(tempDirectory, recursive: true);
            }
            catch
            {
                // Temporary cookie copies are best-effort cleanup.
            }
        }
    }

    private static BrowserProfileCookieResult ReadFirefoxProfileCookies(
        BrowserCookieBrowser browser,
        BrowserProfile profile,
        BrowserCookieProviderRecipe recipe)
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), AppInfo.DisplayName, "Cookies", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);

        try
        {
            var copiedDbPath = Path.Combine(tempDirectory, "cookies.sqlite");
            CopyCookieStore(profile.CookiesPath, copiedDbPath);

            var cookies = new List<BrowserCookieRecord>();
            using var connection = new SqliteConnection($"Data Source={copiedDbPath};Mode=ReadOnly");
            connection.Open();

            using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT host, name, path, value, expiry, isSecure
                FROM moz_cookies
                """;

            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                var host = ReadString(reader, 0);
                var name = ReadString(reader, 1);
                if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(name))
                {
                    continue;
                }

                if (!MatchesAnyDomain(host, recipe.Domains))
                {
                    continue;
                }

                var expiresUnix = reader.IsDBNull(4) ? 0 : reader.GetInt64(4);
                if (IsExpiredUnixCookie(expiresUnix))
                {
                    continue;
                }

                var value = ReadString(reader, 3);
                if (string.IsNullOrEmpty(value))
                {
                    continue;
                }

                cookies.Add(new BrowserCookieRecord(
                    Name: name,
                    Value: value,
                    Domain: NormalizeDomain(host),
                    Path: ReadString(reader, 2) ?? "/",
                    ExpiresUtc: expiresUnix,
                    IsSecure: !reader.IsDBNull(5) && reader.GetInt32(5) != 0,
                    SourceLabel: $"{browser.DisplayName} {profile.DisplayName}"));
            }

            return new BrowserProfileCookieResult(cookies, 0);
        }
        finally
        {
            try
            {
                Directory.Delete(tempDirectory, recursive: true);
            }
            catch
            {
                // Temporary cookie copies are best-effort cleanup.
            }
        }
    }

    private static void CopyCookieStore(string sourcePath, string destinationPath)
    {
        CopyUnlocked(sourcePath, destinationPath);
        CopySidecar(sourcePath + "-wal", destinationPath + "-wal");
        CopySidecar(sourcePath + "-shm", destinationPath + "-shm");
    }

    private static void CopySidecar(string sourcePath, string destinationPath)
    {
        if (!File.Exists(sourcePath))
        {
            return;
        }

        try
        {
            CopyUnlocked(sourcePath, destinationPath);
        }
        catch
        {
            // The main database copy can still be readable without sidecars.
        }
    }

    private static void CopyUnlocked(string sourcePath, string destinationPath)
    {
        using var source = new FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var destination = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None);
        source.CopyTo(destination);
    }

    private static string? ReadString(SqliteDataReader reader, int index)
    {
        if (reader.IsDBNull(index))
        {
            return null;
        }

        return reader.GetString(index);
    }

    private static byte[]? ReadBlob(SqliteDataReader reader, int index)
    {
        if (reader.IsDBNull(index))
        {
            return null;
        }

        return reader.GetFieldValue<byte[]>(index);
    }

    private static bool IsExpiredChromiumCookie(long expiresUtc)
    {
        if (expiresUtc <= 0)
        {
            return false;
        }

        try
        {
            var expires = DateTimeOffset.FromFileTime(expiresUtc * 10);
            return expires <= DateTimeOffset.UtcNow;
        }
        catch
        {
            return false;
        }
    }

    private static bool IsExpiredUnixCookie(long expiresUnix)
    {
        if (expiresUnix <= 0)
        {
            return false;
        }

        try
        {
            var expires = DateTimeOffset.FromUnixTimeSeconds(expiresUnix);
            return expires <= DateTimeOffset.UtcNow;
        }
        catch
        {
            return false;
        }
    }

    private static string? DecryptCookieValue(byte[]? encryptedValue, byte[]? masterKey)
    {
        if (encryptedValue is not { Length: > 0 })
        {
            return null;
        }

        try
        {
            if (HasChromiumAesPrefix(encryptedValue))
            {
                if (masterKey is not { Length: > 0 } || encryptedValue.Length < 3 + 12 + 16)
                {
                    return null;
                }

                var nonce = encryptedValue.AsSpan(3, 12).ToArray();
                var cipherTextLength = encryptedValue.Length - 3 - 12 - 16;
                var cipherText = encryptedValue.AsSpan(15, cipherTextLength).ToArray();
                var tag = encryptedValue.AsSpan(encryptedValue.Length - 16, 16).ToArray();
                var plainText = new byte[cipherTextLength];

                using var aes = new AesGcm(masterKey, tagSizeInBytes: 16);
                aes.Decrypt(nonce, cipherText, tag, plainText);
                return Encoding.UTF8.GetString(plainText);
            }

            var unprotected = ProtectedData.Unprotect(
                encryptedValue,
                optionalEntropy: null,
                DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(unprotected);
        }
        catch
        {
            return null;
        }
    }

    private static bool HasChromiumAesPrefix(byte[] encryptedValue) =>
        encryptedValue.Length >= 3 &&
        encryptedValue[0] == (byte)'v' &&
        (encryptedValue[1] == (byte)'1' || encryptedValue[1] == (byte)'2') &&
        (encryptedValue[2] == (byte)'0' || encryptedValue[2] == (byte)'1');

    private static IEnumerable<BrowserCookieRecord> SelectCookies(
        IReadOnlyCollection<BrowserCookieRecord> cookies,
        BrowserCookieProviderRecipe recipe)
    {
        var relevant = cookies.Where(cookie => IsRelevantCookie(cookie, recipe));
        return relevant
            .GroupBy(cookie => cookie.Name, StringComparer.OrdinalIgnoreCase)
            .Select(group => group
                .OrderByDescending(cookie => cookie.Path.Length)
                .ThenByDescending(cookie => cookie.Domain.Length)
                .ThenByDescending(cookie => cookie.ExpiresUtc)
                .First())
            .OrderBy(cookie => cookie.Name, StringComparer.OrdinalIgnoreCase);
    }

    private static bool IsRelevantCookie(BrowserCookieRecord cookie, BrowserCookieProviderRecipe recipe)
    {
        if (recipe.AllowAnyCookie)
        {
            return true;
        }

        if (ContainsName(recipe.RequiredNames, cookie.Name) ||
            ContainsName(recipe.AnyNames, cookie.Name) ||
            HasAnyPrefix(recipe.AnyPrefixes, cookie.Name) ||
            HasAnySubstring(recipe.AnySubstrings, cookie.Name))
        {
            return true;
        }

        return recipe.RequiredNames.Length == 0 &&
            recipe.AnyNames.Length == 0 &&
            recipe.AnyPrefixes.Length == 0 &&
            recipe.AnySubstrings.Length == 0;
    }

    private static bool IsUsableSession(
        IReadOnlyCollection<BrowserCookieRecord> cookies,
        BrowserCookieProviderRecipe recipe)
    {
        if (cookies.Count == 0)
        {
            return false;
        }

        foreach (var required in recipe.RequiredNames)
        {
            if (!cookies.Any(cookie => string.Equals(cookie.Name, required, StringComparison.OrdinalIgnoreCase)))
            {
                return false;
            }
        }

        if (recipe.RequiredNames.Length == 0 &&
            (recipe.AnyNames.Length > 0 || recipe.AnyPrefixes.Length > 0 || recipe.AnySubstrings.Length > 0))
        {
            return cookies.Any(cookie =>
                ContainsName(recipe.AnyNames, cookie.Name) ||
                HasAnyPrefix(recipe.AnyPrefixes, cookie.Name) ||
                HasAnySubstring(recipe.AnySubstrings, cookie.Name));
        }

        return true;
    }

    private static string BuildHeader(IEnumerable<BrowserCookieRecord> cookies) =>
        string.Join("; ", cookies.Select(cookie => $"{cookie.Name}={cookie.Value}"));

    private static bool MatchesAnyDomain(string host, IReadOnlyCollection<string> patterns)
    {
        var normalizedHost = NormalizeDomain(host);
        return patterns.Any(pattern =>
        {
            var normalizedPattern = NormalizeDomain(pattern);
            return normalizedHost == normalizedPattern ||
                normalizedHost.EndsWith("." + normalizedPattern, StringComparison.OrdinalIgnoreCase) ||
                normalizedPattern.EndsWith("." + normalizedHost, StringComparison.OrdinalIgnoreCase);
        });
    }

    private static string NormalizeDomain(string value) =>
        value.Trim().TrimStart('.').ToLowerInvariant();

    private static bool ContainsName(IReadOnlyCollection<string> names, string candidate) =>
        names.Any(name => string.Equals(name, candidate, StringComparison.OrdinalIgnoreCase));

    private static bool HasAnyPrefix(IReadOnlyCollection<string> prefixes, string candidate) =>
        prefixes.Any(prefix => candidate.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));

    private static bool HasAnySubstring(IReadOnlyCollection<string> substrings, string candidate) =>
        substrings.Any(substring => candidate.Contains(substring, StringComparison.OrdinalIgnoreCase));

    private sealed class BrowserCookieProviderRecipe
    {
        public BrowserCookieProviderRecipe(
            string providerId,
            string[] domains,
            string[]? RequiredNames = null,
            string[]? AnyNames = null,
            string[]? AnyPrefixes = null,
            string[]? AnySubstrings = null,
            bool AllowAnyCookie = false)
        {
            ProviderId = providerId;
            Domains = domains;
            this.RequiredNames = RequiredNames ?? [];
            this.AnyNames = AnyNames ?? [];
            this.AnyPrefixes = AnyPrefixes ?? [];
            this.AnySubstrings = AnySubstrings ?? [];
            this.AllowAnyCookie = AllowAnyCookie;
        }

        public string ProviderId { get; }
        public string[] Domains { get; }
        public string[] RequiredNames { get; }
        public string[] AnyNames { get; }
        public string[] AnyPrefixes { get; }
        public string[] AnySubstrings { get; }
        public bool AllowAnyCookie { get; }
    }

    private sealed record BrowserProfile(string DirectoryPath, string DisplayName, string CookiesPath);

    private sealed record BrowserCookieRecord(
        string Name,
        string Value,
        string Domain,
        string Path,
        long ExpiresUtc,
        bool IsSecure,
        string SourceLabel);

    private sealed record BrowserProfileCookieResult(
        IReadOnlyList<BrowserCookieRecord> Cookies,
        int DecryptionFailures);
}

internal sealed record BrowserCookieBrowser(
    string Id,
    string DisplayName,
    string UserDataDirectory,
    BrowserCookieBrowserKind Kind);

internal enum BrowserCookieBrowserKind
{
    Chromium,
    Firefox,
}

internal sealed record BrowserCookieImportResult(
    bool Succeeded,
    string? CookieHeader,
    string SourceLabel,
    int CookieCount,
    int DecryptionFailures,
    string Message)
{
    public static BrowserCookieImportResult Success(
        string cookieHeader,
        string sourceLabel,
        int cookieCount,
        int decryptionFailures) =>
        new(
            true,
            cookieHeader,
            sourceLabel,
            cookieCount,
            decryptionFailures,
            decryptionFailures > 0
                ? $"Imported {cookieCount} cookies from {sourceLabel}. {decryptionFailures} encrypted values were skipped."
                : $"Imported {cookieCount} cookies from {sourceLabel}.");

    public static BrowserCookieImportResult Failed(string message) =>
        new(false, null, "", 0, 0, message);
}
