# Quick Fix for SSL Certificate Error

## The Problem

When running `npm run codegen`, you're getting:
```
self-signed certificate
unable to verify the first certificate
```

This happens because your local development server uses HTTPS with a self-signed certificate.

## ✅ Solution: Use the Development Script

Run this command:

```bash
npm run codegen:dev
```

This uses a Node.js script that properly sets the environment variable to bypass SSL verification **only during codegen** (not in your app).

## Alternative: PowerShell Command

If the script doesn't work, you can run this directly in PowerShell:

```powershell
$env:NODE_TLS_REJECT_UNAUTHORIZED=0; npm run codegen
```

Or use the PowerShell-specific script:

```bash
npm run codegen:dev:ps
```

## What Changed

1. ✅ Created `scripts/codegen-dev.mjs` - Node.js script that sets the env var properly
2. ✅ Updated `codegen.ts` to default to development endpoint (`https://localhost:7043/graphql`)
3. ✅ Added `codegen:dev` script that works cross-platform

## After Running Codegen

Once codegen succeeds, you'll have:
- Generated types in `src/lib/services/graphql/generated/`
- Typed document nodes ready to use
- Full type safety for your GraphQL operations

## Next Steps

1. Run `npm run codegen:dev`
2. If successful, update `operations/index.ts` to import `LoginDocument`
3. Start using generated types in your code!

## Important Notes

⚠️ **Security Warning**: The `codegen:dev` script disables SSL verification. This is **ONLY** for local development during code generation. Your actual application code still uses proper SSL verification.

✅ **Safe**: This only affects the codegen process, not your running application.
