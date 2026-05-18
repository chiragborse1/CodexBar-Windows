[CmdletBinding()]
param(
    [string] $Configuration = "Release",
    [string] $RuntimeIdentifier = "win-x64",
    [string] $ReleaseTag = "dev",
    [string] $OutputRoot = (Join-Path $PSScriptRoot "..\artifacts"),
    [switch] $SkipBuild
)

$ErrorActionPreference = "Stop"

function Resolve-RepoRoot {
    return (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
}

function Get-SafeRef([string] $Tag) {
    if ([string]::IsNullOrWhiteSpace($Tag)) {
        return "dev"
    }

    return $Tag.Replace("/", "-")
}

function Get-VersionText([string] $SafeRef) {
    if ($SafeRef.StartsWith("v") -and $SafeRef.Length -gt 1 -and [char]::IsDigit($SafeRef[1])) {
        return $SafeRef.Substring(1)
    }

    return $SafeRef
}

function Copy-SwiftRuntimeDlls([string] $Destination) {
    $pathSeparator = [System.IO.Path]::PathSeparator
    $candidateDirs = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)

    foreach ($entry in ($env:PATH -split [regex]::Escape($pathSeparator))) {
        if ([string]::IsNullOrWhiteSpace($entry)) {
            continue
        }

        if ((Test-Path $entry) -and $entry -match "Swift") {
            [void] $candidateDirs.Add((Resolve-Path $entry).Path)
        }
    }

    $swiftCommand = Get-Command swift.exe -ErrorAction SilentlyContinue
    if ($null -ne $swiftCommand) {
        $swiftBin = Split-Path -Parent $swiftCommand.Source
        if (Test-Path $swiftBin) {
            [void] $candidateDirs.Add((Resolve-Path $swiftBin).Path)
        }
    }

    foreach ($dir in $candidateDirs) {
        Get-ChildItem -Path $dir -Filter "*.dll" -File -ErrorAction SilentlyContinue |
            ForEach-Object {
                Copy-Item $_.FullName (Join-Path $Destination $_.Name) -Force
            }
    }
}

function Invoke-IsolatedCliVersion([string] $PackageDir) {
    $oldPath = $env:PATH
    try {
        $env:PATH = "$PackageDir;$env:SystemRoot\System32;$env:SystemRoot"
        & (Join-Path $PackageDir "CodexBarCLI.exe") --version | Out-Null
        if ($LASTEXITCODE -ne 0) {
            exit $LASTEXITCODE
        }
    }
    finally {
        $env:PATH = $oldPath
    }
}

$repoRoot = Resolve-RepoRoot
$safeRef = Get-SafeRef $ReleaseTag
$version = Get-VersionText $safeRef
if ([System.IO.Path]::IsPathRooted($OutputRoot)) {
    $outputRootPath = [System.IO.Path]::GetFullPath($OutputRoot)
}
else {
    $outputRootPath = [System.IO.Path]::GetFullPath((Join-Path $repoRoot $OutputRoot))
}
$packageDir = Join-Path $outputRootPath "codexbar-windows-x86_64"
$projectPath = Join-Path $repoRoot "Windows\CodexBar.Windows\CodexBar.Windows.csproj"

if (-not $SkipBuild) {
    Push-Location $repoRoot
    try {
        swift build -c release --product CodexBarCLI
        dotnet publish $projectPath `
            -c $Configuration `
            -r $RuntimeIdentifier `
            --self-contained false `
            /p:PublishSingleFile=false
    }
    finally {
        Pop-Location
    }
}

Push-Location $repoRoot
try {
    $binDir = (swift build -c release --product CodexBarCLI --show-bin-path).Trim()
}
finally {
    Pop-Location
}

$publishDir = Join-Path $repoRoot "Windows\CodexBar.Windows\bin\$Configuration\net8.0-windows10.0.17763.0\$RuntimeIdentifier\publish"
if (-not (Test-Path $publishDir)) {
    throw "Windows app publish directory was not found: $publishDir"
}

Remove-Item $packageDir -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force -Path $packageDir | Out-Null
Copy-Item (Join-Path $publishDir "*") $packageDir -Recurse -Force

Get-ChildItem -Path $binDir -File |
    Where-Object { $_.Extension -in @(".exe", ".dll", ".pdb") } |
    ForEach-Object {
        Copy-Item $_.FullName (Join-Path $packageDir $_.Name) -Force
    }

Copy-SwiftRuntimeDlls $packageDir
Copy-Item (Join-Path $repoRoot "Windows\CodexBar.Windows\README_RUN.txt") (Join-Path $packageDir "README_RUN.txt") -Force
Copy-Item (Join-Path $repoRoot "Windows\install.ps1") (Join-Path $packageDir "install.ps1") -Force
Copy-Item (Join-Path $repoRoot "Windows\uninstall.ps1") (Join-Path $packageDir "uninstall.ps1") -Force
Set-Content -Path (Join-Path $packageDir "VERSION") -Value $version -NoNewline

& (Join-Path $packageDir "CodexBar-Windows.exe") --smoke-test
if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}

& (Join-Path $packageDir "CodexBar-Windows.exe") --pty-bridge-smoke-test
if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}

Invoke-IsolatedCliVersion $packageDir

$asset = "CodexBar-Windows-$safeRef-windows-x86_64.zip"
$assetPath = Join-Path $outputRootPath $asset
Remove-Item $assetPath -Force -ErrorAction SilentlyContinue
Compress-Archive -Path (Join-Path $packageDir "*") -DestinationPath $assetPath -Force

$hash = (Get-FileHash -Algorithm SHA256 $assetPath).Hash.ToLowerInvariant()
Set-Content -Path "$assetPath.sha256" -Value "$hash  $asset"

if ($env:GITHUB_OUTPUT) {
    "package_dir=$packageDir" | Out-File -FilePath $env:GITHUB_OUTPUT -Encoding utf8 -Append
    "asset=$asset" | Out-File -FilePath $env:GITHUB_OUTPUT -Encoding utf8 -Append
    "asset_path=$assetPath" | Out-File -FilePath $env:GITHUB_OUTPUT -Encoding utf8 -Append
    "sha256_path=$assetPath.sha256" | Out-File -FilePath $env:GITHUB_OUTPUT -Encoding utf8 -Append
}

Write-Host "Packaged $assetPath"
