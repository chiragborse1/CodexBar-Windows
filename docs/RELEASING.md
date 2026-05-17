# Release Process

CodexBar-Windows releases are produced by the `Release` GitHub Actions
workflow.

## Windows Artifact

The Windows artifact is uploaded as `codexbar-windows-x86_64`.

Inside that workflow artifact is:

```text
CodexBar-Windows-<tag-or-ref>-windows-x86_64.zip
CodexBar-Windows-<tag-or-ref>-windows-x86_64.zip.sha256
```

The app zip contains:

- `CodexBar-Windows.exe`
- `CodexBarCLI.exe`
- Swift runtime DLLs copied from the Windows Swift toolchain
- .NET runtime-dependent publish files
- `README_RUN.txt`
- `VERSION`

## Manual Test Release

Run the workflow from GitHub Actions without a tag:

```bash
gh workflow run release-cli.yml --repo chiragborse1/CodexBar-Windows --ref main
gh run watch --repo chiragborse1/CodexBar-Windows --exit-status
```

Download the `codexbar-windows-x86_64` artifact and smoke-test on Windows:

```powershell
Expand-Archive .\codexbar-windows-x86_64.zip -DestinationPath .\artifact -Force
cd .\artifact
Expand-Archive .\CodexBar-Windows-main-windows-x86_64.zip -DestinationPath .\app -Force
cd .\app
.\CodexBar-Windows.exe --smoke-test
.\CodexBarCLI.exe --version
```

## Tagged Release

1. Update `CHANGELOG.md`.
2. Push a tag:

```bash
git tag v<version>
git push origin v<version>
```

3. Create or publish the GitHub release for the same tag.
4. The `Release` workflow uploads the Windows app zip and checksums.
5. Download the release asset on a Windows machine and verify:

```powershell
.\CodexBar-Windows.exe --smoke-test
.\CodexBarCLI.exe --help
.\CodexBarCLI.exe config validate --format json --pretty
```

## Notes

- The current package is runtime-dependent and expects the .NET Desktop Runtime
  8 to be available. A self-contained installer is planned.
- Swift runtime DLLs are bundled locally in the zip so end users do not need a
  Swift toolchain installed for normal use.
- MSIX/WiX installer packaging is not implemented yet.
- Non-Windows desktop signing, appcast, and package-manager release steps are
  not part of CodexBar-Windows releases.
