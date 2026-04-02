# GraphQL Code Generation Setup

## Quick Start

### 1. Install Dependencies

```bash
npm install
```

This will install all required codegen dependencies that were added to `package.json`.

### 2. Generate Types

```bash
npm run codegen
```

This will:
- Fetch your GraphQL schema from the backend
- Scan for `.graphql` operation files
- Generate TypeScript types and document nodes
- Output files to `src/lib/services/graphql/generated/`

### 3. Use Generated Types

After running codegen, you can use the generated types:

```typescript
import { LoginDocument } from '$lib/services/graphql/generated';
import { graphqlMutation } from '$lib/services/graphql';

const result = await graphqlMutation(LoginDocument, {
  variables: {
    username: 'user',  // ✅ Type-checked
    password: 'pass'   // ✅ Type-checked
  }
});
```

## Watch Mode

For development, use watch mode to automatically regenerate types when operations change:

```bash
npm run codegen:watch
```

## Current Status

✅ **Setup Complete**
- Codegen configuration created (`codegen.ts`)
- Dependencies added to `package.json`
- Operation file created (`operations/auth/login.graphql`)
- Client and hooks updated to support TypedDocumentNode
- Operations registry created

⏳ **Next Steps**
1. Run `npm install` to install dependencies
2. Run `npm run codegen` to generate types
3. Update `operations/index.ts` to import generated document nodes
4. Update login page to use `LoginDocument` instead of `buildMutation`

## Troubleshooting

### Issue: Codegen fails to connect to GraphQL endpoint

**Solution**: 
- Ensure your backend GraphQL server is running
- Check `GRAPHQL_ENDPOINT` in `src/lib/config/system.ts`
- For HTTPS with self-signed certificates, you may need to set `NODE_TLS_REJECT_UNAUTHORIZED=0` (development only)

### Issue: Types not generating

**Solution**:
- Check that operation files are in the `documents` path
- Verify GraphQL syntax in operation files
- Check codegen output for errors

### Issue: TypeScript errors after generation

**Solution**:
- Restart TypeScript server in your IDE
- Verify generated files exist in `src/lib/services/graphql/generated/`
- Check that imports are correct

## Migration Notes

The current implementation is **backward compatible**. You can:

1. Continue using `buildMutation` and `buildQuery` (legacy)
2. Gradually migrate to generated document nodes
3. Mix both approaches during migration

The client and hooks support both:
- `TypedDocumentNode` (from codegen) - Recommended
- `RequestDocument` (legacy) - Still works
