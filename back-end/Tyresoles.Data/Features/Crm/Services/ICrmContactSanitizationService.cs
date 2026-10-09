using System.Threading;
using System.Threading.Tasks;
using Tyresoles.Data.Features.Crm.Models;

namespace Tyresoles.Data.Features.Crm.Services;

public interface ICrmContactSanitizationService
{
    Task<CrmContactSanitizationStatsDto> GetStatsAsync(CancellationToken ct = default);
    Task<int> AlignStateCodesAsync(int? limit = null, CancellationToken ct = default);
    Task<int> CleanUnwantedTagsAsync(int? limit = null, CancellationToken ct = default);
    Task<CrmBatchEnrichmentResultDto> EnrichTagsFromWebBatchAsync(int batchSize, CancellationToken ct = default);
}
