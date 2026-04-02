using Tyresoles.Sql.Abstractions;

namespace Tyresoles.Sql.Dialects.NavDialect;

public class NavSqlServerDialect : IDialect
{
    public DbProvider Provider => DbProvider.SqlServer;

    public string QuoteIdentifier(string name)
    {
        return $"[{name}]";
    }

    public string BuildPagination(int? skip, int? take, string orderByClause)
    {
        if (!skip.HasValue && take.HasValue && string.IsNullOrEmpty(orderByClause))
            return $" TOP ({take.Value}) "; // Managed by SqlBuilder at the SELECT top level

        if (!skip.HasValue) return string.Empty;

        var sql = string.IsNullOrEmpty(orderByClause) ? " ORDER BY (SELECT NULL)" : "";
        sql += $" OFFSET {skip.Value} ROWS FETCH NEXT {take ?? int.MaxValue} ROWS ONLY";
        return sql;
    }

    public string BuildTableName(string tableName, string? company, string? schemaPrefix, bool isShared)
    {
        // 1. Prefix with schema if provided
        var schema = string.IsNullOrEmpty(schemaPrefix) ? "dbo" : schemaPrefix;
        
        // 2. NAV Company Handling logic
        if (!isShared && !string.IsNullOrEmpty(company))
        {
            return $"[{schema}].[{company}${tableName}]";
        }

        return $"[{schema}].[{tableName}]";
    }

    public string FormatParameterName(string name) => name;
}
