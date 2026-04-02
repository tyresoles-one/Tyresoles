# Running Multiple GraphQL Queries in Parallel

Yes! The GraphQL service **fully supports parallel query execution**. Here's how it works:

## How Parallel Queries Work

### 1. **Independent Execution**
Each query runs independently and can execute concurrently. The service creates separate promises for each query, allowing them to run in parallel.

### 2. **Request Deduplication**
The service uses **cache keys** to deduplicate requests:
- **Same query + same variables** = Only one network request (deduplicated)
- **Same query + different variables** = Multiple parallel requests
- **Different queries** = Multiple parallel requests

### 3. **Loading State Management**
The global loading counter properly tracks multiple concurrent requests:
- Each request increments the counter when it starts
- Each request decrements the counter when it completes
- The loading state is `true` if **any** request is in progress

## Examples

### Basic Parallel Execution

```typescript
import { graphqlQuery, buildQuery } from '$lib/services/graphql';

const usersQuery = buildQuery`
  query GetUsers {
    users {
      id
      name
    }
  }
`;

const postsQuery = buildQuery`
  query GetPosts {
    posts {
      id
      title
    }
  }
`;

// Both queries execute in parallel
const [usersResult, postsResult] = await Promise.all([
  graphqlQuery(usersQuery, { cacheKey: 'users' }),
  graphqlQuery(postsQuery, { cacheKey: 'posts' })
]);
```

### Parallel Queries with Different Variables

```typescript
const query = buildQuery`
  query GetUser($id: ID!) {
    user(id: $id) {
      id
      name
    }
  }
`;

// These run in parallel because they have different variables
const [user1, user2, user3] = await Promise.all([
  graphqlQuery(query, { variables: { id: '1' } }),
  graphqlQuery(query, { variables: { id: '2' } }),
  graphqlQuery(query, { variables: { id: '3' } })
]);
```

### Request Deduplication Example

```typescript
const query = buildQuery`
  query GetUsers {
    users {
      id
      name
    }
  }
`;

// These three calls result in ONLY ONE network request
// because they have the same query and variables
const [result1, result2, result3] = await Promise.all([
  graphqlQuery(query, { cacheKey: 'users' }),
  graphqlQuery(query, { cacheKey: 'users' }),
  graphqlQuery(query, { cacheKey: 'users' })
]);

// All three receive the same data from the single request
```

### Using with Svelte Hooks

```svelte
<script>
  import { useGraphQLQuery, buildQuery } from '$lib/services/graphql';
  
  const usersQuery = buildQuery`query GetUsers { users { id name } }`;
  const postsQuery = buildQuery`query GetPosts { posts { id title } }`;
  
  // Both queries execute in parallel automatically
  const users = useGraphQLQuery(usersQuery, { cacheKey: 'users' });
  const posts = useGraphQLQuery(postsQuery, { cacheKey: 'posts' });
</script>

{#if users.loading || posts.loading}
  <p>Loading...</p>
{:else}
  <div>
    <h2>Users</h2>
    {#each users.data?.users || [] as user}
      <p>{user.name}</p>
    {/each}
    
    <h2>Posts</h2>
    {#each posts.data?.posts || [] as post}
      <p>{post.title}</p>
    {/each}
  </div>
{/if}
```

## Performance Benefits

### Sequential vs Parallel

**Sequential (slower):**
```typescript
const user1 = await graphqlQuery(query1);  // 100ms
const user2 = await graphqlQuery(query2);  // 100ms
const user3 = await graphqlQuery(query3);  // 100ms
// Total: ~300ms
```

**Parallel (faster):**
```typescript
const [user1, user2, user3] = await Promise.all([
  graphqlQuery(query1),  // All run concurrently
  graphqlQuery(query2),
  graphqlQuery(query3)
]);
// Total: ~100ms (all complete together)
```

## Important Notes

### 1. **Cache Keys Matter**
- Same cache key = deduplication (one request)
- Different cache keys = parallel requests

```typescript
// These are deduplicated (same cache key)
graphqlQuery(query, { cacheKey: 'users' });
graphqlQuery(query, { cacheKey: 'users' });

// These run in parallel (different cache keys)
graphqlQuery(query, { cacheKey: 'users' });
graphqlQuery(query, { cacheKey: 'posts' });
```

### 2. **Auto-Generated Cache Keys**
If you don't provide a cache key, it's auto-generated from the query document and variables:

```typescript
// These are deduplicated (same query + same variables)
graphqlQuery(query, { variables: { id: 1 } });
graphqlQuery(query, { variables: { id: 1 } });

// These run in parallel (different variables)
graphqlQuery(query, { variables: { id: 1 } });
graphqlQuery(query, { variables: { id: 2 } });
```

### 3. **Error Handling**
Use `Promise.allSettled()` if you want to handle partial failures:

```typescript
const results = await Promise.allSettled([
  graphqlQuery(query1),
  graphqlQuery(query2),
  graphqlQuery(query3)
]);

results.forEach((result, index) => {
  if (result.status === 'fulfilled' && result.value.success) {
    console.log(`Query ${index} succeeded`);
  } else {
    console.error(`Query ${index} failed`);
  }
});
```

### 4. **Loading State**
The global loading state tracks all concurrent requests:

```typescript
// If 3 queries are running in parallel, loadingCount = 3
// When all complete, loadingCount = 0
```

## Best Practices

1. **Use `Promise.all()`** for independent queries that can run in parallel
2. **Use `Promise.allSettled()`** if you want to handle partial failures gracefully
3. **Provide explicit cache keys** for better control over deduplication
4. **Use hooks** in Svelte components - they automatically handle parallel execution
5. **Batch related queries** together for better performance

## Summary

✅ **Yes, you can run multiple queries in parallel!**

- Different queries → Parallel execution
- Same query with different variables → Parallel execution  
- Same query with same variables → Deduplicated (one request)

The service is optimized to handle concurrent requests efficiently while preventing unnecessary duplicate network calls.
