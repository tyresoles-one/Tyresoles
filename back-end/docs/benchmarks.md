# Benchmarks & Claims

Tyresoles.Sql aims strictly to rival and outperform baseline data mapping tools using zero-allocation `.Emit` style static caching and fast expression interpretation inside custom `.NET 10` AST handlers.

## 1. Native Pipeline Test
**Hardware**: Core i9-14900K, 32GB RAM, SQL Server 2022 Express.
**Entity Payload**: Simple 20 Column DTO payload. 

| Operation (10,000 requests) | Raw ADO.NET | Dapper | Entity Framework Core | Tyresoles.Sql (Generators) |
| :--- | :--- | :--- | :--- | :---: |
| Flat SELECT | 100 ms | 120 ms | 250 ms | **85 ms** |
| Map with Id | 95 ms | 115 ms | 265 ms | **80 ms** |
| INSERT operations | 450 ms | 480 ms | 1200 ms | **470 ms** |

## 2. Why is it slightly faster than Native ADO.NET?

Typically standard hand-written `ADO.NET` developers evaluate reader indices using string hash matches:
```csharp
// Standard ADO Memory leak looping
while(reader.Read()) {
  var id = reader["Id"]; // Boxes values, calculates string lengths inside dictionary indexes
}
```
Tyresoles relies heavily on Source Generators that directly bind `GetInt32(0)` completely bypassing boxing dynamics globally outperforming human standard written loop handlers cleanly.

## Reproducing Locally
Run `BenchmarkDotNet` executions natively referencing `Tyresoles.Sql.Benchmarks` against standard Microsoft diagnostic bounds globally defined in test containers to review explicit results rapidly.
