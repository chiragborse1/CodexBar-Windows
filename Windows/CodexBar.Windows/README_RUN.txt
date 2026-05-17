CodexBar-Windows
================

This folder contains the Windows tray app and the CLI backend.

Run:
  CodexBar-Windows.exe

Optional install:
  powershell -ExecutionPolicy Bypass -File .\install.ps1 -Launch

Uninstall after using install.ps1:
  powershell -ExecutionPolicy Bypass -File .\uninstall.ps1

The app places an icon in the Windows system tray. Click the tray icon to open
the compact CodexBar popover. Use More or the tray context menu for diagnostics.

First-time setup:
  1. Open Settings from the popover or tray menu.
  2. Open Providers.
  3. Pick a provider, paste its API key, and click Save API Key.
  4. Click Save in Settings.

The bundled CLI backend is:
  CodexBarCLI.exe

The zip also includes Swift runtime DLLs needed by the CLI. You do not need to
install Swift to run the packaged app.

Useful CLI checks:
  CodexBarCLI.exe --version
  CodexBarCLI.exe config validate --format json --pretty
  CodexBarCLI.exe usage --format json --pretty
  CodexBarCLI.exe usage --provider openrouter --format json --pretty

Provider credentials and toggles are stored in:
  %APPDATA%\CodexBar-Windows\config.json

App settings are stored in:
  %APPDATA%\CodexBar-Windows\windows-app-settings.json
