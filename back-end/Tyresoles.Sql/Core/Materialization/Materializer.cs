using System.Collections.Concurrent;
using System.Data;
using System.Linq.Expressions;
using System.Reflection;
using Tyresoles.Sql.Abstractions;

namespace Tyresoles.Sql.Core.Materialization;

public static class Materializer
{
    private static readonly ConcurrentDictionary<int, Delegate> _dynamicCache = new();
    private static readonly Type[] _intArg = { typeof(int) };
    private static readonly HashSet<string> _navSplitBoundaries = new(StringComparer.OrdinalIgnoreCase) { "Id", "Code", "No_", "No", "User ID", "UserID", "Resp_ Center", "RespCenter" };

    /// <summary>Safe conversion from reader value to byte[]. Avoids InvalidCastException when the column is returned as Int32 or other type (e.g. wrong ordinal).</summary>
    internal static byte[] SafeToByteArray(object? value)
    {
        if (value == null || value == DBNull.Value) return Array.Empty<byte>();
        if (value is byte[] b) return b;
        return Array.Empty<byte>();
    }

    /// <summary>Strip table alias from reader column name so "t5.[Role ID]" or "t5.Role ID" matches "Role ID".</summary>
    private static string NormalizeColumnName(string readerColumnName)
    {
        if (string.IsNullOrEmpty(readerColumnName)) return readerColumnName;
        var dot = readerColumnName.IndexOf('.');
        if (dot < 0) return readerColumnName;
        var raw = readerColumnName.AsSpan(dot + 1).Trim();
        if (raw.Length >= 2 && raw[0] == '[' && raw[raw.Length - 1] == ']')
            return raw.Slice(1, raw.Length - 2).ToString();
        return raw.ToString();
    }

    public static Func<IDataReader, T> GetParser<T>(IDataReader reader) where T : class
    {
        var hash = new HashCode();
        hash.Add(typeof(T));
        for (int i = 0; i < reader.FieldCount; i++)
        {
            hash.Add(reader.GetName(i));
        }
        var hashVal = hash.ToHashCode();

        if (_dynamicCache.TryGetValue(hashVal, out var cached))
            return (Func<IDataReader, T>)cached;

        var generated = TryGetGeneratedMapper<T>();
        if (generated != null)
        {
            _dynamicCache[hashVal] = generated;
            return generated;
        }

        var parser = BuildParser<T>(reader);
        _dynamicCache[hashVal] = parser;
        return parser;
    }

    /// <summary>
    /// Tries to use the source-generated *Mapper.Parse(reader) for [NavTable] types. One-time reflection per T, then cached.
    /// </summary>
    private static Func<IDataReader, T>? TryGetGeneratedMapper<T>() where T : class
    {
        var type = typeof(T);
        var mapperTypeName = string.IsNullOrEmpty(type.Namespace) ? type.Name + "Mapper" : type.Namespace + "." + type.Name + "Mapper";
        var mapperType = type.Assembly.GetType(mapperTypeName);
        if (mapperType == null)
            return null;
        var parseMethod = mapperType.GetMethod("Parse", BindingFlags.Public | BindingFlags.Static, [typeof(IDataReader)]);
        if (parseMethod == null || parseMethod.ReturnType != type)
            return null;
        var param = System.Linq.Expressions.Expression.Parameter(typeof(IDataReader), "reader");
        var call = System.Linq.Expressions.Expression.Call(parseMethod, System.Linq.Expressions.Expression.Convert(param, typeof(IDataReader)));
        var lambda = System.Linq.Expressions.Expression.Lambda<Func<IDataReader, T>>(
            System.Linq.Expressions.Expression.Convert(call, typeof(T)), param);
        return lambda.Compile();
    }

    private static Func<IDataReader, T> BuildParser<T>(IDataReader reader)
    {
        var readerParam = Expression.Parameter(typeof(IDataReader), "reader");
        int colIndex = 0;
        
        var init = BuildInitExpression(typeof(T), reader, readerParam, ref colIndex, "Id");
        return Expression.Lambda<Func<IDataReader, T>>(init, readerParam).Compile();
    }
    
    private static Expression BuildInitExpression(Type type, IDataReader reader, ParameterExpression readerParam, ref int colIndex, string splitOn)
    {
        // String and other types without a parameterless constructor cannot use Expression.New; read first column as scalar.
        if (type == typeof(string))
        {
            var isDbNullMethod = typeof(IDataRecord).GetMethod("IsDBNull", _intArg);
            var getStringMethod = typeof(IDataRecord).GetMethod("GetString", _intArg);
            if (isDbNullMethod == null || getStringMethod == null)
                throw new InvalidOperationException("IDataRecord.IsDBNull or GetString not found.");
            var indexExp = Expression.Constant(colIndex);
            var isDbNull = Expression.Call(Expression.Convert(readerParam, typeof(IDataRecord)), isDbNullMethod, indexExp);
            var getStringCall = Expression.Call(Expression.Convert(readerParam, typeof(IDataRecord)), getStringMethod, indexExp);
            return Expression.Condition(isDbNull, Expression.Constant(null, typeof(string)), getStringCall);
        }

        var newExp = Expression.New(type);
        var bindings = new List<MemberBinding>();
        var props = type.GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(p => p.CanWrite).ToArray();
        
        // 1. Map scalar properties until we hit the Split-On boundary
        int startColIndex = colIndex;
        bool hasScalars = props.Any(p => IsPrimitiveMappedType(p.PropertyType));
        var mappedProps = new HashSet<string>();
        
        if (hasScalars)
        {
            while (colIndex < reader.FieldCount)
            {
                var colNameRaw = reader.GetName(colIndex);
                var colName = NormalizeColumnName(colNameRaw);
                
                var prop = props.FirstOrDefault(p => 
                    string.Equals(p.GetCustomAttribute<NavColumnAttribute>()?.Name, colName, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(p.Name, colName, StringComparison.OrdinalIgnoreCase));
                
                // If we've processed at least one column for this type, and we see a recognizable Navision split boundary:
                // We split ONLY if the current object doesn't own this property OR if it has already consumed an identical property (prevents overwrites for explicit payload Selects)
                if (colIndex > startColIndex && _navSplitBoundaries.Contains(colName))
                {
                    if (prop == null || mappedProps.Contains(prop.Name))
                    {
                        break;
                    }
                }

                // Column does not belong to this type (e.g. duplicate names like "Name" in next nested type): stop so the next type can consume it.
                if (prop == null)
                    break;
                
                if (prop != null && IsPrimitiveMappedType(prop.PropertyType)) // Primitive
                {
                    mappedProps.Add(prop.Name);
                    
                    var indexExp = Expression.Constant(colIndex);
                    Expression valueExp;

                // Resolve IDataRecord methods explicitly (interface methods can return null from GetMethod on some runtimes)
                var getValueMethod = typeof(IDataRecord).GetMethod("GetValue", _intArg);
                var isDbNullMethod = typeof(IDataRecord).GetMethod("IsDBNull", _intArg);
                if (getValueMethod == null || isDbNullMethod == null)
                    throw new InvalidOperationException("IDataRecord.GetValue or IsDBNull not found.");

                var getterName = GetReaderMethodName(prop.PropertyType);
                MethodInfo? getTyped = null;
                if (!string.IsNullOrEmpty(getterName))
                    getTyped = typeof(IDataRecord).GetMethod(getterName, _intArg);
                if (getTyped != null)
                    valueExp = Expression.Call(Expression.Convert(readerParam, typeof(IDataRecord)), getTyped, indexExp);
                else
                    valueExp = Expression.Call(Expression.Convert(readerParam, typeof(IDataRecord)), getValueMethod, indexExp);

                Expression finalExp;
                if (prop.PropertyType == typeof(byte[]))
                {
                    // Never cast GetValue result to byte[]; use SafeToByteArray to avoid InvalidCastException when driver returns Int32 or wrong type
                    var safeMethod = typeof(Materializer).GetMethod(nameof(SafeToByteArray), BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public, null, [typeof(object)], null)!;
                    finalExp = Expression.Call(safeMethod, valueExp);
                }
                else
                {
                    var isDbNullCheck = Expression.Call(Expression.Convert(readerParam, typeof(IDataRecord)), isDbNullMethod, indexExp);
                    var defaultValue = Expression.Default(prop.PropertyType);
                    if (valueExp.Type != prop.PropertyType)
                        valueExp = Expression.Convert(valueExp, prop.PropertyType);
                    finalExp = Expression.Condition(isDbNullCheck, defaultValue, valueExp);
                }
                bindings.Add(Expression.Bind(prop, finalExp));
            }
            colIndex++;
            }
        }
        
        // 2. Check for complex navigational graphs (Nest matching)
        foreach (var prop in props)
        {
            if (!IsPrimitiveMappedType(prop.PropertyType) && prop.PropertyType.IsClass && prop.PropertyType != typeof(string))
            {
                if (colIndex < reader.FieldCount)
                {
                    var nestedInit = BuildInitExpression(prop.PropertyType, reader, readerParam, ref colIndex, splitOn);
                    bindings.Add(Expression.Bind(prop, nestedInit));
                }
            }
        }
        
        return Expression.MemberInit(newExp, bindings);
    }

    private static string? GetReaderMethodName(Type propertyType)
    {
        var t = Nullable.GetUnderlyingType(propertyType) ?? propertyType;
        if (t.IsEnum) t = Enum.GetUnderlyingType(t);
        
        if (t == typeof(int)) return "GetInt32";
        if (t == typeof(string)) return "GetString";
        if (t == typeof(bool)) return "GetBoolean";
        if (t == typeof(Guid)) return "GetGuid";
        if (t == typeof(DateTime)) return "GetDateTime";
        if (t == typeof(decimal)) return "GetDecimal";
        if (t == typeof(long)) return "GetInt64";
        if (t == typeof(short)) return "GetInt16";
        if (t == typeof(byte)) return "GetByte";
        if (t == typeof(float)) return "GetFloat";
        if (t == typeof(double)) return "GetDouble";
        if (t == typeof(byte[])) return null; // byte[] mapping is handled manually by GetValue because IDataRecord has no native "GetBytes" directly matching easily inside expressions. We map it as a primitive flag though.
        return null; // Return null intentionally to fallback to complex object navigation
    }
    
    internal static bool IsPrimitiveMappedType(Type propertyType)
    {
        var t = Nullable.GetUnderlyingType(propertyType) ?? propertyType;
        return GetReaderMethodName(t) != null || t == typeof(byte[]);
    }
}
