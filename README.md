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
- Usage error cards include direct recovery actions for provider setup, CLI
  backend selection, and known provider dashboards.
- Usage, Settings, and More now stay inside the popover instead of opening
  classic secondary windows.
- Settings saves API keys for Windows-ready providers in Windows Credential
  Manager and injects them into CLI runs.
- Settings includes a CLI Backend panel for selecting `CodexBarCLI.exe` when
  testing from a dev folder or custom install.
- Settings includes a Manual Web Session panel that can import Edge,
  Chrome/Chromium, Brave, Vivaldi, Opera, or Firefox cookies for supported
  web-session providers, import Windsurf Chromium localStorage sessions, or
  accept a pasted Cookie header/session payload.
- More includes diagnostics, provider compatibility, and a GitHub release
  update check inside the popover.
- More also includes an About panel, and Usage can open the selected
  provider's dashboard when a known account/usage URL is available.
- Windows usage no longer fails early just because a provider has a web source;
  CLI/API/OAuth/manual-cookie fallbacks now run provider by provider.
- Codex and Claude interactive CLI probes use a packaged Windows ConPTY bridge
  when `CodexBar-Windows.exe` is beside `CodexBarCLI.exe`.
- Windows CLI build is wired into GitHub Actions.
- Release packaging verifies `CodexBar-Windows.exe --smoke-test` and
  `CodexBar-Windows.exe --pty-bridge-smoke-test`, plus
  `CodexBarCLI.exe --version` from the packaged folder.
- CI also smoke-tests `CodexBarCLI.exe serve` on Windows through the localhost
  `/health` endpoint.
- Windows release packaging is configured for an app `.zip` artifact containing
  `CodexBar-Windows.exe`, `CodexBarCLI.exe`, bundled Swift runtime DLLs,
  `README_RUN.txt`, install scripts, and `VERSION`.

Current limitations:
- CLI-side automatic browser cookie extraction is still stubbed on Windows;
  use the tray app's browser import or paste a Cookie header.
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
2. If the app cannot find `CodexBarCLI.exe`, use `CLI Backend` to browse to it.
3. Pick a provider in `Provider Setup`.
4. Choose a Windows-ready provider such as OpenRouter, OpenAI, Copilot, Claude,
   ElevenLabs, Moonshot, Kilo, or Venice.
5. Paste the provider API key, keep `Enable provider after saving` checked, and
   click `Save API Key`.
6. The popover focuses that provider and refreshes against it.

For web-session providers, use `Settings` > `Manual Web Session`, select the
provider, then click the import button for the browser where you are signed in.
For Windsurf this imports its Chromium localStorage session payload. If browser
import cannot read a session, paste a Cookie header or supported session
payload and click `Save Cookie`.

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
powershell -ExecutionPolicy Bypass -File .\Windows\ui-dev.ps1
powershell -ExecutionPolicy Bypass -File .\Windows\dev-run.ps1
```

PowerShell 7 also works if `pwsh` is installed.

`ui-dev.ps1` is the fast UI loop: it builds only the WPF Windows app, opens it
as a normal visible window, and uses demo provider data by default so Swift and
provider credentials are not needed. `dev-run.ps1` builds a full local package,
stops any running dev copy, launches the fresh tray app, and verifies it stayed
running. The runnable app folder is:

```text
artifacts\codexbar-windows-x86_64
```

When the repo is opened through WSL as `\\wsl.localhost\...`, `ui-dev.ps1`
stages the Windows UI source into `%LOCALAPPDATA%\CodexBar-Windows\ui-dev-source`
before building because MSBuild/desktop launch behavior is more reliable from a
local Windows path. `dev-run.ps1` and `package-windows.ps1` also normalize WSL
filesystem paths before resolving package locations.

Manual build commands:

```powershell
swift build -c release --product CodexBarCLI
dotnet publish Windows\CodexBar.Windows\CodexBar.Windows.csproj -c Release -r win-x64 --self-contained false
pwsh .\Windows\package-windows.ps1 -ReleaseTag dev
```

## CLI Examples

```powershell
CodexBar-Windows.exe
CodexBar-Windows.exe --window
CodexBar-Windows.exe --window --demo
CodexBarCLI.exe --version
CodexBarCLI.exe usage --format json --pretty
CodexBarCLI.exe usage --format json --provider openrouter --pretty
CodexBarCLI.exe config providers
CodexBarCLI.exe config set-api-key --provider elevenlabs --stdin
CodexBarCLI.exe config set-cookie --provider cursor --stdin
CodexBarCLI.exe cost --provider claude --format json --pretty
CodexBarCLI.exe serve --port 8080 --refresh-interval 60
```

Use `CodexBar-Windows.exe --window` when testing if Windows hides the tray
icon; it opens the same popover as a normal taskbar-visible window. Add
`--demo` to show UI demo data without requiring the CLI backend.

API keys saved from the Windows app are stored in Windows Credential Manager.
Provider toggles, manual Cookie headers, and advanced provider options are
stored in `%APPDATA%\CodexBar-Windows\config.json`.

## Roadmap

1. Keep the Windows tray app and CLI green in CI.
2. Continue matching the macOS menu-card and settings experience on Windows.
3. Move browser import into CLI-side automatic provider fallback where useful.
4. Add signed MSIX/WiX installer packaging.

## Development

Useful commands:

```powershell
pwsh .\Windows\ui-dev.ps1
pwsh .\Windows\ui-dev.ps1 -NoDemo
pwsh .\Windows\ui-dev.ps1 -UsePackage
powershell -ExecutionPolicy Bypass -File .\Windows\ui-dev.ps1
pwsh .\Windows\dev-run.ps1
pwsh .\Windows\dev-run.ps1 -NoBuild
pwsh .\Windows\dev-run.ps1 -NoBuild -NoLaunch -Test
swift build -c release --product CodexBarCLI
dotnet publish Windows\CodexBar.Windows\CodexBar.Windows.csproj -c Release -r win-x64 --self-contained false
pwsh .\Windows\package-windows.ps1 -ReleaseTag dev
swift test
```

The shared Swift engine still contains platform gates for providers that depend
on browser cookies, WebKit, or keychain behavior. Windows work should stay
isolated behind those gates until native Windows implementations exist.

## Attribution

CodexBar-Windows includes MIT-licensed code derived from the original CodexBar
project. Required notices are kept in [NOTICE](NOTICE) and [LICENSE](LICENSE).

## License

MIT. See [LICENSE](LICENSE).
