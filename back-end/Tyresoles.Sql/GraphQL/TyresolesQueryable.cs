using System.Linq;
using System.Linq.Expressions;
using Tyresoles.Sql.Abstractions;

namespace Tyresoles.Sql.GraphQL;

/// <summary>
/// IQueryable implementation that wraps Tyresoles.Sql IQuery and translates LINQ to SQL at execution.
/// </summary>
public sealed class TyresolesQueryable<T> : IQueryable<T>, IAsyncEnumerable<T> where T : class
{
    private readonly TyresolesQueryProvider _provider;
    private readonly Expression _expression;

    public TyresolesQueryable(TyresolesQueryProvider provider, Expression expression)
    {
        _provider = provider ?? throw new ArgumentNullException(nameof(provider));
        _expression = expression ?? Expression.Constant(this);
    }

    public Type ElementType => typeof(T);
    public Expression Expression => _expression;
    public IQueryProvider Provider => _provider;

    public IEnumerator<T> GetEnumerator()
    {
        return ((IEnumerable<T>)Execute()).GetEnumerator();
    }

    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    /// <summary>
    /// Builds and executes the query, returning the result array (synchronous; blocks on async).
    /// </summary>
    internal T[] Execute()
    {
        var query = _provider.BuildQuery<T>(_expression);
        return query.ToArrayAsync().AsTask().GetAwaiter().GetResult();
    }
    
    public IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default)
    {
        var query = _provider.BuildQuery<T>(_expression);
        return query.AsAsyncEnumerable().GetAsyncEnumerator(cancellationToken);
    }
}
