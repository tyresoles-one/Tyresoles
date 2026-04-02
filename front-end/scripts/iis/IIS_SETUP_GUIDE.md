# IIS Setup Guide — Tauri Auto-Update Server

Host the Tauri update files on Windows Server 2022 with IIS.

## Prerequisites

- Windows Server 2022 with **IIS** role installed
- **Static Content** feature enabled (Server Manager → Add Roles → Web Server → Static Content)
- **URL Rewrite** module installed (optional, for no-cache headers on `update.json`)
  - Download: https://www.iis.net/downloads/microsoft/url-rewrite

## Setup Steps

### 1. Create the Physical Directory

```powershell
New-Item -ItemType Directory -Path "C:\inetpub\tyresoles-updates" -Force
```

### 2. Create the IIS Site (or Virtual Directory)

**Option A — Dedicated site** (recommended for separate domain like `app.tyresoles.net`):

1. Open **IIS Manager** → Right-click **Sites** → **Add Website**
2. Site name: `tyresoles-updates`
3. Physical path: `C:\inetpub\tyresoles-updates`
4. Binding: `http` / `app.tyresoles.net` / Port `80`
5. Under the site, add a virtual directory or application named `updates` pointing to your physical path

**Option B — Virtual directory under existing site**:

1. Open **IIS Manager** → Expand your existing site (e.g., `Default Web Site`)
2. Right-click → **Add Virtual Directory**
3. Alias: `updates`
4. Physical path: `C:\inetpub\tyresoles-updates`

### 3. Copy web.config

Copy the provided `web.config` file into the update directory:

```powershell
Copy-Item "d:\Work Desk\Tyresoles\front-end\scripts\iis\web.config" "C:\inetpub\tyresoles-updates\web.config"
```

### 4. Verify MIME Types

In IIS Manager, select the site/virtual directory → double-click **MIME Types** → verify:
- `.json` → `application/json`
- `.exe` → `application/octet-stream`
- `.sig` → `application/octet-stream`

### 5. Test the Endpoint

After publishing your first update using `publish-update.ps1`:

```powershell
Invoke-RestMethod -Uri "http://app.tyresoles.net/updates/update.json"
```

You should see a JSON response with `version`, `platforms`, etc.

## Publishing Updates

After each Tauri build with signing:

```powershell
# Set signing key (only needed once per terminal session)
$env:TAURI_SIGNING_PRIVATE_KEY = Get-Content "$HOME\.tauri\tyresoles.key" -Raw

# Build
cd "d:\Work Desk\Tyresoles\front-end"
npm run tauri:build

# Publish
.\scripts\publish-update.ps1 -Version "0.2.0" -TargetDir "C:\inetpub\tyresoles-updates"
```

### Changing Domains Later

To switch from `http://app.tyresoles.net` to `https://app.tyresoles.in`:

1. Set up the new IIS site with the new domain binding
2. Copy the same `web.config` to the new directory
3. Publish with the new `-BaseUrl`:
   ```powershell
   .\scripts\publish-update.ps1 -Version "0.3.0" `
       -TargetDir "C:\inetpub\tyresoles-updates-new" `
       -BaseUrl "https://app.tyresoles.in/updates"
   ```
4. Update each client's `app-config.json` (in `%APPDATA%/com.tyresoles.app/`) to set the new `updateUrl`, **or** push a new build with the updated default.

## Directory Structure After Publishing

```
C:\inetpub\tyresoles-updates\
├── web.config
├── update.json
└── Tyresoles_0.2.0_x64-setup.exe
```

## Firewall

Ensure port 80 (HTTP) or 443 (HTTPS) is open for incoming connections if clients access over WAN.
