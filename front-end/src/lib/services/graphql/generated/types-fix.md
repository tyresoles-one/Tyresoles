# Generated Types Fix

## Issue
The generated `types.ts` file imports `RequestOptions` from `graphql-request`, but this type is not exported in version 7.4.0.

## Fix Applied
The import has been manually fixed in `types.ts`:

**Before:**
```typescript
import { GraphQLClient, RequestOptions } from 'graphql-request';
type GraphQLClientRequestHeaders = RequestOptions['requestHeaders'];
```

**After:**
```typescript
import { GraphQLClient } from 'graphql-request';
type GraphQLClientRequestHeaders = Record<string, string> | undefined;
```

## Note
If you regenerate types with `npm run codegen:dev`, you may need to reapply this fix. The codegen plugin `typescript-graphql-request` may generate the incorrect import again.

## Permanent Fix
To prevent this from happening again, you can:
1. Use a post-generation script to fix the import
2. Or manually fix after each codegen run
3. Or wait for an update to the codegen plugin

The fix is simple - just replace the `RequestOptions` import with the manual type definition shown above.
