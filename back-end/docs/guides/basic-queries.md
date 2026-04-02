# Basic Queries

Tyresoles.Sql converts strongly-typed C# expressions smoothly down into native dialect SQL.

## 1. Simple Selection
Fetch all records into an array:
```csharp
var users = await db.DefaultTenant
    .Query<User>()
    .ToArrayAsync();
```

## 2. Filtering (`Where` and `WhereRaw`)
Filtering is strictly parameterized automatically.
```csharp
string targetCity = "Mumbai";
var users = await db.DefaultTenant
    .Query<User>()
    .Where(u => u.City == targetCity && u.IsActive)
    .ToArrayAsync();
// Emits SQL: WHERE [City] = @p0 AND [IsActive] = 1
```

For ultra-complex conditions out of standard LINQ bounds, pass exact raw clauses securely using dynamically mapped dictionaries:
```csharp
var users = await db.DefaultTenant
    .Query<User>()
    .WhereRaw("LEN([UserName]) > @len", new Dictionary<string, object> { { "len", 5 } })
    .ToArrayAsync();
```

## 3. Selecting Specific Columns
Lower network bandwidth by exclusively returning targeted memory columns anonymously:
```csharp
var slimUsers = await db.DefaultTenant
    .Query<User>()
    .Select(u => new { u.Id, u.UserName })
    .ToArrayAsync();
```

## 4. Sorting Data
Chain `OrderBy` properties continuously:
```csharp
var topUsers = await db.DefaultTenant
    .Query<User>()
    .OrderBy(u => u.Department)
    .ThenByDescending(u => u.CreatedAt)
    .ToArrayAsync();
```

## 5. First or Default
```csharp
var admin = await db.DefaultTenant
    .Query<User>()
    .Where(u => u.Role == "Admin")
    .FirstOrDefaultAsync();
```
