# CodexBar-Windows Port Plan

CodexBar-Windows is a Windows-focused project for tracking AI coding-provider
usage and reset limits. It currently reuses a shared provider engine and CLI
foundation, with required MIT notices kept in `NOTICE` and `LICENSE`.

## Goal

Bring a complete CodexBar-Windows experience to Windows users:

1. Keep the shared provider and usage engine compiling on Windows.
2. Keep `CodexBarCLI.exe` building and smoke-tested on Windows.
3. Ship a Windows tray app that calls the CLI/provider engine.
4. Fill in Windows-native replacements for macOS-only integrations.

## Current Status

- `CodexBar-Windows.exe` is a native Windows tray app built with WinForms.
- The tray app wraps `CodexBarCLI.exe` for provider usage, config validation,
  provider listing, and settings-driven refreshes.
- `CodexBarCLI.exe` builds on `windows-latest`.
- Windows CI smoke-tests both the CLI and the tray app.
- Release packaging uploads `codexbar-windows-x86_64`, containing the tray app,
  CLI backend, Swift runtime DLLs, publish files, `VERSION`, and
  `README_RUN.txt`.
- The tray popover parses CLI JSON into compact menu-style provider cards,
  includes an in-popover provider switcher, and keeps Settings and More
  diagnostics inside the tray surface.
- Settings saves API keys to Windows Credential Manager, imports Edge,
  Chrome/Chromium, Brave, Vivaldi, Opera, and Firefox cookies for web-session
  providers, imports Windsurf Chromium localStorage sessions, supports manual
  Cookie header/session payload fallback, and focuses the popover on the
  configured provider.
- More includes diagnostics, provider compatibility, config actions, and a
  GitHub release update check.
- Usage includes provider dashboard shortcuts for providers with known usage or
  account URLs.
- The localhost `serve` command runs on Windows and is smoke-tested in CI
  through `GET /health`.
- Windows usage falls through to provider-specific CLI/API/OAuth/manual-cookie
  paths instead of failing early when a provider also has a web source.
- Codex and Claude interactive CLI helpers use the packaged Windows ConPTY
  bridge when the tray app and CLI are in the same folder.
- CLI-side automatic browser-cookie extraction is stubbed on Windows. Windsurf
  has a Windows Chromium localStorage importer; the tray app can import
  supported browser sessions into the config-backed manual credential path.
## Windows App

Project path:

```text
Windows/CodexBar.Windows/CodexBar.Windows.csproj
```

The tray app provides:

- system tray icon and menu
- compact CodexBar-style usage popover
- compact provider switcher in the Usage view
- embedded More view with refresh/copy/config actions and provider
  compatibility
- selected-provider dashboard shortcuts from the Usage view
- embedded update checker for GitHub releases
- scheduled refresh loop
- configurable provider target
- embedded provider API-key setup backed by Windows Credential Manager
- embedded web-session setup with Edge, Chrome/Chromium, Brave, Vivaldi,
  Opera, and Firefox cookie import, Windsurf Chromium localStorage import, and
  manual Cookie header/session payload fallback
- CLI path override
- launch-at-sign-in toggle through the current-user Run registry key
- app settings in `%APPDATA%\CodexBar-Windows\windows-app-settings.json`
- provider config access at `%APPDATA%\CodexBar-Windows\config.json`

## What Works on Windows

- API-backed provider paths that only require HTTP and config/environment
  secrets.
- JSON and text CLI output.
- `serve` local HTTP endpoints on `127.0.0.1` for `/health`, `/usage`, and
  `/cost`.
- `config validate`, `config dump`, `config providers`, provider enable/disable,
  config-backed manual Cookie headers, and provider setup from the tray app.
- supported Windows browser cookie import from the tray app for web-session
  providers.
- Windsurf web usage through Windows Chromium localStorage session import.
- Codex and Claude interactive CLI usage through the packaged ConPTY bridge.
- Local cost commands where their scanner inputs are available.
- Native tray launch and popover refresh through the bundled CLI.

## Known Gaps

- CLI-side OpenAI/Claude web dashboard scraping still needs native Windows
  browser-cookie support.
- CLI-side automatic browser-cookie import is not implemented yet; the tray app
  imports supported browser sessions into config-backed manual credentials.
- MSIX/WiX installer packaging, code signing, and automatic update install are
  not implemented yet.

## Next Steps

1. Move selected browser imports into CLI-side automatic provider fallback.
2. Add `.msix` or WiX installer packaging.
3. Add signed release builds and automatic update install.

## Repo Policy

Windows changes should stay isolated behind platform boundaries. Cross-platform
fixes should preserve the shared provider engine behavior used by the CLI.
