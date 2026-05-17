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

## Runtime Files

- App settings: `%APPDATA%\CodexBar-Windows\windows-app-settings.json`
- Provider config: `%USERPROFILE%\.codexbar\config.json`
- Bundled CLI: `CodexBarCLI.exe` beside `CodexBar-Windows.exe`

## Features

- system tray icon and context menu
- dashboard window
- structured usage dashboard backed by CLI JSON
- raw CLI output view for debugging
- usage refresh through the CLI backend
- provider selection
- CLI path override
- launch at sign-in toggle
- config file/folder shortcuts
- manual smoke-test mode: `CodexBar-Windows.exe --smoke-test`

## Packaging

The `Release` workflow uploads `codexbar-windows-x86_64`. Inside that artifact
is `CodexBar-Windows-<ref>-windows-x86_64.zip`, which contains the app, CLI
backend, Swift runtime DLLs, publish files, `VERSION`, and `README_RUN.txt`.
