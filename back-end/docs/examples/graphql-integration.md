# HotChocolate GraphQL Integration

GraphQL explicitly defines nested querying structures (`[UseProjection]`, `[UseFiltering]`, `[UseSorting]`).
`Tyresoles.Web` interacts heavily inside an `.AsQueryable()` execution limit binding HotChocolate abstractions directly across Tyresoles database operations globally across `.NET 10`.

## Resolvers Example

The `Tyresoles.Sql.Generators` emits bridging rules for `AsQueryable()` strictly designed to handle dynamic GraphQL AST parsing without exploding performance via reflection boundaries perfectly matching your native ADO.NET SQL Builder.

```csharp
using Tyresoles.Sql.Abstractions;
using HotChocolate.Data;

public class UserQueryResolver
{
    [UsePaging(IncludeTotalCount = true, MaxPageSize = 100)]
    [UseProjection]
    [UseFiltering]
    [UseSorting]
    public IQueryable<User> GetUsers([Service] IDataverse db)
    {
         // Binds implicitly via Source Generator Extension `UserQueryExtensions.AsQueryable(query)`
         return db.DefaultTenant.NavCompany("CRONUS").Query<User>().AsQueryable();
    }
}
```

## GraphQL Request Schema Example
```graphql
query {
  users(
    first: 10, 
    where: { status: { eq: ACTIVE }}, 
    order: { name: ASC }
  ) {
    nodes {
      name
      roles {
        description
      }
    }
    totalCount
  }
}
```
This gracefully transpiles sequentially entirely internally pushing the limit offset inside Tyresoles.Sql `Dialect.BuildPagination(...)` boundary specifically.
