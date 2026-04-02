using System.Collections;
using System.Text;
using System.Data.Common;
using System.Linq.Expressions;
using System.Reflection;
using Microsoft.Extensions.Logging;
using Tyresoles.Sql.Abstractions;
using Tyresoles.Sql.Core.Configuration;
using Tyresoles.Sql.Core.Query;
using System.Data;
using Tyresoles.Sql.Core.Metadata;
using Tyresoles.Sql.Core.Materialization;
using Dapper;
using Tyresoles.Sql.Dialects.NavDialect;
using Tyresoles.Sql.Dialects.PostgresDialect;

namespace Tyresoles.Sql.Core;

public class TenantScope : ITenantScope
{
    private readonly TenantConfiguration _config;
    private readonly IDbConnectionFactory _factory;
    private readonly ILogger? _logger;
    private readonly IDbCommandInterceptor[] _interceptors;
    private DbConnection? _connection;
    private IDbTransaction? _transaction;
    
    public string TenantKey => _config.Name;
    public IDialect Dialect { get; }
    private string _currentCompany;
    internal int? CommandTimeoutSeconds => _config.CommandTimeout > 0 ? _config.CommandTimeout : null;

    private async ValueTask<T> ExecuteWithRetryAsync<T>(Func<ValueTask<T>> operation, CancellationToken ct)
    {
        if (!_config.EnableRetry)
            return await operation();
        var lastException = (Exception?)null;
        for (int attempt = 0; attempt <= _config.RetryCount; attempt++)
        {
            try
            {
                return await operation();
            }
            catch (Exception ex) when (attempt < _config.RetryCount)
            {
                if (!IsTransientException(ex))
                    throw;
                lastException = ex;
                await Task.Delay(_config.RetryDelayMilliseconds, ct);
            }
        }
        throw lastException!;
    }

    private static bool IsTransientException(Exception ex)
    {
        if (ex is Microsoft.Data.SqlClient.SqlException sqlEx)
        {
            foreach (Microsoft.Data.SqlClient.SqlError err in sqlEx.Errors)
            {
                switch (err.Number)
                {
                    case -2:   // Timeout
                    case 1205: // Deadlock
                    case 1222: // Lock request
                    case 4060: // Cannot open database
                    case 40197:
                    case 40501: // Service busy
                    case 49918:
                    case 49919:
                    case 49920:
                        return true;
                }
            }
        }
        var msg = ex.Message.AsSpan();
        if (msg.IndexOf("timeout", StringComparison.OrdinalIgnoreCase) >= 0) return true;
        if (msg.IndexOf("connection", StringComparison.OrdinalIgnoreCase) >= 0) return true;
        if (msg.IndexOf("network", StringComparison.OrdinalIgnoreCase) >= 0) return true;
        return false;
    }

    public TenantScope(TenantConfiguration config, IDbConnectionFactory factory, ILogger? logger = null, IEnumerable<IDbCommandInterceptor>? interceptors = null)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(factory);
        if (string.IsNullOrWhiteSpace(config.ConnectionString))
            throw new InvalidOperationException($"Connection string for tenant '{config.Name}' must not be null or empty. Check DataverseOptions.Tenants configuration.");
        _config = config;
        _factory = factory;
        _logger = logger;
        _interceptors = interceptors?.ToArray() ?? Array.Empty<IDbCommandInterceptor>();
        _currentCompany = config.DefaultCompany;

        Dialect = config.Provider switch
        {
            DbProvider.SqlServer => new NavSqlServerDialect(),
            DbProvider.PostgreSQL => new PostgresSqlDialect(),
            _ => new NavSqlServerDialect()
        };
    }

    public ITenantScope WithCompany(string companyCode)
    {
        ArgumentNullException.ThrowIfNull(companyCode);
        _currentCompany = companyCode;
        return this;
    }

    public IQuery<T> Query<T>() where T : class
    {
        var tableName = typeof(T).Name;
        bool isShared = false;
        var attr = typeof(T).GetCustomAttribute<NavTableAttribute>();
        if (attr != null) 
        {
            tableName = attr.Name;
            isShared = attr.IsShared;
        }
        
        return new Query<T>(this, tableName, isShared ? null : _currentCompany);
    }
    
    public IMultipleQuery CreateMultipleQuery() => new MultipleQuery(this);
    
    // -------------------------------------------------------------------------
    // READ Operations (Optimized for Stream/Memory)
    // -------------------------------------------------------------------------

    public async IAsyncEnumerable<T> StreamAsync<T>(IQuery<T> query, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default) where T : class
    {
        ArgumentNullException.ThrowIfNull(query);
        var descriptor = query.Build();
        LogQuery("Query", descriptor.Sql, descriptor.Parameters);
        var connection = GetConnection();
        try
        {
            // Ensure connection open (log before we start yielding anything).
            if (connection.State != ConnectionState.Open) await connection.OpenAsync(ct);
        }
        catch (Exception ex)
        {
            LogQueryError("Query", descriptor.Sql, descriptor.Parameters, ex);
            throw;
        }

        // Use Dapper for raw execution if simple, but here we need custom Materializer for performance/schema mapping?
        // Actually, Dapper is faster than hand-rolled ADO for object mapping if configured right, 
        // BUT our Materializer handles [NavColumn] attributes which Dapper doesn't do out-of-box without TypeHandlers.
        // We stick to our optimized Materializer for typed queries.
        using var cmd = connection.CreateCommand();
        cmd.CommandText = descriptor.Sql;
        if (_config.CommandTimeout > 0) cmd.CommandTimeout = _config.CommandTimeout;
        if (_transaction != null) cmd.Transaction = (DbTransaction)_transaction;

        foreach(var kvp in descriptor.Parameters)
        {
            var p = cmd.CreateParameter();
            p.ParameterName = kvp.Key;
            p.Value = kvp.Value ?? DBNull.Value;
            cmd.Parameters.Add(p);
        }

        foreach (var interceptor in _interceptors)
            await interceptor.OnBeforeExecuteAsync(cmd, ct);

        DbDataReader reader;
        try
        {
            // ExecuteReaderAsync can throw (log it here; still no yield inside this try/catch).
            reader = await cmd.ExecuteReaderAsync(CommandBehavior.SequentialAccess, ct);
        }
        catch (Exception ex)
        {
            LogQueryError("Query", descriptor.Sql, descriptor.Parameters, ex);
            throw;
        }

        using (reader)
        {
            var parser = Materializer.GetParser<T>(reader);
            while (true)
            {
                bool hasRow;
                try
                {
                    hasRow = await reader.ReadAsync(ct);
                }
                catch (Exception ex)
                {
                    LogQueryError("Query", descriptor.Sql, descriptor.Parameters, ex);
                    throw;
                }

                if (!hasRow) break;

                T item;
                try
                {
                    item = parser(reader);
                }
                catch (Exception ex)
                {
                    LogQueryError("Query", descriptor.Sql, descriptor.Parameters, ex);
                    throw;
                }

                yield return item;
            }
        }
    }

    public async ValueTask<T[]> ToArrayAsync<T>(IQuery<T> query, CancellationToken ct = default) where T : class
    {
        ArgumentNullException.ThrowIfNull(query);
        var pool = System.Buffers.ArrayPool<T>.Shared;
        var buffer = pool.Rent(256);
        int count = 0;
        
        try
        {
            await foreach(var item in StreamAsync(query, ct))
            {
                if (count == buffer.Length)
                {
                    var newBuffer = pool.Rent(buffer.Length * 2);
                    Array.Copy(buffer, newBuffer, count);
                    pool.Return(buffer, clearArray: true);
                    buffer = newBuffer;
                }
                buffer[count++] = item;
            }
            
            var result = new T[count];
            Array.Copy(buffer, result, count);
            return result;
        }
        finally
        {
            pool.Return(buffer, clearArray: true);
        }
    }
    
    // -------------------------------------------------------------------------
    // WRITE Operations
    // -------------------------------------------------------------------------

    public async ValueTask InsertAsync<T>(T entity, CancellationToken ct = default) where T : class
    {
         ArgumentNullException.ThrowIfNull(entity);
         var sql = SqlBuilder.GenerateInsert(entity, ResolveTableName<T>());
         LogQuery("Insert", sql.Sql, sql.Parameters);
         try
         {
             await ExecuteWithRetryAsync(async () => await ExecuteInterceptorCommandAsync(sql.Sql, sql.Parameters, ct), ct);
         }
         catch (Exception ex)
         {
             LogQueryError("Insert", sql.Sql, sql.Parameters, ex);
             throw;
         }
    }

    public async ValueTask BulkInsertAsync<T>(IEnumerable<T> entities, CancellationToken ct = default) where T : class
    {
        ArgumentNullException.ThrowIfNull(entities);
        var entityList = entities as IReadOnlyCollection<T> ?? entities.ToList();
        if (entityList.Count == 0) return;
        
        var tableName = ResolveTableName<T>();
        var conn = GetConnection();

        if (conn is Microsoft.Data.SqlClient.SqlConnection sqlConn)
        {
            if (sqlConn.State != ConnectionState.Open) await sqlConn.OpenAsync(ct);
            
            var dt = new DataTable();
            var mappedProps = EntityMetadata<T>.MappedProperties;
            
            foreach (var map in mappedProps)
            {
                var pt = map.Property.PropertyType;
                dt.Columns.Add(map.ColumnName, Nullable.GetUnderlyingType(pt) ?? pt);
            }
            
            foreach (var entity in entityList)
            {
                var row = dt.NewRow();
                foreach (var map in mappedProps)
                {
                    row[map.ColumnName] = map.Property.GetValue(entity) ?? DBNull.Value;
                }
                dt.Rows.Add(row);
            }
            
            using var bulkCopy = new Microsoft.Data.SqlClient.SqlBulkCopy(sqlConn, Microsoft.Data.SqlClient.SqlBulkCopyOptions.Default, _transaction as Microsoft.Data.SqlClient.SqlTransaction)
            {
                DestinationTableName = $"[{tableName}]",
                BatchSize = 5000,
                BulkCopyTimeout = 60
            };
            
            foreach (DataColumn c in dt.Columns)
            {
                bulkCopy.ColumnMappings.Add(c.ColumnName, c.ColumnName);
            }
            
            LogQuery("BulkInsert", $"[SqlBulkCopy] -> [{tableName}] ({entityList.Count} rows)", null);
            try
            {
                await bulkCopy.WriteToServerAsync(dt, ct);
            }
            catch (Exception ex)
            {
                LogQueryError("BulkInsert", $"[SqlBulkCopy] -> [{tableName}] ({entityList.Count} rows)", null, ex);
                throw;
            }
        }
        else 
        {
            // Generic fallback via Dapper
            var sqlGenData = SqlBuilder.GenerateInsert(entityList.First(), tableName);
            LogQuery("BulkInsertFallback", sqlGenData.Sql, null);
            try
            {
                await conn.ExecuteAsync(new CommandDefinition(sqlGenData.Sql, entityList, _transaction, CommandTimeoutSeconds, cancellationToken: ct));
            }
            catch (Exception ex)
            {
                LogQueryError("BulkInsertFallback", sqlGenData.Sql, null, ex);
                throw;
            }
        }
    }
    
    public async ValueTask UpdateAsync<T>(T entity, CancellationToken ct = default) where T : class
    {
         ArgumentNullException.ThrowIfNull(entity);
         var sql = SqlBuilder.GenerateUpdate(entity, ResolveTableName<T>());
         LogQuery("Update", sql.Sql, sql.Parameters);
         try
         {
             await ExecuteWithRetryAsync(async () => await ExecuteInterceptorCommandAsync(sql.Sql, sql.Parameters, ct), ct);
         }
         catch (Exception ex)
         {
             LogQueryError("Update", sql.Sql, sql.Parameters, ex);
             throw;
         }
    }

    public ValueTask UpdateAsync<T>(T entity, params Expression<Func<T, object>>[] selectors) where T : class
        => UpdateAsync(entity, selectors, default);

    public async ValueTask UpdateAsync<T>(T entity, Expression<Func<T, object>>[] selectors, CancellationToken ct = default) where T : class
    {
         ArgumentNullException.ThrowIfNull(entity);
         ArgumentNullException.ThrowIfNull(selectors);
         if (selectors.Length == 0) return;

         var sql = SqlBuilder.GenerateUpdate(entity, ResolveTableName<T>(), selectors);
         LogQuery("UpdatePartial", sql.Sql, sql.Parameters);
         try
         {
             await ExecuteWithRetryAsync(async () => await ExecuteInterceptorCommandAsync(sql.Sql, sql.Parameters, ct), ct);
         }
         catch (Exception ex)
         {
             LogQueryError("UpdatePartial", sql.Sql, sql.Parameters, ex);
             throw;
         }
    }
    
    public async ValueTask UpsertAsync<T>(T entity, CancellationToken ct = default) where T : class
    {
         ArgumentNullException.ThrowIfNull(entity);
         var sql = SqlBuilder.GenerateUpsert(entity, ResolveTableName<T>());
         LogQuery("Upsert", sql.Sql, sql.Parameters);
         try
         {
             await ExecuteWithRetryAsync(async () => await ExecuteInterceptorCommandAsync(sql.Sql, sql.Parameters, ct), ct);
         }
         catch (Exception ex)
         {
             LogQueryError("Upsert", sql.Sql, sql.Parameters, ex);
             throw;
         }
    }
    
    public async ValueTask DeleteAsync<T>(T entity, CancellationToken ct = default) where T : class
    {
         ArgumentNullException.ThrowIfNull(entity);
         var sql = SqlBuilder.GenerateDelete(entity, ResolveTableName<T>());
         LogQuery("Delete", sql.Sql, sql.Parameters);
         try
         {
             await ExecuteWithRetryAsync(async () => await ExecuteInterceptorCommandAsync(sql.Sql, sql.Parameters, ct), ct);
         }
         catch (Exception ex)
         {
             LogQueryError("Delete", sql.Sql, sql.Parameters, ex);
             throw;
         }
    }
    
    public async ValueTask DeleteWhereAsync<T>(Expression<Func<T, bool>> predicate, CancellationToken ct = default) where T : class
    {
        ArgumentNullException.ThrowIfNull(predicate);
        var ctx = new ParameterContext();
        var visitor = new SqlExpressionVisitor(ctx, Dialect);
        visitor.Visit(predicate);
        var whereSql = visitor.GetSql();
        var tableName = ResolveTableName<T>();
        var sql = $"DELETE FROM {tableName} WHERE {whereSql}";
        LogQuery("DeleteWhere", sql, ctx.Parameters);
        try
        {
            await ExecuteWithRetryAsync(async () => await ExecuteInterceptorCommandAsync(sql, ctx.Parameters, ct), ct);
        }
        catch (Exception ex)
        {
            LogQueryError("DeleteWhere", sql, ctx.Parameters, ex);
            throw;
        }
    }
    
    // -------------------------------------------------------------------------
    // Advanced & Raw SQL (Powered by Dapper)
    // -------------------------------------------------------------------------

    public async ValueTask<int> ExecuteNonQueryAsync(string sql, object? parameters = null, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(sql);
        if (string.IsNullOrWhiteSpace(sql)) throw new ArgumentException("SQL must not be empty.", nameof(sql));
        var pDict = ParametersFromObject(parameters);
        if (_logger != null && _logger.IsEnabled(LogLevel.Information))
            LogQuery("ExecuteNonQuery", sql, pDict);
        try
        {
            return await ExecuteWithRetryAsync(async () => await ExecuteInterceptorCommandAsync(sql, pDict, ct), ct);
        }
        catch (Exception ex)
        {
            LogQueryError("ExecuteNonQuery", sql, pDict, ex);
            throw;
        }
    }
    
    public async ValueTask<TResult?> ExecuteScalarAsync<TResult>(string sql, object? parameters = null, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(sql);
        if (string.IsNullOrWhiteSpace(sql)) throw new ArgumentException("SQL must not be empty.", nameof(sql));
        
        var pDict = ParametersFromObject(parameters);
        if (pDict != null)
        {
            foreach (var interceptor in _interceptors)
            {
                if (interceptor is IParameterPreprocessor preprocessor)
                {
                    var transformed = preprocessor.PreProcessParameters(sql, pDict);
                    if (transformed != null) pDict = transformed;
                }
            }
        }

        if (_logger != null && _logger.IsEnabled(LogLevel.Information))
            LogQuery("ExecuteScalar", sql, pDict);
            
        try
        {
            return await ExecuteWithRetryAsync(async () => 
            {
                var conn = GetConnection();
                // We use Dapper here because it handles List/Array parameters (IN @list) automatically
                var res = await conn.ExecuteScalarAsync(new CommandDefinition(sql, pDict, _transaction, CommandTimeoutSeconds, cancellationToken: ct));
                return res == DBNull.Value || res == null ? default : (TResult)Convert.ChangeType(res, typeof(TResult));
            }, ct);
        }
        catch (Exception ex)
        {
            LogQueryError("ExecuteScalar", sql, pDict, ex);
            throw;
        }
    }
    
    public async ValueTask<IEnumerable<T>> QueryAsync<T>(string sql, object? parameters = null, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(sql);
        if (string.IsNullOrWhiteSpace(sql)) throw new ArgumentException("SQL must not be empty.", nameof(sql));
        var pDict = ParametersFromObject(parameters);
        if (_logger != null && _logger.IsEnabled(LogLevel.Information))
            LogQuery("QueryAsync", sql, pDict);
        try
        {
            return await ExecuteWithRetryAsync(async () => await GetConnection().QueryAsync<T>(new CommandDefinition(sql, pDict, _transaction, CommandTimeoutSeconds, cancellationToken: ct)), ct);
        }
        catch (Exception ex)
        {
            LogQueryError("QueryAsync", sql, pDict, ex);
            throw;
        }
    }

    public async IAsyncEnumerable<T> StreamRawAsync<T>(string sql, object? parameters = null, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default) where T : class
    {
        ArgumentNullException.ThrowIfNull(sql);
        if (string.IsNullOrWhiteSpace(sql)) throw new ArgumentException("SQL must not be empty.", nameof(sql));
        var items = await QueryAsync<T>(sql, parameters, ct);
        foreach (var item in items)
            yield return item;
    }

    public async ValueTask<T[]> RawQueryToArrayAsync<T>(string sql, object? parameters = null, CancellationToken ct = default) where T : class
    {
        ArgumentNullException.ThrowIfNull(sql);
        if (string.IsNullOrWhiteSpace(sql)) throw new ArgumentException("SQL must not be empty.", nameof(sql));
        var items = await QueryAsync<T>(sql, parameters, ct);
        return items.ToArray();
    }

    // -------------------------------------------------------------------------
    // Transactions
    // -------------------------------------------------------------------------

    public async ValueTask<IDbTransaction> BeginTransactionAsync(CancellationToken ct = default)
    {
         var connection = GetConnection();
         if (connection.State != ConnectionState.Open) await connection.OpenAsync(ct);
         
         _transaction = await connection.BeginTransactionAsync(ct);
         return _transaction;
    }
    
    public async ValueTask<IDbTransaction> BeginTransactionAsync(IsolationLevel isolationLevel, CancellationToken ct = default)
    {
         var connection = GetConnection();
         if (connection.State != ConnectionState.Open) await connection.OpenAsync(ct);
         
         _transaction = await connection.BeginTransactionAsync(isolationLevel, ct);
         return _transaction;
    }
    
    // -------------------------------------------------------------------------
    // Internals
    // -------------------------------------------------------------------------

    private async ValueTask<int> ExecuteInterceptorCommandAsync(string sql, object? parameters, CancellationToken ct)
    {
        var pDict = ParametersFromObject(parameters);
        if (pDict != null)
        {
            foreach (var interceptor in _interceptors)
            {
                if (interceptor is IParameterPreprocessor preprocessor)
                {
                    var transformed = preprocessor.PreProcessParameters(sql, pDict);
                    if (transformed != null) pDict = transformed;
                }
            }
        }

        var conn = GetConnection();
        if (conn.State != ConnectionState.Open) await conn.OpenAsync(ct);

        // If we have any enumerable parameters, we MUST use Dapper to handle expansion
        bool hasEnumerable = pDict?.Values.Any(v => v is System.Collections.IEnumerable && v is not string) ?? false;

        if (hasEnumerable || _interceptors.Length == 0)
        {
            return await conn.ExecuteAsync(new CommandDefinition(sql, pDict, _transaction, CommandTimeoutSeconds, cancellationToken: ct));
        }

        // Traditional path for single-entity operations to support DbCommand interceptors
        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        if (CommandTimeoutSeconds.HasValue) cmd.CommandTimeout = CommandTimeoutSeconds.Value;
        cmd.Transaction = _transaction as DbTransaction;
        if (pDict != null)
        {
            foreach (var kvp in pDict)
            {
                var p = cmd.CreateParameter();
                p.ParameterName = kvp.Key;
                p.Value = kvp.Value ?? DBNull.Value;
                cmd.Parameters.Add(p);
            }
        }
        foreach (var interceptor in _interceptors) await interceptor.OnBeforeExecuteAsync((DbCommand)cmd, ct);
        return await ((DbCommand)cmd).ExecuteNonQueryAsync(ct);
    }

    internal string ResolveTableName<T>()
    {
        var tableName = typeof(T).Name;
        bool isShared = false;
        var attr = typeof(T).GetCustomAttribute<NavTableAttribute>();
        if (attr != null) 
        {
            tableName = attr.Name;
            isShared = attr.IsShared;
        }

        return Dialect.BuildTableName(tableName, _currentCompany, _config.SchemaPrefix, isShared);
    }

    /// <inheritdoc />
    public string GetQualifiedTableName(string tableName, bool isShared = false)
    {
        ArgumentNullException.ThrowIfNull(tableName);
        return Dialect.BuildTableName(tableName, _currentCompany, _config.SchemaPrefix, isShared);
    }

    internal string? SchemaPrefix => _config.SchemaPrefix;
    internal string CurrentCompany => _currentCompany;

    internal DbConnection GetConnection()
    {
        if (_connection == null)
            _connection = _factory.CreateConnection(_config.ConnectionString);
        return _connection;
    }

    public void Dispose()
    {
        DisposeSync();
    }

    public async ValueTask DisposeAsync()
    {
        if (_transaction != null)
        {
            try { _transaction.Rollback(); }
            catch { /* best effort */ }
            try { _transaction.Dispose(); }
            catch { /* best effort */ }
            _transaction = null;
        }
        if (_connection != null)
        {
            if (_connection is IAsyncDisposable asyncConn)
                await asyncConn.DisposeAsync().ConfigureAwait(false);
            else
                _connection.Dispose();
            _connection = null;
        }
    }

    private void DisposeSync()
    {
        try
        {
            if (_transaction != null)
            {
                try { _transaction.Rollback(); }
                catch { /* best effort */ }
                _transaction.Dispose();
                _transaction = null;
            }
        }
        finally
        {
            if (_connection != null)
            {
                _connection.Dispose();
                _connection = null;
            }
        }
    }

    private string BuildExecutableSql(string sql, IReadOnlyDictionary<string, object>? parameters)
    {
        if (parameters == null || parameters.Count == 0)
            return sql;

        var sb = new StringBuilder(sql);
        // Iterate from longest parameter names to shortest to prevent @p1 matching inside @p10
        var sortedParams = parameters.OrderByDescending(p => p.Key.Length);
        
        foreach (var kvp in sortedParams)
        {
            var pName = kvp.Key.StartsWith("@") ? kvp.Key : "@" + kvp.Key;
            var val = kvp.Value;
            
            string formattedVal;
            if (val == null || val == DBNull.Value) formattedVal = "NULL";
            else if (val is string s) formattedVal = $"'{s.Replace("'", "''")}'";
            else if (val is DateTime dt) formattedVal = $"'{dt:yyyy-MM-dd HH:mm:ss.fff}'";
            else if (val is Guid g) formattedVal = $"'{g}'";
            else if (val is bool b) formattedVal = b ? "1" : "0";
            else if (val is IEnumerable enumerable && !(val is string))
            {
                var items = new List<string>();
                foreach (var item in enumerable)
                {
                    if (item == null || item == DBNull.Value) items.Add("NULL");
                    else if (item is string s2) items.Add($"'{s2.Replace("'", "''")}'");
                    else if (item is DateTime dt2) items.Add($"'{dt2:yyyy-MM-dd HH:mm:ss.fff}'");
                    else items.Add(item.ToString() ?? "NULL");
                }
                formattedVal = items.Count > 0 ? $"({string.Join(", ", items)})" : "()";
            }
            else formattedVal = val.ToString() ?? "NULL";
            
            sb.Replace(pName, formattedVal);
        }

        return sb.ToString();
    }

    internal void LogQuery(string operation, string sql, IReadOnlyDictionary<string, object>? parameters)
    {
        if (_logger == null || !_logger.IsEnabled(LogLevel.Information)) return;

        var executableSql = BuildExecutableSql(sql, parameters);
        
        _logger.LogInformation("[{Operation}] Tenant={Tenant} Executable Query:\n{Sql}",
            operation, _config.Name, executableSql);
            
        if (parameters != null && parameters.Count > 0 && _logger.IsEnabled(LogLevel.Debug))
        {
             _logger.LogDebug("[{Operation}] Parameters: {@Parameters}", operation, parameters);
        }
    }

    internal void LogQueryError(string operation, string sql, IReadOnlyDictionary<string, object>? parameters, Exception exception)
    {
        if (_logger == null || !_logger.IsEnabled(LogLevel.Error)) return;

        var executableSql = BuildExecutableSql(sql, parameters);
        _logger.LogError(exception, "[{Operation}] Tenant={Tenant} Executable Query failed:\n{Sql}",
            operation, _config.Name, executableSql);

        // Extra context when debugging types/values (still piggybacks on logger filters)
        if (parameters != null && parameters.Count > 0 && _logger.IsEnabled(LogLevel.Debug))
        {
            _logger.LogDebug("[{Operation}] Parameters (on error): {@Parameters}", operation, parameters);
        }
    }

    private static IReadOnlyDictionary<string, object>? ParametersFromObject(object? parameters)
    {
        if (parameters == null) return null;
        if (parameters is IReadOnlyDictionary<string, object> ro) return ro;
        if (parameters is Dictionary<string, object> d) return d;
        var dict = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        foreach (var prop in parameters.GetType().GetProperties())
        {
            var v = prop.GetValue(parameters);
            // Preserve C# null for Dapper; coercing to DBNull.Value breaks optional params (e.g. nos, regions on payroll SQL).
            dict[prop.Name] = v!;
        }
        return dict;
    }
}
