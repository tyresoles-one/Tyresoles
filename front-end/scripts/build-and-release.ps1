<#
.SYNOPSIS
    Full automation for Tyresoles Production Release.

.DESCRIPTION
    1. Bumps version in tauri.conf.json
    2. Loads Minisign private key
    3. Builds the Tauri application
    4. Generates/Updates the update.json manifest
    5. Prepares a 'dist' folder with all release artifacts

.PARAMETER Version
    The new version number (e.g., 0.2.1). If omitted, it will use the current version from tauri.conf.json.

.PARAMETER KeyPath
    Path to your 'tyresoles.key'. Default: $HOME\.tauri\tyresoles.key
#>
param(
    [string]$Version,
    [string]$KeyPath = "$HOME\.tauri\tyresoles.key",
    [string]$Notes = "Production Release",
    [switch]$SkipBuild
)

$ErrorActionPreference = "Stop"
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Definition
$projectRoot = Split-Path -Parent $scriptDir
$tauriConfPath = Join-Path $projectRoot "src-tauri\tauri.conf.json"

# 1. Version Handling
$config = Get-Content $tauriConfPath | ConvertFrom-Json
$cargoPath = Join-Path $projectRoot "src-tauri\Cargo.toml"

if (-not $Version) {
    $Version = $config.version
    Write-Host "No version provided, using current: v$Version" -ForegroundColor Cyan
} else {
    Write-Host "Updating version to: v$Version" -ForegroundColor Yellow
    # Update tauri.conf.json
    $config.version = $Version
    $config | ConvertTo-Json -Depth 20 | Out-File $tauriConfPath -Encoding utf8
    
    # Update Cargo.toml (match version = "x.y.z")
    $cargoContent = Get-Content $cargoPath
    $newCargoContent = $cargoContent -replace '^version = ".*"', "version = `"$Version`""
    $newCargoContent | Out-File $cargoPath -Encoding utf8
}

# 2. Key Loading
if (-not (Test-Path $KeyPath)) {
    Write-Error "Signing key not found at $KeyPath. Please ensure your private key is available."
}
Write-Host "Loading signing key..." -ForegroundColor Cyan
$env:TAURI_SIGNING_PRIVATE_KEY = Get-Content $KeyPath -Raw

# 3. Build & Clean
if (-not $SkipBuild) {
    Write-Host "Cleaning old artifacts..." -ForegroundColor Yellow
    $bundleParent = Join-Path $projectRoot "src-tauri\target\release\bundle"
    if (Test-Path $bundleParent) { Remove-Item -Recurse -Force $bundleParent }

    Write-Host "Starting production build..." -ForegroundColor Green
    Set-Location $projectRoot
    npm run tauri build
}

# 4. Prepare Distribution Folder
$distPath = Join-Path $projectRoot "release-artifacts\v$Version"
if (-not (Test-Path $distPath)) { New-Item -ItemType Directory -Path $distPath -Force | Out-Null }

Write-Host "Collecting artifacts to $distPath..." -ForegroundColor Cyan

# Find NSIS (EXE) and MSI
$bundleDir = Join-Path $projectRoot "src-tauri\target\release\bundle"
$nsisDir = Join-Path $bundleDir "nsis"
$msiDir = Join-Path $bundleDir "msi"

$exeFile = Get-ChildItem -Path $nsisDir -Filter "*-setup.exe" | Select-Object -First 1
$msiFile = Get-ChildItem -Path $msiDir -Filter "*.msi" | Select-Object -First 1

if ($exeFile) {
    Copy-Item $exeFile.FullName -Destination $distPath
    $sigFile = "$($exeFile.FullName).sig"
    if (Test-Path $sigFile) {
        Copy-Item $sigFile -Destination $distPath
        $signature = Get-Content $sigFile -Raw
        
        # Build update.json
        $updateJson = @{
            version = $Version
            notes = $Notes
            pub_date = (Get-Date).ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ")
            platforms = @{
                "windows-x86_64" = @{
                    signature = $signature.Trim()
                    url = "http://app.tyresoles.net/updates/$($exeFile.Name)"
                }
            }
        } | ConvertTo-Json -Depth 4
        $updateJson | Out-File (Join-Path $distPath "update.json") -Encoding utf8
        Write-Host "Update manifest generated for NSIS (.exe)" -ForegroundColor Green
    }
}

if ($msiFile) {
    Copy-Item $msiFile.FullName -Destination $distPath
    Write-Host "MSI Installer collected." -ForegroundColor Green
}

Write-Host "`n=== RELEASE PREPARED SUCCESSFULLY ===" -ForegroundColor Green
Write-Host "Upload the contents of $distPath to your web server."
Write-Host "1. $distPath\update.json -> /updates/"
Write-Host "2. $($exeFile.Name) -> /updates/"
Write-Host "3. $($msiFile.Name) -> /downloads/ (Optional for web)"
