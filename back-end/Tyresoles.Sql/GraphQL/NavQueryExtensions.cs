using System.Linq.Expressions;
using Tyresoles.Sql.Abstractions;

namespace Tyresoles.Sql.GraphQL;

public static class NavQueryExtensions
{
    public static async ValueTask<T[]> ExecuteAsync<T>(this IQuery<T> query) where T : class
    {
        return await query.ToArrayAsync();
    }

    /// <summary>
    /// Converts IQuery to IQueryable so that GraphQL (HotChocolate) filtering, sorting, and paging
    /// are translated to SQL and executed in a single round-trip.
    /// </summary>
    public static IQueryable<T> AsQueryable<T>(this IQuery<T> query, ITenantScope scope) where T : class
    {
        var provider = new TyresolesQueryProvider(scope);
        // Wrap in Convert so expression.Type is IQueryable<T> (required by HotChocolate.Data's Select). Provider unwraps Convert to get IQuery<T>.
        var constant = Expression.Constant(query);
        var expression = Expression.Convert(constant, typeof(IQueryable<T>));
        return new TyresolesQueryable<T>(provider, expression);
    }
}
