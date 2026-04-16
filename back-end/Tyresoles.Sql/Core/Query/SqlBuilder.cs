using System.Text;
using System.Linq.Expressions;
using System.Reflection;
using Tyresoles.Sql.Abstractions;
using Tyresoles.Sql.Core.Metadata;

namespace Tyresoles.Sql.Core.Query;

internal class SqlBuilder
{
    private readonly string _table;
    private readonly string? _company;
    private readonly StringBuilder _sb = new();
    private readonly ParameterContext _context;
    private readonly IDialect _dialect;
    private readonly List<string> _wheres = new();
    private readonly List<JoinExpression> _joinExpressions = new();
    private readonly List<string> _joins = new();
    private List<string> _orderBys = new();
    private List<string> _groupBys = new();
    private readonly List<string> _rawSelects = new();
    private readonly List<string> _havings = new();
    private readonly List<(string Alias, string Sql)> _ctes = new();
    private string _select = "*";
    private bool _distinct;
    private int? _skip;
    private int? _take;
    
    // Table aliases: t0 = root, t1, t2, ... for joins
    private const string RootAlias = "t0";
    private int _joinIndex;
    
    // Aggregates
    private string? _aggregateFunction;
    private string? _aggregateColumn; // or Expression
    
    private readonly string? _schemaPrefix;
    private readonly bool _isSharedRoot;

    public SqlBuilder(string table, string? company, ParameterContext? context = null, IDialect? dialect = null, string? schemaPrefix = null, bool isSharedRoot = false)
    {
       _table = table; 
       _company = company;
       _context = context ?? new ParameterContext();
       _dialect = dialect ?? new DialectFallback();
       _schemaPrefix = schemaPrefix;
       _isSharedRoot = isSharedRoot;
    }
    
    private sealed class DialectFallback : IDialect
    {
        public DbProvider Provider => DbProvider.SqlServer;
        public string QuoteIdentifier(string name) => $"[{name}]";
        public string BuildPagination(int? skip, int? take, string orderByClause) => string.Empty;
        public string BuildTableName(string tableName, string? company, string? schemaPrefix, bool isShared) => $"[{tableName}]";
        public string FormatParameterName(string name) => name.StartsWith("@") ? name : "@" + name;
    }
    
    private string Q(string name) => _dialect.QuoteIdentifier(name);
    
    private string ResolveRootTableName() => _dialect.BuildTableName(_table, _company, _schemaPrefix, _isSharedRoot);

    public void AddWhere(Expression expr)
    {
        var visitor = new SqlExpressionVisitor(_context, _dialect);
        visitor.Visit(expr);
        _wheres.Add(visitor.GetSql());
    }

    public void AddWhereRaw(string sql, object? parameters)
    {
        ArgumentNullException.ThrowIfNull(sql);
        if (string.IsNullOrWhiteSpace(sql)) throw new ArgumentException("Raw SQL must not be empty.", nameof(sql));
        if (parameters != null)
        {
             if (parameters is IDictionary<string, object> dict)
             {
                  foreach(var kv in dict) _context.Parameters.Add(kv.Key, kv.Value);
             }
             else
             {
                  foreach(var prop in parameters.GetType().GetProperties())
                  {
                      _context.Parameters.Add(prop.Name, prop.GetValue(parameters) ?? DBNull.Value);
                  }
             }
        }
        _wheres.Add(sql);
    }

    public void AddJoin(JoinExpression join)
    {
        var aliasByType = new Dictionary<Type, string>();
        if (_joinExpressions.Count > 0)
        {
            aliasByType[_joinExpressions[0].LeftType] = RootAlias;
            for (int i = 0; i < _joinExpressions.Count; i++)
                aliasByType[_joinExpressions[i].RightType] = "t" + (i + 1);
        }
        else
        {
            aliasByType[join.LeftType] = RootAlias;
        }

        _joinIndex++;
        var rightAlias = "t" + _joinIndex;
        var rightTableFull = ResolveTableNameForType(join.RightType);
        
        var (leftAliasResolved, leftCol) = ResolveQualifiedColumn(join.LeftKey.Body, aliasByType);
        string leftAlias = leftAliasResolved ?? RootAlias;
        
        var rightCol = GetColumnNameFromArg(join.RightKey.Body);
        
        string joinType = "INNER JOIN";
        switch(join.JoinType)
        {
            case JoinType.Left: joinType = "LEFT JOIN"; break;
            case JoinType.Right: joinType = "RIGHT JOIN"; break;
            case JoinType.Full: joinType = "FULL OUTER JOIN"; break;
        }
        
        _joinExpressions.Add(join);
        _joins.Add($"{joinType} {rightTableFull} {rightAlias} ON {leftAlias}.{Q(leftCol!)} = {rightAlias}.{Q(rightCol!)}");
    }
    
    private string ResolveTableNameForType(Type t)
    {
        var name = EntityMetadataResolvers.GetTableName(t);
        var attr = t.GetCustomAttribute<NavTableAttribute>();
        var isShared = attr?.IsShared ?? false;
        return _dialect.BuildTableName(name, _company, _schemaPrefix, isShared);
    }

    public void SetSelector(LambdaExpression selector)
    {
        if (_joins.Count > 0)
        {
            BuildQualifiedSelect(selector);
            return;
        }
        // Simple column extraction if new { x.A, x.B }
        if (selector.Body is NewExpression ne)
        {
            var cols = new List<string>();
            for (int i = 0; i < ne.Arguments.Count; i++)
            {
                var colName = GetColumnNameFromArg(ne.Arguments[i]);
                if (colName != null)
                {
                    var sql = Q(colName);
                    var targetName = ne.Members?[i].Name;
                    if (targetName != null && !string.Equals(colName, targetName, StringComparison.OrdinalIgnoreCase))
                        sql += " AS " + Q(targetName);
                    cols.Add(sql);
                }
            }
            _select = string.Join(", ", cols);
        }
        else if (selector.Body is ParameterExpression)
        {
            _select = "*";
        }
        // HotChocolate [UseProjection] generates: new T { Code = x.Code, Name = x.Name } = MemberInitExpression
        else if (selector.Body is MemberInitExpression mi)
        {
            var cols = new List<string>();
            foreach (var b in mi.Bindings.OfType<MemberAssignment>())
            {
                if (b.Member is System.Reflection.PropertyInfo prop &&
                    prop.GetCustomAttribute<SqlNotMappedAttribute>() != null)
                    continue;

                var colName = GetColumnNameFromArg(b.Expression);
                // After GroupBy, selector projects IGroupResult.Key; map to the actual GROUP BY column so SQL uses e.g. [Customer No_] not [Key].
                if (string.Equals(colName, "Key", StringComparison.Ordinal) && _groupBys.Count > 0)
                    cols.Add(_groupBys[0] + " AS " + Q(b.Member.Name));
                else if (colName != null)
                {
                    string sql = Q(colName);
                    if (!string.Equals(colName, b.Member.Name, StringComparison.OrdinalIgnoreCase))
                        sql += " AS " + Q(b.Member.Name);
                    cols.Add(sql);
                }
            }
            if (cols.Count > 0)
                _select = string.Join(", ", cols);
            else
                _select = "*"; // fallback if we can't resolve any column
        }
        else 
        {
            var colName = GetColumnNameFromArg(selector.Body);
            if (colName != null) 
                _select = Q(colName);
            else if (selector.Body is MemberExpression me)
                _select = Q(me.Member.Name);
        }
    }
    
    private void BuildQualifiedSelect(LambdaExpression selector)
    {
        var aliasByType = new Dictionary<Type, string>();
        if (_joinExpressions.Count > 0)
        {
            aliasByType[_joinExpressions[0].LeftType] = RootAlias;
            for (int i = 0; i < _joinExpressions.Count; i++)
                aliasByType[_joinExpressions[i].RightType] = "t" + (i + 1);
        }
        
        var parts = new List<string>();
        ExtractColumnsFromExpression(selector.Body, parts, aliasByType);
        // Do not use Distinct() - duplicate column names (e.g. t5.[Name] from LoginPermission and Permission) must stay so each nested type gets its own copy when materializing; collapsing to first occurrence leaves PermissionSet.Name/Action/Icon unbound.
        _select = parts.Count > 0 ? string.Join(", ", parts) : "*";
    }

    private void ExtractColumnsFromExpression(Expression expr, List<string> parts, Dictionary<Type, string> aliasByType)
    {
        if (expr is NewExpression ne)
        {
            foreach (var arg in ne.Arguments) ExtractColumnsFromExpression(arg, parts, aliasByType);
        }
        else if (expr is MemberInitExpression mie)
        {
            ExtractColumnsFromExpression(mie.NewExpression, parts, aliasByType);
            foreach (var binding in mie.Bindings)
            {
                if (binding is MemberAssignment ma)
                {
                    if (ma.Member is System.Reflection.PropertyInfo prop &&
                        prop.GetCustomAttribute<SqlNotMappedAttribute>() != null)
                        continue;
                    ExtractColumnWithTargetName(ma.Expression, ma.Member.Name, parts, aliasByType);
                }
            }
        }
        else if (expr is MemberExpression me)
        {
            var (alias, col) = ResolveQualifiedColumn(expr, aliasByType);
            if (alias != null && col != null)
                parts.Add($"{alias}.{Q(col)}");
            else if (col != null)
                parts.Add(Q(col));
        }
        else if (expr is ParameterExpression)
        {
            // Fallback: If they select a raw parameter directly, map as * as we can't break down its properties here easily
            parts.Add("*");
        }
    }
    
    /// <summary>Emit a select part, aliasing as targetMemberName when it differs from the source column so the materializer binds correctly (e.g. t1.[Name] AS [CustomerName]).</summary>
    private void ExtractColumnWithTargetName(Expression expr, string targetMemberName, List<string> parts, Dictionary<Type, string> aliasByType)
    {
        // Unwrap null-coalescing (e.g. node.Right.Name ?? "") so we can resolve the member access for the column
        if (expr is BinaryExpression bin && bin.NodeType == ExpressionType.Coalesce)
            expr = bin.Left;
        if (expr is MemberExpression me)
        {
            var (alias, col) = ResolveQualifiedColumn(expr, aliasByType);
            if (alias != null && col != null)
            {
                var qualified = $"{alias}.{Q(col)}";
                if (!string.Equals(col, targetMemberName, StringComparison.OrdinalIgnoreCase))
                    qualified += $" AS {Q(targetMemberName)}";
                parts.Add(qualified);
                return;
            }
            if (col != null)
            {
                var part = Q(col);
                if (!string.Equals(col, targetMemberName, StringComparison.OrdinalIgnoreCase))
                    part += $" AS {Q(targetMemberName)}";
                parts.Add(part);
                return;
            }
        }
        ExtractColumnsFromExpression(expr, parts, aliasByType);
    }

    private static (string? alias, string? column) ResolveQualifiedColumn(Expression expr, Dictionary<Type, string> aliasByType)
    {
        if (expr is not MemberExpression me)
            return (null, GetColumnNameFromArg(expr));
        if (me.Expression is MemberExpression parent && (parent.Member.Name == "Left" || parent.Member.Name == "Right"))
        {
            var propType = (parent.Member as System.Reflection.PropertyInfo)?.PropertyType;
            if (propType != null && aliasByType.TryGetValue(propType, out var alias))
            {
                var col = EntityMetadataResolvers.GetColumnName(me.Member);
                return (alias, col);
            }
        }
        var colName = GetColumnNameFromArg(expr);
        return (null, colName);
    }
    
    public void AddOrderBy(string clause) => _orderBys.Add(clause);
    public void AddGroupBy(string clause) => _groupBys.Add(clause);
    public void AddRawSelect(string sql, object? parameters)
    {
        ArgumentNullException.ThrowIfNull(sql);
        if (string.IsNullOrWhiteSpace(sql)) throw new ArgumentException("Raw SQL must not be empty.", nameof(sql));
        if (parameters != null)
        {
            if (parameters is IDictionary<string, object> dict)
                foreach (var kv in dict) _context.Parameters.Add(kv.Key, kv.Value);
            else
                foreach (var prop in parameters.GetType().GetProperties())
                    _context.Parameters.Add(prop.Name, prop.GetValue(parameters) ?? DBNull.Value);
        }
        _rawSelects.Add(sql);
    }
    public void AddHavingRaw(string sql, object? parameters)
    {
        ArgumentNullException.ThrowIfNull(sql);
        if (string.IsNullOrWhiteSpace(sql)) throw new ArgumentException("Raw SQL must not be empty.", nameof(sql));
        if (parameters != null)
        {
            if (parameters is IDictionary<string, object> dict)
                foreach (var kv in dict) _context.Parameters.Add(kv.Key, kv.Value);
            else
                foreach (var prop in parameters.GetType().GetProperties())
                    _context.Parameters.Add(prop.Name, prop.GetValue(parameters) ?? DBNull.Value);
        }
        _havings.Add(sql);
    }
    
    public void AddCte(string alias, string sql)
    {
        _ctes.Add((alias, sql));
    }
    public void SetDistinct(bool distinct) => _distinct = distinct;
    public void SetSkip(int skip) => _skip = skip;
    public void SetTake(int take) => _take = take;
    
    // Internal helper exposed for Query
    public static string GetColumnNameFromExpr(LambdaExpression expr)
    {
         return GetColumnNameFromArg(expr.Body) ?? "*";
    }

    /// <summary>
    /// Like <see cref="GetColumnNameFromExpr"/> but prefixes <c>t0.</c>/<c>t1.</c> when the member has
    /// <see cref="JoinSqlAliasAttribute"/> (required for JOINs where column names collide, e.g. <c>No_</c>).
    /// </summary>
    public static string GetQualifiedColumnSqlFromExpr(LambdaExpression expr, IDialect dialect)
    {
        var member = GetMemberFromBody(expr.Body);
        if (member == null) return "*";
        var colName = EntityMetadataResolvers.GetColumnName(member);
        var quoted = dialect.QuoteIdentifier(colName);
        var ja = member.GetCustomAttribute<JoinSqlAliasAttribute>();
        return ja != null ? $"{ja.Alias}.{quoted}" : quoted;
    }

    private static MemberInfo? GetMemberFromBody(Expression body)
    {
        if (body is BinaryExpression bin && bin.NodeType == ExpressionType.Coalesce)
            body = bin.Left;
        if (body is MemberExpression m) return m.Member;
        if (body is UnaryExpression u && u.Operand is MemberExpression um) return um.Member;
        return null;
    }

    private static string? GetColumnNameFromArg(Expression expr)
    {
        if (expr is BinaryExpression bin && bin.NodeType == ExpressionType.Coalesce)
            expr = bin.Left;
            
        MemberInfo? member = null;
        if (expr is MemberExpression m) member = m.Member;
        else if (expr is UnaryExpression u && u.Operand is MemberExpression um) member = um.Member;
        if (member == null) return null;
        return EntityMetadataResolvers.GetColumnName(member);
    }

    private string GetColumnName(LambdaExpression expr)
    {
        return GetColumnNameFromArg(expr.Body) ?? "*";
    }

    public void SetAggregate(string func, LambdaExpression? selector)
    {
        _aggregateFunction = func;
        if (selector != null)
        {
             var colName = GetColumnName(selector);
             _aggregateColumn = colName == "*" ? "*" : Q(colName);
        }
        else
        {
             _aggregateColumn = "*";
        }
    }

    public QueryDescriptor Build()
    {
         if (_ctes.Count > 0)
         {
             _sb.Append("WITH ");
             for(int i = 0; i < _ctes.Count; i++)
             {
                 if (i > 0) _sb.Append(", ");
                 _sb.Append($"{Q(_ctes[i].Alias)} AS ({_ctes[i].Sql})");
             }
             _sb.Append(" ");
         }
         
         _sb.Append("SELECT ");
         if (_distinct && _aggregateFunction == null) _sb.Append("DISTINCT ");
         
         if (_take.HasValue && !_skip.HasValue && _aggregateFunction == null) 
             _sb.Append($"TOP ({_take}) ");
         
         if (_aggregateFunction != null)
         {
             _sb.Append($"{_aggregateFunction}({_aggregateColumn})");
         }
         else
         {
             _sb.Append(_select);
             if (_rawSelects.Count > 0)
             {
                 // Always separate the projected columns from SelectRaw fragments. When _select is "*",
                 // we must emit "*, (expr) AS Alias" — omitting the comma produced invalid SQL: "*(".
                 _sb.Append(", ");
                 _sb.Append(string.Join(", ", _rawSelects));
             }
         }

         var rootTableFull = ResolveRootTableName();
         _sb.Append($" FROM {rootTableFull} {RootAlias}");
         
         foreach(var j in _joins) _sb.Append(" " + j);
         
         if (_wheres.Count > 0)
         {
             _sb.Append(" WHERE ");
             _sb.Append(string.Join(" AND ", _wheres));
         }

         if (_groupBys.Count > 0)
         {
             _sb.Append(" GROUP BY ");
             _sb.Append(string.Join(", ", _groupBys));
         }

         if (_havings.Count > 0)
         {
             _sb.Append(" HAVING ");
             _sb.Append(string.Join(" AND ", _havings));
         }

         if (_aggregateFunction == null && _orderBys.Count > 0)
             _sb.Append($" ORDER BY {string.Join(", ", _orderBys)}");
             
         if (_skip.HasValue && _aggregateFunction == null)
         {
             if (_orderBys.Count == 0) _sb.Append(" ORDER BY (SELECT NULL)");
             _sb.Append($" OFFSET {_skip} ROWS FETCH NEXT {_take ?? int.MaxValue} ROWS ONLY");
         }

         return new QueryDescriptor { Sql = _sb.ToString(), Parameters = _context.Parameters };
    }
    
    // Write Ops Generators
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<(Type, string), string> _insertCache = new();
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<(Type, string), string> _updateCache = new();
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<(Type, string), string> _deleteCache = new();
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<(Type, string), string> _upsertCache = new();

    public static QueryDescriptor GenerateInsert<T>(T entity, string tableName)
    {
        var ctx = new ParameterContext();
        
        if (!_insertCache.TryGetValue((typeof(T), tableName), out var template))
        {
            var sb = new StringBuilder();
            sb.Append($"INSERT INTO {tableName} (");
            var colNames = new List<string>();
            var paramNames = new List<string>();
            int pIdx = 0;
            
            foreach(var map in EntityMetadata<T>.MappedProperties)
            {
                if (IsReadOnlySqlColumn(map.ColumnName)) continue;
                colNames.Add($"[{map.ColumnName}]");
                paramNames.Add($"@p{pIdx++}");
            }
            sb.Append(string.Join(", ", colNames));
            sb.Append(") VALUES (");
            sb.Append(string.Join(", ", paramNames));
            sb.Append(")");
            template = sb.ToString();
            _insertCache.TryAdd((typeof(T), tableName), template);
        }
        
        foreach(var map in EntityMetadata<T>.MappedProperties)
        {
            if (IsReadOnlySqlColumn(map.ColumnName)) continue;
            ctx.Add(map.Property.GetValue(entity));
        }
        
        return new QueryDescriptor { Sql = template, Parameters = ctx.Parameters };
    }
    
    public static QueryDescriptor GenerateUpdate<T>(T entity, string tableName)
    {
        var keyProps = EntityMetadata<T>.KeyProperties;
        if (keyProps.Count == 0) throw new InvalidOperationException($"Could not determine primary key for Update on {typeof(T).Name}. Use [NavKey] attribute.");
        
        var ctx = new ParameterContext();
        
        if (!_updateCache.TryGetValue((typeof(T), tableName), out var template))
        {
            var sb = new StringBuilder();
            sb.Append($"UPDATE {tableName} SET ");
            var updates = new List<string>();
            int pIdx = 0;
            
            foreach(var map in EntityMetadata<T>.MappedProperties)
            {
                if (keyProps.Any(k => k.Name == map.Property.Name)) continue;
                if (IsReadOnlySqlColumn(map.ColumnName)) continue;
                updates.Add($"[{map.ColumnName}] = @p{pIdx++}");
            }
            sb.Append(string.Join(", ", updates));
            
            sb.Append(" WHERE ");
            var whereParts = new List<string>();
            foreach(var keyProp in keyProps)
            {
                EntityMetadata<T>.PropertyToColumnMap.TryGetValue(keyProp.Name, out var keyCol);
                keyCol ??= keyProp.Name;
                whereParts.Add($"[{keyCol}] = @p{pIdx++}");
            }
            sb.Append(string.Join(" AND ", whereParts));
            template = sb.ToString();
            _updateCache.TryAdd((typeof(T), tableName), template);
        }
        
        // Populate parameters securely in identical order
        foreach(var map in EntityMetadata<T>.MappedProperties)
        {
             if (keyProps.Any(k => k.Name == map.Property.Name)) continue;
             if (IsReadOnlySqlColumn(map.ColumnName)) continue;
             ctx.Add(map.Property.GetValue(entity));
        }
        foreach(var keyProp in keyProps)
        {
             ctx.Add(keyProp.GetValue(entity));
        }
        
        return new QueryDescriptor { Sql = template, Parameters = ctx.Parameters };
    }

    public static QueryDescriptor GenerateUpdate<T>(T entity, string tableName, Expression<Func<T, object>>[] selectors)
    {
        var keyProps = EntityMetadata<T>.KeyProperties;
        if (keyProps.Count == 0) throw new InvalidOperationException($"Could not determine primary key for Update on {typeof(T).Name}. Use [NavKey] attribute.");
        
        var ctx = new ParameterContext();
        
        // Resolve property names from selectors
        var propsToUpdate = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach(var selector in selectors)
        {
            var propName = GetPropertyName(selector.Body);
            if (propName != null) propsToUpdate.Add(propName);
        }
        if (propsToUpdate.Count == 0) throw new ArgumentException("No valid properties selected for update.");

        var sb = new StringBuilder();
        sb.Append($"UPDATE {tableName} SET ");
        
        var updates = new List<string>();
        int pIdx = 0;
        
        // Loop through all mapped properties to maintain correct order and handle mapped column names
        foreach(var map in EntityMetadata<T>.MappedProperties)
        {
            if (!propsToUpdate.Contains(map.Property.Name)) continue;
            if (keyProps.Any(k => k.Name == map.Property.Name)) continue;
            if (IsReadOnlySqlColumn(map.ColumnName)) continue;
            
            updates.Add($"[{map.ColumnName}] = @p{pIdx++}");
            ctx.Add(map.Property.GetValue(entity));
        }
        
        sb.Append(string.Join(", ", updates));
        sb.Append(" WHERE ");
        
        var whereParts = new List<string>();
        foreach(var keyProp in keyProps)
        {
            EntityMetadata<T>.PropertyToColumnMap.TryGetValue(keyProp.Name, out var keyCol);
            keyCol ??= keyProp.Name;
            whereParts.Add($"[{keyCol}] = @p{pIdx++}");
            ctx.Add(keyProp.GetValue(entity));
        }
        sb.Append(string.Join(" AND ", whereParts));
        
        return new QueryDescriptor { Sql = sb.ToString(), Parameters = ctx.Parameters };
    }

    private static string? GetPropertyName(Expression expr)
    {
        if (expr is UnaryExpression u && u.NodeType == ExpressionType.Convert) expr = u.Operand;
        if (expr is MemberExpression m) return m.Member.Name;
        return null;
    }
    
    public static QueryDescriptor GenerateDelete<T>(T entity, string tableName)
    {
        var keyProps = EntityMetadata<T>.KeyProperties;
        if (keyProps.Count == 0) throw new InvalidOperationException($"Could not determine primary key for Delete on {typeof(T).Name}. Use [NavKey] attribute.");
        
        var ctx = new ParameterContext();
        
        if (!_deleteCache.TryGetValue((typeof(T), tableName), out var template))
        {
            var sb = new StringBuilder();
            var whereParts = new List<string>();
            int pIdx = 0;
            foreach(var keyProp in keyProps)
            {
                EntityMetadata<T>.PropertyToColumnMap.TryGetValue(keyProp.Name, out var keyCol);
                keyCol ??= keyProp.Name;
                whereParts.Add($"[{keyCol}] = @p{pIdx++}");
            }
            sb.Append($"DELETE FROM {tableName} WHERE {string.Join(" AND ", whereParts)}");
            template = sb.ToString();
            _deleteCache.TryAdd((typeof(T), tableName), template);
        }
        
        return new QueryDescriptor { Sql = template, Parameters = ctx.Parameters };
    }
    
    public static QueryDescriptor GenerateUpsert<T>(T entity, string tableName)
    {
        var keyProps = EntityMetadata<T>.KeyProperties;
        if (keyProps.Count == 0) throw new InvalidOperationException($"Could not determine primary key for Upsert on {typeof(T).Name}. Use [NavKey] attribute.");
        
        var ctx = new ParameterContext();
        
        if (!_upsertCache.TryGetValue((typeof(T), tableName), out var template))
        {
            // SQL Server MERGE implementation
            var sb = new StringBuilder();
            sb.Append($"MERGE INTO {tableName} AS target USING (SELECT ");
            var cols = new List<string>();
            var paramNames = new List<string>();
            var insertCols = new List<string>();
            var updateCols = new List<string>();
            var matchCols = new List<string>();

            int pIdx = 0;
            foreach (var map in EntityMetadata<T>.MappedProperties)
            {
                if (IsReadOnlySqlColumn(map.ColumnName)) continue;
                var p = $"@p{pIdx++}";
                cols.Add($"{p} AS [{map.ColumnName}]");
                insertCols.Add($"[{map.ColumnName}]");
                paramNames.Add($"source.[{map.ColumnName}]");
                
                if (keyProps.Any(k => k.Name == map.Property.Name))
                {
                    matchCols.Add($"target.[{map.ColumnName}] = source.[{map.ColumnName}]");
                }
                else
                {
                    updateCols.Add($"target.[{map.ColumnName}] = source.[{map.ColumnName}]");
                }
            }

            sb.Append(string.Join(", ", cols));
            sb.Append($") AS source ON {string.Join(" AND ", matchCols)}");
            
            if (updateCols.Count > 0)
            {
                sb.Append($" WHEN MATCHED THEN UPDATE SET {string.Join(", ", updateCols)}");
            }
            
            sb.Append($" WHEN NOT MATCHED THEN INSERT ({string.Join(", ", insertCols)}) VALUES ({string.Join(", ", paramNames)});");
            
            template = sb.ToString();
            _upsertCache.TryAdd((typeof(T), tableName), template);
        }
        
        foreach(var map in EntityMetadata<T>.MappedProperties)
        {
            if (IsReadOnlySqlColumn(map.ColumnName)) continue;
            ctx.Add(map.Property.GetValue(entity));
        }
        
        return new QueryDescriptor { Sql = template, Parameters = ctx.Parameters };
    }

    /// <summary>SQL Server timestamp/rowversion columns are read-only and must be excluded from UPDATE SET.</summary>
    private static bool IsReadOnlySqlColumn(string columnName) =>
        string.Equals(columnName, "timestamp", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(columnName, "rowversion", StringComparison.OrdinalIgnoreCase);
}
