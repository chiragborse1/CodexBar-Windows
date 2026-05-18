# Windows App

CodexBar-Windows includes a native Windows tray app in:

```text
Windows/CodexBar.Windows
```

The app uses WinForms for the Windows tray host, with a WPF
popover as the primary user-facing UI around `CodexBarCLI.exe`. It keeps the
provider engine in Swift while giving Windows users a normal tray entry point.
The popover follows the macOS CodexBar menu-card model and includes Usage,
Settings, and More views without leaving the tray surface.

## Build

Fast local development loop:

```powershell
pwsh .\Windows\dev-run.ps1
```

`dev-run.ps1` stops any running `CodexBar-Windows.exe`, builds a local package,
launches `artifacts\codexbar-windows-x86_64\CodexBar-Windows.exe`, and checks
that the tray app stays running. Useful variants:

```powershell
pwsh .\Windows\dev-run.ps1 -NoBuild
pwsh .\Windows\dev-run.ps1 -NoBuild -NoLaunch -Test
pwsh .\Windows\dev-run.ps1 -Configuration Debug
```

Manual build:

```powershell
swift build -c release --product CodexBarCLI
dotnet publish Windows\CodexBar.Windows\CodexBar.Windows.csproj -c Release -r win-x64 --self-contained false
pwsh .\Windows\package-windows.ps1 -ReleaseTag dev
```

The package script copies `CodexBarCLI.exe` and Swift runtime DLLs beside
`CodexBar-Windows.exe`. The release workflow runs the same script.

## Install Script

The packaged folder includes:

```powershell
powershell -ExecutionPolicy Bypass -File .\install.ps1 -Launch
powershell -ExecutionPolicy Bypass -File .\uninstall.ps1
```

`install.ps1` copies the extracted app to
`%LOCALAPPDATA%\Programs\CodexBar-Windows`, creates Start Menu shortcuts,
registers the app in Windows Apps & Features, and can create a desktop shortcut
with `-DesktopShortcut`. `uninstall.ps1` removes shortcuts, uninstall
registration, install folder, launch-at-sign-in entry, and app settings unless
`-KeepSettings` is passed.

## Runtime Files

- App settings: `%APPDATA%\CodexBar-Windows\windows-app-settings.json`
- Provider config: `%APPDATA%\CodexBar-Windows\config.json`
- Bundled CLI: `CodexBarCLI.exe` beside `CodexBar-Windows.exe`

## Features

- system tray icon and context menu
- WPF CodexBar-style usage popover
- compact menu-style provider cards with usage bars, reset text, credits, cost,
  status, and errors
- compact provider switcher at the top of Usage
- embedded More view with raw CLI output, config actions, and provider
  compatibility
- in-popover GitHub release update check
- usage refresh through the CLI backend
- provider selection with an `enabled` scope that mirrors config
- embedded API-key setup controls backed by Windows Credential Manager
- embedded web-session setup with Edge, Chrome, and Brave cookie import plus
  manual Cookie header fallback
- CLI path override
- embedded settings for refresh scope, refresh interval, startup behavior, and
  provider setup
- launch at sign-in toggle
- start minimized toggle
- config file/folder shortcuts
- manual smoke-test mode: `CodexBar-Windows.exe --smoke-test`

## Provider Setup

Open `Settings` inside the tray popover, choose a provider, paste its API key,
and click `Save API Key`. The key is written to Windows Credential Manager and
the tray app injects the provider-specific environment variable into bundled
CLI runs. The provider toggle is still written through `CodexBarCLI.exe config
enable`.

For web-session providers, use `Settings` > `Manual Web Session`, choose the
provider, and import a signed-in session from Edge, Chrome, or Brave. The app
reads Chromium cookie stores through Windows DPAPI, then saves the result
through `CodexBarCLI.exe config set-cookie --stdin`. If browser import cannot
read the session, paste a Cookie header and click `Save Cookie`.

The tray app defaults to the `enabled` provider scope. Use the compact provider
switcher at the top of Usage for quick scope changes. When a key is saved from
Settings, the scope moves to that provider so the popover shows the configured
provider instead of every known provider.

## Packaging

Tagged releases upload `CodexBar-Windows-<tag>-windows-x86_64.zip` directly to
the GitHub Release. Manual workflow artifacts are named
`codexbar-windows-x86_64`; extracting that artifact once gives the runnable app
folder with the app, CLI backend, Swift runtime DLLs, publish files, `VERSION`,
`README_RUN.txt`, `install.ps1`, and `uninstall.ps1`.
