using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Tyresoles.Sql.Abstractions;

namespace Tyresoles.Sql.Dialects.NavDialect;

public static class NavMetadataExtensions
{
    /// <summary>
    /// Retrieves a list of NAV tables currently recognized within the bound tenant SQL schema.
    /// </summary>
    public static async Task<IEnumerable<string>> GetNavTablesAsync(this ITenantScope scope, CancellationToken ct = default)
    {
        // For SQL Server Nav Schema, table names typically end gracefully or contain basic $ prefixes
        // Standard query against INFORMATION_SCHEMA for NAV tables under current execution schema
        var sql = @"
            SELECT TABLE_NAME 
            FROM INFORMATION_SCHEMA.TABLES 
            WHERE TABLE_TYPE = 'BASE TABLE'
            AND TABLE_NAME NOT LIKE 'timestamp'
            ORDER BY TABLE_NAME";
            
        return await scope.QueryAsync<string>(sql, null, ct);
    }
}
