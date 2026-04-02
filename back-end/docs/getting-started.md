# Getting Started with Tyresoles.Sql

## Installation

Install the core library via the .NET CLI:
```bash
dotnet add package Tyresoles.Sql
```

To enable zero-overhead memory mappers, install the Source Generator analyzer:
```bash
dotnet add package Tyresoles.Sql.Generators --prerelease
```

## Configuration (Dependency Injection)

Register the `Dataverse` layer securely inside your `Program.cs` or `Startup.cs`.

```csharp
using Tyresoles.Sql.Core;
using Tyresoles.Sql.Core.Configuration;
using Tyresoles.Sql.Abstractions;

builder.Services.Configure<DataverseOptions>(options =>
{
    options.DefaultTenantKey = "NavLive";
    options.Tenants.Add("NavLive", new TenantConfiguration 
    { 
        ConnectionString = builder.Configuration.GetConnectionString("NavDb")!,
        Provider = DbProvider.SqlServer,
        DefaultCompany = "CRONUS"
    });
    
    options.Tenants.Add("AppDb", new TenantConfiguration 
    { 
        ConnectionString = builder.Configuration.GetConnectionString("PgDb")!,
        Provider = DbProvider.PostgreSQL
    });
});

builder.Services.AddSingleton<IDbConnectionFactory, SqlConnectionFactory>();
builder.Services.AddSingleton<IDataverse, Dataverse>();
```

## Your First Query

Inject `IDataverse` and spawn a `TenantScope` to execute thread-safe queries:

```csharp
public class UserService 
{
    private readonly IDataverse _db;
    public UserService(IDataverse db) => _db = db;

    public async Task<User[]> GetActiveUsersAsync(CancellationToken ct = default) 
    {
        // Spawns a lightweight scope connection automatically choosing the PostgreSQL dialect
        using var scope = _db.ForTenant("AppDb");
        
        return await scope.Query<User>()
             .Where(u => u.IsActive)
             .OrderByDescending(u => u.CreatedAt)
             .ToArrayAsync(ct);
    }
}
```

## Source Generator Setup
Ensure your models are correctly decorated. The Source Generator watches strictly for `[NavTable]`:

```csharp
[NavTable("User", IsShared = true)] // IsShared prevents Company prefixing
public class User 
{
    [NavKey] 
    public string Id { get; set; }

    [NavColumn("User Name")] 
    public string UserName { get; set; }
}
```
The generator automatically runs silently in the background, emitting `UserMapper.g.cs` and translating ADO.NET arrays directly into `User` structs without using slower Reflection.
