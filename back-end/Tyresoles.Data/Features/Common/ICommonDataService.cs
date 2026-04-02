using System.Linq;
using Dataverse.NavLive;
using Tyresoles.Sql.Abstractions;

namespace Tyresoles.Data.Features.Common;

public interface ICommonDataService
{
    /// <summary>Get group details by category and optional comma-separated codes.</summary>
    Task<IReadOnlyList<GroupDetails>> GetGroupDetailsAsync(string category, string? codes, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<State>> GetStateAsync(string? codes = null, CancellationToken cancellationToken = default);

    /// <summary>NAV Post Code table (PIN + city + state). GraphQL-friendly for paging/filtering.</summary>
    IQueryable<PostCode> GetPostCodesQuery(ITenantScope scope);
}
