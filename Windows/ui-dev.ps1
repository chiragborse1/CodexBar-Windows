[CmdletBinding()]
param(
    [ValidateSet("Debug", "Release")]
    [string] $Configuration = "Debug",

    [switch] $NoBuild,
    [switch] $UsePackage,
    [switch] $NoDemo,
    [string] $PackageDir
)

$ErrorActionPreference = "Stop"

function Write-Step([string] $Message) {
    Write-Host "==> $Message"
}

function Resolve-RepoRoot {
    return (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
}

function Resolve-OptionalPath([string] $RepoRoot, [string] $Value) {
    if ([string]::IsNullOrWhiteSpace($Value)) {
        return $null
    }

    if ([System.IO.Path]::IsPathRooted($Value)) {
        return [System.IO.Path]::GetFullPath($Value)
    }

    return [System.IO.Path]::GetFullPath((Join-Path $RepoRoot $Value))
}

function Test-CommandAvailable([string] $Name) {
    return $null -ne (Get-Command $Name -ErrorAction SilentlyContinue)
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

function Start-CheckedApp(
    [string] $AppPath,
    [string] $WorkingDirectory,
    [string[]] $Arguments
) {
    if (-not (Test-Path $AppPath)) {
        throw "Missing Windows app executable: $AppPath"
    }

    Write-Step "$([System.IO.Path]::GetFileName($AppPath)) $($Arguments -join ' ')"
    $process = Start-Process `
        -FilePath $AppPath `
        -ArgumentList $Arguments `
        -WorkingDirectory $WorkingDirectory `
        -PassThru

    for ($i = 0; $i -lt 20; $i++) {
        Start-Sleep -Milliseconds 250
        $running = Get-Process -Id $process.Id -ErrorAction SilentlyContinue
        if ($null -ne $running -and -not $running.HasExited) {
            Write-Host "OK: CodexBar-Windows is running as a visible dev window."
            return
        }
    }

    throw "CodexBar-Windows exited immediately. Run the app from PowerShell to see the Windows error dialog."
}

function Start-SourceUi(
    [string] $RepoRoot,
    [string] $Configuration,
    [bool] $NoBuild,
    [bool] $NoDemo
) {
    if (-not (Test-CommandAvailable "dotnet")) {
        return $false
    }

    $project = Join-Path $RepoRoot "Windows\CodexBar.Windows\CodexBar.Windows.csproj"
    if (-not (Test-Path $project)) {
        throw "Missing Windows app project: $project"
    }

    if (-not $NoBuild) {
        Write-Step "Building Windows UI ($Configuration)"
        & dotnet build $project -c $Configuration
        if ($LASTEXITCODE -ne 0) {
            throw "dotnet build failed with exit code $LASTEXITCODE."
        }
    }
    else {
        Write-Step "Skipping build"
    }

    $targetFramework = "net8.0-windows10.0.17763.0"
    $outputDir = Join-Path $RepoRoot "Windows\CodexBar.Windows\bin\$Configuration\$targetFramework"
    $appPath = Join-Path $outputDir "CodexBar-Windows.exe"
    $arguments = @("--window")
    if (-not $NoDemo) {
        $arguments += "--demo"
    }

    Start-CheckedApp $appPath $outputDir $arguments
    return $true
}

function Start-PackagedUi(
    [string] $RepoRoot,
    [string] $PackageDir,
    [bool] $NoDemo
) {
    $resolvedPackageDir = Resolve-OptionalPath $RepoRoot $PackageDir
    if ([string]::IsNullOrWhiteSpace($resolvedPackageDir)) {
        $resolvedPackageDir = Join-Path $RepoRoot "artifacts\codexbar-windows-x86_64"
    }

    if (-not (Test-Path $resolvedPackageDir)) {
        throw "Package folder does not exist: $resolvedPackageDir"
    }

    $appPath = Join-Path $resolvedPackageDir "CodexBar-Windows.exe"
    $arguments = @("--window")
    if (-not $NoDemo) {
        $arguments += "--demo"
    }

    Start-CheckedApp $appPath $resolvedPackageDir $arguments
}

$repoRoot = Resolve-RepoRoot
Stop-CodexBarProcesses

if ($UsePackage) {
    Start-PackagedUi $repoRoot $PackageDir $NoDemo
    exit 0
}

$startedFromSource = Start-SourceUi $repoRoot $Configuration $NoBuild $NoDemo
if ($startedFromSource) {
    exit 0
}

Write-Step "dotnet was not found; falling back to an existing package"
Start-PackagedUi $repoRoot $PackageDir $NoDemo
