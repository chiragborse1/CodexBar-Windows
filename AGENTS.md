# Repository Guidelines

## Project Structure
- `Windows/CodexBar.Windows`: native WinForms tray app and Windows packaging script.
- `Sources/CodexBarCLI`: Swift command-line entry point used by the Windows app.
- `Sources/CodexBarCore`: shared provider, configuration, cache, and fetcher engine.
- `TestsLinux`: lightweight cross-platform Swift tests that avoid macOS UI dependencies.
- `docs`: Windows setup notes plus provider/configuration reference material.

## Build, Test, Package
- Build the CLI: `swift build -c release --product CodexBarCLI`.
- Build the Windows app: `dotnet publish Windows/CodexBar.Windows/CodexBar.Windows.csproj -c Release -r win-x64 --self-contained false`.
- Package the Windows release folder on Windows: `pwsh ./Windows/package-windows.ps1 -ReleaseTag dev`.
- Run Swift tests where Swift is installed: `swift test --parallel`.

## Development Notes
- Keep Windows UI changes inside `Windows/CodexBar.Windows`.
- Keep provider behavior in `Sources/CodexBarCore` and CLI behavior in `Sources/CodexBarCLI`.
- Preserve platform checks around browser, keychain, WebKit, and PTY behavior.
- Do not reintroduce non-Windows desktop packaging, appcasts, package-manager release scripts, or fork-sync workflows.

## Release Notes
- CI and release workflows build on `windows-latest`.
- Release artifacts are ZIP files named `CodexBar-Windows-<tag>-windows-x86_64.zip`.
- The package script copies the WinForms app, `CodexBarCLI.exe`, Swift runtime DLLs found in the Windows Swift toolchain PATH, `README_RUN.txt`, and `VERSION`.
