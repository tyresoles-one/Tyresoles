# GraphQL Network Service - Svelte 5 Runes Edition

A performant, optimized GraphQL client fully compatible with Svelte 5 runes for the front-end application.

## Features

- ✅ **Svelte 5 Runes Compatible** - Uses `$state` for reactive state management
- ✅ **Request Deduplication** - Prevents duplicate concurrent requests
- ✅ **LRU Cache** - Optimized caching with automatic eviction (max 100 entries)
- ✅ **Type-safe** queries and mutations with TypeScript
- ✅ **Automatic authentication** token handling
- ✅ **Error handling** with user-friendly messages
- ✅ **Loading state management** with reactive counters
- ✅ **Svelte hooks** for reactive queries and mutations

## Installation

Install the required packages:

```bash
npm install graphql-request graphql
```

## Quick Start

### Basic Query

```typescript
import { graphqlQuery, buildQuery } from '$lib/services/graphql';

const query = buildQuery`
  query GetUsers {
    users {
      id
      name
      email
    }
  }
`;

const result = await graphqlQuery(query, {
  cacheKey: 'users',
  cacheTTL: 60000 // 1 minute
});

if (result.success) {
  console.log(result.data);
}
```

### Using Svelte 5 Runes Hooks

```svelte
<script lang="ts">
  import { useGraphQLQuery, buildQuery } from '$lib/services/graphql';
  
  const query = buildQuery`
    query GetUsers {
      users {
        id
        name
        email
      }
    }
  `;
  
  // Fully reactive with Svelte 5 runes
  const { data, loading, error, refetch } = useGraphQLQuery(query, {
    cacheKey: 'users',
    onSuccess: (data) => console.log('Users loaded:', data)
  });
</script>

{#if loading}
  <p>Loading users...</p>
{:else if error}
  <p class="error">Error: {error}</p>
{:else if data}
  <ul>
    {#each data.users as user}
      <li>{user.name} - {user.email}</li>
    {/each}
  </ul>
  <button on:click={refetch}>Refresh</button>
{/if}
```

### Global Loading State

```svelte
<script lang="ts">
  import { graphqlLoading } from '$lib/services/graphql/store';
</script>

{#if $graphqlLoading}
  <div class="global-loader">Loading...</div>
{/if}
```

## Performance Optimizations

### 1. Request Deduplication
Concurrent requests with the same query and variables are automatically deduplicated:

```typescript
// These two calls will result in only one network request
const promise1 = graphqlQuery(query, { variables: { id: 1 } });
const promise2 = graphqlQuery(query, { variables: { id: 1 } });
```

### 2. LRU Cache
The cache automatically evicts least-recently-used entries when it reaches 100 items:

```typescript
// Cache is automatically managed
const result = await graphqlQuery(query, {
  cacheKey: 'users', // Auto-generated if not provided
  cacheTTL: 300000 // 5 minutes
});
```

### 3. Reactive State with Runes
Uses Svelte 5 `$state` for optimal reactivity:

```svelte
<script>
  // State is automatically reactive - no stores needed!
  const { data, loading, error } = useGraphQLQuery(query);
</script>

<!-- Automatically updates when state changes -->
{#if loading}Loading...{/if}
```

## API Reference

### Core Functions

#### `graphqlQuery<TData, TVariables>(document, options?)`

Execute a GraphQL query with caching and deduplication.

**Options:**
- `variables?: TVariables` - Query variables
- `cacheKey?: string` - Cache key (auto-generated if not provided)
- `cacheTTL?: number` - Cache time-to-live in milliseconds (default: 5 minutes)
- `skipCache?: boolean` - Skip cache lookup and storage
- `skipLoading?: boolean` - Skip global loading state management

**Returns:** `Promise<GraphQLResult<TData>>`

#### `graphqlMutation<TData, TVariables>(document, options?)`

Execute a GraphQL mutation (never cached or deduplicated).

**Options:**
- `variables?: TVariables` - Mutation variables
- `skipLoading?: boolean` - Skip global loading state management

**Returns:** `Promise<GraphQLResult<TData>>`

### Hooks (Svelte 5 Runes)

#### `useGraphQLQuery<TData, TVariables>(document, options?)`

Reactive hook for GraphQL queries using Svelte 5 runes.

**Returns:**
- `data: TData | undefined` - Reactive data (updates automatically)
- `loading: boolean` - Reactive loading state
- `error: string | undefined` - Reactive error state
- `refetch: () => Promise<void>` - Function to refetch the query

#### `useGraphQLMutation<TData, TVariables>(document, options?)`

Reactive hook for GraphQL mutations using Svelte 5 runes.

**Returns:**
- `mutate: (variables?) => Promise<GraphQLResult<TData>>` - Mutation function
- `loading: boolean` - Reactive loading state
- `error: string | undefined` - Reactive error state

### Global Loading State

#### `graphqlLoading`

Reactive store for global GraphQL loading state:

```svelte
<script>
  import { graphqlLoading } from '$lib/services/graphql/store';
</script>

{#if $graphqlLoading}
  <div>Any GraphQL request is in progress...</div>
{/if}
```

## Configuration

Update the backend URL in `$lib/config/system.ts`:

```typescript
export const BACKEND_DEV_URL = 'http://localhost:7149';
export const BACKEND_PROD_URL = 'https://app.tyresoles.in';
```

The GraphQL endpoint will be automatically set to `${BACKEND_BASE_URL}/graphql`.

## Authentication

The service automatically retrieves the authentication token from `localStorage.getItem('user')`. Make sure your auth system stores the token in this format:

```typescript
localStorage.setItem('user', JSON.stringify({ token: 'your-jwt-token' }));
```

You can customize the token retrieval in `client.ts` by modifying the `getAuthToken()` function.

## Advanced Usage

### Custom Cache Keys

```typescript
import { createCacheKey } from '$lib/services/graphql';

const cacheKey = createCacheKey('GetUser', { id: 123 });
const result = await graphqlQuery(query, { cacheKey });
```

### Clearing Cache

```typescript
import { clearGraphQLCache } from '$lib/services/graphql';

// Clear specific cache entry
clearGraphQLCache('users');

// Clear all cache
clearGraphQLCache();
```

### Skipping Global Loading

```typescript
// Use local loading state instead
const result = await graphqlQuery(query, {
  skipLoading: true
});
```

## Performance Tips

1. **Use caching** for frequently accessed data
2. **Leverage request deduplication** - multiple components can call the same query safely
3. **Use hooks** for automatic reactivity without manual state management
4. **Clear cache** after mutations that affect cached queries
5. **Use fragments** for reusable field selections

## Type Safety

Full TypeScript support with type inference:

```typescript
interface User {
  id: string;
  name: string;
  email: string;
}

interface GetUsersResponse {
  users: User[];
}

const result = await graphqlQuery<GetUsersResponse>(query);
// result.data is fully typed!
```
