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

- Windows Credential Manager storage is not implemented yet.
- Browser cookie import for Edge, Chrome, Firefox, and Chromium is not implemented yet.
- Interactive CLI providers still need Windows ConPTY support.
- The package is not code-signed yet, so Windows SmartScreen may warn.
