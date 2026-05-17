# CodexBar-Windows v0.1.0-alpha.4

Provider setup and first-run polish release.

## What changed

- Added API-key setup controls in `Settings > Providers`.
- Added provider enable/disable actions from the Windows Settings UI.
- The app now defaults to configured providers instead of querying every known
  provider.
- Saving an API key focuses the popover on that provider.
- The popover now shows setup and Windows-pending states instead of raw red
  errors where possible.
- Removed the duplicate DPI manifest setting so the Windows build is warning-free.

## Install

Download `CodexBar-Windows-v0.1.0-alpha.4-windows-x86_64.zip`, extract it,
then run:

```powershell
.\CodexBar-Windows.exe
```

First-time setup:

```text
Settings > Providers > select provider > paste API key > Save API Key > Save
```

## Known gaps

- Windows Credential Manager storage is not implemented yet.
- Browser cookie import for Edge, Chrome, Firefox, and Chromium is not implemented yet.
- Interactive CLI providers still need Windows ConPTY support.
- The package is not code-signed yet, so Windows SmartScreen may warn.
