# Codegen Troubleshooting Guide

## SSL Certificate Issues

### Problem
```
unable to verify the first certificate
```

This happens when:
- Using HTTPS with self-signed certificates (common in development)
- Certificate chain is incomplete
- Node.js can't verify the SSL certificate

### Solution 1: Use Development Script (Recommended for Local Development)

For local development with self-signed certificates, use:

```bash
npm run codegen:dev
```

This script temporarily disables SSL certificate verification **only during codegen** (not in your app).

**⚠️ WARNING**: Only use this in development! Never disable SSL verification in production.

### Solution 2: Install cross-env (If Not Already Installed)

If you get an error about `cross-env`, install it:

```bash
npm install -D cross-env
```

### Solution 3: Manual Environment Variable

You can also set the environment variable manually:

**Windows (PowerShell):**
```powershell
$env:NODE_TLS_REJECT_UNAUTHORIZED=0; npm run codegen
```

**Windows (CMD):**
```cmd
set NODE_TLS_REJECT_UNAUTHORIZED=0 && npm run codegen
```

**Linux/Mac:**
```bash
NODE_TLS_REJECT_UNAUTHORIZED=0 npm run codegen
```

### Solution 4: Use HTTP Instead of HTTPS (Development Only)

If your local development server supports HTTP, you can temporarily change the endpoint in `codegen.ts`:

```typescript
const GRAPHQL_ENDPOINT = MODE === 'development' 
  ? 'http://localhost:7043/graphql'  // Use HTTP instead of HTTPS
  : `${BACKEND_PROD_URL}/graphql`;
```

### Solution 5: Use Schema File Instead of URL

If you have access to your GraphQL schema file, you can use it directly:

1. Export your schema to a file (e.g., `schema.graphql`)
2. Update `codegen.ts`:

```typescript
const config: CodegenConfig = {
  schema: './schema.graphql',  // Use file instead of URL
  // ... rest of config
};
```

### Solution 6: Use Introspection Query

You can manually fetch the introspection result and save it:

1. Run introspection query manually
2. Save result to `schema.json`
3. Update `codegen.ts`:

```typescript
const config: CodegenConfig = {
  schema: './schema.json',  // Use introspection JSON
  // ... rest of config
};
```

## Other Common Issues

### Issue: "Failed to load schema"

**Causes:**
- Backend server not running
- Wrong endpoint URL
- Network connectivity issues
- CORS issues

**Solutions:**
1. Verify backend is running: `curl https://localhost:7043/graphql`
2. Check endpoint in `codegen.ts` matches your backend
3. Check network/firewall settings
4. Verify CORS is configured on backend

### Issue: "No documents found"

**Causes:**
- Operation files not in correct location
- Wrong file extensions in `documents` config

**Solutions:**
1. Verify operation files are in `src/**/*.graphql` or match your `documents` pattern
2. Check file extensions match (`.graphql`, `.gql`, `.ts`, `.svelte`)
3. Ensure files are not in `.gitignore` or excluded

### Issue: TypeScript Errors After Generation

**Causes:**
- Generated types don't match current schema
- Import paths incorrect
- TypeScript server needs restart

**Solutions:**
1. Regenerate types: `npm run codegen`
2. Restart TypeScript server in your IDE
3. Check import paths in generated files
4. Verify `tsconfig.json` includes generated directory

## Best Practices

1. **Development**: Use `npm run codegen:dev` for local development with self-signed certs
2. **Production**: Always use proper SSL certificates
3. **CI/CD**: Use proper certificate validation in CI/CD pipelines
4. **Schema Sync**: Run codegen regularly to keep types in sync with backend

## Quick Reference

```bash
# Standard codegen (requires valid SSL certificate)
npm run codegen

# Development codegen (bypasses SSL verification)
npm run codegen:dev

# Watch mode (standard)
npm run codegen:watch

# Watch mode (development)
npm run codegen:watch:dev
```
