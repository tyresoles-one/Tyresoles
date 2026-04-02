using System;
using System.Collections.Generic;
using System.Text;
using Tyresoles.Sql.Abstractions;

namespace Tyresoles.Data.Features.Sales
{
    public interface IProteanService
    {
        public Task<List<SalesLineForEInvoice>> GetSalesInvLinesForInvoiceAsync(ITenantScope scope, CancellationToken ct = default);
        public Task<List<SalesLineForEInvoice>> GetSalesCrnLinesForInvoiceAsync(ITenantScope scope, CancellationToken ct = default);
        Task<List<Tyresoles.Data.Features.Protean.EInvoiceCandidate>> GetSalesLinesForEInvoiceAsync(ITenantScope scope, CancellationToken ct = default);
        Task<(int Processed, int Errors)> RunEInvProcessAsync(ITenantScope scope, Tyresoles.Protean.Services.IEInvoiceService eInvoiceService, CancellationToken ct = default);
        Task<(int Processed, int Errors)> RunEWBProcessAsync(ITenantScope scope, Tyresoles.Protean.Services.IEWaybillService eWaybillService, CancellationToken ct = default);

        /// <summary>Equivalent to Tyresoles.Live GetEwbByDocNo. Fetches E-Waybill details for a specific document and updates the database.</summary>
        Task<int> GetEwbByDocNoAsync(ITenantScope scope, string docType, string docNo, Tyresoles.Protean.Services.IEWaybillService eWaybillService, CancellationToken ct = default);

        /// <summary>Loads <c>E-Inv Json</c> from NAV, deserializes, and verifies on NIC (PDF path). Same flow as Tyresoles.Live <c>Database.VerifyEInoice</c>.</summary>
        Task<string?> VerifyEInvoiceAsync(ITenantScope scope, string type, string no, CancellationToken ct = default);
    }
}
