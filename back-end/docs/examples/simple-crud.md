# Simple CRUD Example

A minimal web API endpoint built utilizing the Tyresoles.Sql fluent builder completely side-by-side with Minimal APIs natively.

### 1. Model Definition
```csharp
[NavTable("Employees")]
public class Employee {
    [NavKey] public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = string.Empty;
    public decimal Salary { get; set; }
}
```

### 2. Endpoints Registration (`Program.cs`)
```csharp
var app = builder.Build();

app.MapGet("/api/staff", async (IDataverse db) => {
    using var scope = db.DefaultTenant;
    return Results.Ok(await scope.Query<Employee>().ToArrayAsync());
});

app.MapPost("/api/staff", async (Employee emp, IDataverse db) => {
    using var scope = db.DefaultTenant;
    await scope.InsertAsync(emp);
    return Results.Created($"/api/staff/{emp.Id}", emp);
});

app.MapPut("/api/staff/{id}", async (string id, Employee emp, IDataverse db) => {
    using var scope = db.DefaultTenant;
    emp.Id = id; // Ensure Key is strictly bound natively onto the object
    await scope.UpdateAsync(emp);
    return Results.NoContent();
});

app.MapDelete("/api/staff/{id}", async (string id, IDataverse db) => {
    using var scope = db.DefaultTenant;
    await scope.ExecuteNonQueryAsync("DELETE FROM [Employees] WHERE [Id] = @Id", new { Id = id });
    return Results.NoContent();
});

app.Run();
```
