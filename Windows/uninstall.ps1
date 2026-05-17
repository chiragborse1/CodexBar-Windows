[CmdletBinding()]
param(
    [string] $InstallDir = (Join-Path $env:LOCALAPPDATA "Programs\CodexBar-Windows"),
    [switch] $KeepSettings
)

$ErrorActionPreference = "Stop"

$installDirPath = [System.IO.Path]::GetFullPath($InstallDir)
$shortcutDir = Join-Path $env:APPDATA "Microsoft\Windows\Start Menu\Programs\CodexBar-Windows"
$settingsDir = Join-Path $env:APPDATA "CodexBar-Windows"
$runKeyPath = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Run"

Get-Process -Name "CodexBar-Windows" -ErrorAction SilentlyContinue | Stop-Process -Force

if (Test-Path $runKeyPath) {
    Remove-ItemProperty -Path $runKeyPath -Name "CodexBar-Windows" -ErrorAction SilentlyContinue
}

Remove-Item $shortcutDir -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item $installDirPath -Recurse -Force -ErrorAction SilentlyContinue

if (-not $KeepSettings) {
    Remove-Item $settingsDir -Recurse -Force -ErrorAction SilentlyContinue
}

Write-Host "Uninstalled CodexBar-Windows."
