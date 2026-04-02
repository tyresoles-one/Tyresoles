namespace Tyresoles.Sql.Abstractions;

public enum DbProvider
{
    SqlServer,
    PostgreSQL
}

/// <summary>
/// Provides database-specific syntax translation.
/// </summary>
public interface IDialect
{
    DbProvider Provider { get; }

    /// <summary>
    /// Quotes an identifier, e.g., columnname -> [columnname] for SQL Server or "columnname" for PG.
    /// </summary>
    string QuoteIdentifier(string name);

    /// <summary>
    /// Generates the pagination syntax.
    /// </summary>
    string BuildPagination(int? skip, int? take, string orderByClause);

    /// <summary>
    /// Generates proper table name reference for NAV Company handling and schema scopes.
    /// </summary>
    string BuildTableName(string tableName, string? company, string? schemaPrefix, bool isShared);

    /// <summary>
    /// Formats the parameter name. e.g. @p0
    /// </summary>
    string FormatParameterName(string name);
}
