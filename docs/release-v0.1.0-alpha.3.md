# CodexBar-Windows v0.1.0-alpha.3

WPF popover repair release.

## What changed

- Replaced the broken WinForms popover from `0.1.0-alpha.2` with a WPF popover.
- Provider rows now render as rounded cards with icon badges, status pills,
  progress bars, reset text, credits, cost, and error summaries.
- The tray host, diagnostics window, install scripts, and CLI backend remain
  unchanged.

## Install

Download `CodexBar-Windows-v0.1.0-alpha.3-windows-x86_64.zip`, extract it,
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
