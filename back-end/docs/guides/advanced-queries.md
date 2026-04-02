# Advanced Queries

## Joins
You can join tables fluently. `Tyresoles.Sql` uses `JoinQuery<TLeft, TRight>` abstractions to help you anonymously project merged outcomes seamlessly.

```csharp
var customerOrders = await db.DefaultTenant
    .Query<Customer>()
    .Join<Order, HeaderDto>(
        c => c.Id,                // Left Key (Customer)
        o => o.CustomerId,        // Right Key (Order)
        j => new HeaderDto { 
            CustomerName = j.Left.Name, 
            OrderTotal = j.Right.Amount 
        },
        JoinType.Left              // Type
    )
    .Where(result => result.OrderTotal > 1000)
    .ToArrayAsync();
```

## Pagination
Pagination operates strictly cursor or page-aware via `OFFSET FETCH` natively on the server dialect.

```csharp
var pageRequest = await db.DefaultTenant
    .Query<Customer>()
    .OrderBy(c => c.Name) // Pagination absolutely requires an explicit order
    .Page(cursor: "0", pageSize: 50)
    .ExecuteAsync();

foreach(var cust in pageRequest.Items) {
    Console.WriteLine(cust.Name);
}

if (pageRequest.HasNextPage) {
    // pageRequest.NextCursor contains "50"
}
```

## Execution Streaming (IAsyncEnumerable)
For huge multi-gigabyte queries where Arrays will trigger `OutOfMemoryException`, use Native Streaming directly out of the `DbDataReader`:

```csharp
var stream = db.DefaultTenant
    .Query<Logs>()
    .Where(l => l.Severity == "Error")
    .AsAsyncEnumerable();

// Memory stays completely flat:
await foreach(var log in stream) {
    await WriteToFileAsync(log); 
}
```

## Simple Aggregates
```csharp
var isActive = await db.DefaultTenant.Query<User>().AnyAsync();
var totalSales = await db.DefaultTenant.Query<Invoice>().SumAsync(x => x.Amount);
var maxDiscount = await db.DefaultTenant.Query<Invoice>().MaxAsync(x => x.Discount);
```
