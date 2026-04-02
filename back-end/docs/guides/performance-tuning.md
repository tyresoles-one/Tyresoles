# Performance Tuning

To achieve **Ultimate Speed**, strictly map logic back to C# `ValueTypes` bypassing Heap Gen0 overhead bounds directly! Here are 3 primary architectural rules specifically required for `Tyresoles.Sql`

## 1. Optimize Reflection Out Completely
Reflection caches statically inside `EntityMetadata<T>` running exactly one execution branch during its very first invocation ensuring Table and Column names map into memory statically without GC hits per query request. Ensure you do not instantiate dynamic instances heavily. Use generators strictly to emit `UserMapper.g.cs`.

## 2. Ensure Connection Pooling
Instead of instantiating `Dataverse` manually, rely heavily on `.NET` Dependency Injection scopes enforcing `IDbConnectionFactory`.
`TenantScope` explicitly resolves an existing ADO.NET pool. Do not use `.Dispose()` closures nested inside internal classes.

## 3. Opt for StreamAsync for Large Collections
Because ADO.NET resolves `ExecuteReaderAsync` via massive string memory arrays:
- **Avoid `.ToArrayAsync()`**: Buffers literally the entire payload straight into Gen 2 Heap chunks.
- **Do This**: 
```csharp
await foreach(var usr in scope.StreamAsync(query)) 
{
    // Write purely to Response Stream directly 
}
```
