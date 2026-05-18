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
- Settings saves API keys to Windows Credential Manager, imports Edge, Chrome,
  Brave, and Firefox cookies for web-session providers, supports manual Cookie
  header fallback, and focuses the popover on the configured provider.
- More includes diagnostics, provider compatibility, config actions, and a
  GitHub release update check.
- Windows usage falls through to provider-specific CLI/API/OAuth/manual-cookie
  paths instead of failing early when a provider also has a web source.
- CLI-side automatic browser-cookie extraction is stubbed on Windows. The
  Windows tray app can import Edge, Chrome, Brave, and Firefox cookie stores
  into the config-backed manual Cookie header path.
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
- compact provider switcher in the Usage view
- embedded More view with refresh/copy/config actions and provider
  compatibility
- embedded update checker for GitHub releases
- scheduled refresh loop
- configurable provider target
- embedded provider API-key setup backed by Windows Credential Manager
- embedded web-session setup with Edge, Chrome, Brave, and Firefox cookie
  import plus manual Cookie header fallback
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
- Edge, Chrome, Brave, and Firefox cookie import from the tray app for supported
  web-session providers.
- Local cost commands where their scanner inputs are available.
- Native tray launch and popover refresh through the bundled CLI.

## Known Gaps

- CLI-side OpenAI/Claude web dashboard scraping still needs native Windows
  browser-cookie support.
- Codex/Claude interactive CLI sessions need ConPTY-backed PTY support.
- CLI-side automatic browser-cookie import is not implemented yet.
- MSIX/WiX installer packaging, code signing, and automatic update install are
  not implemented yet.

## Next Steps

1. Add additional Chromium browser cookie import.
2. Add ConPTY support for interactive provider CLIs.
3. Add `.msix` or WiX installer packaging.
4. Add signed release builds and automatic update install.

## Repo Policy

Windows changes should stay isolated behind platform boundaries. Cross-platform
fixes should preserve the shared provider engine behavior used by the CLI.
