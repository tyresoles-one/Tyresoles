using Tyresoles.Sql.Abstractions;

namespace Tyresoles.Sql.Dialects.PostgresDialect;

public class PostgresSqlDialect : IDialect
{
    public DbProvider Provider => DbProvider.PostgreSQL;

    public string QuoteIdentifier(string name)
    {
        return $"\"{name}\"";
    }

    public string BuildPagination(int? skip, int? take, string orderByClause)
    {
        var sql = "";
        if (take.HasValue) sql += $" LIMIT {take.Value}";
        if (skip.HasValue) sql += $" OFFSET {skip.Value}";
        return sql;
    }

    public string BuildTableName(string tableName, string? company, string? schemaPrefix, bool isShared)
    {
        var schema = string.IsNullOrEmpty(schemaPrefix) ? "public" : schemaPrefix;
        return $"\"{schema}\".\"{tableName}\"";
    }

    public string FormatParameterName(string name)
    {
        // Positional or named? For now assuming Dapper translates @p0 generically, or we do natively.
        return name;
    }
}
