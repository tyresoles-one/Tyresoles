namespace Tyresoles.Sql.Abstractions;

[AttributeUsage(AttributeTargets.Property)]
public class NavDateAttribute : Attribute
{
}

[AttributeUsage(AttributeTargets.Property)]
public class NavCodeAttribute : Attribute
{
}

[AttributeUsage(AttributeTargets.Property)]
public class NavOptionAttribute : Attribute
{
    public string Mapping { get; }
    public NavOptionAttribute(string mapping)
    {
        Mapping = mapping;
    }
}

[AttributeUsage(AttributeTargets.Property)]
public class NavColumnAttribute : Attribute
{
    public string Name { get; }
    public NavColumnAttribute(string name) => Name = name;
}

/// <summary>
/// When set on a DTO property used in JOIN queries, SQL predicates/order emit <c>t0.[Column]</c> instead of
/// <c>[Column]</c> so names like <c>No_</c> are not ambiguous between root (e.g. Purchase Line) and joined tables.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class JoinSqlAliasAttribute : Attribute
{
    public string Alias { get; }
    public JoinSqlAliasAttribute(string alias) => Alias = alias;
}

/// <summary>
/// CLR-only property: not a NAV column. Excluded from SQL projection (MemberInit) and bulk insert column lists.
/// Use for values supplied via <see cref="IQuery{T}.SelectRaw"/> or resolvers.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class SqlNotMappedAttribute : Attribute;

[AttributeUsage(AttributeTargets.Class)]
public class NavKeyAttribute : Attribute
{
    public string[] KeyColumns { get; }
    public NavKeyAttribute(params string[] keyColumns) => KeyColumns = keyColumns;
}

[AttributeUsage(AttributeTargets.Class)]
public class NavTableAttribute : Attribute 
{
    public string Name { get; }
    public bool IsShared { get; set; }

    public NavTableAttribute(string name) => Name = name;
}
