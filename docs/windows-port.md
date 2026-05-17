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
- The tray popover parses CLI JSON into provider cards and keeps Settings and
  More diagnostics inside the tray surface.
- Settings saves API keys to Windows Credential Manager, supports manual Cookie
  headers for web-session providers, and focuses the popover on the configured
  provider.
- More includes diagnostics, provider compatibility, config actions, and a
  GitHub release update check.
- Browser-cookie extraction is stubbed on Windows.
- PTY-backed interactive provider helpers are stubbed on Windows.
- The localhost `serve` command is stubbed on Windows while Foundation
  networking portability is completed.

## Windows App

Project path:

```text
Windows/CodexBar.Windows/CodexBar.Windows.csproj
```

The tray app provides:

- system tray icon and menu
- compact CodexBar-style usage popover
- embedded More view with refresh/copy/config actions and provider
  compatibility
- embedded update checker for GitHub releases
- scheduled refresh loop
- configurable provider target
- embedded provider API-key setup backed by Windows Credential Manager
- embedded manual Cookie header setup for web-session providers
- CLI path override
- launch-at-sign-in toggle through the current-user Run registry key
- app settings in `%APPDATA%\CodexBar-Windows\windows-app-settings.json`
- provider config access at `%APPDATA%\CodexBar-Windows\config.json`

## What Works on Windows

- API-backed provider paths that only require HTTP and config/environment
  secrets.
- JSON and text CLI output.
- `config validate`, `config dump`, `config providers`, provider enable/disable,
  config-backed manual Cookie headers, and provider setup from the tray app.
- Local cost commands where their scanner inputs are available.
- Native tray launch and popover refresh through the bundled CLI.

## Known Gaps

- OpenAI/Claude web dashboard scraping needs Windows browser-cookie support.
- Codex/Claude interactive CLI sessions need ConPTY-backed PTY support.
- Native browser-cookie extraction for Edge, Chrome, Firefox, and Chromium is
  not implemented yet; manual Cookie headers are the current bridge.
- MSIX/WiX installer packaging, code signing, and automatic update install are
  not implemented yet.

## Next Steps

1. Add Edge/Chrome/Firefox cookie import.
2. Add ConPTY support for interactive provider CLIs.
3. Add `.msix` or WiX installer packaging.
4. Add signed release builds and automatic update install.

## Repo Policy

Windows changes should stay isolated behind platform boundaries. Cross-platform
fixes should preserve the shared provider engine behavior used by the CLI.
