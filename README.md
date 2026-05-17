# CodexBar-Windows

> Track AI coding-provider limits from Windows.

[![CI](https://github.com/chiragborse1/CodexBar-Windows/actions/workflows/ci.yml/badge.svg)](https://github.com/chiragborse1/CodexBar-Windows/actions/workflows/ci.yml)
[![License: MIT](https://img.shields.io/badge/license-MIT-6e5aff?style=flat-square)](LICENSE)

CodexBar-Windows is a Windows-focused usage tracker for AI coding providers.
The first milestone is a Windows CLI that exposes provider usage and cost data
as text or JSON. The next milestone is a native Windows tray app that consumes
the same CLI/core engine.

## Status

Current build status:
- Windows CLI build is wired into GitHub Actions.
- `CodexBarCLI.exe --help` and `CodexBarCLI.exe --version` pass smoke tests on
  `windows-latest`.
- Windows release packaging is configured for a `.zip` artifact.

Current limitations:
- The Windows tray app is not implemented yet.
- Browser cookie extraction is stubbed on Windows.
- PTY-backed Codex and Claude CLI sessions are stubbed on Windows.
- The localhost HTTP `serve` command is stubbed on Windows.

See [docs/windows-port.md](docs/windows-port.md) for the current implementation
plan and compatibility notes.

## Install

Windows release binaries will be published from this repository once the first
Windows release is tagged:

<https://github.com/chiragborse1/CodexBar-Windows/releases>

Until then, build the CLI from source on Windows:

```powershell
swift build -c release --product CodexBarCLI
$binDir = swift build -c release --product CodexBarCLI --show-bin-path
& "$binDir\CodexBarCLI.exe" --help
```

## CLI Examples

```powershell
CodexBarCLI.exe --version
CodexBarCLI.exe usage --format json --provider all --pretty
CodexBarCLI.exe config providers
CodexBarCLI.exe config set-api-key --provider elevenlabs --stdin
CodexBarCLI.exe cost --provider claude --format json --pretty
```

Provider toggles and API keys are stored in the local CodexBar-Windows config
file. The default config location is currently inherited from the shared CLI
engine and will be migrated to a Windows-native app data location in a later
phase.

## Roadmap

1. Keep the Windows CLI green in CI.
2. Add a Windows tray shell.
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
swift test
```

The package also contains macOS/Linux code inherited by the shared provider
engine. Windows work should stay isolated behind platform checks so the shared
code remains easy to maintain.

## Attribution

CodexBar-Windows includes MIT-licensed code derived from the original CodexBar
project. Required notices are kept in [NOTICE](NOTICE) and [LICENSE](LICENSE).

## License

MIT. See [LICENSE](LICENSE).
