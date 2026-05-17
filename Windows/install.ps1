[CmdletBinding()]
param(
    [string] $InstallDir = (Join-Path $env:LOCALAPPDATA "Programs\CodexBar-Windows"),
    [switch] $NoShortcut,
    [switch] $DesktopShortcut,
    [switch] $Launch
)

$ErrorActionPreference = "Stop"

function New-AppShortcut([string] $ShortcutPath, [string] $TargetPath, [string] $WorkingDirectory, [string] $Description, [string] $Arguments = "") {
    $shortcutParent = Split-Path -Parent $ShortcutPath
    New-Item -ItemType Directory -Force -Path $shortcutParent | Out-Null
    $shell = New-Object -ComObject WScript.Shell
    $shortcut = $shell.CreateShortcut($ShortcutPath)
    $shortcut.TargetPath = $TargetPath
    $shortcut.Arguments = $Arguments
    $shortcut.WorkingDirectory = $WorkingDirectory
    $shortcut.Description = $Description
    $shortcut.IconLocation = "$TargetPath,0"
    $shortcut.Save()
}

function Register-UninstallEntry([string] $InstallDirPath, [string] $AppPath) {
    $keyPath = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\CodexBar-Windows"
    $uninstallCommand = "powershell.exe -ExecutionPolicy Bypass -File `"$InstallDirPath\uninstall.ps1`""
    New-Item -Path $keyPath -Force | Out-Null
    New-ItemProperty -Path $keyPath -Name "DisplayName" -Value "CodexBar-Windows" -PropertyType String -Force | Out-Null
    New-ItemProperty -Path $keyPath -Name "DisplayVersion" -Value (Get-VersionText $InstallDirPath) -PropertyType String -Force | Out-Null
    New-ItemProperty -Path $keyPath -Name "Publisher" -Value "Chirag Borse" -PropertyType String -Force | Out-Null
    New-ItemProperty -Path $keyPath -Name "InstallLocation" -Value $InstallDirPath -PropertyType String -Force | Out-Null
    New-ItemProperty -Path $keyPath -Name "DisplayIcon" -Value $AppPath -PropertyType String -Force | Out-Null
    New-ItemProperty -Path $keyPath -Name "UninstallString" -Value $uninstallCommand -PropertyType String -Force | Out-Null
    New-ItemProperty -Path $keyPath -Name "NoModify" -Value 1 -PropertyType DWord -Force | Out-Null
    New-ItemProperty -Path $keyPath -Name "NoRepair" -Value 1 -PropertyType DWord -Force | Out-Null
}

function Get-VersionText([string] $InstallDirPath) {
    $versionPath = Join-Path $InstallDirPath "VERSION"
    if (Test-Path $versionPath) {
        $value = (Get-Content $versionPath -Raw).Trim()
        if (-not [string]::IsNullOrWhiteSpace($value)) {
            return $value
        }
    }
    return "dev"
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
    $programsDir = Join-Path $env:APPDATA "Microsoft\Windows\Start Menu\Programs"
    $shortcutDir = Join-Path $programsDir "CodexBar-Windows"
    New-AppShortcut `
        (Join-Path $shortcutDir "CodexBar-Windows.lnk") `
        $installedApp `
        $installDirPath `
        "Launch CodexBar-Windows"
    New-AppShortcut `
        (Join-Path $shortcutDir "Uninstall CodexBar-Windows.lnk") `
        "powershell.exe" `
        $installDirPath `
        "Uninstall CodexBar-Windows" `
        "-ExecutionPolicy Bypass -File `"$installDirPath\uninstall.ps1`""
}

if ($DesktopShortcut) {
    $desktopDir = [Environment]::GetFolderPath("DesktopDirectory")
    New-AppShortcut `
        (Join-Path $desktopDir "CodexBar-Windows.lnk") `
        $installedApp `
        $installDirPath `
        "Launch CodexBar-Windows"
}

Register-UninstallEntry $installDirPath $installedApp

Write-Host "Installed CodexBar-Windows to $installDirPath"

if ($Launch) {
    Start-Process -FilePath $installedApp -WorkingDirectory $installDirPath
}
