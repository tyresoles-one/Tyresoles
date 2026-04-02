# GraphQL Operations

This directory contains GraphQL operation files (queries, mutations, subscriptions, and fragments).

## Structure

```
operations/
├── auth/
│   ├── login.graphql
│   └── logout.graphql (example)
├── users/
│   ├── getUser.graphql (example)
│   └── listUsers.graphql (example)
└── fragments/
    └── userCard.graphql (example)
```

## Naming Conventions

- **Queries**: `GetUser`, `ListUsers`, `SearchUsers`
- **Mutations**: `CreateUser`, `UpdateUser`, `DeleteUser`, `Login`
- **Fragments**: `UserCard`, `UserDetails`

## Usage

After running `npm run codegen`, operations are available as typed document nodes:

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

Or use the operations registry:

```typescript
import { Operations } from '$lib/services/graphql/operations';

const result = await graphqlMutation(Operations.mutations.login, {
  variables: {
    username: 'user',
    password: 'pass'
  }
});
```

## Adding New Operations

1. Create a `.graphql` file in the appropriate directory
2. Write your operation (query/mutation/subscription)
3. Run `npm run codegen` to generate types
4. Import and use the generated document node

## Example: Creating a Query

1. Create `operations/users/getUser.graphql`:

```graphql
query GetUser($id: ID!) {
  user(id: $id) {
    id
    name
    email
  }
}
```

2. Run codegen: `npm run codegen`

3. Use in your code:

```typescript
import { GetUserDocument } from '$lib/services/graphql/generated';

const result = await graphqlQuery(GetUserDocument, {
  variables: { id: '123' }
});
```
