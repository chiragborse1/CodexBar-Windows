[CmdletBinding()]
param(
    [string] $InstallDir = (Join-Path $env:LOCALAPPDATA "Programs\CodexBar-Windows"),
    [switch] $NoShortcut,
    [switch] $Launch
)

$ErrorActionPreference = "Stop"

function New-StartMenuShortcut([string] $TargetPath, [string] $WorkingDirectory) {
    $programsDir = Join-Path $env:APPDATA "Microsoft\Windows\Start Menu\Programs"
    $shortcutDir = Join-Path $programsDir "CodexBar-Windows"
    New-Item -ItemType Directory -Force -Path $shortcutDir | Out-Null

    $shell = New-Object -ComObject WScript.Shell
    $shortcutPath = Join-Path $shortcutDir "CodexBar-Windows.lnk"
    $shortcut = $shell.CreateShortcut($shortcutPath)
    $shortcut.TargetPath = $TargetPath
    $shortcut.WorkingDirectory = $WorkingDirectory
    $shortcut.Description = "CodexBar-Windows"
    $shortcut.Save()
}

$sourceDir = $PSScriptRoot
$appPath = Join-Path $sourceDir "CodexBar-Windows.exe"
if (-not (Test-Path $appPath)) {
    throw "CodexBar-Windows.exe was not found beside install.ps1. Run this script from the extracted app folder."
}

$installDirPath = [System.IO.Path]::GetFullPath($InstallDir)
$sourceDirPath = [System.IO.Path]::GetFullPath($sourceDir)

if (-not [string]::Equals($sourceDirPath.TrimEnd([char]"\"), $installDirPath.TrimEnd([char]"\"), [StringComparison]::OrdinalIgnoreCase)) {
    Get-Process -Name "CodexBar-Windows" -ErrorAction SilentlyContinue | Stop-Process -Force
    New-Item -ItemType Directory -Force -Path $installDirPath | Out-Null
    Copy-Item (Join-Path $sourceDir "*") $installDirPath -Recurse -Force
}

$installedApp = Join-Path $installDirPath "CodexBar-Windows.exe"
if (-not $NoShortcut) {
    New-StartMenuShortcut $installedApp $installDirPath
}

Write-Host "Installed CodexBar-Windows to $installDirPath"

if ($Launch) {
    Start-Process -FilePath $installedApp -WorkingDirectory $installDirPath
}
