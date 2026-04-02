using System.Collections.Concurrent;
using Tyresoles.Sql.Abstractions;

namespace Tyresoles.Sql.Core;

/// <summary>
/// Manages concurrent query execution across multiple TenantScope instances.
/// Each parallel task gets its own DB connection for true parallelism.
/// Bounded by a semaphore to prevent connection pool exhaustion.
/// </summary>
public sealed class ParallelQueryScope : IAsyncDisposable
{
    private readonly IDataverse _dataverse;
    private readonly string _tenantKey;
    private readonly SemaphoreSlim _semaphore;
    private readonly ConcurrentBag<ITenantScope> _scopes = new();

    public ParallelQueryScope(IDataverse dataverse, string tenantKey, int maxParallelism = 8)
    {
        _dataverse = dataverse ?? throw new ArgumentNullException(nameof(dataverse));
        _tenantKey = tenantKey ?? throw new ArgumentNullException(nameof(tenantKey));
        _semaphore = new SemaphoreSlim(maxParallelism, maxParallelism);
    }

    /// <summary>Execute a query on a dedicated scope (its own connection).</summary>
    public async Task<T> RunAsync<T>(Func<ITenantScope, Task<T>> work, CancellationToken ct = default)
    {
        await _semaphore.WaitAsync(ct);
        var scope = _dataverse.ForTenant(_tenantKey);
        _scopes.Add(scope);
        try
        {
            return await work(scope);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    /// <summary>Execute a void operation on a dedicated scope.</summary>
    public async Task RunAsync(Func<ITenantScope, Task> work, CancellationToken ct = default)
    {
        await _semaphore.WaitAsync(ct);
        var scope = _dataverse.ForTenant(_tenantKey);
        _scopes.Add(scope);
        try
        {
            await work(scope);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    /// <summary>Execute multiple operations in parallel, each on its own scope. Returns all results.</summary>
    public async Task<T[]> RunAllAsync<T>(
        IEnumerable<Func<ITenantScope, Task<T>>> operations,
        CancellationToken ct = default)
    {
        var tasks = operations.Select(op => RunAsync(op, ct));
        return await Task.WhenAll(tasks);
    }

    /// <summary>Execute two independent operations in parallel, return both results.</summary>
    public async Task<(T1, T2)> RunAsync<T1, T2>(
        Func<ITenantScope, Task<T1>> work1,
        Func<ITenantScope, Task<T2>> work2,
        CancellationToken ct = default)
    {
        var t1 = RunAsync(work1, ct);
        var t2 = RunAsync(work2, ct);
        await Task.WhenAll(t1, t2);
        return (t1.Result, t2.Result);
    }

    /// <summary>Execute three independent operations in parallel, return all results.</summary>
    public async Task<(T1, T2, T3)> RunAsync<T1, T2, T3>(
        Func<ITenantScope, Task<T1>> work1,
        Func<ITenantScope, Task<T2>> work2,
        Func<ITenantScope, Task<T3>> work3,
        CancellationToken ct = default)
    {
        var t1 = RunAsync(work1, ct);
        var t2 = RunAsync(work2, ct);
        var t3 = RunAsync(work3, ct);
        await Task.WhenAll(t1, t2, t3);
        return (t1.Result, t2.Result, t3.Result);
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var scope in _scopes)
        {
            try { await scope.DisposeAsync(); }
            catch { /* best effort cleanup */ }
        }
        _semaphore.Dispose();
    }
}
