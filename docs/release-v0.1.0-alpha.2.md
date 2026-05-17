# CodexBar-Windows v0.1.0-alpha.2

Windows UI parity alpha.

## What changed

- Tray icon now opens a compact CodexBar-style usage popover.
- Provider rows render as cards with usage bars, reset text, status, credits,
  cost, and error summaries.
- The previous table dashboard is now a diagnostics window behind `More` and
  the tray context menu.
- Settings now uses General, Display, Providers, Advanced, and About sections.

## Install

Download `CodexBar-Windows-v0.1.0-alpha.2-windows-x86_64.zip`, extract it,
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
