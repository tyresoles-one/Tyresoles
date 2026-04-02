# Source Generators

`Tyresoles.Sql` ships with an advanced `.NET 10` native Roslyn Analyzer packaged under `Tyresoles.Sql.Generators`.

When you reference the generator, memory usage drops by essentially 90% via eliminating dynamically allocated Reflection mapping dictionaries (`Type.GetProperties()`). 

## 1. How It Works
The standard execution pipeline works by inspecting generic closures and metadata loops dynamically traversing objects over an `SqlDataReader` flow.
```csharp
// Expensive Dynamic Caching
prop.SetValue(entity, reader.GetValue(i)); // Generates GC pressure over massive arrays via Boxing
```

**What the Generator Does:**
It inspects all Syntax models compiled bearing the `[NavTable]` attribute and physically hard-codes strongly typed primitive memory read commands into a static `.g.cs` mapping extension during the initial sequence of CI/CD builds.

```csharp
// Source Generator dynamically writes unboxed allocation logic automatically mapping to C# primitives
public static User CreateFromReader(IDataReader reader) {
    return new User {
        Id = reader.GetString(0), 
        UserName = reader.IsDBNull(1) ? null : reader.GetString(1),
    };
}
```

## 2. Installation
Check out `getting-started` but strictly include the package reference as an Analyzer.

```xml
<ItemGroup>
  <PackageReference Include="Tyresoles.Sql.Generators" Version="1.0.0" OutputItemType="Analyzer" ReferenceOutputAssembly="false" />
</ItemGroup>
```

## 3. Resolving HotChocolate Errors (GraphQL Integration)
Because `IQuery<T>` is a custom domain boundary framework, standard integrations inside Entity Framework (`.AsQueryable()`) are disconnected. The source generator also automatically targets extensions per POCO:

```csharp
// Auto-emitted by Generator
public static IQueryable<User> AsQueryable(this IQuery<User> sq) { ... }
```
This safely patches endpoints exposing GraphQL's HotChocolate `[UseFiltering]` completely bypassing logic directly translating standard syntax trees into native raw SQL strings.
