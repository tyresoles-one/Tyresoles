# Stores

Centralized state management with localStorage persistence.

## Persisted Store

Generic store that syncs to `localStorage` with debounced writes and lazy reads.

### Features

- ✅ **Debounced writes** – batches rapid updates (default 50ms) to avoid blocking
- ✅ **Lazy read** – reads from `localStorage` once on init (client-only)
- ✅ **SSR-safe** – no `localStorage` on server; store still works with initial value
- ✅ **Full Svelte store contract** – `subscribe`, `set`, `update`, plus `get()` for sync reads
- ✅ **Type-safe** – generic `T` with TypeScript support
- ✅ **Customizable** – custom serialization, storage (e.g. `sessionStorage`), key prefix

### Basic Usage

```ts
import { createPersistedStore } from '$lib/stores';

// Simple store
const userPrefs = createPersistedStore('prefs', { theme: 'light', lang: 'en' });

// In Svelte component
$userPrefs.theme; // 'light'
userPrefs.set({ theme: 'dark', lang: 'en' });
userPrefs.update((prev) => ({ ...prev, theme: 'dark' }));
userPrefs.get(); // current value without subscribing
```

### With Options

```ts
const settings = createPersistedStore('settings', { notifications: true }, {
  debounceMs: 100,           // write delay (default 50ms)
  prefix: 'myapp',           // key becomes "myapp:settings"
  storage: sessionStorage,   // use sessionStorage instead
});
```

### Custom Serialization

For non-JSON types like `Date`, `Map`, etc.:

```ts
const dateStore = createPersistedStore('lastVisit', new Date(), {
  serialize: (date) => date.toISOString(),
  deserialize: (str) => new Date(str),
});
```

## Auth Store

Pre-built store for authentication data (token, user info).

### Usage

```ts
import { authStore, isAuthenticated, getAuthToken, clearAuth } from '$lib/stores/auth';

// In Svelte component
{#if $authStore.token}
  <p>Welcome, {$authStore.username}!</p>
{/if}

// Set auth data after login
authStore.set({
  token: 'abc123',
  username: 'user@example.com',
  expiresAt: '2026-02-01T12:00:00Z',
  user: { ... }
});

// Check authentication
if (isAuthenticated()) {
  // user is logged in
}

// Get token for API calls
const token = getAuthToken();

// Logout
clearAuth();
```

### Integration

The auth store is automatically used by:
- **Login page** – stores session data on successful login
- **GraphQL client** – reads token for Authorization header
- **Auth guards** – can check `isAuthenticated()` in hooks/middleware

## Creating Custom Stores

```ts
import { createPersistedStore } from '$lib/stores';

// Shopping cart
export const cartStore = createPersistedStore('cart', { items: [], total: 0 });

// User preferences
export const prefsStore = createPersistedStore('prefs', {
  theme: 'light',
  notifications: true,
  language: 'en',
});

// Recent searches
export const searchHistoryStore = createPersistedStore('searches', [], {
  debounceMs: 200, // longer delay for less critical data
});
```

## Performance Tips

1. **Debounce wisely** – use shorter delays (0-50ms) for critical data like auth; longer (100-500ms) for less critical data like UI preferences.
2. **Avoid large objects** – `localStorage` has a ~5-10MB limit; store only what you need.
3. **Use `get()` for one-off reads** – avoids creating a subscription if you just need the current value.
4. **Prefix keys** – prevents collisions if you have multiple apps on the same domain.

## SSR Considerations

The store is SSR-safe:
- On the server, `localStorage` is unavailable; the store uses the initial value and doesn't persist.
- On the client, the store hydrates from `localStorage` on first init.
- No hydration mismatch: the initial render uses the same value as the server (the `initial` param), then updates on client after hydration.
