---
description: Build and Run Tyresoles Tauri App
---

Follow these steps to develop, build, and run the Tauri desktop application.

### 1. Prerequisites
Ensure you have the following installed:
- [Rust](https://www.rust-lang.org/tools/install)
- [Node.js](https://nodejs.org/)

### 2. Configuration Setup
Before running the app, ensure you have an `app-config.json` file in the `src-tauri` directory. You can start by copying the example:

```bash
cp src-tauri/app-config.json.example src-tauri/app-config.json
```

Edit `src-tauri/app-config.json` to set your desired URL and mode:
```json
{
  "url": "http://localhost:5173",
  "mode": "client"
}
```

### 3. Development Mode
To run the app in development mode with hot-reloading:

// turbo
```bash
npm run tauri:dev
```

### 4. Production Build
To create a production-ready installer/executable:

// turbo
```bash
npm run tauri:build
```

The output will be located in `src-tauri/target/release/bundle/`.

### 5. Running External Navision Configs
To test the Navision execution logic:
1. Place your `.config` files in `src-tauri/resources/nav-configs/`.
2. Build or run the app.
3. Use the `run_navision` command from the Svelte frontend as documented in the implementation plan.

### 6. Building with Auto-Update Signing

To build a release that supports auto-updates, set the signing key before building:

```powershell
$env:TAURI_SIGNING_PRIVATE_KEY = Get-Content "$HOME\.tauri\tyresoles.key" -Raw
```

// turbo
```bash
npm run tauri:build
```

This produces signed installers + `.sig` files in `src-tauri/target/release/bundle/nsis/`.

### 7. Publishing an Update to IIS

After building with signing, publish to the IIS update server:

// turbo
```powershell
.\scripts\publish-update.ps1 -Version "<new_version>" -TargetDir "C:\inetpub\tyresoles-updates"
```

To publish to a different domain:
```powershell
.\scripts\publish-update.ps1 -Version "<new_version>" -TargetDir "C:\inetpub\tyresoles-updates" -BaseUrl "https://app.tyresoles.in/updates"
```
