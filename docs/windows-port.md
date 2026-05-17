# CodexBar-Windows Port Plan

CodexBar-Windows is a Windows-focused project for tracking AI coding-provider
usage and reset limits. It currently reuses a shared provider engine and CLI
foundation, with required upstream MIT notices kept in `NOTICE` and `LICENSE`.

## Goal

Bring a complete CodexBar-Windows experience to Windows users. The practical
path is:

1. Keep the shared provider and usage engine compiling on Windows.
2. Make `CodexBarCLI` build and run on Windows.
3. Build a Windows tray app that calls the CLI/core engine.
4. Fill in Windows-native replacements for macOS-only integrations.

## Current Status

This repository currently has the first Windows CLI build pass wired into CI and
release packaging. The compatibility layer is intentionally conservative:

- Windows PATH discovery understands `;` separators and common executable
  extensions such as `.exe`, `.cmd`, and `.bat`.
- POSIX process-group cleanup is isolated away from Windows builds.
- The POSIX local HTTP server returns an unsupported error on Windows.
- PTY-backed Codex and Claude CLI sessions return unsupported errors on
  Windows until a ConPTY implementation is added.
- SweetCookieKit is represented by an empty Windows module so browser-cookie
  providers can compile while native Windows cookie extraction is built.

## Implementation Plan

### Phase 1: Windows CLI

The first milestone is a working Windows build of `CodexBarCLI`.

Expected support:
- API-key providers.
- OAuth/device-flow providers that do not depend on macOS Keychain.
- Local JSON/config based providers.
- JSON output for a future tray UI.

Expected initial gaps:
- Browser cookie extraction through SweetCookieKit.
- AppKit/SwiftUI menu bar UI.
- macOS Keychain and Security CLI fallbacks.
- POSIX PTY-backed Codex/Claude session capture.

### Phase 2: Windows Tray App

After the CLI builds reliably, the Windows UI should be a separate tray shell.
The recommended shape is a Tauri 2 tray application with the Swift CLI as a
sidecar process.

Why this shape:
- The current macOS app is tightly coupled to AppKit menu bar APIs.
- Tauri gives Windows tray support, auto-start hooks, notifications, and a
small web UI surface.
- The CLI boundary keeps provider logic shared and testable.

### Phase 3: Windows Parity

Once the tray app exists, fill in native Windows replacements:
- Windows Credential Manager for stored tokens and API keys.
- Browser profile cookie extraction for Edge, Chrome, Firefox, and Chromium.
- Windows process/session handling for CLI-backed providers.
- Windows installer and update flow.

## Repo Policy

Windows changes should stay small and isolated until the tray app exists.
Cross-platform fixes should avoid breaking macOS/Linux behavior in the shared
provider engine.
