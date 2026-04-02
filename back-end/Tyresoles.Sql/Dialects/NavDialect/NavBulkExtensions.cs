using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Tyresoles.Sql.Abstractions;
using Tyresoles.Sql.Core;
using Tyresoles.Sql.Core.Metadata;
using Tyresoles.Sql.Core.Query;
using Dapper;

namespace Tyresoles.Sql.Dialects.NavDialect;

public static class NavBulkExtensions
{
    /// <summary>
    /// Bulk updates instances via a native single RPC network jump using raw ADO.NET internals for massive speed.
    /// </summary>
    public static async Task ModifyRangeAsync<T>(this ITenantScope scope, IEnumerable<T> entities, CancellationToken ct = default) where T : class
    {
        var entityList = entities.ToList();
        if (!entityList.Any()) return;

        using var transaction = await scope.BeginTransactionAsync(ct);
        try
        {
            // Leverages Dapper's native execution over Iterables to automatically format sequential sp_executesql batches
            // Tyresoles.Sql SqlBuilder.GenerateUpdate dynamically caches logic here.
            var tableName = EntityMetadataResolvers.GetTableName(typeof(T));
            
            // Build the base SQL schema
            var sqlGenData = SqlBuilder.GenerateUpdate(entityList.First(), tableName);
            
            // Note: ExecuteNonQueryAsync natively detects IEnumerable parameters payload inside Dapper 
            // and executes a bulk unrolled statement efficiently against the SQL Server.
            await scope.ExecuteNonQueryAsync(sqlGenData.Sql, entityList, ct);
            transaction.Commit();
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }
}
