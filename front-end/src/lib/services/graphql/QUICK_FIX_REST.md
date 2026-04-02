# Quick Fix: Use REST API Temporarily

If you want to test the login page immediately while GraphQL is being set up on the backend, you can temporarily use the existing REST API.

## Current Situation

- ❌ GraphQL endpoint doesn't exist on backend (`/graphql` returns 404)
- ✅ REST API login exists at `/api/auth/login`

## Quick Fix: Update Login Page to Use REST

You can temporarily modify the login page to use REST API:

```typescript
// In handleLogin function, replace GraphQL call with:
const response = await fetch(`${BACKEND_BASE_URL}/api/auth/login`, {
  method: 'POST',
  headers: {
    'Content-Type': 'application/json',
  },
  body: JSON.stringify({
    userId: values.username,
    password: values.password,
    mode: 'webapp'
  })
});

const result = await response.json();
if (result && result.ID) {
  // Handle success
}
```

## Better Solution: Set Up GraphQL

For the long term, set up GraphQL on the backend following:
- `backend/Tyresoles.One.Web/GRAPHQL_SETUP_NEEDED.md`

This will give you:
- ✅ Type safety
- ✅ Single endpoint
- ✅ Better developer experience
- ✅ Already generated types ready to use
