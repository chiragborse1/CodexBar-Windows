# CodexBar-Windows

> Track AI coding-provider limits from Windows.

[![CI](https://github.com/chiragborse1/CodexBar-Windows/actions/workflows/ci.yml/badge.svg)](https://github.com/chiragborse1/CodexBar-Windows/actions/workflows/ci.yml)
[![License: MIT](https://img.shields.io/badge/license-MIT-6e5aff?style=flat-square)](LICENSE)

CodexBar-Windows is a Windows-focused usage tracker for AI coding providers.
It ships a native Windows tray app that wraps the shared CLI/provider engine,
plus a standalone CLI for scripts and terminal workflows.

## Status

Current build status:
- Native Windows tray app is wired into GitHub Actions and release packaging.
- Windows CLI build is wired into GitHub Actions.
- Release packaging verifies `CodexBar-Windows.exe --smoke-test` and
  `CodexBarCLI.exe --version` from the packaged folder.
- Windows release packaging is configured for an app `.zip` artifact containing
  `CodexBar-Windows.exe`, `CodexBarCLI.exe`, bundled Swift runtime DLLs,
  `README_RUN.txt`, and `VERSION`.

Current limitations:
- Browser cookie extraction is stubbed on Windows.
- PTY-backed Codex and Claude CLI sessions are stubbed on Windows.
- The localhost HTTP `serve` command is stubbed on Windows.

See [docs/windows-port.md](docs/windows-port.md) for the current implementation
plan and compatibility notes.

## Install

Windows release binaries will be published from this repository once a release
is tagged:

<https://github.com/chiragborse1/CodexBar-Windows/releases>

Manual workflow artifacts can be downloaded from the `Release` or `CI` GitHub
Actions workflow. The Windows artifact is named `codexbar-windows-x86_64`.

To build from source on Windows:

```powershell
swift build -c release --product CodexBarCLI
dotnet publish Windows\CodexBar.Windows\CodexBar.Windows.csproj -c Release -r win-x64 --self-contained false
pwsh .\Windows\package-windows.ps1 -ReleaseTag dev
```

## CLI Examples

```powershell
CodexBar-Windows.exe
CodexBarCLI.exe --version
CodexBarCLI.exe usage --format json --provider all --pretty
CodexBarCLI.exe config providers
CodexBarCLI.exe config set-api-key --provider elevenlabs --stdin
CodexBarCLI.exe cost --provider claude --format json --pretty
```

Provider toggles and API keys are stored in the local CodexBar-Windows config
file at `%APPDATA%\CodexBar-Windows\config.json`.

## Roadmap

1. Keep the Windows tray app and CLI green in CI.
2. Improve dashboard rendering for structured provider JSON.
3. Add Windows Credential Manager storage.
4. Add browser profile cookie extraction for Edge, Chrome, Firefox, and
   Chromium.
5. Replace PTY stubs with Windows ConPTY support where provider CLIs require an
   interactive terminal.
6. Add a Windows installer and update channel.

## Development

Useful commands:

```powershell
swift build -c release --product CodexBarCLI
dotnet publish Windows\CodexBar.Windows\CodexBar.Windows.csproj -c Release -r win-x64 --self-contained false
pwsh .\Windows\package-windows.ps1 -ReleaseTag dev
swift test
```

The shared Swift engine still contains platform gates for providers that depend
on browser cookies, WebKit, keychain, or PTY behavior. Windows work should stay
isolated behind those gates until native Windows implementations exist.

## Attribution

CodexBar-Windows includes MIT-licensed code derived from the original CodexBar
project. Required notices are kept in [NOTICE](NOTICE) and [LICENSE](LICENSE).

## License

MIT. See [LICENSE](LICENSE).
