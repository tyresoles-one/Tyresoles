# 🚀 Tyresoles Production Release Guide

This guide ensures a consistent, high-quality production release for both the Tyresoles Web distribution and the Tauri Desktop client. Follow these steps to prepare your build and deploy updates to users.

---

## 🛠️ Prerequisites

Before you begin, ensure you have:
1.  **Minisign Private Key**: Located at `$HOME\.tauri\tyresoles.key`.
2.  **Node.js & Rust**: Installed and updated on your build machine.
3.  **IIS URL Rewrite**: Installed on the Windows Server to handle SvelteKit routing.
4.  **Admin Privileges**: Required to update files in the IIS website root.

---

## 🏗️ Method 1: Automated Release (Recommended)

We have provided a PowerShell script to automate the entire version bumping, building, and manifest generation process.

### 1. Run the Release Script
Open PowerShell in the `front-end` directory and run:

```powershell
# Bumps version to 0.2.1, builds the app, and signs it
.\scripts\build-and-release.ps1 -Version "0.2.1" -Notes "Fixed ERP launching and added settings UI."
```

### 2. Collect Artifacts
The script will create a new folder:
`front-end\release-artifacts\v0.2.1\`

Inside you will find:
-   `Tyresoles_0.2.1_x64-setup.exe` (The updater installer)
-   `Tyresoles_0.2.1_x64_en-US.msi` (The first-time installer)
-   `update.json` (The manifest file for the auto-updater)

### 3. Upload to Server
Move these files to your Windows Server:
1.  **EXE & update.json**: Upload to `C:\inetpub\wwwroot\updates\`
2.  **MSI**: Upload to `C:\inetpub\wwwroot\downloads\`

---

## 🔨 Method 2: Manual Release

If you prefer to perform the steps manually, follow this sequence:

### 1. Update Version
Edit `front-end/src-tauri/tauri.conf.json`:
```json
"version": "0.2.1" 
```

### 2. Build & Sign
Set the environment variable for your private key and build:
```powershell
$env:TAURI_SIGNING_PRIVATE_KEY = Get-Content "$HOME\.tauri\tyresoles.key" -Raw
npm run tauri build
```

### 3. Generate `update.json`
Locate the `.sig` file in `src-tauri\target\release\bundle\nsis\Tyresoles_0.2.1_x64-setup.exe.sig`.
Open it in Notepad, copy the content, and paste it into a JSON formatted like this:

```json
{
  "version": "0.2.1",
  "notes": "Release Notes Here",
  "pub_date": "2026-03-17T12:00:00Z",
  "platforms": {
    "windows-x86_64": {
      "signature": "PASTE_ENTIRE_SIG_CONTENT_HERE",
      "url": "http://app.tyresoles.net/updates/Tyresoles_0.2.1_x64-setup.exe"
    }
  }
}
```

---

## 🌐 Server Configuration (IIS)

To ensure the updater works correctly, your IIS server must be configured with correct MIME types and Rewrite rules.

### 1. MIME Types
Open **IIS Manager** → Select your site → **MIME Types**. Add:
-   `.msi` → `application/octet-stream`
-   `.exe` (or `.msi.zip`) → `application/octet-stream`
-   `.json` → `application/json`

### 2. URL Rewrite (web.config)
Ensure a `web.config` file exists in the root of your web site:

```xml
<?xml version="1.0" encoding="UTF-8"?>
<configuration>
  <system.webServer>
    <rewrite>
      <rules>
        <rule name="SvelteKit SPA" stopProcessing="true">
          <match url=".*" />
          <conditions logicalGrouping="MatchAll">
            <add input="{REQUEST_FILENAME}" matchType="IsFile" negate="true" />
            <add input="{REQUEST_FILENAME}" matchType="IsDirectory" negate="true" />
          </conditions>
          <action type="Rewrite" url="/" />
        </rule>
      </rules>
    </rewrite>
    <staticContent>
      <clientCache cacheControlMode="NoCache" />
    </staticContent>
  </system.webServer>
</configuration>
```

---

## 🏁 Verification Checklist

- [ ] Check `https://app.tyresoles.net/updates/update.json` in your browser. It should show the new version.
- [ ] Launch the v0.2.0 app. It should show the "Update Available" banner within 5 seconds.
- [ ] Click **Update Now**. The app should restart into v0.2.1.
- [ ] Go to **Settings** in the browser. The "Download for Windows" button should point to the new MSI.

> [!CAUTION]
> **Signature Mismatch**: If you see "Signature verification failed" in the logs, it means the `signature` in your `update.json` does not match the file you uploaded OR the wrong public key is in `tauri.conf.json`.
