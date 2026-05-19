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

function ConvertTo-FileSystemPath([string] $Value) {
    if ([string]::IsNullOrWhiteSpace($Value)) {
        return $Value
    }

    $fileSystemPrefix = "Microsoft.PowerShell.Core\FileSystem::"
    if ($Value.StartsWith($fileSystemPrefix, [StringComparison]::OrdinalIgnoreCase)) {
        return $Value.Substring($fileSystemPrefix.Length)
    }

    return $Value
}

function Resolve-RepoRoot {
    $resolved = Resolve-Path -LiteralPath (Join-Path $PSScriptRoot "..")
    return ConvertTo-FileSystemPath $resolved.ProviderPath
}

function Resolve-OptionalPath([string] $RepoRoot, [string] $Value) {
    if ([string]::IsNullOrWhiteSpace($Value)) {
        return $null
    }

    $fileSystemValue = ConvertTo-FileSystemPath $Value
    if ([System.IO.Path]::IsPathRooted($fileSystemValue)) {
        return [System.IO.Path]::GetFullPath($fileSystemValue)
    }

    return [System.IO.Path]::GetFullPath((Join-Path $RepoRoot $fileSystemValue))
}

function Test-CommandAvailable([string] $Name) {
    return $null -ne (Get-Command $Name -ErrorAction SilentlyContinue)
}

function Test-UncPath([string] $Path) {
    $fileSystemPath = ConvertTo-FileSystemPath $Path
    return $fileSystemPath.StartsWith("\\", [StringComparison]::Ordinal)
}

function Resolve-LocalStageRoot {
    $localAppData = [Environment]::GetFolderPath("LocalApplicationData")
    if ([string]::IsNullOrWhiteSpace($localAppData)) {
        $localAppData = $env:TEMP
    }

    return Join-Path $localAppData "CodexBar-Windows\ui-dev-source"
}

function Copy-UiProjectToLocalStage([string] $RepoRoot) {
    $sourceRoot = Join-Path $RepoRoot "Windows\CodexBar.Windows"
    if (-not (Test-Path $sourceRoot)) {
        throw "Missing Windows app project folder: $sourceRoot"
    }

    $stageRoot = Resolve-LocalStageRoot
    Write-Step "Staging WSL/UNC UI source to $stageRoot"

    if (Test-Path $stageRoot) {
        Remove-Item $stageRoot -Recurse -Force
    }

    [void] (New-Item -ItemType Directory -Force -Path $stageRoot)
    Get-ChildItem $sourceRoot -Force |
        Where-Object { $_.Name -ne "bin" -and $_.Name -ne "obj" } |
        ForEach-Object {
            Copy-Item -Path $_.FullName -Destination $stageRoot -Recurse -Force
        }

    return $stageRoot
}

function Resolve-UiProjectRoot(
    [string] $RepoRoot,
    [bool] $NoBuild
) {
    $sourceRoot = Join-Path $RepoRoot "Windows\CodexBar.Windows"
    if (-not (Test-UncPath $RepoRoot)) {
        return $sourceRoot
    }

    $stageRoot = Resolve-LocalStageRoot
    if ($NoBuild) {
        if (-not (Test-Path $stageRoot)) {
            throw "No staged UI build exists at $stageRoot. Run ui-dev.ps1 once without -NoBuild."
        }

        Write-Step "Using staged WSL/UNC UI source at $stageRoot"
        return $stageRoot
    }

    return Copy-UiProjectToLocalStage $RepoRoot
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

    $projectRoot = Resolve-UiProjectRoot $RepoRoot $NoBuild
    $project = Join-Path $projectRoot "CodexBar.Windows.csproj"
    if (-not (Test-Path $project)) {
        throw "Missing Windows app project: $project"
    }

    if (-not $NoBuild) {
        Write-Step "Building Windows UI ($Configuration)"
        Push-Location $projectRoot
        try {
            & dotnet build $project -c $Configuration
            if ($LASTEXITCODE -ne 0) {
                throw "dotnet build failed with exit code $LASTEXITCODE."
            }
        }
        finally {
            Pop-Location
        }
    }
    else {
        Write-Step "Skipping build"
    }

    $targetFramework = "net8.0-windows10.0.17763.0"
    $outputDir = Join-Path $projectRoot "bin\$Configuration\$targetFramework"
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
