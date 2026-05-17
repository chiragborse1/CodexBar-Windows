---
summary: "CodexBar-Windows architecture overview: Windows shell, CLI, provider engine, and packaging."
read_when:
  - Reviewing architecture before feature work
  - Refactoring the Windows tray app, CLI, or shared provider engine
---

# Architecture Overview

## Modules

- `Windows/CodexBar.Windows`: WinForms tray app, dashboard, settings, startup integration, and release packaging script.
- `Sources/CodexBarCLI`: Swift CLI entry point used directly by users and by the Windows tray app.
- `Sources/CodexBarCore`: provider registry, fetchers, config models, cache stores, cost scanners, logging, and platform gates.
- `Sources/CodexBarMacros` and `Sources/CodexBarMacroSupport`: SwiftSyntax macro support for provider registration.
- `TestsLinux`: small cross-platform Swift tests that avoid desktop UI dependencies.

## Runtime Flow

1. `CodexBar-Windows.exe` starts in the notification area.
2. The tray app resolves `CodexBarCLI.exe` beside itself, from settings, or from `PATH`.
3. Dashboard refresh runs `CodexBarCLI.exe usage --provider <id> --format json --pretty`.
4. The Windows UI parses the JSON into summary rows and keeps the raw CLI output available for debugging.
5. Provider configuration is opened from `%APPDATA%\CodexBar-Windows\config.json`.
6. Windows app preferences are stored in `%APPDATA%\CodexBar-Windows\windows-app-settings.json`.

## Packaging

The `Windows/package-windows.ps1` script builds and packages:

- the WinForms publish output
- `CodexBarCLI.exe`
- Swift runtime DLLs found in the Windows Swift toolchain `PATH`
- `README_RUN.txt`
- `VERSION`

The script validates the package by running `CodexBar-Windows.exe --smoke-test`
and `CodexBarCLI.exe --version` from the packaged folder.

## Platform Boundaries

Windows-native work belongs in `Windows/CodexBar.Windows`. Shared provider and
CLI fixes belong in `Sources/CodexBarCore` and `Sources/CodexBarCLI`.

Browser-cookie extraction, keychain access, WebKit dashboards, and PTY-backed
interactive commands must stay behind platform checks until Windows-native
Credential Manager, browser-cookie, and ConPTY implementations are added.
