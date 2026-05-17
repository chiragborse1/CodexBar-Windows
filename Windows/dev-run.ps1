[CmdletBinding()]
param(
    [ValidateSet("Debug", "Release")]
    [string] $Configuration = "Release",

    [string] $RuntimeIdentifier = "win-x64",
    [string] $ReleaseTag = "dev",
    [string] $OutputRoot,

    [switch] $NoBuild,
    [switch] $NoLaunch,
    [switch] $Test
)

$ErrorActionPreference = "Stop"

function Write-Step([string] $Message) {
    Write-Host "==> $Message"
}

function Resolve-RepoRoot {
    return (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
}

function Resolve-OutputRoot([string] $RepoRoot, [string] $Value) {
    if ([string]::IsNullOrWhiteSpace($Value)) {
        $Value = Join-Path $RepoRoot "artifacts"
    }

    if ([System.IO.Path]::IsPathRooted($Value)) {
        return [System.IO.Path]::GetFullPath($Value)
    }

    return [System.IO.Path]::GetFullPath((Join-Path $RepoRoot $Value))
}

function Stop-CodexBarProcesses {
    $processes = @(Get-Process -Name "CodexBar-Windows" -ErrorAction SilentlyContinue)
    if ($processes.Count -eq 0) {
        return
    }

    Write-Step "Stopping running CodexBar-Windows instances"
    foreach ($process in $processes) {
        if ($process.MainWindowHandle -ne [IntPtr]::Zero) {
            [void] $process.CloseMainWindow()
        }
    }

    for ($i = 0; $i -lt 20; $i++) {
        $remaining = @(Get-Process -Name "CodexBar-Windows" -ErrorAction SilentlyContinue)
        if ($remaining.Count -eq 0) {
            return
        }

        Start-Sleep -Milliseconds 250
    }

    Get-Process -Name "CodexBar-Windows" -ErrorAction SilentlyContinue |
        Stop-Process -Force -ErrorAction SilentlyContinue
}

function Invoke-CheckedNative(
    [string] $FilePath,
    [string[]] $Arguments,
    [string] $WorkingDirectory
) {
    $command = [System.IO.Path]::GetFileName($FilePath)
    if ($Arguments.Count -gt 0) {
        $command = "$command $($Arguments -join ' ')"
    }

    Write-Step $command
    $process = Start-Process `
        -FilePath $FilePath `
        -ArgumentList $Arguments `
        -WorkingDirectory $WorkingDirectory `
        -Wait `
        -PassThru
    $exitCode = $process.ExitCode

    if ($exitCode -ne 0) {
        throw "$command failed with exit code $exitCode."
    }
}

function Test-DevPackage([string] $PackageDir) {
    $appPath = Join-Path $PackageDir "CodexBar-Windows.exe"
    $cliPath = Join-Path $PackageDir "CodexBarCLI.exe"

    if (-not (Test-Path $appPath)) {
        throw "Missing Windows app executable: $appPath"
    }
    if (-not (Test-Path $cliPath)) {
        throw "Missing CLI executable: $cliPath"
    }

    Invoke-CheckedNative $appPath @("--smoke-test") $PackageDir
    Invoke-CheckedNative $cliPath @("--version") $PackageDir
}

function Start-DevApp([string] $PackageDir) {
    $appPath = Join-Path $PackageDir "CodexBar-Windows.exe"
    if (-not (Test-Path $appPath)) {
        throw "Missing Windows app executable: $appPath"
    }

    Write-Step "Launching $appPath"
    $process = Start-Process -FilePath $appPath -WorkingDirectory $PackageDir -PassThru

    for ($i = 0; $i -lt 20; $i++) {
        Start-Sleep -Milliseconds 250
        $running = Get-Process -Id $process.Id -ErrorAction SilentlyContinue
        if ($null -ne $running -and -not $running.HasExited) {
            Write-Host "OK: CodexBar-Windows is running from $PackageDir"
            return
        }
    }

    throw "CodexBar-Windows exited immediately. Run with -NoLaunch -Test or check Windows Event Viewer for crash details."
}

$repoRoot = Resolve-RepoRoot
$outputRootPath = Resolve-OutputRoot $repoRoot $OutputRoot
$packageDir = Join-Path $outputRootPath "codexbar-windows-x86_64"
$packageScript = Join-Path $PSScriptRoot "package-windows.ps1"

if (-not (Test-Path $packageScript)) {
    throw "Missing package script: $packageScript"
}

Stop-CodexBarProcesses

if ($NoBuild) {
    Write-Step "Skipping build; using $packageDir"
    if (-not (Test-Path $packageDir)) {
        throw "Package folder does not exist: $packageDir"
    }
}
else {
    Write-Step "Building local Windows package"
    & $packageScript `
        -Configuration $Configuration `
        -RuntimeIdentifier $RuntimeIdentifier `
        -ReleaseTag $ReleaseTag `
        -OutputRoot $outputRootPath

    if ($LASTEXITCODE -ne 0) {
        throw "package-windows.ps1 failed with exit code $LASTEXITCODE."
    }
}

if ($Test) {
    Test-DevPackage $packageDir
}

if ($NoLaunch) {
    Write-Step "Launch skipped. Package is ready at $packageDir"
    exit 0
}

Start-DevApp $packageDir
