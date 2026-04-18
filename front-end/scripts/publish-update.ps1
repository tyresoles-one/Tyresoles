<#
.SYNOPSIS
    Publish a Tauri NSIS update to the IIS update directory.

.DESCRIPTION
    Copies the NSIS installer and its signature from the Tauri build output,
    generates a Tauri-compatible update.json, and deploys everything to the
    specified target directory (local path or UNC share).

.PARAMETER Version
    SemVer version string for this release (e.g. "0.2.0").

.PARAMETER TargetDir
    Destination directory where IIS serves update files.
    Example: "C:\inetpub\tyresoles-updates" or "\\server\tyresoles-updates"

.PARAMETER BaseUrl
    Public base URL where IIS serves the update files.
    Default: "http://app.tyresoles.net/updates"

.PARAMETER Notes
    Optional release notes (plain text).

.PARAMETER BuildDir
    Path to the Tauri NSIS build output directory.
    Default: auto-detected from src-tauri\target\release\bundle\nsis

.EXAMPLE
    .\publish-update.ps1 -Version "0.2.0" -TargetDir "C:\inetpub\tyresoles-updates"
    .\publish-update.ps1 -Version "0.3.0" -TargetDir "\\server\share" -BaseUrl "https://app.tyresoles.in/updates"
#>
param(
    [Parameter(Mandatory)][string]$Version,
    [Parameter(Mandatory)][string]$TargetDir,
    [string]$BaseUrl = "http://app.tyresoles.net/updates",
    [string]$Notes = "",
    [string]$BuildDir = ""
)

$ErrorActionPreference = "Stop"

# ── Resolve build directory ───────────────────────────────────────
$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Definition
$projectRoot = Split-Path -Parent $scriptRoot

if (-not $BuildDir) {
    # Switch from NSIS to WIX (MSI) to support correct overwriting/updates on Windows
    $BuildDir = Join-Path $projectRoot "src-tauri\target\release\bundle\msi"
    
    # Check if 'msi' exists, if not try 'wix' (older Tauri v1 name)
    if (-not (Test-Path $BuildDir)) {
         $BuildDir = Join-Path $projectRoot "src-tauri\target\release\bundle\wix"
    }
}

if (-not (Test-Path $BuildDir)) {
    Write-Error "Build directory (MSI) not found: $BuildDir`nRun 'npm run tauri:build' first."
    exit 1
}

# ── Find the MSI installer (.msi) and signature (.sig) ────────────
$msiFile = Get-ChildItem -Path $BuildDir -Filter "*.msi" | Where-Object { $_.Name -notlike "*_en-US.msi" -or $_.Name -like "*x64_en-US.msi" } | Select-Object -First 1
if (-not $msiFile) {
    Write-Error "No MSI installer (*.msi) found in $BuildDir"
    exit 1
}

# Tauri signs MSI files by adding .sig extension
$sigFile = Get-Item -Path "$($msiFile.FullName).sig" -ErrorAction SilentlyContinue
if (-not $sigFile) {
    # Check if .msi.sig (common pattern)
    $sigFile = Get-Item -Path "$($msiFile.FullName).sig" -ErrorAction SilentlyContinue
}

if (-not $sigFile) {
    Write-Error "Signature file not found for $($msiFile.Name).`nEnsure you set TAURI_SIGNING_PRIVATE_KEY and that the MSI is signed."
    exit 1
}

Write-Host "Installer : $($msiFile.Name)" -ForegroundColor Cyan
Write-Host "Signature : $($sigFile.Name)" -ForegroundColor Cyan
Write-Host "Version   : $Version" -ForegroundColor Cyan
Write-Host "Target    : $TargetDir" -ForegroundColor Cyan
Write-Host "Base URL  : $BaseUrl" -ForegroundColor Cyan
Write-Host ""

# ── Ensure target directory exists ────────────────────────────────
if (-not (Test-Path $TargetDir)) {
    New-Item -ItemType Directory -Path $TargetDir -Force | Out-Null
    Write-Host "Created target directory: $TargetDir" -ForegroundColor Yellow
}

# ── Copy installer ────────────────────────────────────────────────
$destMsi = Join-Path $TargetDir $msiFile.Name
Copy-Item -Path $msiFile.FullName -Destination $destMsi -Force
Write-Host "Copied installer -> $destMsi" -ForegroundColor Green

# ── Read signature ────────────────────────────────────────────────
$signature = Get-Content -Path $sigFile.FullName -Raw
$signature = $signature.Trim()

# ── Build URL ─────────────────────────────────────────────────────
$baseUrlTrimmed = $BaseUrl.TrimEnd("/")
$downloadUrl = "$baseUrlTrimmed/$($msiFile.Name)"

# ── Generate update.json ──────────────────────────────────────────
if (-not $Notes) {
    $Notes = "Release $Version"
}

$pubDate = (Get-Date).ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ")

$updateJson = @{
    version   = $Version
    notes     = $Notes
    pub_date  = $pubDate
    platforms = @{
        "windows-x86_64" = @{
            signature = $signature
            url       = $downloadUrl
        }
    }
} | ConvertTo-Json -Depth 4

$updateJsonPath = Join-Path $TargetDir "update.json"
$updateJson | Out-File -FilePath $updateJsonPath -Encoding utf8 -Force
Write-Host "Generated update.json -> $updateJsonPath" -ForegroundColor Green

# ── Summary ───────────────────────────────────────────────────────
Write-Host ""
Write-Host "=== Publish Complete ===" -ForegroundColor Green
Write-Host "Clients pointing to '$baseUrlTrimmed/update.json' will now see v$Version."
Write-Host ""
Write-Host "update.json contents:" -ForegroundColor DarkGray
Write-Host $updateJson -ForegroundColor DarkGray
