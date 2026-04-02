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
    public IQueryable<PostCode> GetPostCodesQuery(ITenantScope scope)
    {
        IQuery<PostCode> query = scope.Query<PostCode>()
            .OrderBy(p => p.Code)
            .ThenBy(p => p.City);
        return query.AsQueryable(scope);
    }
}
