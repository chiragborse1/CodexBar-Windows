# Providers

CodexBar-Windows keeps the shared provider registry, but Windows support varies
by source type.

## Works Best Today

These providers have API-token, local-file, or local-probe paths that do not
require macOS-only browser, WebKit, or Keychain behavior:

```text
openai, gemini, antigravity, copilot, zai, kilo, vertexai, jetbrains, kimik2,
moonshot, synthetic, warp, openrouter, elevenlabs, doubao, deepseek,
codebuff, crof, venice, bedrock
```

Some of these still require external tools or credentials, such as `gcloud`,
provider API keys, or vendor CLI login files.

The Windows app shows the same compatibility split in the dashboard
`Compatibility` tab.

## Partial on Windows

These providers have shared parsing/config support, but one or more default
paths currently depend on browser cookies, WebKit, or interactive terminal
sessions:

```text
codex, claude, cursor, opencode, opencodego, alibaba, factory, manus, kimi,
minimax, augment, kiro, amp, ollama, windsurf, perplexity, mimo, abacus, mistral,
commandcode, stepfun, grok
```

Use API keys, manual cookies, or provider-specific environment variables where
those modes exist. Native Windows browser-cookie import, Credential Manager
storage, and ConPTY-backed interactive sessions are tracked in
[windows-port.md](windows-port.md).

## Provider IDs

```text
all, codex, openai, claude, cursor, opencode, opencodego, alibaba, factory,
gemini, antigravity, copilot, zai, minimax, manus, kimi, kilo, kiro, vertexai,
augment, jetbrains, kimik2, moonshot, amp, ollama, synthetic, warp, openrouter,
elevenlabs, windsurf, perplexity, mimo, doubao, abacus, mistral, deepseek,
codebuff, crof, venice, commandcode, stepfun, bedrock, grok
```

Use `.\CodexBarCLI.exe config providers` for the list built into the current
binary.
