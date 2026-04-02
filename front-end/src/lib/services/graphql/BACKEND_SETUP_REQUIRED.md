# Backend GraphQL Setup Required

## Current Issue

The frontend is configured to use GraphQL, but the backend doesn't have GraphQL configured yet. This causes a **404 error** when trying to access `/graphql`.

## The Problem

- ✅ Frontend: GraphQL client is ready
- ✅ Frontend: Types are generated
- ❌ Backend: GraphQL endpoint doesn't exist (`/graphql` returns 404)

## Solutions

### Option 1: Set Up GraphQL on Backend (Recommended)

See `backend/Tyresoles.One.Web/GRAPHQL_SETUP_NEEDED.md` for detailed instructions.

**Quick Steps:**
1. Install HotChocolate packages
2. Configure GraphQL in `Program.cs`
3. Create GraphQL types (Query, Mutation)
4. Map GraphQL endpoint: `app.MapGraphQL()`

### Option 2: Use REST API Temporarily

You can temporarily use the existing REST API while GraphQL is being set up. The login endpoint exists at:
- `POST /api/auth/login` with `userId`, `password`, `mode`

## CORS Issue

Also note: The CORS policy needs to allow `https://localhost:7043`. Currently it only allows:
- `http://localhost` (any port)
- But NOT `https://localhost` (any port)

**Fix needed in `Program.cs`:**
```csharp
.SetIsOriginAllowed(origin => {
    // ... existing code ...
    
    // Add this:
    if (origin.StartsWith("https://localhost:", StringComparison.OrdinalIgnoreCase)) {
        return true;
    }
    
    // ... rest of code ...
})
```

## Next Steps

1. **If setting up GraphQL**: Follow the guide in `backend/Tyresoles.One.Web/GRAPHQL_SETUP_NEEDED.md`
2. **If using REST temporarily**: Update login page to use REST API
3. **Fix CORS**: Add HTTPS localhost support

## Testing

Once GraphQL is set up, test at:
- GraphQL Playground: `https://localhost:7043/graphql`
- The frontend should then work with the generated types
