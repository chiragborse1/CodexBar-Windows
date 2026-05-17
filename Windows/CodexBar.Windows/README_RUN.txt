CodexBar-Windows
================

This folder contains the Windows tray app and the CLI backend.

Run:
  CodexBar-Windows.exe

Optional install:
  powershell -ExecutionPolicy Bypass -File .\install.ps1 -Launch

Uninstall after using install.ps1:
  powershell -ExecutionPolicy Bypass -File .\uninstall.ps1

The app places an icon in the Windows system tray. Open the dashboard from the
tray icon or by double-clicking it.

The bundled CLI backend is:
  CodexBarCLI.exe

The zip also includes Swift runtime DLLs needed by the CLI. You do not need to
install Swift to run the packaged app.

Useful CLI checks:
  CodexBarCLI.exe --version
  CodexBarCLI.exe config validate --format json --pretty
  CodexBarCLI.exe usage --provider all --format json --pretty

Provider credentials and toggles are stored in:
  %APPDATA%\CodexBar-Windows\config.json

App settings are stored in:
  %APPDATA%\CodexBar-Windows\windows-app-settings.json
