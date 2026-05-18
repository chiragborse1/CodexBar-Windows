# CodexBar-Windows v0.1.0-alpha.5

In-popover Settings and More release.

## What changed

- Settings now opens inside the tray popover.
- More now opens inside the tray popover.
- The tray context menu Settings and Diagnostics entries route back into the
  popover instead of opening classic Windows forms.
- Added embedded controls for provider scope, refresh interval, launch at
  sign-in, start minimized, provider API-key setup, and provider enable/disable.
- Added embedded diagnostics with raw CLI output, copy, config validation,
  config file/folder shortcuts, and provider compatibility.
- Refreshed the visual design with a wider popover, segmented navigation,
  softer cards, styled WPF buttons, and cleaner spacing.
- Added Windows Credential Manager-backed API-key storage.
- Added browser session import for Edge, Chrome/Chromium, Brave, Vivaldi,
  Opera, Firefox, and Windsurf Chromium localStorage.
- Added a packaged Windows ConPTY bridge for Codex and Claude CLI probes.

## Install

Download `CodexBar-Windows-v0.1.0-alpha.5-windows-x86_64.zip`, extract it,
then run:

```powershell
.\CodexBar-Windows.exe
```

First-time setup:

```text
Settings > Provider Setup > select provider > paste API key > Save API Key
```

## Known gaps

- CLI-side automatic browser-cookie import is not implemented yet; use the
  tray app import flow or manual Cookie/session payload setup.
- The package is not code-signed yet, so Windows SmartScreen may warn.
