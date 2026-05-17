# CodexBar-Windows CLI

`CodexBarCLI.exe` is the Swift provider engine used by the Windows tray app.
It can also be run directly from PowerShell, scripts, and automation.

## Install

Download the `codexbar-windows-x86_64` artifact from the `CI` or `Release`
workflow, extract the inner app zip, and run the CLI beside
`CodexBar-Windows.exe`.

```powershell
.\CodexBarCLI.exe --version
.\CodexBarCLI.exe usage --provider all --format json --pretty
```

## Build

```powershell
swift build -c release --product CodexBarCLI
pwsh .\Windows\package-windows.ps1 -ReleaseTag dev
```

## Configuration

On Windows, the default config path is:

```text
%APPDATA%\CodexBar-Windows\config.json
```

Set `CODEXBAR_CONFIG` to use another config file for tests or scripts.

## Common Commands

```powershell
.\CodexBarCLI.exe usage --provider all --format json --pretty
.\CodexBarCLI.exe usage --provider openrouter --source api
.\CodexBarCLI.exe cost --provider claude --format json --pretty
.\CodexBarCLI.exe config validate --format json --pretty
.\CodexBarCLI.exe config dump --pretty
.\CodexBarCLI.exe config providers
.\CodexBarCLI.exe config enable --provider openrouter
.\CodexBarCLI.exe config disable --provider cursor
.\CodexBarCLI.exe config set-api-key --provider openrouter --stdin
.\CodexBarCLI.exe config set-cookie --provider cursor --stdin
```

## Useful Flags

- `--provider <id|all>` selects one provider or every registered provider.
- `--source <auto|web|cli|oauth|api>` selects the provider source when supported.
- `--format text|json` controls output shape.
- `--pretty` formats JSON for reading.
- `--no-color` disables ANSI output.
- `--json-only` suppresses non-JSON diagnostics.
- `--status` includes status-page data where implemented.

## Windows Compatibility

API-backed and local-file providers are the most reliable today. Providers that
depend on browser cookie extraction, WebKit dashboards, or interactive PTY
sessions are still behind Windows platform gates until native implementations
land.

See [providers.md](providers.md) and [windows-port.md](windows-port.md).
