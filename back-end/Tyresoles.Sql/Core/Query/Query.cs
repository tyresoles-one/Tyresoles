using System.Collections.Immutable;
using System.Linq.Expressions;
using Tyresoles.Sql.Abstractions;

namespace Tyresoles.Sql.Core.Query;

internal record JoinExpression(LambdaExpression LeftKey, LambdaExpression RightKey, LambdaExpression Selector, Type LeftType, Type RightType, JoinType JoinType);

public class Query<T> : IQuery<T>, IQueryInternal where T : class
{
    private readonly TenantScope _scope;
    private readonly string _tableName;
    private readonly string? _company;
    private readonly ImmutableList<Expression> _wheres;
    private readonly ImmutableList<(string Sql, object? Parameters)> _rawWheres;
    private readonly ImmutableList<(string format, IQueryInternal subquery)> _subqueryWheres;
    private readonly ImmutableList<JoinExpression> _joins;
    private readonly LambdaExpression? _selector;
    private readonly ImmutableList<string> _orderBys;
    private readonly ImmutableList<string> _groupBys;
    private readonly ImmutableList<(string Sql, object? Parameters)> _rawSelects;
    private readonly ImmutableList<(string Sql, object? Parameters)> _havingRaws;
    private readonly IQueryInternal? _unionQuery;
    private readonly bool _unionAll;
    private readonly ImmutableList<(string Alias, IQueryInternal Subquery)> _ctes;
    private readonly int? _skip;
    private readonly int? _take;
    private readonly bool _distinct;
    
    internal int? SkipValue => _skip;
    internal IDialect Dialect => _scope.Dialect;
    bool IQueryInternal.HasJoins => _joins.Count > 0;

    public Query(TenantScope scope, string tableName, string? company)
        : this(scope, tableName, company, ImmutableList<Expression>.Empty, ImmutableList<(string, object?)>.Empty, ImmutableList<(string, IQueryInternal)>.Empty, ImmutableList<JoinExpression>.Empty, null, ImmutableList<string>.Empty, ImmutableList<string>.Empty, ImmutableList<(string, object?)>.Empty, ImmutableList<(string, object?)>.Empty, null, false, ImmutableList<(string, IQueryInternal)>.Empty, null, null, false)
    {
    }

    internal Query(
        TenantScope scope, 
        string tableName, 
        string? company,
        ImmutableList<Expression> wheres,
        ImmutableList<(string, object?)> rawWheres,
        ImmutableList<(string format, IQueryInternal subquery)> subqueryWheres,
        ImmutableList<JoinExpression> joins,
        LambdaExpression? selector,
        ImmutableList<string> orderBys,
        ImmutableList<string> groupBys,
        ImmutableList<(string Sql, object? Parameters)> rawSelects,
        ImmutableList<(string Sql, object? Parameters)> havingRaws,
        IQueryInternal? unionQuery,
        bool unionAll,
        ImmutableList<(string Alias, IQueryInternal Subquery)> ctes,
        int? skip,
        int? take,
        bool distinct)
    {
        _scope = scope;
        _tableName = tableName;
        _company = company;
        _wheres = wheres;
        _rawWheres = rawWheres;
        _subqueryWheres = subqueryWheres;
        _joins = joins;
        _selector = selector;
        _orderBys = orderBys;
        _groupBys = groupBys;
        _rawSelects = rawSelects;
        _havingRaws = havingRaws;
        _unionQuery = unionQuery;
        _unionAll = unionAll;
        _ctes = ctes;
        _skip = skip;
        _take = take;
        _distinct = distinct;
    }

    public IQuery<T> Where(Expression<Func<T, bool>> predicate)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        return new Query<T>(_scope, _tableName, _company, _wheres.Add(predicate), _rawWheres, _subqueryWheres, _joins, _selector, _orderBys, _groupBys, _rawSelects, _havingRaws, _unionQuery, _unionAll, _ctes, _skip, _take, _distinct);
    }
    
    public IQuery<T> Where(string sql, object? parameters = null)
    {
        ArgumentNullException.ThrowIfNull(sql);
        if (string.IsNullOrWhiteSpace(sql)) throw new ArgumentException("Raw SQL must not be empty.", nameof(sql));
        return new Query<T>(_scope, _tableName, _company, _wheres, _rawWheres.Add((sql, parameters)), _subqueryWheres, _joins, _selector, _orderBys, _groupBys, _rawSelects, _havingRaws, _unionQuery, _unionAll, _ctes, _skip, _take, _distinct);
    }

    public IQuery<T> Where<U>(IQuery<U> subquery, bool exists = true) where U : class
    {
        ArgumentNullException.ThrowIfNull(subquery);
        var op = exists ? "EXISTS ({0})" : "NOT EXISTS ({0})";
        return new Query<T>(_scope, _tableName, _company, _wheres, _rawWheres, _subqueryWheres.Add((op, (IQueryInternal)subquery)), _joins, _selector, _orderBys, _groupBys, _rawSelects, _havingRaws, _unionQuery, _unionAll, _ctes, _skip, _take, _distinct);
    }

    public IQuery<T> Where<U>(Expression<Func<T, object>> selector, IQuery<U> subquery, SubqueryOperator op = SubqueryOperator.In) where U : class
    {
        ArgumentNullException.ThrowIfNull(selector);
        ArgumentNullException.ThrowIfNull(subquery);
        var quoted = SqlBuilder.GetQualifiedColumnSqlFromExpr(selector, _scope.Dialect);
        
        string sqlOp = op switch {
            SubqueryOperator.In => "IN",
            SubqueryOperator.NotIn => "NOT IN",
            SubqueryOperator.Equals => "=",
            SubqueryOperator.NotEquals => "<>",
            SubqueryOperator.GreaterThan => ">",
            SubqueryOperator.LessThan => "<",
            SubqueryOperator.GreaterThanOrEqual => ">=",
            SubqueryOperator.LessThanOrEqual => "<=",
            _ => "IN"
        };

        return new Query<T>(_scope, _tableName, _company, _wheres, _rawWheres, _subqueryWheres.Add(($"{quoted} {sqlOp} ({{0}})", (IQueryInternal)subquery)), _joins, _selector, _orderBys, _groupBys, _rawSelects, _havingRaws, _unionQuery, _unionAll, _ctes, _skip, _take, _distinct);
    }

    public IQuery<T> Union(IQuery<T> other)
    {
        ArgumentNullException.ThrowIfNull(other);
        return new Query<T>(_scope, _tableName, _company, _wheres, _rawWheres, _subqueryWheres, _joins, _selector, _orderBys, _groupBys, _rawSelects, _havingRaws, (IQueryInternal)other, false, _ctes, _skip, _take, _distinct);
    }

    public IQuery<T> UnionAll(IQuery<T> other)
    {
        ArgumentNullException.ThrowIfNull(other);
        return new Query<T>(_scope, _tableName, _company, _wheres, _rawWheres, _subqueryWheres, _joins, _selector, _orderBys, _groupBys, _rawSelects, _havingRaws, (IQueryInternal)other, true, _ctes, _skip, _take, _distinct);
    }

    public IQuery<T> With<U>(string alias, IQuery<U> cteQuery) where U : class
    {
        ArgumentNullException.ThrowIfNull(cteQuery);
        return new Query<T>(_scope, _tableName, _company, _wheres, _rawWheres, _subqueryWheres, _joins, _selector, _orderBys, _groupBys, _rawSelects, _havingRaws, _unionQuery, _unionAll, _ctes.Add((alias, (IQueryInternal)cteQuery)), _skip, _take, _distinct);
    }

    public IQuery<T> HavingRaw(string sql, object? parameters = null)
    {
        ArgumentNullException.ThrowIfNull(sql);
        if (string.IsNullOrWhiteSpace(sql)) throw new ArgumentException("Raw SQL must not be empty.", nameof(sql));
        return new Query<T>(_scope, _tableName, _company, _wheres, _rawWheres, _subqueryWheres, _joins, _selector, _orderBys, _groupBys, _rawSelects, _havingRaws.Add((sql, parameters)), _unionQuery, _unionAll, _ctes, _skip, _take, _distinct);
    }

    public IQuery<TResult> Select<TResult>(Expression<Func<T, TResult>> selector) where TResult : class
    {
        ArgumentNullException.ThrowIfNull(selector);
        return new Query<TResult>(_scope, _tableName, _company, _wheres, _rawWheres, _subqueryWheres, _joins, selector, _orderBys, _groupBys, _rawSelects, _havingRaws, _unionQuery, _unionAll, _ctes, _skip, _take, _distinct);
    }

    public IQuery<T> SelectRaw(string sqlFragment, object? parameters = null)
    {
        ArgumentNullException.ThrowIfNull(sqlFragment);
        if (string.IsNullOrWhiteSpace(sqlFragment)) throw new ArgumentException("Raw SQL fragment must not be empty.", nameof(sqlFragment));
        return new Query<T>(_scope, _tableName, _company, _wheres, _rawWheres, _subqueryWheres, _joins, _selector, _orderBys, _groupBys, _rawSelects.Add((sqlFragment, parameters)), _havingRaws, _unionQuery, _unionAll, _ctes, _skip, _take, _distinct);
    }
    
    public IQuery<T> Distinct()
    {
        return new Query<T>(_scope, _tableName, _company, _wheres, _rawWheres, _subqueryWheres, _joins, _selector, _orderBys, _groupBys, _rawSelects, _havingRaws, _unionQuery, _unionAll, _ctes, _skip, _take, true);
    }

    public IQuery<TResult> Join<U, TResult>(Expression<Func<T, object>> tKey, Expression<Func<U, object>> uKey, Expression<Func<JoinQuery<T, U>, TResult>> selector, JoinType type = JoinType.Inner) where U : class where TResult : class
    {
        ArgumentNullException.ThrowIfNull(tKey);
        ArgumentNullException.ThrowIfNull(uKey);
        ArgumentNullException.ThrowIfNull(selector);
        var joinExp = new JoinExpression(tKey, uKey, selector, typeof(T), typeof(U), type);
        return new Query<TResult>(_scope, _tableName, _company, _wheres, _rawWheres, _subqueryWheres, _joins.Add(joinExp), selector, _orderBys, _groupBys, _rawSelects, _havingRaws, _unionQuery, _unionAll, _ctes, _skip, _take, _distinct);
    }

    // Legacy shim for existing code usage
    public IQuery<TResult> InnerJoin<U, TResult>(Expression<Func<T, object>> tKey, Expression<Func<U, object>> uKey, Expression<Func<JoinQuery<T, U>, TResult>> selector) where U : class where TResult : class
        => Join(tKey, uKey, selector, JoinType.Inner);

    public IQuery<T> OrderBy<TKey>(Expression<Func<T, TKey>> key)
    {
        ArgumentNullException.ThrowIfNull(key);
        var quoted = SqlBuilder.GetQualifiedColumnSqlFromExpr(key, _scope.Dialect);
        return new Query<T>(_scope, _tableName, _company, _wheres, _rawWheres, _subqueryWheres, _joins, _selector, _orderBys.Add($"{quoted} ASC"), _groupBys, _rawSelects, _havingRaws, _unionQuery, _unionAll, _ctes, _skip, _take, _distinct);
    }

    public IQuery<T> OrderByDescending<TKey>(Expression<Func<T, TKey>> key)
    {
        ArgumentNullException.ThrowIfNull(key);
        var quoted = SqlBuilder.GetQualifiedColumnSqlFromExpr(key, _scope.Dialect);
        return new Query<T>(_scope, _tableName, _company, _wheres, _rawWheres, _subqueryWheres, _joins, _selector, _orderBys.Add($"{quoted} DESC"), _groupBys, _rawSelects, _havingRaws, _unionQuery, _unionAll, _ctes, _skip, _take, _distinct);
    }

    public IQuery<T> ThenBy<TKey>(Expression<Func<T, TKey>> key) => OrderBy(key);
    public IQuery<T> ThenByDescending<TKey>(Expression<Func<T, TKey>> key) => OrderByDescending(key);

    public IQuery<T> Skip(int count)
    {
        return new Query<T>(_scope, _tableName, _company, _wheres, _rawWheres, _subqueryWheres, _joins, _selector, _orderBys, _groupBys, _rawSelects, _havingRaws, _unionQuery, _unionAll, _ctes, count, _take, _distinct);
    }

    public IQuery<T> Take(int count)
    {
        return new Query<T>(_scope, _tableName, _company, _wheres, _rawWheres, _subqueryWheres, _joins, _selector, _orderBys, _groupBys, _rawSelects, _havingRaws, _unionQuery, _unionAll, _ctes, _skip, count, _distinct);
    }

    public IQuery<T> Offset(int count) => Skip(count);
    public IQuery<T> Limit(int count) => Take(count);

    public IPagedQuery<T> Page(string? cursor, int pageSize)
    {
        int skip = 0;
        if (!string.IsNullOrEmpty(cursor) && int.TryParse(cursor, out int c)) skip = c;
        
        return new PagedQuery<T>(new Query<T>(_scope, _tableName, _company, _wheres, _rawWheres, _subqueryWheres, _joins, _selector, _orderBys, _groupBys, _rawSelects, _havingRaws, _unionQuery, _unionAll, _ctes, skip, pageSize + 1, _distinct), pageSize);
    }

    public IPagedQuery<T> KeysetPage<TKey>(Expression<Func<T, TKey>> keySelector, TKey lastValue, int pageSize)
    {
        var query = this.Take(pageSize + 1);
        if (lastValue != null)
        {
            var colName = SqlBuilder.GetColumnNameFromExpr(keySelector);
            var qualified = SqlBuilder.GetQualifiedColumnSqlFromExpr(keySelector, _scope.Dialect);
            var isDescending = _orderBys.Any(o => o.Contains(colName) && o.EndsWith("DESC"));
            
            var op = isDescending ? "<" : ">";
            query = query.Where($"{qualified} {op} @lastValue", new { lastValue });
        }
        return new PagedQuery<T>((Query<T>)query, pageSize, useKeyset: true);
    }

    public IQuery<IGroupResult<TKey, T>> GroupBy<TKey>(Expression<Func<T, TKey>> key)
    {
        var quoted = SqlBuilder.GetQualifiedColumnSqlFromExpr(key, _scope.Dialect);
        return new Query<IGroupResult<TKey, T>>(_scope, _tableName, _company, _wheres, _rawWheres, _subqueryWheres, _joins, null, _orderBys, _groupBys.Add(quoted), _rawSelects, _havingRaws, _unionQuery, _unionAll, _ctes, _skip, _take, _distinct);
    }

    public ValueTask<T[]> ToArrayAsync(CancellationToken ct = default) => _scope.ToArrayAsync(this, ct);

    public IAsyncEnumerable<T> AsAsyncEnumerable() => _scope.StreamAsync(this);

    public async ValueTask<T?> FirstOrDefaultAsync(CancellationToken ct = default)
    {
        var limited = new Query<T>(_scope, _tableName, _company, _wheres, _rawWheres, _subqueryWheres, _joins, _selector, _orderBys, _groupBys, _rawSelects, _havingRaws, _unionQuery, _unionAll, _ctes, _skip, 1, _distinct);
        var res = await _scope.ToArrayAsync(limited, ct);
        return res.FirstOrDefault();
    }
    
    public async ValueTask<int> CountAsync(CancellationToken ct = default)
    {
        var res = await ExecuteAggregateAsync<int>("COUNT", null, ct);
        return res;
    }

    public async ValueTask<bool> AnyAsync(CancellationToken ct = default)
    {
        var count = await CountAsync(ct);
        return count > 0;
    }

    public async ValueTask<TResult> SumAsync<TResult>(Expression<Func<T, TResult>> selector, CancellationToken ct = default)
        => await ExecuteAggregateAsync<TResult>("SUM", selector, ct);

    public async ValueTask<TResult> AverageAsync<TResult>(Expression<Func<T, TResult>> selector, CancellationToken ct = default)
        => await ExecuteAggregateAsync<TResult>("AVG", selector, ct);

    public async ValueTask<TResult> MinAsync<TResult>(Expression<Func<T, TResult>> selector, CancellationToken ct = default)
        => await ExecuteAggregateAsync<TResult>("MIN", selector, ct);
        
    public async ValueTask<TResult> MaxAsync<TResult>(Expression<Func<T, TResult>> selector, CancellationToken ct = default)
        => await ExecuteAggregateAsync<TResult>("MAX", selector, ct);
        


    private async ValueTask<TResult> ExecuteAggregateAsync<TResult>(string func, LambdaExpression? selector, CancellationToken ct)
    {
         var descriptor = BuildInternal(new ParameterContext(), func, selector);
         var res = await _scope.ExecuteScalarAsync<TResult>(descriptor.Sql, descriptor.Parameters, ct);
         return res!;
    }

    public QueryDescriptor Build()
    {
        return BuildInternal(new ParameterContext(), null, null);
    }
    
    QueryDescriptor IQueryInternal.Build(ParameterContext context, string? parameterPrefix)
    {
        if (parameterPrefix != null)
        {
            context.PushPrefix(parameterPrefix);
            try { return BuildInternal(context, null, null); }
            finally { context.PopPrefix(); }
        }
        return BuildInternal(context, null, null);
    }
    
    private QueryDescriptor BuildInternal(ParameterContext context, string? aggFunc, LambdaExpression? aggSelector)
    {
        var builder = new SqlBuilder(_tableName, _scope.CurrentCompany, context, _scope.Dialect, _scope.SchemaPrefix, _company == null);
        
        if (_ctes.Count > 0)
        {
            foreach (var cte in _ctes)
            {
                var subDescriptor = cte.Subquery.Build(context, null);
                builder.AddCte(cte.Alias, subDescriptor.Sql);
            }
        }
        
        if (_distinct) builder.SetDistinct(true);
        foreach(var w in _wheres) builder.AddWhere(w);
        foreach(var rw in _rawWheres) builder.AddWhereRaw(rw.Sql, rw.Parameters);
        
        // Compile subqueries passing the shared context exactly
        foreach(var sw in _subqueryWheres)
        {
             var subDescriptor = sw.subquery.Build(context, null);
             builder.AddWhereRaw(string.Format(sw.format, subDescriptor.Sql), null); 
             // Note: parameters are already tracked seamlessly inside the shared context during subquery.Build!
        }
        
        foreach (var j in _joins) builder.AddJoin(j);
        foreach (var g in _groupBys) builder.AddGroupBy(g);
        foreach (var rs in _rawSelects) builder.AddRawSelect(rs.Sql, rs.Parameters);
        foreach (var h in _havingRaws) builder.AddHavingRaw(h.Sql, h.Parameters);
        
        if (aggFunc != null) builder.SetAggregate(aggFunc, aggSelector);
        else if (_selector != null) builder.SetSelector(_selector);
        
        foreach (var o in _orderBys) builder.AddOrderBy(o);
        if (_skip.HasValue) builder.SetSkip(_skip.Value);
        if (_take.HasValue) builder.SetTake(_take.Value);
        
        var descriptor = builder.Build();
        if (_unionQuery != null)
        {
            var other = _unionQuery.Build(context, null);
            var unionOp = _unionAll ? " UNION ALL " : " UNION ";
            descriptor = new QueryDescriptor
            {
                Sql = $"({descriptor.Sql}){unionOp}({other.Sql})",
                Parameters = descriptor.Parameters
            };
        }
        return descriptor;
    }

    public IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default)
    {
        return AsAsyncEnumerable().GetAsyncEnumerator(cancellationToken);
    }
}

internal class PagedQuery<T> : IPagedQuery<T> where T : class
{
    private readonly Query<T> _query;
    private readonly int _pageSize;
    private readonly bool _useKeyset;
    
    public PagedQuery(Query<T> query, int pageSize, bool useKeyset = false)
    {
        _query = query;
        _pageSize = pageSize; 
        _useKeyset = useKeyset;
    }

    public async ValueTask<PagedResult<T>> ExecuteAsync(CancellationToken ct = default)
    {
        var items = await _query.ToArrayAsync(ct);
        bool hasNext = items.Length > _pageSize;
        var resultItems = hasNext ? items.AsSpan(0, _pageSize).ToArray() : items;
        
        string? nextCursor = null;
        if (hasNext)
        {
            if (_useKeyset)
            {
                 // Need to extract the last value dynamically based on query. 
                 // Returning simple indicator so caller knows to use last object for next page
                 nextCursor = "HAS_MORE"; 
            }
            else
            {
                nextCursor = ((_query.SkipValue ?? 0) + _pageSize).ToString();
            }
        }

        return new PagedResult<T>
        {
            Items = resultItems,
            HasNextPage = hasNext,
            NextCursor = nextCursor,
        };
    }
}
