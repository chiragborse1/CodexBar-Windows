# CodexBar-Windows v0.1.0-alpha.1

First public Windows alpha.

## What is included

- Native Windows tray app: `CodexBar-Windows.exe`
- CLI backend: `CodexBarCLI.exe`
- Structured dashboard backed by CLI JSON
- Provider compatibility tab
- AppData-backed Windows config path
- User-profile install/uninstall scripts
- CI and release packaging for Windows x64

## Install

Download `CodexBar-Windows-v0.1.0-alpha.1-windows-x86_64.zip`, extract it,
then run:

```powershell
.\CodexBar-Windows.exe
```

Optional install into `%LOCALAPPDATA%\Programs\CodexBar-Windows`:

```powershell
powershell -ExecutionPolicy Bypass -File .\install.ps1 -Launch
```

## Known gaps

- Windows Credential Manager storage is not implemented yet.
- Browser cookie import for Edge, Chrome, Firefox, and Chromium is not implemented yet.
- Interactive CLI providers still need Windows ConPTY support.
- The package is not code-signed yet, so Windows SmartScreen may warn.
