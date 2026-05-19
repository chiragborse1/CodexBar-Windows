namespace CodexBar.Windows;

internal sealed record ProviderCatalogEntry(
    string Id,
    string DisplayName,
    string Support,
    string Notes);

internal static class ProviderCatalog
{
    public static readonly ProviderCatalogEntry[] Entries =
    [
        new("enabled", "Enabled providers", "Selector", "Queries providers enabled in config."),
        new("all", "All providers", "Selector", "Queries every known provider."),
        new("openai", "OpenAI", "Ready", "API-backed usage path."),
        new("gemini", "Gemini", "Ready", "Uses Gemini CLI credentials and API calls."),
        new("antigravity", "Antigravity", "Ready", "Uses local probe when Antigravity is installed."),
        new("copilot", "Copilot", "Ready", "Uses GitHub/Copilot API token flow."),
        new("zai", "z.ai", "Ready", "API-token usage path."),
        new("kilo", "Kilo", "Ready", "API token path works; CLI fallback depends on local auth."),
        new("vertexai", "Vertex AI", "Ready", "Uses Google ADC and Cloud Monitoring API."),
        new("jetbrains", "JetBrains AI", "Ready", "Reads local JetBrains quota files."),
        new("kimik2", "Kimi K2", "Ready", "API-key usage path."),
        new("moonshot", "Moonshot / Kimi API", "Ready", "API-key balance path."),
        new("synthetic", "Synthetic", "Ready", "API-key quota path."),
        new("warp", "Warp", "Ready", "API-token GraphQL path."),
        new("openrouter", "OpenRouter", "Ready", "API-token credits path."),
        new("elevenlabs", "ElevenLabs", "Ready", "API-key subscription usage path."),
        new("doubao", "Doubao", "Ready", "API-key probe path."),
        new("deepseek", "DeepSeek", "Ready", "API-key balance path."),
        new("codebuff", "Codebuff", "Ready", "API token or local credentials path."),
        new("crof", "Crof", "Ready", "API-key usage path."),
        new("venice", "Venice", "Ready", "API-key balance path."),
        new("bedrock", "AWS Bedrock", "Ready", "AWS-backed quota path."),
        new("codex", "Codex", "Ready", "Uses OAuth/API or Windows ConPTY-backed CLI."),
        new("claude", "Claude", "Ready", "Uses API/OAuth or Windows ConPTY-backed CLI."),
        new("cursor", "Cursor", "Partial", "Supports Windows browser cookie import for web mode."),
        new("opencode", "OpenCode", "Partial", "Supports Windows browser cookie import for web mode."),
        new("opencodego", "OpenCode Go", "Partial", "Supports Windows browser cookie import for web mode."),
        new("alibaba", "Alibaba", "Partial", "API key can work; web mode supports browser cookie import."),
        new("factory", "Droid / Factory", "Partial", "Supports Windows browser cookie import; local-storage import is pending."),
        new("minimax", "MiniMax", "Partial", "API key can work; web mode supports browser cookie import."),
        new("manus", "Manus", "Partial", "Manual/env token and browser cookie import can work."),
        new("kimi", "Kimi", "Partial", "Manual/env token and browser cookie import can work."),
        new("kiro", "Kiro", "Partial", "CLI usage depends on local Kiro CLI behavior."),
        new("amp", "Amp", "Partial", "Supports Windows browser cookie import for web mode."),
        new("augment", "Augment", "Partial", "CLI path can work; browser cookie import is available."),
        new("ollama", "Ollama", "Partial", "Web path supports browser cookie import."),
        new("windsurf", "Windsurf", "Ready", "Uses Windows Chromium localStorage session import."),
        new("perplexity", "Perplexity", "Partial", "Manual/env session and browser cookie import can work."),
        new("mimo", "Xiaomi MiMo", "Partial", "Supports Windows browser cookie import."),
        new("abacus", "Abacus AI", "Partial", "Supports Windows browser cookie import."),
        new("mistral", "Mistral", "Partial", "Supports Windows browser cookie import."),
        new("commandcode", "Command Code", "Partial", "Supports Windows browser cookie import."),
        new("stepfun", "StepFun", "Partial", "Supports Windows browser cookie import."),
        new("grok", "Grok", "Partial", "CLI path can work; browser billing fallback is pending."),
    ];

    public static string[] ProviderIds => Entries.Select(entry => entry.Id).ToArray();

    public static string[] ApiKeyProviderIds =>
    [
        "openai",
        "claude",
        "zai",
        "minimax",
        "alibaba",
        "kilo",
        "synthetic",
        "openrouter",
        "elevenlabs",
        "moonshot",
        "venice",
        "copilot",
        "kimik2",
        "warp",
        "codebuff",
        "crof",
        "doubao",
        "deepseek",
    ];

    public static ProviderCatalogEntry[] ApiKeyEntries => ApiKeyProviderIds
        .Select(id => Entries.FirstOrDefault(
            entry => string.Equals(entry.Id, id, StringComparison.OrdinalIgnoreCase)))
        .Where(entry => entry is not null)
        .Select(entry => entry!)
        .ToArray();

    public static string[] CookieProviderIds =>
    [
        "codex",
        "claude",
        "cursor",
        "opencode",
        "opencodego",
        "alibaba",
        "factory",
        "minimax",
        "manus",
        "kimi",
        "amp",
        "augment",
        "ollama",
        "windsurf",
        "perplexity",
        "mimo",
        "abacus",
        "mistral",
        "commandcode",
        "stepfun",
        "grok",
    ];

    public static ProviderCatalogEntry[] CookieEntries => CookieProviderIds
        .Select(id => Entries.FirstOrDefault(
            entry => string.Equals(entry.Id, id, StringComparison.OrdinalIgnoreCase)))
        .Where(entry => entry is not null)
        .Select(entry => entry!)
        .ToArray();

    public static string DisplayNameFor(string id)
    {
        var entry = Entries.FirstOrDefault(
            item => string.Equals(item.Id, id, StringComparison.OrdinalIgnoreCase));
        return entry?.DisplayName ?? id;
    }

    public static string SupportFor(string id)
    {
        var entry = Entries.FirstOrDefault(
            item => string.Equals(item.Id, id, StringComparison.OrdinalIgnoreCase));
        return entry?.Support ?? "";
    }

    public static bool SupportsConfigApiKey(string id) =>
        ApiKeyProviderIds.Any(provider => string.Equals(provider, id, StringComparison.OrdinalIgnoreCase));

    public static bool SupportsBrowserSession(string id) =>
        CookieProviderIds.Any(provider => string.Equals(provider, id, StringComparison.OrdinalIgnoreCase));

    public static string NotesFor(string id)
    {
        var entry = Entries.FirstOrDefault(
            item => string.Equals(item.Id, id, StringComparison.OrdinalIgnoreCase));
        return entry?.Notes ?? "";
    }

    public static string? DashboardUrlFor(string id) =>
        id.ToLowerInvariant() switch
        {
            "codex" => "https://chatgpt.com/codex/settings/usage",
            "openai" => "https://platform.openai.com/usage",
            "claude" => "https://console.anthropic.com/settings/billing",
            "gemini" => "https://gemini.google.com",
            "vertexai" => "https://console.cloud.google.com/vertex-ai",
            "copilot" => "https://github.com/settings/copilot",
            "cursor" => "https://cursor.com/dashboard?tab=usage",
            "opencode" or "opencodego" => "https://opencode.ai",
            "openrouter" => "https://openrouter.ai/settings/credits",
            "zai" => "https://z.ai/manage-apikey/subscription",
            "kilo" => "https://app.kilo.ai/usage",
            "moonshot" or "kimik2" => "https://platform.moonshot.ai/console/account",
            "minimax" => "https://platform.minimax.io/user-center/payment/coding-plan?cycle_type=3",
            "mistral" => "https://admin.mistral.ai/organization/usage",
            "perplexity" => "https://www.perplexity.ai/account/usage",
            "windsurf" => "https://windsurf.com/subscription/usage",
            "warp" => "https://docs.warp.dev/reference/cli/api-keys",
            "elevenlabs" => "https://elevenlabs.io/app/developers/usage",
            "bedrock" => "https://console.aws.amazon.com/bedrock",
            "deepseek" => "https://platform.deepseek.com/usage",
            "doubao" => "https://console.volcengine.com/ark",
            "venice" => "https://venice.ai/settings/api",
            "factory" => "https://app.factory.ai/settings/billing",
            "abacus" => "https://apps.abacus.ai/chatllm/admin/compute-points-usage",
            "amp" => "https://ampcode.com/settings",
            "augment" => "https://app.augmentcode.com/account/subscription",
            "codebuff" => "https://www.codebuff.com/usage",
            "commandcode" => "https://commandcode.ai/studio",
            "crof" => "https://crof.ai/dashboard",
            "grok" => "https://grok.com/?_s=usage",
            "kimi" => "https://www.kimi.com/code/console",
            "manus" => "https://manus.im",
            "mimo" => "https://platform.xiaomimimo.com/#/console/balance",
            "ollama" => "https://ollama.com/settings",
            "stepfun" => "https://platform.stepfun.com/plan-usage",
            _ => null,
        };

    public static string? StatusUrlFor(string id) =>
        id.ToLowerInvariant() switch
        {
            "codex" or "openai" => "https://status.openai.com/",
            "claude" => "https://status.claude.com/",
            "copilot" => "https://www.githubstatus.com/",
            "cursor" => "https://status.cursor.com",
            "factory" => "https://status.factory.ai",
            _ => null,
        };

    public static string? ChangelogUrlFor(string id) =>
        id.ToLowerInvariant() switch
        {
            "codex" => "https://github.com/openai/codex/releases",
            "claude" => "https://github.com/anthropics/claude-code/releases",
            "gemini" => "https://github.com/google-gemini/gemini-cli/releases",
            "grok" => "https://x.ai/news",
            _ => null,
        };
}
