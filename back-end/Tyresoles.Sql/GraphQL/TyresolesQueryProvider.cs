using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using Tyresoles.Sql.Abstractions;

namespace Tyresoles.Sql.GraphQL;

/// <summary>
/// IQueryProvider that translates LINQ expression trees into Tyresoles.Sql IQuery and executes them.
/// </summary>
public sealed class TyresolesQueryProvider : IQueryProvider
{
    private readonly ITenantScope _scope;

    public TyresolesQueryProvider(ITenantScope scope)
    {
        _scope = scope ?? throw new ArgumentNullException(nameof(scope));
    }

    public IQueryable CreateQuery(Expression expression)
    {
        var elementType = TypeOf(expression);
        if (elementType == null)
            throw new ArgumentException("Expression does not describe a queryable sequence.", nameof(expression));
        return (IQueryable)Activator.CreateInstance(
            typeof(TyresolesQueryable<>).MakeGenericType(elementType),
            this,
            expression)!;
    }

    public IQueryable<TElement> CreateQuery<TElement>(Expression expression)
    {
        return (IQueryable<TElement>)Activator.CreateInstance(
            typeof(TyresolesQueryable<>).MakeGenericType(typeof(TElement)),
            this,
            expression)!;
    }

    private static Type? TypeOf(Expression expression)
    {
        var type = expression.Type;
        if (typeof(IQueryable).IsAssignableFrom(type) && type.IsGenericType)
            return type.GetGenericArguments()[0];
        return null;
    }

    internal IQuery<T> BuildQuery<T>(Expression expression) where T : class
    {
        IQuery<T> query = null!;
        ApplyCalls(expression, ref query);
        return query;
    }

    private void ApplyCalls<T>(Expression expression, ref IQuery<T> query) where T : class
    {
        // Unwrap Convert(Constant(query)) from AsQueryable so we get the IQuery<T>
        if (expression is UnaryExpression ue && ue.NodeType == ExpressionType.Convert && ue.Operand is ConstantExpression convertConst)
        {
            if (convertConst.Value is IQuery<T> q)
            {
                query = q;
                return;
            }
            if (convertConst.Value is TyresolesQueryable<T>)
            {
                query = _scope.Query<T>();
                return;
            }
        }
        if (expression is ConstantExpression ce)
        {
            if (ce.Value is IQuery<T> q)
            {
                query = q;
                return;
            }
            if (ce.Value is TyresolesQueryable<T>)
            {
                query = _scope.Query<T>();
                return;
            }
        }
        if (expression is not MethodCallExpression call)
            return;

        var method = call.Method;
        var declaringType = method.DeclaringType;

        if (declaringType != typeof(Queryable) && declaringType != typeof(Enumerable))
        {
            ApplyCalls(call.Object!, ref query);
            return;
        }

        ApplyCalls(call.Arguments[0], ref query);

        var name = method.Name;
        if (name == nameof(Queryable.Where) && method.GetGenericArguments().Length == 1)
        {
            var predicate = (LambdaExpression)StripQuote(call.Arguments[1]);
            query = query.Where((Expression<Func<T, bool>>)predicate);
            return;
        }
        if (name == nameof(Queryable.OrderBy))
        {
            var keySelector = (LambdaExpression)StripQuote(call.Arguments[1]);
            var keyType = keySelector.ReturnType;
            var orderBy = typeof(QueryExtensions).GetMethod(nameof(QueryExtensions.OrderByHelper), BindingFlags.Public | BindingFlags.Static)!
                .MakeGenericMethod(typeof(T), keyType);
            query = (IQuery<T>)orderBy.Invoke(null, [query, keySelector])!;
            return;
        }
        if (name == nameof(Queryable.OrderByDescending))
        {
            var keySelector = (LambdaExpression)StripQuote(call.Arguments[1]);
            var keyType = keySelector.ReturnType;
            var orderByDesc = typeof(QueryExtensions).GetMethod(nameof(QueryExtensions.OrderByDescendingHelper), BindingFlags.Public | BindingFlags.Static)!
                .MakeGenericMethod(typeof(T), keyType);
            query = (IQuery<T>)orderByDesc.Invoke(null, [query, keySelector])!;
            return;
        }
        if (name == nameof(Queryable.Skip))
        {
            var count = (int)((ConstantExpression)call.Arguments[1]).Value!;
            query = query.Skip(count);
            return;
        }
        if (name == nameof(Queryable.Take))
        {
            var count = (int)((ConstantExpression)call.Arguments[1]).Value!;
            query = query.Take(count);
            return;
        }
        if (name == nameof(Queryable.Select))
        {
            var selectorExpr = StripQuote(call.Arguments[1]);
            // selectorExpr is a LambdaExpression: Func<T, TResult>
            // We only push down the projection when TResult == T (HotChocolate's member-init projection).
            // If the query has joins, do NOT replace the selector: the join's selector (node.Left/Right) is needed
            // so SqlBuilder emits qualified column names (t0.[Amount], t1.[Name]). Replacing with x.Amount would emit
            // unqualified [Amount] and cause "Ambiguous column name" when both tables have that column.
            if (selectorExpr is LambdaExpression lambda && lambda.ReturnType == typeof(T) && lambda.Body is MemberInitExpression
                && query is Tyresoles.Sql.Core.Query.IQueryInternal qi && !qi.HasJoins)
            {
                var typedSelector = (Expression<Func<T, T>>)lambda;
                var helper = typeof(QueryExtensions)
                    .GetMethod(nameof(QueryExtensions.SelectSameTypeHelper), BindingFlags.Public | BindingFlags.Static)!
                    .MakeGenericMethod(typeof(T));
                query = (IQuery<T>)helper.Invoke(null, [query, typedSelector])!;
            }
            // If TResult != T (e.g., anonymous projection), we can't push it to SQL — ignore it and leave SELECT *.
            return;
        }
    }

    private static Expression StripQuote(Expression e)
    {
        if (e is UnaryExpression u && u.NodeType == ExpressionType.Quote)
            return u.Operand;
        return e;
    }

    public object? Execute(Expression expression)
    {
        return Execute<object>(expression);
    }
    
    public TResult Execute<TResult>(Expression expression)
    {
        if (expression is not MethodCallExpression call)
            throw new NotSupportedException("Only method call execution is supported.");
        
        var method = call.Method.Name;
        var elementType = TypeOf(call.Arguments[0]) ?? typeof(TResult);
        var execMethod = typeof(TyresolesQueryProvider)
            .GetMethod(nameof(ExecuteInternal), BindingFlags.NonPublic | BindingFlags.Instance)!
            .MakeGenericMethod(elementType, typeof(TResult));
            
        return (TResult)execMethod.Invoke(this, [expression, method])!;
    }
    
    private TResult ExecuteInternal<T, TResult>(Expression expression, string methodName) where T : class
    {
        var query = BuildQuery<T>(((MethodCallExpression)expression).Arguments[0]);

        return methodName switch
        {
            nameof(Queryable.Count) => (TResult)(object)query.CountAsync().AsTask().GetAwaiter().GetResult(),
            // Hot Chocolate UsePaging(IncludeTotalCount) uses LongCount for total count on IQueryable.
            nameof(Queryable.LongCount) => (TResult)(object)(long)query.CountAsync().AsTask().GetAwaiter().GetResult(),
            nameof(Queryable.Any) => (TResult)(object)query.AnyAsync().AsTask().GetAwaiter().GetResult(),
            nameof(Queryable.FirstOrDefault) => (TResult)(object)query.FirstOrDefaultAsync().AsTask().GetAwaiter().GetResult()!,
            nameof(Queryable.First) => (TResult)(object)query.FirstOrDefaultAsync().AsTask().GetAwaiter().GetResult()!,
            _ => throw new NotSupportedException($"Execution of {methodName} is not supported.")
        };
    }
}

internal static class QueryExtensions
{
    public static IQuery<T> OrderByHelper<T, TKey>(IQuery<T> query, Expression<Func<T, TKey>> keySelector) where T : class
        => query.OrderBy(keySelector);
    public static IQuery<T> OrderByDescendingHelper<T, TKey>(IQuery<T> query, Expression<Func<T, TKey>> keySelector) where T : class
        => query.OrderByDescending(keySelector);
    /// <summary>
    /// Bridges HotChocolate's Select&lt;T,T&gt; projection into IQuery&lt;T&gt;.Select&lt;T&gt;,
    /// so SqlBuilder emits only the requested columns instead of SELECT *.
    /// </summary>
    public static IQuery<T> SelectSameTypeHelper<T>(IQuery<T> query, Expression<Func<T, T>> selector) where T : class
        => query.Select(selector);
}
