using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using Tyresoles.Sql.Abstractions;
using Tyresoles.Sql.Core.Metadata;

namespace Tyresoles.Sql.Core.Query;

internal class ParameterContext
{
    public Dictionary<string, object> Parameters { get; } = new();
    private int _counter;
    private string? _prefix;
    private Stack<(string?, int)>? _prefixStack;

    /// <summary>Use a prefix for parameter names (e.g. "q0" -> @q0_p0, @q0_p1). Use with PushPrefix/PopPrefix for multi-query batches.</summary>
    public void PushPrefix(string prefix)
    {
        _prefixStack ??= new Stack<(string?, int)>();
        _prefixStack.Push((_prefix, _counter));
        _prefix = prefix;
        _counter = 0;
    }

    public void PopPrefix()
    {
        if (_prefixStack != null && _prefixStack.Count > 0)
        {
            var (prevPrefix, prevCounter) = _prefixStack.Pop();
            _prefix = prevPrefix;
            _counter = prevCounter;
        }
    }

    public string Add(object? value)
    {
        var name = _prefix != null ? $"@{_prefix}_p{_counter++}" : $"@p{_counter++}";
        Parameters[name] = value ?? DBNull.Value;
        return name;
    }
}

internal class SqlExpressionVisitor : ExpressionVisitor
{
    private readonly StringBuilder _sb = new();
    private readonly ParameterContext _context;
    private readonly IDialect? _dialect;

    public SqlExpressionVisitor(ParameterContext context, IDialect? dialect = null)
    {
        _context = context;
        _dialect = dialect;
    }

    public string GetSql() => _sb.ToString();
    
    private string Q(string name) => _dialect != null ? _dialect.QuoteIdentifier(name) : $"[{name}]";

    private bool IsNavCodeMember(Expression? expr)
    {
        while (expr != null)
        {
            if (expr is MemberExpression m && m.Expression != null && m.Expression.NodeType == ExpressionType.Parameter)
                return EntityMetadataResolvers.IsNavCode(m.Member);
            
            if (expr is UnaryExpression u && u.NodeType == ExpressionType.Convert)
            {
                expr = u.Operand;
                continue;
            }
            
            if (expr is MethodCallExpression mc && (mc.Method.Name == "ToLower" || mc.Method.Name == "ToUpper" || mc.Method.Name == "Trim"))
            {
                expr = mc.Object;
                continue;
            }
            
            break;
        }
        return false;
    }

    protected override Expression VisitBinary(BinaryExpression node)
    {
        _sb.Append("(");
        
        bool isLeftNavCode = IsNavCodeMember(node.Left);
        bool isRightNavCode = IsNavCodeMember(node.Right);

        // Intercept C# parameter values and evaluate them explicitly upper-case natively to hit Binary/CI B-Tree indexes directly
        object? coerceValue = null;
        if (isLeftNavCode && CanEvaluate(node.Right))
        {
            var val = Evaluate(node.Right);
            coerceValue = val is string s ? s.ToUpperInvariant() : val;
        }
        else if (isRightNavCode && CanEvaluate(node.Left))
        {
            var val = Evaluate(node.Left);
            coerceValue = val is string s ? s.ToUpperInvariant() : val;
        }

        if (coerceValue != null && isRightNavCode) 
        {
            _sb.Append(_context.Add(coerceValue));
        }
        else 
        {
            Visit(node.Left);
        }
        
        switch (node.NodeType)
        {
            case ExpressionType.Equal:
                if (IsNuLL(node.Right)) _sb.Append(" IS NULL");
                else _sb.Append(" = ");
                break;
            case ExpressionType.NotEqual:
                if (IsNuLL(node.Right)) _sb.Append(" IS NOT NULL");
                else _sb.Append(" <> ");
                break;
            case ExpressionType.GreaterThan: _sb.Append(" > "); break;
            case ExpressionType.GreaterThanOrEqual: _sb.Append(" >= "); break;
            case ExpressionType.LessThan: _sb.Append(" < "); break;
            case ExpressionType.LessThanOrEqual: _sb.Append(" <= "); break;
            case ExpressionType.AndAlso: _sb.Append(" AND "); break;
            case ExpressionType.OrElse: _sb.Append(" OR "); break;
            case ExpressionType.Coalesce: _sb.Append(" ISNULL("); Visit(node.Left); _sb.Append(", "); Visit(node.Right); _sb.Append(")"); return node; // T-SQL ISNULL
            default: throw new NotSupportedException($"Operator {node.NodeType} not supported");
        }

        if (!IsNuLL(node.Right))
        {
             if (coerceValue != null && isLeftNavCode)
                 _sb.Append(_context.Add(coerceValue));
             else 
                 Visit(node.Right);
        }
        
        _sb.Append(")");
        return node;
    }

    protected override Expression VisitUnary(UnaryExpression node)
    {
        if (node.NodeType == ExpressionType.Not)
        {
            _sb.Append(" NOT (");
            Visit(node.Operand);
            _sb.Append(")");
            return node;
        }
        if (node.NodeType == ExpressionType.Convert)
        {
            Visit(node.Operand);
            return node;
        }
        return base.VisitUnary(node);
    }
    
    private bool IsNuLL(Expression node)
    {
        return node is ConstantExpression c && c.Value == null;
    }

    protected override Expression VisitMember(MemberExpression node)
    {
        if (node.Type == typeof(DateTime) || node.Type == typeof(DateTime?))
        {
            // Date logic
        }

        if (node.Expression != null && node.Expression.NodeType == ExpressionType.Parameter)
        {
             // Simple column access
             if (node.Member.Name == "HasValue") // Nullable HasValue check
             {
                 // Usually rewritten by compiler?
                 // Ignore for now.
             }
             
             // Handle DateTime Properties
             if (node.Member.DeclaringType == typeof(DateTime) || node.Member.DeclaringType == typeof(DateTime?))
             {
                 if (node.Member.Name == "Year") { _sb.Append("YEAR("); Visit(node.Expression); _sb.Append(")"); return node; }
                 if (node.Member.Name == "Month") { _sb.Append("MONTH("); Visit(node.Expression); _sb.Append(")"); return node; }
                 if (node.Member.Name == "Day") { _sb.Append("DAY("); Visit(node.Expression); _sb.Append(")"); return node; }
             }

             if (node.Member.Name == "Length" && node.Type == typeof(int))
             {
                 _sb.Append("LEN("); Visit(node.Expression); _sb.Append(")"); return node;
             }

             var colName = EntityMetadataResolvers.GetColumnName(node.Member);
             var ja = node.Member.GetCustomAttribute<JoinSqlAliasAttribute>();
             if (ja != null)
                 _sb.Append(ja.Alias).Append('.');
             _sb.Append(Q(colName));
             return node;
        }

        if (node.Expression != null && node.Expression.NodeType == ExpressionType.MemberAccess)
        {
            // Possibly nested member access or closure variable
             if (CanEvaluate(node))
             {
                 var val = Evaluate(node);
                 _sb.Append(_context.Add(val));
                 return node;
             }
             else 
             {
                 // It's a nested table mapping (like uc.Right.Default)
                 var name = EntityMetadataResolvers.GetColumnName(node.Member);
                 var jaNested = node.Member.GetCustomAttribute<JoinSqlAliasAttribute>();
                 if (jaNested != null)
                     _sb.Append(jaNested.Alias).Append('.');
                 _sb.Append(Q(name));
                 return node;
             }
        }
        if (CanEvaluate(node))
        {
             var val = Evaluate(node);
             _sb.Append(_context.Add(val));
             return node;
        }

        throw new NotSupportedException($"Member access {node.Member.Name} not supported");
    }

    protected override Expression VisitInvocation(InvocationExpression node)
    {
        if (CanEvaluate(node))
        {
            var val = Evaluate(node);
            _sb.Append(_context.Add(val));
            return node;
        }
        return base.VisitInvocation(node);
    }

    protected override Expression VisitConstant(ConstantExpression node)
    {
        _sb.Append(_context.Add(node.Value));
        return node;
    }

    protected override Expression VisitMethodCall(MethodCallExpression node)
    {
        if (CanEvaluate(node))
        {
            var val = Evaluate(node);
            _sb.Append(_context.Add(val));
            return node;
        }

        if (node.Method.Name == "ToLower" && node.Method.DeclaringType == typeof(string))
        {
            if (node.Object is MemberExpression me && me.Expression != null && me.Expression.NodeType != ExpressionType.Parameter)
            {
                var val = Evaluate(node);
                _sb.Append(_context.Add(val));
                return node;
            }
            
            // Replaced LOWER() rendering to preserve B-Tree index scanning automatically on CI collations
            Visit(node.Object!);
            return node;
        }

        if (node.Method.Name == "ToUpper" && node.Method.DeclaringType == typeof(string))
        {
            if (node.Object is MemberExpression me && me.Expression != null && me.Expression.NodeType != ExpressionType.Parameter)
            {
                var val = Evaluate(node);
                _sb.Append(_context.Add(val));
                return node;
            }
            
            // Replaced UPPER() rendering to preserve B-Tree index scanning automatically on CI collations
            Visit(node.Object!);
            return node;
        }
        // GraphQL string filters (contains / startsWith / endsWith) compile to these methods.
        // Use LOWER on both sides so LIKE is case-insensitive regardless of database collation.
        if (node.Method.Name == "Contains" && node.Method.DeclaringType == typeof(string))
        {
            var arg = node.Arguments[0];
            var val = Evaluate(arg);
            var pattern = val is string s ? $"%{s}%" : $"%{val}%";
            _sb.Append("LOWER(");
            Visit(node.Object);
            _sb.Append(") LIKE LOWER(");
            _sb.Append(_context.Add(pattern));
            _sb.Append(")");
            return node;
        }

        if (node.Method.Name == "StartsWith" && node.Method.DeclaringType == typeof(string))
        {
            var arg = node.Arguments[0];
            var val = Evaluate(arg);
            var pattern = val is string s ? $"{s}%" : $"{val}%";
            _sb.Append("LOWER(");
            Visit(node.Object);
            _sb.Append(") LIKE LOWER(");
            _sb.Append(_context.Add(pattern));
            _sb.Append(")");
            return node;
        }

        if (node.Method.Name == "EndsWith" && node.Method.DeclaringType == typeof(string))
        {
            var arg = node.Arguments[0];
            var val = Evaluate(arg);
            var pattern = val is string s ? $"%{s}" : $"%{val}";
            _sb.Append("LOWER(");
            Visit(node.Object);
            _sb.Append(") LIKE LOWER(");
            _sb.Append(_context.Add(pattern));
            _sb.Append(")");
            return node;
        }
        
        // Contains(collection, value) - extension: collection in Arguments[0], column in Arguments[1]
        if (node.Method.Name == "Contains" && node.Object == null && node.Arguments.Count >= 2)
        {
            var list = Evaluate(node.Arguments[0]);
            var enumerable = list as System.Collections.IEnumerable;
            if (enumerable == null)
                goto NotContainsExtension;
            var values = new List<object>();
            bool isNavCode = IsNavCodeMember(node.Arguments[1]);
            if (enumerable != null)
            {
                foreach (var item in enumerable)
                {
                    var v = item;
                    if (isNavCode && v is string s) v = s.ToUpperInvariant();
                    values.Add(v!);
                }
            }
            if (values.Count == 0) { _sb.Append(" 1=0"); return node; }
            Visit(node.Arguments[1]);
            if (values.Count > 100 && _dialect?.Provider == DbProvider.SqlServer)
            {
                var jsonStr = System.Text.Json.JsonSerializer.Serialize(values);
                var pName = _context.Add(jsonStr);
                _sb.Append($" IN (SELECT value FROM OPENJSON({pName}))");
                return node;
            }
            if (values.Count > 100 && _dialect?.Provider == DbProvider.PostgreSQL)
            {
                var pName = _context.Add(values);
                _sb.Append($" = ANY({pName})");
                return node;
            }
            _sb.Append(" IN (");
            for (int i = 0; i < values.Count; i++)
            {
                if (i > 0) _sb.Append(", ");
                _sb.Append(_context.Add(values[i]));
            }
            _sb.Append(")");
            return node;
        }
        // Contains(collection, value) - extension as instance call: Object = collection, Arguments[0] = column (e.g. list.Contains(a.Team))
        if (node.Method.Name == "Contains" && node.Object != null && node.Arguments.Count == 1 && IsEnumerableContainsMethod(node.Method))
        {
            var list = Evaluate(node.Object);
            var enumerable = list as System.Collections.IEnumerable;
            if (enumerable == null)
                goto NotContainsInstance;
            var values = new List<object>();
            bool isNavCode = IsNavCodeMember(node.Arguments[0]);
            foreach (var item in enumerable)
            {
                var v = item;
                if (isNavCode && v is string s) v = s.ToUpperInvariant();
                values.Add(v!);
            }
            if (values.Count == 0) { _sb.Append(" 1=0"); return node; }
            Visit(node.Arguments[0]);
            if (values.Count > 100 && _dialect?.Provider == DbProvider.SqlServer)
            {
                var jsonStr = System.Text.Json.JsonSerializer.Serialize(values);
                var pName = _context.Add(jsonStr);
                _sb.Append($" IN (SELECT value FROM OPENJSON({pName}))");
                return node;
            }
            if (values.Count > 100 && _dialect?.Provider == DbProvider.PostgreSQL)
            {
                var pName = _context.Add(values);
                _sb.Append($" = ANY({pName})");
                return node;
            }
            _sb.Append(" IN (");
            for (int i = 0; i < values.Count; i++)
            {
                if (i > 0) _sb.Append(", ");
                _sb.Append(_context.Add(values[i]));
            }
            _sb.Append(")");
            return node;
        }
        NotContainsInstance:
        NotContainsExtension:

        if (node.Method.Name == "Contains" && node.Object != null && typeof(System.Collections.IEnumerable).IsAssignableFrom(node.Method.DeclaringType!))
        {
            // List.Contains(x.Id) -> IN Logic (instance method)
            var list = Evaluate(node.Object);
            // We need to expand list to parameters
            // This is complex. 
            // IN (@p1, @p2, ...)
            var enumerable = list as System.Collections.IEnumerable;
            var values = new List<object>();
            bool isNavCode = IsNavCodeMember(node.Arguments[0]);
            
            if (enumerable != null) 
            { 
                foreach(var item in enumerable) 
                {
                    var v = item;
                    if (isNavCode && v is string s) v = s.ToUpperInvariant();
                    values.Add(v);
                } 
            }
            
            if (values.Count == 0)
            {
                _sb.Append(" 1=0"); // False
                return node;
            }
            
            Visit(node.Arguments[0]); // The property
            
            if (values.Count > 100 && _dialect?.Provider == DbProvider.SqlServer)
            {
                var jsonStr = System.Text.Json.JsonSerializer.Serialize(values);
                var pName = _context.Add(jsonStr);
                _sb.Append($" IN (SELECT value FROM OPENJSON({pName}))");
                return node;
            }
            
            if (values.Count > 100 && _dialect?.Provider == DbProvider.PostgreSQL)
            {
                // Postgres Arrays
                var pName = _context.Add(values);
                _sb.Append($" = ANY({pName})");
                return node;
            }
            
            _sb.Append(" IN (");
            for(int i=0; i<values.Count; i++)
            {
                if (i > 0) _sb.Append(", ");
                _sb.Append(_context.Add(values[i]));
            }
            _sb.Append(")");
            return node;
        }
        
        throw new NotSupportedException($"Method {node.Method.Name} not supported");
    }

    private object? Evaluate(Expression node) => ParameterEvaluator.Evaluate(node);
    
    private class ParameterFinderVisitor : ExpressionVisitor
    {
        public bool HasParameter { get; private set; }
        protected override Expression VisitParameter(ParameterExpression node)
        {
            HasParameter = true;
            return base.VisitParameter(node);
        }
    }
    
    private bool CanEvaluate(Expression node)
    {
        var finder = new ParameterFinderVisitor();
        finder.Visit(node);
        return !finder.HasParameter;
    }

    private static bool IsEnumerableContainsMethod(MethodInfo method)
    {
        return method.Name == "Contains" && method.DeclaringType == typeof(Enumerable);
    }
}
