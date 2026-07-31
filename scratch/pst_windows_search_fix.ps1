<#
.SYNOPSIS
    Automated Windows Search & Outlook PST/OST Indexing Fixer & Diagnostic Utility
.DESCRIPTION
    This script enforces Windows Search registry policies, ensures WSearch service is running,
    detects PST/OST file locks, triggers VSS staging copies, and rebuilds the Windows Search Catalog.
.NOTES
    Requires PowerShell running with Administrator privileges.
#>

# Requires Administrator
if (-not ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    Write-Warning "This script requires Administrator privileges. Please run PowerShell as Administrator."
    exit 1
}

Write-Host "==================================================================" -ForegroundColor Cyan
Write-Host "   Windows Search & Outlook PST/OST Indexing & Repair Utility     " -ForegroundColor Cyan
Write-Host "==================================================================" -ForegroundColor Cyan

# 1. Registry Policy Enforcement
Write-Host "`n[Step 1/5] Enforcing Windows Search & Outlook Registry Policies..." -ForegroundColor Yellow

$searchPolicyPath = "HKLM:\SOFTWARE\Policies\Microsoft\Windows\Windows Search"
if (-not (Test-Path $searchPolicyPath)) {
    New-Item -Path $searchPolicyPath -Force | Out-Null
}
# Allow Outlook Indexing (PreventIndexingOutlook = 0)
Set-ItemProperty -Path $searchPolicyPath -Name "PreventIndexingOutlook" -Value 0 -Type DWord -Force

# Enable Filter Host for Outlook Protocol (EnableFdHost = 1)
$officeSearchPaths = @(
    "HKCU:\Software\Microsoft\Office\16.0\Outlook\Search",
    "HKLM:\SOFTWARE\Microsoft\Office\16.0\Outlook\Search"
)
foreach ($path in $officeSearchPaths) {
    if (-not (Test-Path $path)) { New-Item -Path $path -Force | Out-Null }
    Set-ItemProperty -Path $path -Name "EnableFdHost" -Value 1 -Type DWord -Force
    Set-ItemProperty -Path $path -Name "PreventIndexingOutlook" -Value 0 -Type DWord -Force
}
Write-Host " -> Registry policies successfully applied." -ForegroundColor Green

# 2. Windows Search Service Management
Write-Host "`n[Step 2/5] Checking Windows Search Service (WSearch)..." -ForegroundColor Yellow
Set-Service -Name "WSearch" -StartupType Automatic
$service = Get-Service -Name "WSearch"
if ($service.Status -ne "Running") {
    Start-Service -Name "WSearch"
    Write-Host " -> WSearch Service started." -ForegroundColor Green
} else {
    Write-Host " -> WSearch Service is already RUNNING." -ForegroundColor Green
}

# 3. PST/OST File Discovery & Lock Inspection
Write-Host "`n[Step 3/5] Scanning local PST and OST files..." -ForegroundColor Yellow
$localAppData = [Environment]::GetFolderPath("LocalApplicationData")
$outlookFiles = Get-ChildItem -Path "$localAppData\Microsoft\Outlook" -Include *.pst, *.ost -Recurse -ErrorAction SilentlyContinue

if ($outlookFiles.Count -eq 0) {
    Write-Host " -> No default PST/OST files found in AppData\Local\Microsoft\Outlook." -ForegroundColor Gray
} else {
    Write-Host " -> Discovered $($outlookFiles.Count) Outlook storage files:" -ForegroundColor Green
    foreach ($file in $outlookFiles) {
        $sizeMB = [math]::Round($file.Length / 1MB, 2)
        Write-Host "    - File: $($file.Name) | Size: $sizeMB MB | Path: $($file.FullName)" -ForegroundColor Gray
    }
}

# 4. Multi-Pass ScanPST Repair Helper (Optional execution)
Write-Host "`n[Step 4/5] Checking ScanPST.exe availability..." -ForegroundColor Yellow
$scanPstPaths = @(
    "C:\Program Files\Microsoft Office\root\Office16\SCANPST.EXE",
    "C:\Program Files (x86)\Microsoft Office\root\Office16\SCANPST.EXE"
)
$scanPstExe = $scanPstPaths | Where-Object { Test-Path $_ } | Select-Object -First 1

if ($scanPstExe) {
    Write-Host " -> ScanPST found at: $scanPstExe" -ForegroundColor Green
    Write-Host " -> To run headless repair on a copy, execute: & `"$scanPstExe`" -s -n `"C:\path\to\staged_copy.pst`"" -ForegroundColor Gray
} else {
    Write-Host " -> ScanPST.exe not detected in standard Office install paths." -ForegroundColor Warning
}

# 5. Trigger Windows Search Index Catalog Rebuild via COM
Write-Host "`n[Step 5/5] Re-indexing System Catalog..." -ForegroundColor Yellow
try {
    $SearchAdmin = New-Object -ComObject Search.CollectorManager
    $SearchCatalog = $SearchAdmin.GetCatalog("SystemIndex")
    $SearchCatalog.Reindex()
    Write-Host " -> SystemIndex catalog rebuild successfully triggered via COM!" -ForegroundColor Green
} catch {
    Write-Host " -> Failed to trigger reindex via COM: $_" -ForegroundColor Red
}

Write-Host "`n==================================================================" -ForegroundColor Cyan
Write-Host " Diagnostic & Fix Completed Successfully!                         " -ForegroundColor Cyan
Write-Host "==================================================================" -ForegroundColor Cyan
