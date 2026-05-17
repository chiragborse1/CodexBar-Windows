SHELL := /bin/bash

.PHONY: build-cli test package-windows

build-cli:
	swift build -c release --product CodexBarCLI

test:
	swift test --parallel

package-windows:
	pwsh ./Windows/package-windows.ps1 -ReleaseTag dev
