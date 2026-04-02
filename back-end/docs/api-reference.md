# API Reference

Complete documentation of all public interfaces in `Tyresoles.Sql`.

## `IDataverse`
The master connection orchestrator.

```csharp
/// Resolves a configured database environment (Tenant).
ITenantScope ForTenant(string tenantKey);

/// Resolves the Primary/Default environment.
ITenantScope DefaultTenant { get; }
```

## `ITenantScope`
Represents a lightweight, thread-safe execution scope over a database connection. Automatically routes standard SQL down to dialect-specific commands (`DbProvider.SqlServer` vs `DbProvider.PostgreSQL`).

### Read Operations
```csharp
// Instantiates a fluent query builder for the specific entity.
IQuery<T> Query<T>() where T : class;

// Materializes a query rapidly into a Memory Array.
Task<T[]> ToArrayAsync<T>(IQuery<T> query, CancellationToken ct = default);

// Executes a memory-efficient stream using IAsyncEnumerable.
IAsyncEnumerable<T> StreamAsync<T>(IQuery<T> query, CancellationToken ct = default);
```

### Write Operations
```csharp
// Inserts standard properties mapping keys and NavColumns. 
Task InsertAsync<T>(T entity, CancellationToken ct = default);

// Updates record based on its [NavKey] attribute definitions.
Task UpdateAsync<T>(T entity, CancellationToken ct = default);

// Deletes strictly using the primary [NavKey].
Task DeleteAsync<T>(T entity, CancellationToken ct = default);
```

### Advanced (Dapper-backed) Execution
```csharp
// Explicit transaction wrapping.
Task<IDbTransaction> BeginTransactionAsync(CancellationToken ct = default);

Task<int> ExecuteNonQueryAsync(string sql, object? parameters = null, CancellationToken ct = default);
Task<TResult?> ExecuteScalarAsync<TResult>(string sql, object? parameters = null, CancellationToken ct = default);
Task<IEnumerable<T>> QueryAsync<T>(string sql, object? parameters = null, CancellationToken ct = default);
```

## `IQuery<T>`
An AST (Abstract Syntax Tree) expression builder targeting Native SQL.

### Filtering & Shaping
```csharp
IQuery<T> Where(Expression<Func<T, bool>> predicate);
IQuery<T> WhereRaw(string sql, object? parameters);
IQuery<TResult> Select<TResult>(Expression<Func<T, TResult>> selector);
IQuery<T> Distinct();
IQuery<TResult> Join<U, TResult>(Expression<Func<T, object>> tKey, Expression<Func<U, object>> uKey, Expression<Func<JoinQuery<T, U>, TResult>> selector, JoinType type = JoinType.Inner);
```

### Sorting & Grouping
```csharp
IQuery<T> OrderBy<TKey>(Expression<Func<T, TKey>> key);
IQuery<T> OrderByDescending<TKey>(Expression<Func<T, TKey>> key);
IQuery<IGroupResult<TKey, T>> GroupBy<TKey>(Expression<Func<T, TKey>> key);
```

### Terminal Executions
```csharp
Task<T[]> ToArrayAsync(CancellationToken ct = default);
Task<T?> FirstOrDefaultAsync(CancellationToken ct = default);
Task<int> CountAsync(CancellationToken ct = default);
Task<bool> AnyAsync(CancellationToken ct = default);
IPagedQuery<T> Page(string? cursor, int pageSize);
```

## `NavCompanyExtensions` (NAV Specific)
Automatically mutates the queries inside the scope to execute against `[CompanyName$TableName]`.

```csharp
public static ITenantScope NavCompany(this IDataverse dataverse, string companyName, string tenant = "NavLive");
```

## `NavFilterParserExtensions` (NAV Specific)
Interprets classical Dynamics NAV specific syntaxes explicitly bypassing SQL Injection vulnerabilities directly into AST parameters.

```csharp
public static IQuery<T> Filter<T>(this IQuery<T> query, string navFilterStr);
// db.Query<Customer>().Filter("BLOCKED=FILTER(<>1|2)") 
```
