using System.Linq;
using Dataverse.NavLive;
using Tyresoles.Sql.Abstractions;
using Tyresoles.Sql.GraphQL;

namespace Tyresoles.Data.Features.Common;

public sealed class CommonDataService : ICommonDataService
{
    private readonly IDataverseDataService _dataService;

    public CommonDataService(IDataverseDataService dataService)
    {
        _dataService = dataService;
    }
    public async Task<IReadOnlyList<GroupCategory>> GetGroupCategoriesAsync(int type, string? respCenters, CancellationToken cancellationToken = default)
    {
        using var scope = _dataService.ForNavLive();
        var query = scope.Query<GroupCategory>().Where(g => g.Type == type);
            

        if (!string.IsNullOrWhiteSpace(respCenters))
        {
            var codeList = respCenters.Split(',', StringSplitOptions.RemoveEmptyEntries)
                                .Select(c => c.Trim())
                                .ToList();
            if (codeList.Count > 0)
            {
                query = query.Where(g => codeList.Contains(g.ResponsibilityCenter));
            }
        }

        var results = await query.ToArrayAsync(cancellationToken);
        return results.ToList();
    }
    public async Task<IReadOnlyList<GroupDetails>> GetGroupDetailsAsync(string category, string? codes, CancellationToken cancellationToken = default)
    {
        using var scope = _dataService.ForNavLive();
        var query = scope.Query<GroupDetails>().Where(g => g.Category == category);

        if (!string.IsNullOrWhiteSpace(codes))
        {
            var codeList = codes.Split(',', StringSplitOptions.RemoveEmptyEntries)
                                .Select(c => c.Trim())
                                .ToList();
            if (codeList.Count > 0)
            {
                query = query.Where(g => codeList.Contains(g.Code));
            }
        }

        var results = await query.ToArrayAsync(cancellationToken);
        return results.ToList();
    }

    public async Task<IReadOnlyList<State>> GetStateAsync(string? codes = null, CancellationToken cancellationToken = default)
    {
        using var scope = _dataService.ForNavLive();
        var query = scope.Query<State>();

        if (!string.IsNullOrWhiteSpace(codes))
        {
            var codeList = codes.Split(',', StringSplitOptions.RemoveEmptyEntries)
                                .Select(c => c.Trim())
                                .ToList();
            if (codeList.Count > 0)
            {
                query = query.Where(g => codeList.Contains(g.Code));
            }
        }
        var results = await query.ToArrayAsync(cancellationToken);
        return results.ToList();
    }

    /// <inheritdoc />
    /// <remarks>
    /// Same NAV <c>State</c> table as <see cref="GetStateAsync"/>; this shape is for GraphQL
    /// <c>[UseFiltering]</c>/<c>[UseSorting]</c>/<c>[UsePaging]</c> without pre-sorting in SQL.
    /// </remarks>
    public IQueryable<State> GetStatesQuery(ITenantScope scope)
    {
        return scope.Query<State>().AsQueryable(scope);
    }

    /// <inheritdoc />
    /// <remarks>
    /// No fixed <c>OrderBy</c> here — <c>Query.GetPostCodes</c> uses Hot Chocolate
    /// <c>[UseSorting]</c> / client <c>order</c> so projection, filter, sort, and paging compose cleanly.
    /// </remarks>
    public IQueryable<PostCode> GetPostCodesQuery(ITenantScope scope)
    {
        return scope.Query<PostCode>().AsQueryable(scope);
    }
}
