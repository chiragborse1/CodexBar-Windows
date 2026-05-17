# Windows App

CodexBar-Windows includes a native Windows tray app in:

```text
Windows/CodexBar.Windows
```

The app uses WinForms for the Windows tray host and diagnostics, with a WPF
popover as the primary user-facing UI around `CodexBarCLI.exe`. It keeps the
provider engine in Swift while giving Windows users a normal tray entry point.
The popover follows the macOS CodexBar menu-card model; the larger table view is
kept as diagnostics.

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
`%LOCALAPPDATA%\Programs\CodexBar-Windows` and creates a Start Menu shortcut.
`uninstall.ps1` removes the shortcut, install folder, launch-at-sign-in entry,
and app settings unless `-KeepSettings` is passed.

## Runtime Files

- App settings: `%APPDATA%\CodexBar-Windows\windows-app-settings.json`
- Provider config: `%APPDATA%\CodexBar-Windows\config.json`
- Bundled CLI: `CodexBarCLI.exe` beside `CodexBar-Windows.exe`

## Features

- system tray icon and context menu
- WPF CodexBar-style usage popover
- provider cards with rounded panels, icon badges, usage bars, reset text,
  credits, cost, status, and errors
- diagnostics window with structured usage table, compatibility table, and raw
  CLI output
- usage refresh through the CLI backend
- provider selection with an `enabled` scope that mirrors config
- API-key setup controls for providers supported by the CLI config store
- CLI path override
- tabbed settings sections for General, Display, Providers, Advanced, and About
- launch at sign-in toggle
- start minimized toggle
- config file/folder shortcuts
- manual smoke-test mode: `CodexBar-Windows.exe --smoke-test`

## Provider Setup

Open `Settings > Providers`, choose a provider, paste its API key, and click
`Save API Key`. The setting is written to
`%APPDATA%\CodexBar-Windows\config.json` through `CodexBarCLI.exe config
set-api-key --stdin`, so the same config works from the app and CLI.

The tray app defaults to the `enabled` provider scope. When a key is saved from
Settings, the Display scope moves to that provider so the popover shows the
configured provider instead of every known provider.

## Packaging

Tagged releases upload `CodexBar-Windows-<tag>-windows-x86_64.zip` directly to
the GitHub Release. Manual workflow artifacts are named
`codexbar-windows-x86_64`; extracting that artifact once gives the runnable app
folder with the app, CLI backend, Swift runtime DLLs, publish files, `VERSION`,
`README_RUN.txt`, `install.ps1`, and `uninstall.ps1`.
