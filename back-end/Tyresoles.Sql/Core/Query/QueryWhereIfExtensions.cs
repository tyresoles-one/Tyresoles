using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Text.Json;
using Tyresoles.Sql.Abstractions;
using Tyresoles.Sql.Core.Metadata;
using Tyresoles.Sql.Core.Query;

namespace Tyresoles.Sql;

/// <summary>
/// Conditional filters for <see cref="IQuery{T}"/>: avoids allocating a predicate or IN clause when the condition is false.
/// Collection overloads emit the same IN / OPENJSON / ANY strategies as expression-based <c>Contains</c> for large lists.
/// </summary>
public static class QueryWhereIfExtensions
{
    private const int InClauseSplitThreshold = 100;

    /// <summary>
    /// Applies <paramref name="predicate"/> only when <paramref name="condition"/> is true; otherwise returns <paramref name="query"/> unchanged.
    /// </summary>
    public static IQuery<T> WhereIf<T>(this IQuery<T> query, bool condition, Expression<Func<T, bool>> predicate) where T : class
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(predicate);
        return condition ? query.Where(predicate) : query;
    }

    /// <summary>
    /// When <paramref name="condition"/> is true and <paramref name="values"/> is not null, adds
    /// <c>WHERE column IN (...)</c> (or OPENJSON / ANY for large lists, matching the expression visitor).
    /// When <paramref name="values"/> is null, no filter is applied. When the sequence is empty, adds <c>1 = 0</c> (same as empty <c>Contains</c>).
    /// </summary>
    public static IQuery<T> WhereIf<T, TKey>(this IQuery<T> query, bool condition, Expression<Func<T, object>> columnSelector, IEnumerable<TKey>? values) where T : class
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(columnSelector);
        if (!condition || values is null)
            return query;
        if (query is not Query<T> q)
            throw new InvalidOperationException("WhereIf with a value list requires a query created by Tyresoles.Sql (Query<T>).");
        if (values.TryGetNonEnumeratedCount(out var c) && c == 0)
            return q.Where(FalseWhereSql);
        return AddWhereIn(q, columnSelector, values);
    }

    /// <summary>
    /// Span/array overload: avoids IEnumerable allocation when filtering by a materialized array.
    /// </summary>
    public static IQuery<T> WhereIf<T, TKey>(this IQuery<T> query, bool condition, Expression<Func<T, object>> columnSelector, TKey[]? values) where T : class
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(columnSelector);
        if (!condition || values is null)
            return query;
        return WhereIf(query, true, columnSelector, values.AsSpan());
    }

    /// <summary>
    /// Span overload for zero-allocation filtering when values are already contiguous.
    /// </summary>
    public static IQuery<T> WhereIf<T, TKey>(this IQuery<T> query, bool condition, Expression<Func<T, object>> columnSelector, ReadOnlySpan<TKey> values) where T : class
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(columnSelector);
        if (!condition)
            return query;
        if (query is not Query<T> q)
            throw new InvalidOperationException("WhereIf with a value list requires a query created by Tyresoles.Sql (Query<T>).");
        if (values.Length == 0)
            return q.Where(FalseWhereSql);
        return AddWhereIn(q, columnSelector, values);
    }

    private const string FalseWhereSql = "1 = 0";

    private static IQuery<T> AddWhereIn<T, TKey>(Query<T> query, Expression<Func<T, object>> columnSelector, IEnumerable<TKey> values) where T : class
    {
        var member = TryGetMember(columnSelector);
        var isNavCode = member != null && EntityMetadataResolvers.IsNavCode(member);
        var list = new List<object>();
        foreach (var item in values)
        {
            if (item is null)
                continue;
            object v = item;
            if (isNavCode && v is string s)
                v = s.ToUpperInvariant();
            list.Add(v);
        }
        if (list.Count == 0)
            return query.Where(FalseWhereSql);
        var quoted = SqlBuilder.GetQualifiedColumnSqlFromExpr(columnSelector, query.Dialect);
        var (sql, parameters) = BuildInClause(query.Dialect, quoted, list);
        return query.Where(sql, parameters);
    }

    private static IQuery<T> AddWhereIn<T, TKey>(Query<T> query, Expression<Func<T, object>> columnSelector, ReadOnlySpan<TKey> values) where T : class
    {
        var member = TryGetMember(columnSelector);
        var isNavCode = member != null && EntityMetadataResolvers.IsNavCode(member);
        var list = new List<object>(values.Length);
        for (var i = 0; i < values.Length; i++)
        {
            var item = values[i];
            if (item is null)
                continue;
            object v = item!;
            if (isNavCode && v is string s)
                v = s.ToUpperInvariant();
            list.Add(v);
        }
        if (list.Count == 0)
            return query.Where(FalseWhereSql);
        var quoted = SqlBuilder.GetQualifiedColumnSqlFromExpr(columnSelector, query.Dialect);
        var (sql, parameters) = BuildInClause(query.Dialect, quoted, list);
        return query.Where(sql, parameters);
    }

    private static (string Sql, Dictionary<string, object> Parameters) BuildInClause(IDialect dialect, string quotedColumn, List<object> values)
    {
        var id = Guid.NewGuid().ToString("N");
        var provider = dialect.Provider;

        if (values.Count > InClauseSplitThreshold && provider == DbProvider.SqlServer)
        {
            var json = JsonSerializer.Serialize(values);
            var p = "@wif_" + id + "_j";
            return ($"{quotedColumn} IN (SELECT value FROM OPENJSON({p}))", new Dictionary<string, object>(StringComparer.Ordinal) { [p] = json });
        }

        if (values.Count > InClauseSplitThreshold && provider == DbProvider.PostgreSQL)
        {
            var p = "@wif_" + id + "_a";
            return ($"{quotedColumn} = ANY({p})", new Dictionary<string, object>(StringComparer.Ordinal) { [p] = values });
        }

        var sb = new StringBuilder(quotedColumn.Length + values.Count * 8 + 16);
        var dict = new Dictionary<string, object>(values.Count, StringComparer.Ordinal);
        sb.Append(quotedColumn).Append(" IN (");
        for (var i = 0; i < values.Count; i++)
        {
            if (i > 0) sb.Append(", ");
            var name = "@wif_" + id + "_" + i;
            sb.Append(name);
            dict[name] = values[i];
        }
        sb.Append(')');
        return (sb.ToString(), dict);
    }

    private static MemberInfo? TryGetMember<T>(Expression<Func<T, object>> selector)
    {
        Expression body = selector.Body;
        if (body is UnaryExpression u && u.NodeType == ExpressionType.Convert)
            body = u.Operand;
        return body is MemberExpression m ? m.Member : null;
    }
}
