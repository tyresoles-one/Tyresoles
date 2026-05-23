<#
.SYNOPSIS
    Full automation for Tyresoles production release (Windows: NSIS only — one product in Apps & features).

.DESCRIPTION
    1. Bumps version in tauri.conf.json + Cargo.toml
    2. Loads Minisign private key
    3. Builds the Tauri app (Windows bundle should be NSIS-only; do not ship MSI for the same app id)
    4. Writes update.json using the signed NSIS *-setup.exe + .sig (Tauri createUpdaterArtifacts)
    5. Copies release files to release-artifacts\v<Version>
    6. Optionally duplicates the setup as Tyresoles_Latest_x64-setup.exe for a fixed /downloads/ URL

    Use a single Windows installer type (NSIS) with auto-update. Shipping both MSI and NSIS causes duplicate
    "Tyresoles" entries on the same PC.

.PARAMETER Version
    New version (e.g. 0.4.0). If omitted, uses the current version from tauri.conf.json.

.PARAMETER Notes
    Release notes in update.json (e.g. "Major improvements").

.PARAMETER KeyPath
    Path to tyresoles.key. Default: $HOME\.tauri\tyresoles.key

.PARAMETER SkipBuild
    Skip npm run tauri build.

.PARAMETER StableDownloadName
    Second copy of the NSIS installer under release-artifacts\v<Version> (e.g. Tyresoles_Latest_x64-setup.exe).
    Set to empty string to skip. Default: Tyresoles_Latest_x64-setup.exe
#>
param(
    [string]$Version,
    [string]$KeyPath = "$HOME\.tauri\tyresoles.key",
    [string]$Notes = "Production Release",
    [switch]$SkipBuild,
    [string]$StableDownloadName = "Tyresoles_Latest_x64-setup.exe"
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
    $config.version = $Version
    $config | ConvertTo-Json -Depth 20 | Out-File $tauriConfPath -Encoding utf8

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
    Write-Host "Cleaning old bundle artifacts..." -ForegroundColor Yellow
    $bundleParent = Join-Path $projectRoot "src-tauri\target\release\bundle"
    if (Test-Path $bundleParent) { Remove-Item -Recurse -Force $bundleParent }

    Write-Host "Starting production build (Windows: NSIS installer only — see src-tauri\tauri.conf.json bundle.targets)..." -ForegroundColor Green
    Set-Location $projectRoot
    npm run tauri build
}

# 4. Prepare Distribution Folder
$distPath = Join-Path $projectRoot "release-artifacts\v$Version"
if (-not (Test-Path $distPath)) { New-Item -ItemType Directory -Path $distPath -Force | Out-Null }

Write-Host "Collecting artifacts to $distPath..." -ForegroundColor Cyan

$bundleDir = Join-Path $projectRoot "src-tauri\target\release\bundle"
$nsisDir = Join-Path $bundleDir "nsis"
$msiDir = Join-Path $bundleDir "msi"

$exeFile = Get-ChildItem -Path $nsisDir -Filter "*-setup.exe" -ErrorAction SilentlyContinue | Select-Object -First 1

if (-not $exeFile) {
    Write-Error "NSIS *-setup.exe not found under $nsisDir. Fix the build or run without -SkipBuild."
}

Copy-Item $exeFile.FullName -Destination $distPath
$sigFile = "$($exeFile.FullName).sig"
if (-not (Test-Path $sigFile)) {
    Write-Error "Signature not found: $sigFile (require createUpdaterArtifacts + TAURI_SIGNING_PRIVATE_KEY)."
}
Copy-Item $sigFile -Destination $distPath
$signature = Get-Content $sigFile -Raw

# update.json — URL must match where you host the same file the signature was generated for (the .exe).
$updateJson = @{
    version   = $Version
    notes     = $Notes
    pub_date  = (Get-Date).ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ")
    platforms = @{
        "windows-x86_64" = @{
            signature = $signature.Trim()
            url       = "http://app.tyresoles.net/updates/$($exeFile.Name)"
        }
    }
} | ConvertTo-Json -Depth 4
$updateJson | Out-File (Join-Path $distPath "update.json") -Encoding utf8
Write-Host "update.json generated (NSIS + Minisign)." -ForegroundColor Green

# Also copy common Tauri updater archives if present (some pipelines host .nsis.zip; optional upload)
Get-ChildItem -Path $nsisDir -Filter "*.nsis.zip" -ErrorAction SilentlyContinue | ForEach-Object {
    Copy-Item $_.FullName -Destination $distPath
    $zs = "$($_.FullName).sig"
    if (Test-Path $zs) { Copy-Item $zs -Destination $distPath }
    Write-Host "Included updater archive: $($_.Name)" -ForegroundColor Cyan
}

# Stable filename for app-config downloadUrl (optional)
if ($StableDownloadName) {
    Copy-Item $exeFile.FullName -Destination (Join-Path $distPath $StableDownloadName) -Force
    Write-Host "Stable download copy: $StableDownloadName" -ForegroundColor Cyan
}

$msiFile = Get-ChildItem -Path $msiDir -Filter "*.msi" -ErrorAction SilentlyContinue | Select-Object -First 1
if ($msiFile) {
    Write-Warning "MSI found: $($msiFile.Name). Do not upload this for Tyresoles if you also ship NSIS — use one Windows installer only. Clean target\release\bundle and rebuild if bundle.targets should exclude msi."
}

Write-Host "`n=== RELEASE PREPARED SUCCESSFULLY ===" -ForegroundColor Green
Write-Host "Upload everything under: $distPath"
Write-Host "  • update.json + $($exeFile.Name) + $($exeFile.Name).sig  ->  http://app.tyresoles.net/updates/  (auto-update)"
if ($StableDownloadName) {
    Write-Host "  • $StableDownloadName  ->  /downloads/  (manual full installer; same NSIS binary)"
}
