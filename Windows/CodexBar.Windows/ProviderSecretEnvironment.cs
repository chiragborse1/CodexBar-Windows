namespace CodexBar.Windows;

internal static class ProviderSecretEnvironment
{
    private static readonly IReadOnlyDictionary<string, string> PrimaryEnvironmentKeys =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["openai"] = "OPENAI_ADMIN_KEY",
            ["claude"] = "ANTHROPIC_ADMIN_KEY",
            ["zai"] = "Z_AI_API_KEY",
            ["minimax"] = "MINIMAX_API_KEY",
            ["alibaba"] = "ALIBABA_CODING_PLAN_API_KEY",
            ["kilo"] = "KILO_API_KEY",
            ["synthetic"] = "SYNTHETIC_API_KEY",
            ["openrouter"] = "OPENROUTER_API_KEY",
            ["elevenlabs"] = "ELEVENLABS_API_KEY",
            ["moonshot"] = "MOONSHOT_API_KEY",
            ["venice"] = "VENICE_API_KEY",
            ["copilot"] = "COPILOT_API_TOKEN",
            ["kimik2"] = "KIMI_K2_API_KEY",
            ["warp"] = "WARP_API_KEY",
            ["codebuff"] = "CODEBUFF_API_KEY",
            ["crof"] = "CROF_API_KEY",
            ["doubao"] = "ARK_API_KEY",
            ["deepseek"] = "DEEPSEEK_API_KEY",
        };

    public static void ApplyStoredCredentials(IDictionary<string, string?> environment)
    {
        foreach (var provider in ProviderCatalog.ApiKeyProviderIds)
        {
            ApplyStoredCredential(environment, provider);
        }
    }

    public static string? PrimaryEnvironmentKeyFor(string provider)
    {
        return PrimaryEnvironmentKeys.TryGetValue(provider, out var key) ? key : null;
    }

    private static void ApplyStoredCredential(IDictionary<string, string?> environment, string provider)
    {
        var environmentKey = PrimaryEnvironmentKeyFor(provider);
        if (string.IsNullOrWhiteSpace(environmentKey))
        {
            return;
        }

        if (environment.TryGetValue(environmentKey, out var existing) &&
            !string.IsNullOrWhiteSpace(existing))
        {
            return;
        }

        var secret = WindowsCredentialStore.ReadApiKey(provider);
        if (!string.IsNullOrWhiteSpace(secret))
        {
            environment[environmentKey] = secret;
        }
    }
}
