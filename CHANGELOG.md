# Changelog

## 0.1.0-alpha.3 - 2026-05-17

### Changed
- Replaced the broken WinForms popover implementation with a WPF popover.
- Added WPF-rendered provider cards with rounded panels, shadow, icon badges,
  status pills, progress bars, reset text, credits, cost, and error summaries.
- Kept WinForms for the Windows tray host and diagnostics while moving the
  primary user-facing surface to WPF.

## 0.1.0-alpha.2 - 2026-05-17

### Added
- Compact CodexBar-style tray popover as the primary Windows UI.
- Provider usage cards with usage bars, reset text, status, credits, cost, and
  error summaries.
- Tabbed Settings sections for General, Display, Providers, Advanced, and About.

### Changed
- Moved the grid dashboard, raw CLI output, and provider compatibility table
  into a secondary diagnostics window.
- Updated tray behavior so a normal tray click opens the popover.

## 0.1.0-alpha.1 - 2026-05-17

### Added
- Native Windows tray app with dashboard, settings, launch-at-sign-in support,
  config shortcuts, and CLI smoke-test mode.
- Structured dashboard rendering from `CodexBarCLI.exe usage --format json`.
- Provider compatibility view in the dashboard.
- Windows release packaging that bundles the tray app, CLI backend, Swift
  runtime DLLs, `README_RUN.txt`, install scripts, and `VERSION`.
- Windows-only CI and release workflows.
- User-profile install and uninstall scripts with Start Menu shortcut support.

### Changed
- Re-scoped the repository around CodexBar-Windows and removed non-Windows app
  packaging, appcast, website, and fork-maintenance artifacts.
- Updated project metadata and notices for the CodexBar-Windows port.
- Manual GitHub Actions artifacts now extract directly to runnable app files.
- Windows defaults now use `%APPDATA%\CodexBar-Windows\config.json`.

### Known Gaps
- Browser-cookie extraction is not implemented on Windows yet.
- PTY-backed interactive provider helpers need Windows ConPTY support.
- Windows Credential Manager storage and installer packaging are planned next.
