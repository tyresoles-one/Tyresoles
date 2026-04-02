# JWT and Session Store

## Overview

- **Login** returns a JWT in `LoginResult.token`. Token includes: `sub` (userId), `userSecurityId`, `userType`, `entityType`, `entityCode`, `department`, `sessionId`, and standard `exp`/`iat`.
- **Expiry**: DEALER and SALES users get 6-hour tokens; others get 7 days (configurable in `appsettings.json` under `Jwt`).
- **Session store**: Each successful login creates a session (in-memory by default). Admins can list and kill sessions via GraphQL.

## Configuration (appsettings.json)

```json
"Jwt": {
  "Secret": "At least 32 characters or base64-encoded key",
  "Issuer": "Tyresoles",
  "Audience": "Tyresoles",
  "DealerSalesExpiryHours": 6,
  "DefaultExpiryHours": 168
}
```

In production, set `Secret` to a strong base64 key (e.g. 32+ bytes) or a long random string.

## Using the token

Send the token in the `Authorization` header for protected operations:

```
Authorization: Bearer <token>
```

- **Login** – no token required.
- **getSessions**, **killSession**, **killSessionsByUser** – require a valid Bearer token (any authenticated user).

## GraphQL

- **Query**: `getSessions(userId: String)` – list active sessions (optional filter by `userId`).
- **Mutations**: `killSession(sessionId: String!)`, `killSessionsByUser(userId: String!)`.

## Reading claims in resolvers

Use `HttpContext.User` or inject `IHttpContextAccessor` and read claims, e.g.:

- `User.FindFirstValue(ClaimTypes.NameIdentifier)` or `sub` → userId  
- `User.FindFirstValue("userSecurityId")`, `"userType"`, `"entityType"`, `"entityCode"`, `"department"`, `"sessionId"`

## Session store

Default is `InMemorySessionStore` (single instance; sessions lost on restart). For multiple instances or persistence, implement `ISessionStore` (e.g. Redis) and register it in `Program.cs` instead of `InMemorySessionStore`.
