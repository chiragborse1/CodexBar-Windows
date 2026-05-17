# Windows App

CodexBar-Windows includes a native Windows tray app in:

```text
Windows/CodexBar.Windows
```

The app is a WinForms shell around `CodexBarCLI.exe`. It keeps the provider
engine in Swift while giving Windows users a normal tray entry point.

## Build

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
- dashboard window
- structured usage dashboard backed by CLI JSON
- provider compatibility tab
- raw CLI output view for debugging
- usage refresh through the CLI backend
- provider selection
- CLI path override
- launch at sign-in toggle
- start minimized toggle
- config file/folder shortcuts
- manual smoke-test mode: `CodexBar-Windows.exe --smoke-test`

## Packaging

Tagged releases upload `CodexBar-Windows-<tag>-windows-x86_64.zip` directly to
the GitHub Release. Manual workflow artifacts are named
`codexbar-windows-x86_64`; extracting that artifact once gives the runnable app
folder with the app, CLI backend, Swift runtime DLLs, publish files, `VERSION`,
`README_RUN.txt`, `install.ps1`, and `uninstall.ps1`.
