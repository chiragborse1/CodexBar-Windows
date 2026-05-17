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
  CLI backend, runtime files, `VERSION`, and `README_RUN.txt`.
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
- dashboard window with refresh/copy/config/settings actions
- scheduled refresh loop
- configurable provider target
- CLI path override
- launch-at-sign-in toggle through the current-user Run registry key
- app settings in `%APPDATA%\CodexBar-Windows\windows-app-settings.json`
- provider config access at `%USERPROFILE%\.codexbar\config.json`

## What Works on Windows

- API-backed provider paths that only require HTTP and config/environment
  secrets.
- JSON and text CLI output.
- `config validate`, `config dump`, `config providers`, provider enable/disable,
  and config-backed API key storage.
- Local cost commands where their scanner inputs are available.
- Native tray launch and dashboard refresh through the bundled CLI.

## Known Gaps

- OpenAI/Claude web dashboard scraping needs Windows browser-cookie support.
- Codex/Claude interactive CLI sessions need ConPTY-backed PTY support.
- Windows Credential Manager should replace Keychain-only secret paths.
- MSIX/WiX installer and auto-update channel are not implemented yet.

## Next Steps

1. Improve the tray dashboard with structured JSON cards.
2. Add Windows Credential Manager storage for secrets.
3. Add Edge/Chrome/Firefox cookie import.
4. Add ConPTY support for interactive provider CLIs.
5. Add `.msix` or WiX installer packaging.

## Repo Policy

Windows changes should stay isolated behind platform boundaries. Cross-platform
fixes should avoid breaking macOS/Linux behavior in the shared provider engine.
