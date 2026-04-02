using Tyresoles.Data.Features.Sales.Reports;
using Tyresoles.Sql.Abstractions;

namespace Tyresoles.Data.Features.Accounts.Reports;

public interface IAccountsReportService
{
    Task<byte[]> RenderReportAsync(
        ITenantScope scope,
        string reportName,
        SalesReportParams parameters,
        CancellationToken cancellationToken = default);

    IReadOnlyList<string> GetReportNames();

    Task<List<ReportMeta>> GetReportMetaAsync(
        ITenantScope scope,
        string? userId = null,
        CancellationToken cancellationToken = default);
}
