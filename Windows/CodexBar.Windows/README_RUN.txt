CodexBar-Windows
================

This folder contains the Windows tray app and the CLI backend.

Run:
  CodexBar-Windows.exe

Optional install:
  powershell -ExecutionPolicy Bypass -File .\install.ps1 -Launch
  powershell -ExecutionPolicy Bypass -File .\install.ps1 -Launch -DesktopShortcut

Uninstall after using install.ps1:
  powershell -ExecutionPolicy Bypass -File .\uninstall.ps1

The app places an icon in the Windows system tray. Click the tray icon to open
the compact CodexBar popover. Use Settings and More inside the popover for
provider setup, diagnostics, raw CLI output, and config actions.

First-time setup:
  1. Open Settings inside the popover.
  2. Pick a provider in Provider Setup.
  3. Paste its API key and click Save API Key.
  4. Return to Usage.

For web-session providers, open Settings > Manual Web Session and paste a
Cookie header.

The bundled CLI backend is:
  CodexBarCLI.exe

The zip also includes Swift runtime DLLs needed by the CLI. You do not need to
install Swift to run the packaged app.

Useful CLI checks:
  CodexBarCLI.exe --version
  CodexBarCLI.exe config validate --format json --pretty
  CodexBarCLI.exe usage --format json --pretty
  CodexBarCLI.exe usage --provider openrouter --format json --pretty

API keys saved from the app are stored in Windows Credential Manager.

Provider toggles, manual Cookie headers, and advanced provider options are
stored in:
  %APPDATA%\CodexBar-Windows\config.json

App settings are stored in:
  %APPDATA%\CodexBar-Windows\windows-app-settings.json
