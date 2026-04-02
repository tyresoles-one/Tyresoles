using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Tyresoles.Sql.Abstractions;
using Tyresoles.Sql.Core.Materialization;

namespace Tyresoles.Sql.Core.Query;

/// <summary>Represents a raw SQL query entry for the batch.</summary>
internal sealed class RawQueryEntry : IQueryInternal
{
    private readonly string _sql;
    private readonly object? _parameters;

    public RawQueryEntry(string sql, object? parameters)
    {
        _sql = sql;
        _parameters = parameters;
    }

    public bool HasJoins => false;

    public QueryDescriptor Build(ParameterContext context, string? parameterPrefix)
    {
        // For raw SQL, we need to remap parameter names with prefix to avoid collisions
        var finalSql = _sql;
        if (_parameters != null && !string.IsNullOrEmpty(parameterPrefix))
        {
            var props = _parameters.GetType().GetProperties();
            foreach (var prop in props)
            {
                var val = prop.GetValue(_parameters);
                var prefixedName = $"{parameterPrefix}_{prop.Name}";
                context.Parameters[prefixedName] = val ?? DBNull.Value;
                // Replace @paramName with @prefix_paramName in SQL
                finalSql = finalSql.Replace("@" + prop.Name, "@" + prefixedName, StringComparison.OrdinalIgnoreCase);
            }
            // Also handle Dictionary<string, object> parameters
            if (_parameters is IDictionary<string, object> dict)
            {
                foreach (var kv in dict)
                {
                    var prefixedName = $"{parameterPrefix}_{kv.Key}";
                    if (!context.Parameters.ContainsKey(prefixedName))
                        context.Parameters[prefixedName] = kv.Value ?? DBNull.Value;
                    finalSql = finalSql.Replace("@" + kv.Key, "@" + prefixedName, StringComparison.OrdinalIgnoreCase);
                }
            }
        }
        else if (_parameters != null)
        {
            // No prefix — add params directly
            if (_parameters is IDictionary<string, object> dict)
            {
                foreach (var kv in dict)
                    context.Parameters[kv.Key] = kv.Value ?? DBNull.Value;
            }
            else
            {
                var props = _parameters.GetType().GetProperties();
                foreach (var prop in props)
                    context.Parameters[prop.Name] = prop.GetValue(_parameters) ?? DBNull.Value;
            }
        }

        return new QueryDescriptor { Sql = finalSql };
    }
}

internal class MultipleQuery : IMultipleQuery
{
    private readonly TenantScope _scope;
    private readonly List<IQueryInternal> _queries = new();
    
    public MultipleQuery(TenantScope scope)
    {
        _scope = scope;
    }
    
    public IMultipleQuery Add<T>(IQuery<T> query) where T : class
    {
        if (query is not IQueryInternal qi)
            throw new ArgumentException("Query must be the internal implementation.", nameof(query));
        _queries.Add(qi);
        return this;
    }

    public IMultipleQuery AddRaw<T>(string sql, object? parameters = null) where T : class
    {
        _queries.Add(new RawQueryEntry(sql, parameters));
        return this;
    }

    public async ValueTask<Tuple<T1[], T2[]>> ExecuteAsync<T1, T2>(CancellationToken ct = default) where T1 : class where T2 : class
    {
        if (_queries.Count != 2) throw new InvalidOperationException("Add exactly 2 queries before calling ExecuteAsync<T1, T2>");
        return await ExecuteWithMaterializerAsync<T1, T2>(ct);
    }

    public async ValueTask<Tuple<T1[], T2[], T3[]>> ExecuteAsync<T1, T2, T3>(CancellationToken ct = default) where T1 : class where T2 : class where T3 : class
    {
        if (_queries.Count != 3) throw new InvalidOperationException("Add exactly 3 queries before calling ExecuteAsync<T1, T2, T3>");
        return await ExecuteWithMaterializerAsync<T1, T2, T3>(ct);
    }

    public async ValueTask<Tuple<T1[], T2[], T3[], T4[]>> ExecuteAsync<T1, T2, T3, T4>(CancellationToken ct = default) where T1 : class where T2 : class where T3 : class where T4 : class
    {
        if (_queries.Count != 4) throw new InvalidOperationException("Add exactly 4 queries before calling ExecuteAsync<T1, T2, T3, T4>");
        return await ExecuteWithMaterializerAsync<T1, T2, T3, T4>(ct);
    }

    public async ValueTask<Tuple<T1[], T2[], T3[], T4[], T5[]>> ExecuteAsync<T1, T2, T3, T4, T5>(CancellationToken ct = default) where T1 : class where T2 : class where T3 : class where T4 : class where T5 : class
    {
        if (_queries.Count != 5) throw new InvalidOperationException("Add exactly 5 queries before calling ExecuteAsync<T1, T2, T3, T4, T5>");
        return await ExecuteWithMaterializerAsync<T1, T2, T3, T4, T5>(ct);
    }

    /// <summary>Reads the current result set using the Materializer so [NavColumn] (e.g. No_) maps correctly to properties (e.g. No).</summary>
    private static async Task<T[]> ReadResultSetAsync<T>(DbDataReader reader, CancellationToken ct) where T : class
    {
        var parser = Materializer.GetParser<T>(reader);
        var list = new List<T>();
        while (await reader.ReadAsync(ct))
            list.Add(parser(reader));
        return list.ToArray();
    }

    private async ValueTask<Tuple<T1[], T2[]>> ExecuteWithMaterializerAsync<T1, T2>(CancellationToken ct) where T1 : class where T2 : class
    {
        var (sql, paramContext) = BuildMergedSql();
        _scope.LogQuery("MultipleQuery", sql, paramContext.Parameters);
        try
        {
            var conn = _scope.GetConnection();
            if (conn.State != ConnectionState.Open) await conn.OpenAsync(ct);
            using var cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            if (_scope.CommandTimeoutSeconds is int timeout) cmd.CommandTimeout = timeout;
            foreach (var kv in paramContext.Parameters)
            {
                var p = cmd.CreateParameter();
                p.ParameterName = kv.Key;
                p.Value = kv.Value ?? DBNull.Value;
                cmd.Parameters.Add(p);
            }
            using var reader = (DbDataReader)await cmd.ExecuteReaderAsync(CommandBehavior.SequentialAccess, ct);
            var res1 = await ReadResultSetAsync<T1>(reader, ct);
            await reader.NextResultAsync(ct);
            var res2 = await ReadResultSetAsync<T2>(reader, ct);
            return Tuple.Create(res1, res2);
        }
        catch (Exception ex)
        {
            _scope.LogQueryError("MultipleQuery", sql, paramContext.Parameters, ex);
            throw;
        }
    }

    private async ValueTask<Tuple<T1[], T2[], T3[]>> ExecuteWithMaterializerAsync<T1, T2, T3>(CancellationToken ct) where T1 : class where T2 : class where T3 : class
    {
        var (sql, paramContext) = BuildMergedSql();
        _scope.LogQuery("MultipleQuery", sql, paramContext.Parameters);
        try
        {
            var conn = _scope.GetConnection();
            if (conn.State != ConnectionState.Open) await conn.OpenAsync(ct);
            using var cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            if (_scope.CommandTimeoutSeconds is int timeout) cmd.CommandTimeout = timeout;
            foreach (var kv in paramContext.Parameters)
            {
                var p = cmd.CreateParameter();
                p.ParameterName = kv.Key;
                p.Value = kv.Value ?? DBNull.Value;
                cmd.Parameters.Add(p);
            }
            using var reader = (DbDataReader)await cmd.ExecuteReaderAsync(CommandBehavior.SequentialAccess, ct);
            var res1 = await ReadResultSetAsync<T1>(reader, ct);
            await reader.NextResultAsync(ct);
            var res2 = await ReadResultSetAsync<T2>(reader, ct);
            await reader.NextResultAsync(ct);
            var res3 = await ReadResultSetAsync<T3>(reader, ct);
            return Tuple.Create(res1, res2, res3);
        }
        catch (Exception ex)
        {
            _scope.LogQueryError("MultipleQuery", sql, paramContext.Parameters, ex);
            throw;
        }
    }

    private async ValueTask<Tuple<T1[], T2[], T3[], T4[]>> ExecuteWithMaterializerAsync<T1, T2, T3, T4>(CancellationToken ct) where T1 : class where T2 : class where T3 : class where T4 : class
    {
        var (sql, paramContext) = BuildMergedSql();
        _scope.LogQuery("MultipleQuery", sql, paramContext.Parameters);
        try
        {
            var conn = _scope.GetConnection();
            if (conn.State != ConnectionState.Open) await conn.OpenAsync(ct);
            using var cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            if (_scope.CommandTimeoutSeconds is int timeout) cmd.CommandTimeout = timeout;
            foreach (var kv in paramContext.Parameters)
            {
                var p = cmd.CreateParameter();
                p.ParameterName = kv.Key;
                p.Value = kv.Value ?? DBNull.Value;
                cmd.Parameters.Add(p);
            }
            using var reader = (DbDataReader)await cmd.ExecuteReaderAsync(CommandBehavior.SequentialAccess, ct);
            var res1 = await ReadResultSetAsync<T1>(reader, ct);
            await reader.NextResultAsync(ct);
            var res2 = await ReadResultSetAsync<T2>(reader, ct);
            await reader.NextResultAsync(ct);
            var res3 = await ReadResultSetAsync<T3>(reader, ct);
            await reader.NextResultAsync(ct);
            var res4 = await ReadResultSetAsync<T4>(reader, ct);
            return Tuple.Create(res1, res2, res3, res4);
        }
        catch (Exception ex)
        {
            _scope.LogQueryError("MultipleQuery", sql, paramContext.Parameters, ex);
            throw;
        }
    }

    private async ValueTask<Tuple<T1[], T2[], T3[], T4[], T5[]>> ExecuteWithMaterializerAsync<T1, T2, T3, T4, T5>(CancellationToken ct) where T1 : class where T2 : class where T3 : class where T4 : class where T5 : class
    {
        var (sql, paramContext) = BuildMergedSql();
        _scope.LogQuery("MultipleQuery", sql, paramContext.Parameters);
        try
        {
            var conn = _scope.GetConnection();
            if (conn.State != ConnectionState.Open) await conn.OpenAsync(ct);
            using var cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            if (_scope.CommandTimeoutSeconds is int timeout) cmd.CommandTimeout = timeout;
            foreach (var kv in paramContext.Parameters)
            {
                var p = cmd.CreateParameter();
                p.ParameterName = kv.Key;
                p.Value = kv.Value ?? DBNull.Value;
                cmd.Parameters.Add(p);
            }
            using var reader = (DbDataReader)await cmd.ExecuteReaderAsync(CommandBehavior.SequentialAccess, ct);
            var res1 = await ReadResultSetAsync<T1>(reader, ct);
            await reader.NextResultAsync(ct);
            var res2 = await ReadResultSetAsync<T2>(reader, ct);
            await reader.NextResultAsync(ct);
            var res3 = await ReadResultSetAsync<T3>(reader, ct);
            await reader.NextResultAsync(ct);
            var res4 = await ReadResultSetAsync<T4>(reader, ct);
            await reader.NextResultAsync(ct);
            var res5 = await ReadResultSetAsync<T5>(reader, ct);
            return Tuple.Create(res1, res2, res3, res4, res5);
        }
        catch (Exception ex)
        {
            _scope.LogQueryError("MultipleQuery", sql, paramContext.Parameters, ex);
            throw;
        }
    }

    private (string Sql, ParameterContext ParamContext) BuildMergedSql()
    {
        var sql = new StringBuilder();
        var paramContext = new ParameterContext();
        MergeQueries(sql, paramContext);
        return (sql.ToString(), paramContext);
    }
    
    /// <summary>Builds each query with a shared ParameterContext and per-query prefix so parameter names are unique without string replacement.</summary>
    private void MergeQueries(StringBuilder sql, ParameterContext paramContext)
    {
        for (int i = 0; i < _queries.Count; i++)
        {
            var desc = _queries[i].Build(paramContext, "q" + i);
            sql.AppendLine(desc.Sql);
            sql.AppendLine(";"); // Ensure T-SQL separation
        }
    }
}
