[CmdletBinding()]
param(
    [string] $InstallDir = (Join-Path $env:LOCALAPPDATA "Programs\CodexBar-Windows"),
    [switch] $KeepSettings
)

$ErrorActionPreference = "Stop"

$installDirPath = [System.IO.Path]::GetFullPath($InstallDir)
$shortcutDir = Join-Path $env:APPDATA "Microsoft\Windows\Start Menu\Programs\CodexBar-Windows"
$desktopShortcut = Join-Path ([Environment]::GetFolderPath("DesktopDirectory")) "CodexBar-Windows.lnk"
$settingsDir = Join-Path $env:APPDATA "CodexBar-Windows"
$runKeyPath = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Run"
$uninstallKeyPath = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\CodexBar-Windows"

Get-Process -Name "CodexBar-Windows" -ErrorAction SilentlyContinue | Stop-Process -Force

if (Test-Path $runKeyPath) {
    Remove-ItemProperty -Path $runKeyPath -Name "CodexBar-Windows" -ErrorAction SilentlyContinue
}

Remove-Item $shortcutDir -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item $desktopShortcut -Force -ErrorAction SilentlyContinue
Remove-Item $uninstallKeyPath -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item $installDirPath -Recurse -Force -ErrorAction SilentlyContinue

if (-not $KeepSettings) {
    Remove-Item $settingsDir -Recurse -Force -ErrorAction SilentlyContinue
}

Write-Host "Uninstalled CodexBar-Windows."
