using System.Data;
using System.Data.Common;
using System.Linq.Expressions;

namespace Tyresoles.Sql.Abstractions;

public interface IDataverse
{
    ITenantScope ForTenant(string tenantKey);
    ITenantScope DefaultTenant { get; }
}

public interface ITenantScope : IDisposable, IAsyncDisposable
{
    string TenantKey { get; }
    IDialect Dialect { get; }
    IQuery<T> Query<T>() where T : class;
    ValueTask<T[]> ToArrayAsync<T>(IQuery<T> query, CancellationToken ct = default) where T : class;
    IAsyncEnumerable<T> StreamAsync<T>(IQuery<T> query, CancellationToken ct = default) where T : class;
    ITenantScope WithCompany(string companyCode);

    IMultipleQuery CreateMultipleQuery();
    
    // Write Operations
    ValueTask InsertAsync<T>(T entity, CancellationToken ct = default) where T : class;
    ValueTask BulkInsertAsync<T>(IEnumerable<T> entities, CancellationToken ct = default) where T : class;
    ValueTask UpdateAsync<T>(T entity, CancellationToken ct = default) where T : class;
    ValueTask UpdateAsync<T>(T entity, params Expression<Func<T, object>>[] selectors) where T : class;
    ValueTask UpdateAsync<T>(T entity, Expression<Func<T, object>>[] selectors, CancellationToken ct = default) where T : class;
    ValueTask DeleteAsync<T>(T entity, CancellationToken ct = default) where T : class;
    ValueTask DeleteWhereAsync<T>(Expression<Func<T, bool>> predicate, CancellationToken ct = default) where T : class;
    ValueTask UpsertAsync<T>(T entity, CancellationToken ct = default) where T : class;
    
    // Transactions
    ValueTask<IDbTransaction> BeginTransactionAsync(CancellationToken ct = default);
    ValueTask<IDbTransaction> BeginTransactionAsync(IsolationLevel isolationLevel, CancellationToken ct = default);
    
    // Low-Level (Powered by Dapper)
    ValueTask<TResult?> ExecuteScalarAsync<TResult>(string sql, object? parameters = null, CancellationToken ct = default);
    ValueTask<int> ExecuteNonQueryAsync(string sql, object? parameters = null, CancellationToken ct = default);
    ValueTask<IEnumerable<T>> QueryAsync<T>(string sql, object? parameters = null, CancellationToken ct = default);
    
    /// <summary>Execute raw SQL and return results as an async stream.</summary>
    IAsyncEnumerable<T> StreamRawAsync<T>(string sql, object? parameters = null, CancellationToken ct = default) where T : class;
    /// <summary>Execute raw SQL and return all results as an array.</summary>
    ValueTask<T[]> RawQueryToArrayAsync<T>(string sql, object? parameters = null, CancellationToken ct = default) where T : class;

    /// <summary>Resolve a NAV table name to the qualified SQL identifier (schema and company prefix).</summary>
    string GetQualifiedTableName(string tableName, bool isShared = false);
}

public interface IDbCommandInterceptor
{
    ValueTask OnBeforeExecuteAsync(DbCommand command, CancellationToken ct);
}

/// <summary>Optional: transform parameters before the command is created so params are created with correct types from the start.</summary>
public interface IParameterPreprocessor
{
    IReadOnlyDictionary<string, object>? PreProcessParameters(string sql, IReadOnlyDictionary<string, object> parameters);
}

public interface IQuery<T> where T : class
{
    IQuery<T> Where(Expression<Func<T, bool>> predicate);
    IQuery<T> Where(string sql, object? parameters = null);
    IQuery<T> Where<U>(IQuery<U> subquery, bool exists = true) where U : class;
    IQuery<T> Where<U>(Expression<Func<T, object>> selector, IQuery<U> subquery, SubqueryOperator op = SubqueryOperator.In) where U : class;
    IQuery<T> With<U>(string alias, IQuery<U> cteQuery) where U : class;
    IQuery<T> Union(IQuery<T> other);
    IQuery<T> UnionAll(IQuery<T> other);
    IQuery<TResult> Select<TResult>(Expression<Func<T, TResult>> selector) where TResult : class;
    IQuery<T> SelectRaw(string sqlFragment, object? parameters = null);
    IQuery<T> Distinct();
    
    // Joins
    IQuery<TResult> Join<U, TResult>(Expression<Func<T, object>> tKey, Expression<Func<U, object>> uKey, Expression<Func<JoinQuery<T, U>, TResult>> selector, JoinType type = JoinType.Inner) where U : class where TResult : class;
    
    // Ordering
    IQuery<T> OrderBy<TKey>(Expression<Func<T, TKey>> key);
    IQuery<T> OrderByDescending<TKey>(Expression<Func<T, TKey>> key);
    IQuery<T> ThenBy<TKey>(Expression<Func<T, TKey>> key);
    IQuery<T> ThenByDescending<TKey>(Expression<Func<T, TKey>> key);
    
    // Execution
    IQuery<T> Skip(int count);
    IQuery<T> Take(int count);
    IQuery<T> Offset(int count);
    IQuery<T> Limit(int count);
    IPagedQuery<T> Page(string? cursor, int pageSize);
    IPagedQuery<T> KeysetPage<TKey>(Expression<Func<T, TKey>> keySelector, TKey lastValue, int pageSize);
    ValueTask<T[]> ToArrayAsync(CancellationToken ct = default);
    IAsyncEnumerable<T> AsAsyncEnumerable();
    ValueTask<T?> FirstOrDefaultAsync(CancellationToken ct = default);
    ValueTask<int> CountAsync(CancellationToken ct = default);
    ValueTask<bool> AnyAsync(CancellationToken ct = default);
    
    // Aggregates
    IQuery<IGroupResult<TKey, T>> GroupBy<TKey>(Expression<Func<T, TKey>> key);
    IQuery<T> HavingRaw(string sql, object? parameters = null);

    ValueTask<TResult> SumAsync<TResult>(Expression<Func<T, TResult>> selector, CancellationToken ct = default);
    ValueTask<TResult> AverageAsync<TResult>(Expression<Func<T, TResult>> selector, CancellationToken ct = default);
    ValueTask<TResult> MinAsync<TResult>(Expression<Func<T, TResult>> selector, CancellationToken ct = default);
    ValueTask<TResult> MaxAsync<TResult>(Expression<Func<T, TResult>> selector, CancellationToken ct = default);
    
    // Raw SQL
    QueryDescriptor Build();
}

public enum SubqueryOperator { In, NotIn, Equals, NotEquals, GreaterThan, LessThan, GreaterThanOrEqual, LessThanOrEqual }
public enum JoinType { Inner, Left, Right, Full }

public interface IPagedQuery<T> where T : class
{
    ValueTask<PagedResult<T>> ExecuteAsync(CancellationToken ct = default);
}

public interface IMultipleQuery
{
    IMultipleQuery Add<T>(IQuery<T> query) where T : class;
    /// <summary>Add a raw SQL statement to the batch. Dapper maps results to T.</summary>
    IMultipleQuery AddRaw<T>(string sql, object? parameters = null) where T : class;
    ValueTask<Tuple<T1[], T2[]>> ExecuteAsync<T1, T2>(CancellationToken ct = default) where T1 : class where T2 : class;
    ValueTask<Tuple<T1[], T2[], T3[]>> ExecuteAsync<T1, T2, T3>(CancellationToken ct = default) where T1 : class where T2 : class where T3 : class;
    ValueTask<Tuple<T1[], T2[], T3[], T4[]>> ExecuteAsync<T1, T2, T3, T4>(CancellationToken ct = default) where T1 : class where T2 : class where T3 : class where T4 : class;
    ValueTask<Tuple<T1[], T2[], T3[], T4[], T5[]>> ExecuteAsync<T1, T2, T3, T4, T5>(CancellationToken ct = default) where T1 : class where T2 : class where T3 : class where T4 : class where T5 : class;
}
