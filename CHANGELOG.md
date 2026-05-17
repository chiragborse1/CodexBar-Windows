# Changelog

## 0.1.0 - Unreleased

### Added
- Native Windows tray app with dashboard, settings, launch-at-sign-in support,
  config shortcuts, and CLI smoke-test mode.
- Structured dashboard rendering from `CodexBarCLI.exe usage --format json`.
- Windows release packaging that bundles the tray app, CLI backend, Swift
  runtime DLLs, `README_RUN.txt`, and `VERSION`.
- Windows-only CI and release workflows.

### Changed
- Re-scoped the repository around CodexBar-Windows and removed non-Windows app
  packaging, appcast, website, and fork-maintenance artifacts.
- Updated project metadata and notices for the CodexBar-Windows port.

### Known Gaps
- Browser-cookie extraction is not implemented on Windows yet.
- PTY-backed interactive provider helpers need Windows ConPTY support.
- Windows Credential Manager storage and installer packaging are planned next.
