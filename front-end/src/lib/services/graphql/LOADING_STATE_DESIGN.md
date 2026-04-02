# Global Loading State Design

The GraphQL service uses a **counter-based loading state system** that efficiently tracks multiple concurrent requests. Here's how it's designed:

## Architecture Overview

### 1. **Counter-Based System**

The loading state uses a simple counter (`loadingCount`) that tracks how many requests are currently in progress:

```typescript
let loadingCount = $state(0);  // Svelte 5 rune for reactivity

function incrementLoading(): void {
  loadingCount++;
}

function decrementLoading(): void {
  loadingCount = Math.max(0, loadingCount - 1);
}

export function isLoading(): boolean {
  return loadingCount > 0;
}
```

### 2. **How It Works**

#### Request Lifecycle

1. **Request Starts**: `incrementLoading()` is called → `loadingCount++`
2. **Request Completes**: `decrementLoading()` is called → `loadingCount--`
3. **Loading State**: `isLoading()` returns `true` if `loadingCount > 0`

#### Example with Multiple Requests

```typescript
// Request 1 starts
incrementLoading();  // loadingCount = 1, isLoading() = true

// Request 2 starts (parallel)
incrementLoading();  // loadingCount = 2, isLoading() = true

// Request 1 completes
decrementLoading();  // loadingCount = 1, isLoading() = true

// Request 2 completes
decrementLoading();  // loadingCount = 0, isLoading() = false
```

### 3. **Safety Features**

#### Protection Against Negative Counts

```typescript
function decrementLoading(): void {
  loadingCount = Math.max(0, loadingCount - 1);
  // Ensures count never goes below 0
}
```

This prevents edge cases where:
- A request completes twice
- An error occurs and cleanup runs multiple times
- Race conditions in parallel requests

#### Skip Loading Option

Each request can opt-out of global loading tracking:

```typescript
// This request won't affect global loading state
await graphqlQuery(query, { skipLoading: true });
```

Useful when:
- Managing loading state locally in a component
- Using hooks that manage their own loading state
- Background requests that shouldn't show global loading

## Implementation Details

### Core Loading State (`client.ts`)

```typescript
/**
 * Loading state management using Svelte 5 runes
 * Reactive state that components can subscribe to
 */
let loadingCount = $state(0);

/**
 * Get current loading state
 */
export function isLoading(): boolean {
  return loadingCount > 0;
}

/**
 * Increment loading counter
 */
function incrementLoading(): void {
  loadingCount++;
}

/**
 * Decrement loading counter
 */
function decrementLoading(): void {
  loadingCount = Math.max(0, loadingCount - 1);
}
```

### Usage in Queries/Mutations

```typescript
export async function graphqlQuery(...) {
  // ...
  
  const requestPromise = (async () => {
    try {
      if (!skipLoading) {
        incrementLoading();  // Start tracking
      }
      
      const data = await client.request(...);
      return data;
    } finally {
      if (!skipLoading) {
        decrementLoading();  // Always decrement, even on error
      }
    }
  })();
  
  // ...
}
```

**Key Points:**
- ✅ Increment when request starts
- ✅ Decrement in `finally` block (always runs, even on error)
- ✅ Respect `skipLoading` option

### Reactive Store (`store.ts`)

For components that need to reactively subscribe to loading state:

```typescript
class GraphQLLoadingStore {
  #subscribers = new Set<() => void>();
  #rafId: number | null = null;
  #lastValue = false;

  constructor() {
    this.#poll();  // Start polling
  }

  #poll() {
    const currentValue = getIsLoading();
    if (currentValue !== this.#lastValue) {
      this.#lastValue = currentValue;
      this.#subscribers.forEach((fn) => fn());  // Notify subscribers
    }
    this.#rafId = requestAnimationFrame(() => this.#poll());
  }

  subscribe(fn: () => void) {
    this.#subscribers.add(fn);
    fn();  // Call immediately with current value
    return () => {
      this.#subscribers.delete(fn);
      if (this.#subscribers.size === 0) {
        cancelAnimationFrame(this.#rafId);  // Clean up when no subscribers
      }
    };
  }
}

export const graphqlLoading = new GraphQLLoadingStore();
```

**Design Decisions:**
- Uses `requestAnimationFrame` for efficient polling (60fps max)
- Only notifies subscribers when value actually changes
- Automatically cleans up when no subscribers remain
- Compatible with Svelte's `$` store syntax

## Usage Examples

### 1. Direct Function Call

```typescript
import { isLoading } from '$lib/services/graphql';

if (isLoading()) {
  console.log('A GraphQL request is in progress');
}
```

### 2. Reactive Store (Recommended)

```svelte
<script>
  import { graphqlLoading } from '$lib/services/graphql/store';
</script>

{#if $graphqlLoading}
  <div class="global-loader">
    <Spinner />
    <p>Loading...</p>
  </div>
{/if}
```

### 3. Custom Subscription

```typescript
import { subscribeLoading } from '$lib/services/graphql';

const unsubscribe = subscribeLoading((loading) => {
  console.log('Loading state changed:', loading);
});

// Later...
unsubscribe();
```

### 4. Skip Global Loading

```typescript
// Component manages its own loading state
const { data, loading } = useGraphQLQuery(query, {
  skipLoading: true  // Don't affect global loading
});

// Or in direct calls
await graphqlQuery(query, {
  skipLoading: true
});
```

## Parallel Request Handling

The counter system naturally handles parallel requests:

```typescript
// All three start simultaneously
const [r1, r2, r3] = await Promise.all([
  graphqlQuery(query1),  // loadingCount: 0 → 1
  graphqlQuery(query2),  // loadingCount: 1 → 2
  graphqlQuery(query3)   // loadingCount: 2 → 3
]);

// isLoading() = true (because loadingCount = 3)

// As each completes:
// r1 completes: loadingCount: 3 → 2
// r2 completes: loadingCount: 2 → 1
// r3 completes: loadingCount: 1 → 0

// isLoading() = false (because loadingCount = 0)
```

## Benefits of This Design

### ✅ **Simple & Efficient**
- Single counter instead of complex state management
- O(1) operations (increment/decrement)
- No array tracking or request IDs needed

### ✅ **Accurate**
- Always reflects actual request state
- Handles parallel requests correctly
- Prevents race conditions with `Math.max(0, ...)`

### ✅ **Flexible**
- Can skip global loading per-request
- Works with hooks that manage local state
- Compatible with Svelte 5 runes

### ✅ **Performance**
- Minimal overhead (just a counter)
- Efficient polling with `requestAnimationFrame`
- Auto-cleanup when not needed

## Edge Cases Handled

### 1. **Request Fails**
```typescript
try {
  incrementLoading();
  await request();
} finally {
  decrementLoading();  // Always decrements, even on error
}
```

### 2. **Multiple Rapid Requests**
```typescript
// All increment before any complete
incrementLoading();  // 1
incrementLoading();  // 2
incrementLoading();  // 3
decrementLoading();  // 2
decrementLoading();  // 1
decrementLoading();  // 0
```

### 3. **Negative Count Protection**
```typescript
// Even if decrement is called too many times
decrementLoading();  // 0 → 0 (protected by Math.max)
```

### 4. **Skip Loading**
```typescript
// These don't affect global state
await graphqlQuery(query1, { skipLoading: true });
await graphqlQuery(query2, { skipLoading: true });
// loadingCount remains unchanged
```

## Comparison with Alternatives

### ❌ **Array-Based Tracking**
```typescript
// More complex, less efficient
const activeRequests: string[] = [];
```

### ❌ **Boolean Flag**
```typescript
// Can't handle parallel requests correctly
let isLoading = false;
```

### ✅ **Counter-Based (Current)**
```typescript
// Simple, efficient, handles parallel requests
let loadingCount = 0;
```

## Summary

The global loading state uses a **counter-based system** that:

1. **Tracks requests** with a simple increment/decrement counter
2. **Handles parallel requests** naturally (count = number of active requests)
3. **Uses Svelte 5 runes** (`$state`) for reactivity
4. **Provides a reactive store** for component subscriptions
5. **Allows opt-out** per-request with `skipLoading`
6. **Protects against edge cases** with `Math.max(0, ...)`

This design is **simple, efficient, and accurate** for tracking multiple concurrent GraphQL requests across your application.
