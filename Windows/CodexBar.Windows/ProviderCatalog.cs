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
        new("all", "All enabled providers", "Selector", "Queries enabled providers from config."),
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
        new("codex", "Codex", "Partial", "OAuth/API paths are preferred; interactive CLI needs ConPTY."),
        new("claude", "Claude", "Partial", "OAuth/API paths are preferred; interactive CLI needs ConPTY."),
        new("cursor", "Cursor", "Partial", "Needs Windows browser cookie import for web mode."),
        new("opencode", "OpenCode", "Partial", "Needs Windows browser cookie import for web mode."),
        new("opencodego", "OpenCode Go", "Partial", "Needs Windows browser cookie import for web mode."),
        new("alibaba", "Alibaba", "Partial", "API key can work; web mode needs browser cookies."),
        new("factory", "Droid / Factory", "Partial", "Needs Windows browser cookie/local-storage import."),
        new("minimax", "MiniMax", "Partial", "API key can work; web mode needs browser cookies."),
        new("manus", "Manus", "Partial", "Manual cookie/env can work; auto import is pending."),
        new("kimi", "Kimi", "Partial", "Manual token/env can work; auto import is pending."),
        new("kiro", "Kiro", "Partial", "CLI usage depends on local Kiro CLI behavior."),
        new("amp", "Amp", "Partial", "Needs Windows browser cookie import for web mode."),
        new("augment", "Augment", "Partial", "CLI path can work; browser fallback is pending."),
        new("ollama", "Ollama", "Partial", "Web path needs browser cookie import."),
        new("windsurf", "Windsurf", "Partial", "Needs Windows browser/local-storage import."),
        new("perplexity", "Perplexity", "Partial", "Manual/env session can work; auto import is pending."),
        new("mimo", "Xiaomi MiMo", "Partial", "Needs Windows browser cookie import."),
        new("abacus", "Abacus AI", "Partial", "Needs Windows browser cookie import."),
        new("mistral", "Mistral", "Partial", "Needs Windows browser cookie import."),
        new("commandcode", "Command Code", "Partial", "Needs Windows browser cookie import."),
        new("stepfun", "StepFun", "Partial", "Needs Windows browser cookie import."),
        new("grok", "Grok", "Partial", "CLI path can work; browser billing fallback is pending."),
    ];

    public static string[] ProviderIds => Entries.Select(entry => entry.Id).ToArray();

    public static string DisplayNameFor(string id)
    {
        var entry = Entries.FirstOrDefault(
            item => string.Equals(item.Id, id, StringComparison.OrdinalIgnoreCase));
        return entry?.DisplayName ?? id;
    }
}
