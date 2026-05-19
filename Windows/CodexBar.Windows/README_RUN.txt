CodexBar-Windows
================

This folder contains the Windows tray app and the CLI backend.

Run:
  CodexBar-Windows.exe

If Windows hides the tray icon while testing:
  CodexBar-Windows.exe --window

To view the UI with built-in demo provider data:
  CodexBar-Windows.exe --window --demo

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

For web-session providers, open Settings > Manual Web Session, pick the
provider, and import from the browser where you are signed in. Windsurf imports
its Chromium localStorage session payload. If browser import cannot read your
session, paste a Cookie header or supported session payload and click Save
Cookie.

The bundled CLI backend is:
  CodexBarCLI.exe

The zip also includes Swift runtime DLLs needed by the CLI. You do not need to
install Swift to run the packaged app.

Useful CLI checks:
  CodexBarCLI.exe --version
  CodexBarCLI.exe config validate --format json --pretty
  CodexBarCLI.exe usage --format json --pretty
  CodexBarCLI.exe usage --provider openrouter --format json --pretty
  CodexBarCLI.exe serve --port 8080 --refresh-interval 60

Codex and Claude CLI probes use the packaged Windows ConPTY bridge when
CodexBar-Windows.exe is beside CodexBarCLI.exe.

API keys saved from the app are stored in Windows Credential Manager.

Provider toggles, manual Cookie headers, and advanced provider options are
stored in:
  %APPDATA%\CodexBar-Windows\config.json

App settings are stored in:
  %APPDATA%\CodexBar-Windows\windows-app-settings.json
