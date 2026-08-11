using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Tyresoles.Data.Features.Common;
using Tyresoles.Sql.Abstractions;

namespace Tyresoles.Data.Features.Merger;

public interface IMergerService
{
    List<string> GetOldCompanies(string business = "");
    Task<List<string>> GetOldRespCentersAsync(ITenantScope scope, string company, CancellationToken ct = default);
    Task<EntityMapper?> GetMergerEntityAsync(ITenantScope scope, ITenantScope oldScope, EntityMapper request, CancellationToken ct = default);
    Task<string> PrepareEntityAsync(ITenantScope scope, ITenantScope oldScope, EntityMapper request, CancellationToken ct = default);
    Task<List<InvLineMapper>> GetOldInvLinesAsync(ITenantScope scope, InvoiceMapper invoice, CancellationToken ct = default);
    Task<string> CreateClaimOnOldInvAsync(ITenantScope scope, ITenantScope oldScope, InvLineMapper invoice, CancellationToken ct = default);
}
