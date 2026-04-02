# GraphQL Network Layer Implementation Guide

This guide provides step-by-step instructions for implementing the recommended GraphQL architecture with code generation.

## Quick Start

### 1. Install Dependencies

```bash
npm install -D @graphql-codegen/cli @graphql-codegen/typescript @graphql-codegen/typescript-operations @graphql-codegen/typescript-graphql-request @graphql-typed-document-node/core
```

### 2. Create Codegen Configuration

Create `front-end/codegen.ts`:

```typescript
import type { CodegenConfig } from '@graphql-codegen/cli';
import { GRAPHQL_ENDPOINT } from './src/lib/config/system';

const config: CodegenConfig = {
  schema: GRAPHQL_ENDPOINT, // Or use introspection
  documents: [
    'src/**/*.graphql',
    'src/**/*.gql',
    'src/**/*.ts',
    'src/**/*.svelte'
  ],
  generates: {
    'src/lib/services/graphql/generated/': {
      preset: 'client',
      plugins: [],
      presetConfig: {
        gqlTagName: 'gql',
      },
    },
    'src/lib/services/graphql/generated/types.ts': {
      plugins: [
        'typescript',
        'typescript-operations',
        'typescript-graphql-request',
      ],
      config: {
        avoidOptionals: {
          field: true,
        },
        defaultScalarType: 'unknown',
        nonOptionalTypename: true,
        dedupeFragments: true,
        inlineFragmentTypes: 'combine',
        scalars: {
          DateTime: 'string',
          Date: 'string',
          JSON: 'Record<string, unknown>',
        },
      },
    },
  },
  ignoreNoDocuments: true,
  hooks: {
    afterAllFileWrite: ['prettier --write'],
  },
};

export default config;
```

### 3. Add npm Scripts

Add to `package.json`:

```json
{
  "scripts": {
    "codegen": "graphql-codegen --config codegen.ts",
    "codegen:watch": "graphql-codegen --config codegen.ts --watch"
  }
}
```

### 4. Create Your First Operation

Create `src/lib/services/graphql/operations/auth/login.graphql`:

```graphql
mutation Login($username: String!, $password: String!) {
  login(input: {
    username: $username
    password: $password
  }) {
    token
    username
  }
}
```

### 5. Generate Types

```bash
npm run codegen
```

### 6. Use Generated Types

```typescript
import { LoginDocument } from '$lib/services/graphql/generated';
import { graphqlMutation } from '$lib/services/graphql';

const result = await graphqlMutation(LoginDocument, {
  variables: {
    username: 'user',
    password: 'pass'
  }
});
```

---

## Enhanced Client Implementation

### Updated Client with Typed Document Nodes

```typescript
// client.ts
import { GraphQLClient, type RequestDocument } from 'graphql-request';
import type { TypedDocumentNode } from '@graphql-typed-document-node/core';
import { GRAPHQL_ENDPOINT, DEFAULT_ERROR_MESSAGE } from '$lib/config/system';
import { toast } from 'svelte-sonner';
import type { GraphQLError } from './types';

// ... existing cache and error handling code ...

/**
 * Enhanced GraphQL Query with type inference from TypedDocumentNode
 */
export async function graphqlQuery<
  TData = unknown,
  TVariables = Record<string, unknown>
>(
  document: TypedDocumentNode<TData, TVariables> | RequestDocument,
  options: GraphQLQueryOptions<TVariables> = {}
): Promise<GraphQLResult<TData>> {
  // Implementation remains the same, but now with type inference
  // Types are automatically inferred from TypedDocumentNode
  const { variables, cacheKey, cacheTTL, skipCache = false, skipLoading = false } = options;

  // ... rest of implementation
}

/**
 * Enhanced GraphQL Mutation with type inference
 */
export async function graphqlMutation<
  TData = unknown,
  TVariables = Record<string, unknown>
>(
  document: TypedDocumentNode<TData, TVariables> | RequestDocument,
  options: GraphQLMutationOptions<TVariables> = {}
): Promise<GraphQLResult<TData>> {
  // Implementation with full type safety
  // ... existing implementation
}
```

---

## Operation Registry Pattern

### Create Operation Registry

```typescript
// operations/index.ts
import { LoginDocument } from '../generated/types';
// Import other operations as you create them

export const Operations = {
  mutations: {
    login: LoginDocument,
    // Add more mutations here
  },
  queries: {
    // Add queries here
  },
  fragments: {
    // Add fragments here
  },
} as const;

// Type-safe operation access
export type OperationName = keyof typeof Operations.mutations | keyof typeof Operations.queries;
```

### Usage

```typescript
import { Operations } from '$lib/services/graphql/operations';
import { graphqlMutation } from '$lib/services/graphql';

const result = await graphqlMutation(Operations.mutations.login, {
  variables: {
    username: 'user',
    password: 'pass'
  }
});
```

---

## Enhanced Hooks

### Updated Hooks with Type Inference

```typescript
// hooks.ts
import type { TypedDocumentNode } from '@graphql-typed-document-node/core';
import { graphqlQuery, graphqlMutation, type GraphQLResult } from './client';
import { onMount } from 'svelte';

/**
 * Enhanced query hook with full type inference
 */
export function useGraphQLQuery<
  TData = unknown,
  TVariables = Record<string, unknown>
>(
  document: TypedDocumentNode<TData, TVariables>,
  options: UseGraphQLQueryOptions<TVariables> = {}
): UseGraphQLQueryResult<TData> {
  // Types are automatically inferred from TypedDocumentNode
  // No manual type definitions needed
  // ... existing implementation
}

/**
 * Enhanced mutation hook with full type inference
 */
export function useGraphQLMutation<
  TData = unknown,
  TVariables = Record<string, unknown>
>(
  document: TypedDocumentNode<TData, TVariables>,
  options: UseGraphQLMutationOptions<TVariables> = {}
): UseGraphQLMutationResult<TData, TVariables> {
  // Full type safety, no manual types
  // ... existing implementation
}
```

---

## Component Usage Examples

### Example 1: Login Mutation

```svelte
<script lang="ts">
  import { graphqlMutation } from '$lib/services/graphql';
  import { Operations } from '$lib/services/graphql/operations';
  import { toast } from 'svelte-sonner';
  import { goto } from '$app/navigation';

  let username = $state('');
  let password = $state('');
  let loading = $state(false);

  async function handleLogin() {
    loading = true;
    
    const result = await graphqlMutation(Operations.mutations.login, {
      variables: {
        username,
        password
      },
      skipLoading: true
    });

    loading = false;

    if (result.success && result.data?.login) {
      localStorage.setItem('user', JSON.stringify(result.data.login));
      toast.success('Login successful!');
      goto('/');
    }
  }
</script>
```

### Example 2: Query with Hook

```svelte
<script lang="ts">
  import { useGraphQLQuery } from '$lib/services/graphql/hooks';
  import { Operations } from '$lib/services/graphql/operations';

  // If you have a GetUser query
  const { data, loading, error, refetch } = useGraphQLQuery(
    Operations.queries.getUser,
    {
      variables: { id: '123' },
      cacheKey: 'user-123'
    }
  );
</script>

{#if loading}
  <p>Loading...</p>
{:else if error}
  <p>Error: {error}</p>
{:else if data?.user}
  <div>
    <h1>{data.user.name}</h1>
    <p>{data.user.email}</p>
  </div>
{/if}
```

---

## Fragment Usage

### Create Fragment

```graphql
# operations/fragments/userCard.graphql
fragment UserCard on User {
  id
  name
  email
  avatar
}
```

### Use Fragment in Query

```graphql
# operations/users/getUser.graphql
#import "./fragments/userCard.graphql"

query GetUser($id: ID!) {
  user(id: $id) {
    ...UserCard
    createdAt
  }
}
```

### Use in Component

```typescript
import { GetUserDocument } from '$lib/services/graphql/generated';

// Fragment types are automatically included
const { data } = await graphqlQuery(GetUserDocument, {
  variables: { id: '123' }
});

// data.user has all fields from UserCard fragment + createdAt
```

---

## Migration Strategy

### Phase 1: Setup (Non-Breaking)

1. Install dependencies
2. Create codegen config
3. Generate types (empty initially)
4. Verify build still works

### Phase 2: Convert One Operation

1. Create `.graphql` file for login mutation
2. Generate types
3. Update login page to use generated types
4. Test thoroughly

### Phase 3: Gradual Migration

1. Convert operations one by one
2. Update components incrementally
3. Keep old API working
4. Document new patterns

### Phase 4: Cleanup

1. Remove manual type definitions
2. Update all components
3. Remove old builder functions (optional)
4. Update documentation

---

## Best Practices

### 1. Operation File Organization

```
operations/
├── auth/
│   ├── login.graphql
│   └── logout.graphql
├── users/
│   ├── getUser.graphql
│   ├── listUsers.graphql
│   └── updateUser.graphql
└── fragments/
    ├── userCard.graphql
    └── userDetails.graphql
```

### 2. Naming Conventions

- **Queries**: `GetUser`, `ListUsers`, `SearchUsers`
- **Mutations**: `CreateUser`, `UpdateUser`, `DeleteUser`
- **Fragments**: `UserCard`, `UserDetails`

### 3. Variable Naming

Use descriptive variable names:
```graphql
# ✅ Good
query GetUser($userId: ID!) { ... }

# ❌ Bad
query GetUser($id: ID!) { ... }
```

### 4. Fragment Reusability

Create reusable fragments for common fields:
```graphql
fragment UserBasicInfo on User {
  id
  name
  email
}
```

### 5. Type Safety

Always use generated types:
```typescript
// ✅ Good
import { LoginDocument } from './generated';

// ❌ Bad
const mutation = buildMutation`...`;
```

---

## Troubleshooting

### Issue: Types not generating

**Solution**: Check that:
- GraphQL endpoint is accessible
- Operation files are in `documents` path
- Schema is valid

### Issue: Type errors after generation

**Solution**: 
- Regenerate types: `npm run codegen`
- Check schema changes
- Verify operation syntax

### Issue: IDE not showing autocomplete

**Solution**:
- Restart TypeScript server
- Check `tsconfig.json` includes generated files
- Verify imports are correct

---

## Advanced: Custom Scalars

If your schema uses custom scalars, configure them in `codegen.ts`:

```typescript
config: {
  scalars: {
    DateTime: 'string',
    Date: 'string',
    JSON: 'Record<string, unknown>',
    Money: 'number',
  },
}
```

---

## Advanced: Multiple Schemas

If you have multiple GraphQL endpoints:

```typescript
const config: CodegenConfig = {
  schema: {
    main: 'https://api.example.com/graphql',
    admin: 'https://admin.example.com/graphql',
  },
  generates: {
    'src/lib/services/graphql/generated/main/': {
      // ... config for main schema
    },
    'src/lib/services/graphql/generated/admin/': {
      // ... config for admin schema
    },
  },
};
```

---

## Performance Tips

1. **Use Fragments** - Reduce query duplication
2. **Cache Strategically** - Set appropriate TTLs
3. **Batch Operations** - Use aliases for multiple queries
4. **Lazy Load** - Generate types only when needed

---

## Next Steps

1. Set up code generation
2. Convert login mutation as proof of concept
3. Evaluate developer experience
4. Plan full migration
5. Train team on new patterns
