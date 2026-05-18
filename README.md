# CodexBar-Windows

> Track AI coding-provider limits from Windows.

[![CI](https://github.com/chiragborse1/CodexBar-Windows/actions/workflows/ci.yml/badge.svg)](https://github.com/chiragborse1/CodexBar-Windows/actions/workflows/ci.yml)
[![License: MIT](https://img.shields.io/badge/license-MIT-6e5aff?style=flat-square)](LICENSE)

CodexBar-Windows is a Windows-focused usage tracker for AI coding providers.
It ships a native Windows tray app that wraps the shared CLI/provider engine,
plus a standalone CLI for scripts and terminal workflows.

## Status

Current build status:
- Native Windows tray app opens a compact WPF tray popover shaped after the
  macOS menu-card flow.
- Usage includes a compact provider switcher inside the popover.
- Usage, Settings, and More now stay inside the popover instead of opening
  classic secondary windows.
- Settings saves API keys for Windows-ready providers in Windows Credential
  Manager and injects them into CLI runs.
- Settings includes a Manual Web Session panel that can import Edge, Chrome,
  or Brave cookies for supported web-session providers, or accept a pasted
  Cookie header.
- More includes diagnostics, provider compatibility, and a GitHub release
  update check inside the popover.
- Windows usage no longer fails early just because a provider has a web source;
  CLI/API/OAuth/manual-cookie fallbacks now run provider by provider.
- Windows CLI build is wired into GitHub Actions.
- Release packaging verifies `CodexBar-Windows.exe --smoke-test` and
  `CodexBarCLI.exe --version` from the packaged folder.
- Windows release packaging is configured for an app `.zip` artifact containing
  `CodexBar-Windows.exe`, `CodexBarCLI.exe`, bundled Swift runtime DLLs,
  `README_RUN.txt`, install scripts, and `VERSION`.

Current limitations:
- CLI-side automatic browser cookie extraction is still stubbed on Windows;
  use the tray app's Edge/Chrome/Brave import or paste a Cookie header.
- PTY-backed Codex and Claude CLI sessions are stubbed on Windows.
- The localhost HTTP `serve` command is stubbed on Windows.
- Some macOS charts, provider icons, and animation details are still being
  translated into native Windows UI.

See [docs/windows-port.md](docs/windows-port.md) for the current implementation
plan and compatibility notes.

## Install

Windows release binaries are published from this repository:

<https://github.com/chiragborse1/CodexBar-Windows/releases>

Download the latest `CodexBar-Windows-<version>-windows-x86_64.zip` release
asset, extract it, and run `CodexBar-Windows.exe`.

Click the Windows tray icon to open the compact usage popover. Use the provider
switcher at the top of Usage to change scope quickly. Use `Settings` or `More`
inside the popover for setup, raw CLI output, config actions, and the provider
compatibility view.

First-time setup:

1. Open the tray popover and click `Settings`.
2. Pick a provider in `Provider Setup`.
3. Choose a Windows-ready provider such as OpenRouter, OpenAI, Copilot, Claude,
   ElevenLabs, Moonshot, Kilo, or Venice.
4. Paste the provider API key, keep `Enable provider after saving` checked, and
   click `Save API Key`.
5. The popover focuses that provider and refreshes against it.

For web-session providers, use `Settings` > `Manual Web Session`, select the
provider, then click `Import Edge`, `Import Chrome`, or `Import Brave`. If the
browser import cannot read a session, paste a Cookie header and click
`Save Cookie`.

To install it into your user profile and create a Start Menu shortcut:

```powershell
powershell -ExecutionPolicy Bypass -File .\install.ps1 -Launch
powershell -ExecutionPolicy Bypass -File .\install.ps1 -Launch -DesktopShortcut
```

Manual workflow artifacts can also be downloaded from the `Release` or `CI`
GitHub Actions workflow. The Windows artifact is named
`codexbar-windows-x86_64`; extracting it once gives you `CodexBar-Windows.exe`
and `CodexBarCLI.exe`.

To build from source on Windows:

```powershell
pwsh .\Windows\dev-run.ps1
```

That command builds a local package, stops any running dev copy, launches the
fresh tray app, and verifies it stayed running. The runnable app folder is:

```text
artifacts\codexbar-windows-x86_64
```

Manual build commands:

```powershell
swift build -c release --product CodexBarCLI
dotnet publish Windows\CodexBar.Windows\CodexBar.Windows.csproj -c Release -r win-x64 --self-contained false
pwsh .\Windows\package-windows.ps1 -ReleaseTag dev
```

## CLI Examples

```powershell
CodexBar-Windows.exe
CodexBarCLI.exe --version
CodexBarCLI.exe usage --format json --pretty
CodexBarCLI.exe usage --format json --provider openrouter --pretty
CodexBarCLI.exe config providers
CodexBarCLI.exe config set-api-key --provider elevenlabs --stdin
CodexBarCLI.exe config set-cookie --provider cursor --stdin
CodexBarCLI.exe cost --provider claude --format json --pretty
```

API keys saved from the Windows app are stored in Windows Credential Manager.
Provider toggles, manual Cookie headers, and advanced provider options are
stored in `%APPDATA%\CodexBar-Windows\config.json`.

## Roadmap

1. Keep the Windows tray app and CLI green in CI.
2. Continue matching the macOS menu-card and settings experience on Windows.
3. Expand browser profile cookie extraction beyond Edge, Chrome, and Brave to
   Firefox and additional Chromium variants.
4. Replace PTY stubs with Windows ConPTY support where provider CLIs require an
   interactive terminal.
5. Add signed MSIX/WiX installer packaging.

## Development

Useful commands:

```powershell
pwsh .\Windows\dev-run.ps1
pwsh .\Windows\dev-run.ps1 -NoBuild
pwsh .\Windows\dev-run.ps1 -NoBuild -NoLaunch -Test
swift build -c release --product CodexBarCLI
dotnet publish Windows\CodexBar.Windows\CodexBar.Windows.csproj -c Release -r win-x64 --self-contained false
pwsh .\Windows\package-windows.ps1 -ReleaseTag dev
swift test
```

The shared Swift engine still contains platform gates for providers that depend
on browser cookies, WebKit, keychain, or PTY behavior. Windows work should stay
isolated behind those gates until native Windows implementations exist.

## Attribution

CodexBar-Windows includes MIT-licensed code derived from the original CodexBar
project. Required notices are kept in [NOTICE](NOTICE) and [LICENSE](LICENSE).

## License

MIT. See [LICENSE](LICENSE).
