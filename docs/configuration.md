# Configuration

CodexBar-Windows uses one JSON config file for provider settings, enabled
providers, source preferences, manual cookies, regions, workspaces, and token
accounts. API keys saved from the Windows tray app are stored in Windows
Credential Manager and injected into bundled CLI runs.

## Location

Default Windows path:

```text
%APPDATA%\CodexBar-Windows\config.json
```

Override path:

```powershell
$env:CODEXBAR_CONFIG="C:\path\to\config.json"
```

The Windows tray app always points the bundled CLI at the same config file.

## Shape

```json
{
  "version": 1,
  "providers": [
    {
      "id": "openrouter",
      "enabled": true,
      "source": "api",
      "apiKey": "sk-or-..."
    }
  ]
}
```

## Provider Fields

- `id`: provider identifier.
- `enabled`: enables or disables the provider.
- `source`: `auto`, `web`, `cli`, `oauth`, or `api`.
- `apiKey`: token for API-backed providers when managed directly through the
  CLI/config file.
- `cookieSource`: `auto`, `manual`, or `off`.
- `cookieHeader`: manual HTTP `Cookie` header value.
- `region`: provider-specific region.
- `workspaceID`: provider-specific workspace or organization ID.
- `tokenAccounts`: multi-account token data.

## Commands

```powershell
.\CodexBarCLI.exe config validate --format json --pretty
.\CodexBarCLI.exe config dump --pretty
.\CodexBarCLI.exe config providers
.\CodexBarCLI.exe config enable --provider openrouter
.\CodexBarCLI.exe config disable --provider cursor
Get-Content .\token.txt | .\CodexBarCLI.exe config set-api-key --provider openrouter --stdin
Get-Content .\cookie.txt | .\CodexBarCLI.exe config set-cookie --provider cursor --stdin
```

Keep this file private. It can contain API keys if you use the CLI
`set-api-key` command directly, and it can contain manual Cookie headers.
