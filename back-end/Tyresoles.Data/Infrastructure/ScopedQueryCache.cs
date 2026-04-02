using System.Collections.Concurrent;

namespace Tyresoles.Data.Infrastructure;

/// <summary>
/// Per-request cache for expensive lookups (table name resolution, reference data, etc.).
/// Register as Scoped in DI — one instance per HTTP request.
/// Thread-safe for concurrent access from ParallelQueryScope.
/// </summary>
public sealed class ScopedQueryCache
{
    private readonly ConcurrentDictionary<string, object> _cache = new();

    /// <summary>Get a cached value or compute and cache it (async).</summary>
    public async Task<T> GetOrAddAsync<T>(string key, Func<Task<T>> factory)
    {
        if (_cache.TryGetValue(key, out var cached))
            return (T)cached;
        var result = await factory();
        _cache.TryAdd(key, result!);
        return result;
    }

    /// <summary>Get a cached value or compute and cache it (sync).</summary>
    public T GetOrAdd<T>(string key, Func<T> factory)
    {
        if (_cache.TryGetValue(key, out var cached))
            return (T)cached;
        var result = factory();
        _cache.TryAdd(key, result!);
        return result;
    }

    /// <summary>Try to get a cached value.</summary>
    public bool TryGet<T>(string key, out T value)
    {
        if (_cache.TryGetValue(key, out var cached))
        {
            value = (T)cached;
            return true;
        }
        value = default!;
        return false;
    }

    /// <summary>Explicitly set a cache entry.</summary>
    public void Set<T>(string key, T value)
    {
        _cache[key] = value!;
    }
}
