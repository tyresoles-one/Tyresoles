using System.Reflection;
using System.Collections.Concurrent;
using System.Linq.Expressions;
using Tyresoles.Sql.Abstractions;

namespace Tyresoles.Sql.Core.Metadata;

/// <summary>
/// Pre-caches reflection metadata per entity type to ensure zero-alloc and AOT-safe executions.
/// </summary>
public static class EntityMetadata<T>
{
    public static readonly string TableName;
    public static readonly bool IsShared;
    public static readonly IReadOnlyList<PropertyInfo> KeyProperties;
    public static readonly IReadOnlyDictionary<string, string> PropertyToColumnMap;
    // Fast path array for materializer
    public static readonly IReadOnlyList<(PropertyInfo Property, string ColumnName, NavDateAttribute? NavDate)> MappedProperties;

    static EntityMetadata()
    {
        var type = typeof(T);
        var tableAttr = type.GetCustomAttribute<NavTableAttribute>();
        TableName = tableAttr?.Name ?? type.Name;
        IsShared = tableAttr?.IsShared ?? false;

        var props = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);
        var propMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var mapped = new List<(PropertyInfo, string, NavDateAttribute?)>();

        foreach (var p in props)
        {
            if (p.GetCustomAttribute<SqlNotMappedAttribute>() != null)
                continue;

            var navCol = p.GetCustomAttribute<NavColumnAttribute>();
            var colName = navCol?.Name ?? p.Name;
            propMap[p.Name] = colName;
            
            if (p.CanWrite)
            {
                var dateAttr = p.GetCustomAttribute<NavDateAttribute>();
                mapped.Add((p, colName, dateAttr));
            }
        }

        PropertyToColumnMap = propMap;
        MappedProperties = mapped;

        var keyAttr = type.GetCustomAttribute<NavKeyAttribute>();
        if (keyAttr != null && keyAttr.KeyColumns.Length > 0)
        {
            var keys = new List<PropertyInfo>();
            foreach(var k in keyAttr.KeyColumns)
            {
                var prop = props.FirstOrDefault(p => 
                     string.Equals(p.GetCustomAttribute<NavColumnAttribute>()?.Name, k, StringComparison.OrdinalIgnoreCase) || 
                     string.Equals(p.Name, k, StringComparison.OrdinalIgnoreCase));
                     
                if (prop != null) keys.Add(prop);
            }
            KeyProperties = keys;
        }
        else
        {
            var fallback = props.FirstOrDefault(p => p.Name == "No" || p.Name == "Id" || p.Name == "EntryNo" || p.Name == "Code");
            KeyProperties = fallback != null ? new[] { fallback } : Array.Empty<PropertyInfo>();
        }
    }
}

/// <summary>
/// Non-generic resolver for dynamic types.
/// </summary>
public static class EntityMetadataResolvers
{
    private static readonly ConcurrentDictionary<Type, string> _tableNames = new();
    private static readonly ConcurrentDictionary<MemberInfo, string> _columnNames = new();
    private static readonly ConcurrentDictionary<MemberInfo, bool> _navCodes = new();
    
    public static string GetTableName(Type t)
    {
        return _tableNames.GetOrAdd(t, type => 
        {
            var attr = type.GetCustomAttribute<NavTableAttribute>();
            return attr?.Name ?? type.Name;
        });
    }

    public static string GetColumnName(MemberInfo member)
    {
        return _columnNames.GetOrAdd(member, m => {
            var navCol = m.GetCustomAttribute<NavColumnAttribute>();
            return navCol?.Name ?? m.Name;
        });
    }

    public static bool IsNavCode(MemberInfo member)
    {
        return _navCodes.GetOrAdd(member, m => m.GetCustomAttribute<NavCodeAttribute>() != null);
    }
}
