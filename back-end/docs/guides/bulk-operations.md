# Bulk Operations

Standard line-by-line `.InsertAsync()` operations are slow due to DB transaction overhead per row. `Tyresoles.Sql` provides optimized extension methods bridging straight across to ADO.NET using `sp_executesql` batch streams natively via `Dapper`.

## `ModifyRangeAsync`
Safely injects massive `100,000+` updates in a single bound pipeline RPC network call.

### 1. Requirements
Ensure your target POCO is mapped accurately identifying exactly its primary key via `[NavKey]` so the builder dynamically configures the `WHERE` closure sequentially inside the batch tree.

```csharp
[NavTable("Vendor")]
[NavKey("No_")]
public class Vendor {
   public string No { get; set; } = string.Empty;
   public string Name { get; set; } = string.Empty;
}
```

### 2. Execution Scenario

```csharp
using Tyresoles.Sql.Dialects.NavDialect;

public class SyncService {
    private readonly IDataverse _db;
    public SyncService(IDataverse db) => _db = db;
    
    public async Task BulkSyncVendorsAsync(IEnumerable<Vendor> newVendors, CancellationToken ct) {
        // Enforce the specific scope schema (Tenant)
        // Dialect automatically routes specific IDialect bulk formatting parameters.
        using var scope = _db.ForTenant("NavLive").NavCompany("CRONUS");

        // Triggers single Dapper connection pool pipeline avoiding 100,000 internal network jumps
        await scope.ModifyRangeAsync(newVendors, ct); 
    }
}
```
