# FortiClient Remote Access Module — Recovery Command Reference
# Run all commands in an ELEVATED (Admin) PowerShell or CMD

---

## STEP 1 — Kill any hung installer/process locks

```powershell
# Kill all hung msiexec processes
Get-Process -Name msiexec -ErrorAction SilentlyContinue | Stop-Process -Force

# Stop FortiClient services if running
Stop-Service -Name "FortiClient" -Force -ErrorAction SilentlyContinue
Stop-Service -Name "fgsvc" -Force -ErrorAction SilentlyContinue
Stop-Service -Name "fortishield" -Force -ErrorAction SilentlyContinue
```

---

## STEP 2 — Remove stale service registrations (ghost driver keys)

```cmd
reg delete "HKLM\SYSTEM\CurrentControlSet\Services\fortishield" /f
reg delete "HKLM\SYSTEM\ControlSet001\Services\fortishield" /f
reg delete "HKLM\SYSTEM\ControlSet002\Services\fortishield" /f
reg delete "HKLM\SYSTEM\CurrentControlSet\Services\fmon" /f
reg delete "HKLM\SYSTEM\CurrentControlSet\Services\FortiSSL" /f
reg delete "HKLM\SYSTEM\CurrentControlSet\Services\FortiSPN" /f
reg delete "HKLM\SYSTEM\CurrentControlSet\Services\FortiWF" /f
reg delete "HKLM\SYSTEM\CurrentControlSet\Services\fortisslvpn" /f
```

---

## STEP 3 — Remove ghost MSI product GUID (the "repair/remove" screen fix)

> Replace `<PACKED_GUID>` with your product code — for this machine it is:
> **`F61547A2346E87C439EE87F6607F3804`**

```cmd
reg delete "HKLM\SOFTWARE\Classes\Installer\Products\F61547A2346E87C439EE87F6607F3804" /f
reg delete "HKLM\SOFTWARE\Classes\Installer\Features\F61547A2346E87C439EE87F6607F3804" /f
reg delete "HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Installer\UserData\S-1-5-18\Products\F61547A2346E87C439EE87F6607F3804" /f
reg delete "HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\F61547A2346E87C439EE87F6607F3804" /f
reg delete "HKLM\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\F61547A2346E87C439EE87F6607F3804" /f
```

> **How to find your GUID if it's different:**
```powershell
Get-ChildItem "HKLM:\SOFTWARE\Classes\Installer\Products" |
  Where-Object { (Get-ItemProperty $_.PSPath -ErrorAction SilentlyContinue).ProductName -like "*forti*" } |
  Select-Object PSChildName, @{N="Name";E={(Get-ItemProperty $_.PSPath).ProductName}}
```

---

## STEP 4 — Remove leftover Fortinet registry hive

```cmd
reg delete "HKLM\SOFTWARE\Fortinet" /f
reg delete "HKLM\SOFTWARE\WOW6432Node\Fortinet" /f
```

---

## STEP 5 — Enable / Re-register the Remote Access (VPN) module after install

After a fresh install, if the Remote Access module is disabled or missing:

```powershell
# Start the VPN/Remote Access services
Start-Service -Name "FortiClient" -ErrorAction SilentlyContinue
Start-Service -Name "fgsvc" -ErrorAction SilentlyContinue      # FortiGate Service
Start-Service -Name "FortiSSL" -ErrorAction SilentlyContinue   # SSL VPN tunnel

# Set them to auto-start on boot
Set-Service -Name "FortiClient" -StartupType Automatic -ErrorAction SilentlyContinue
Set-Service -Name "fgsvc"       -StartupType Automatic -ErrorAction SilentlyContinue
```

```cmd
rem Re-register via sc if Start-Service fails
sc config FortiClient start= auto
sc config fgsvc start= auto
sc start FortiClient
sc start fgsvc
```

---

## STEP 6 — Restart Windows Installer + reboot (always last step)

```powershell
# Restart the MSI service to clear locks
Stop-Service msiserver -Force
Start-Sleep 2
Start-Service msiserver

# Then reboot
Restart-Computer -Force
```

---

## QUICK ONE-LINER NUKE (for next time — paste into Admin PowerShell)

```powershell
$guid = "F61547A2346E87C439EE87F6607F3804"
$keys = @(
  "HKLM\SOFTWARE\Classes\Installer\Products\$guid",
  "HKLM\SOFTWARE\Classes\Installer\Features\$guid",
  "HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Installer\UserData\S-1-5-18\Products\$guid",
  "HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\$guid",
  "HKLM\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall\$guid",
  "HKLM\SYSTEM\CurrentControlSet\Services\fortishield",
  "HKLM\SYSTEM\ControlSet001\Services\fortishield",
  "HKLM\SYSTEM\CurrentControlSet\Services\FortiSSL",
  "HKLM\SYSTEM\CurrentControlSet\Services\FortiSPN",
  "HKLM\SYSTEM\CurrentControlSet\Services\fmon",
  "HKLM\SOFTWARE\Fortinet",
  "HKLM\SOFTWARE\WOW6432Node\Fortinet"
)
Get-Process msiexec -ErrorAction SilentlyContinue | Stop-Process -Force
foreach ($k in $keys) { reg delete $k /f 2>$null }
Stop-Service msiserver -Force; Start-Sleep 2; Start-Service msiserver
Write-Host "DONE - Run FortiClient installer as Admin" -ForegroundColor Green
```

---

## Diagnostic — Check what's blocking the installer

```powershell
# Run this BEFORE installing to spot problems early
Get-Process | Where-Object { $_.Name -like "*forti*" -or $_.Name -like "*msiexec*" } | Select Name, Id
Get-Service | Where-Object { $_.Name -like "*forti*" } | Select Name, Status, StartType
@("HKLM:\SOFTWARE\Classes\Installer\Products\F61547A2346E87C439EE87F6607F3804",
  "HKLM:\SYSTEM\CurrentControlSet\Services\fortishield",
  "HKLM:\SOFTWARE\Fortinet") | ForEach-Object { "$_ : $(if(Test-Path $_){'EXISTS'}else{'clean'})" }
```
