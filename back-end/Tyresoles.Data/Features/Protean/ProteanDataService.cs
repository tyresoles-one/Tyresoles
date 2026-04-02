using System.Collections.Generic;
using System.Globalization;
using Dataverse.NavLive;
using Tyresoles.Protean.Models.EWaybill;
using Tyresoles.Sql.Abstractions;

namespace Tyresoles.Data.Features.Protean;

/// <summary>
/// Data-access layer for Protean GSP workflows.
/// Ports logic from Tyresoles.One.Data.Navision.Db.Protean.cs into the Tyresoles.Sql pattern.
/// </summary>
public sealed class ProteanDataService : IProteanDataService
{
    // ──────────────────────────────────────────────────────────────────────────
    // E-Invoice: Pending candidates
    // ──────────────────────────────────────────────────────────────────────────

    /// <inheritdoc/>
    public async Task<IReadOnlyList<EInvoiceCandidate>> GetPendingEInvoicesAsync(
        ITenantScope scope,
        CancellationToken ct = default)
    {
        var invs = await GetPendingInvoicesInternalAsync(scope, "INV", ct).ConfigureAwait(false);
        var crns = await GetPendingCrMemosInternalAsync(scope, "CRN", ct).ConfigureAwait(false);
        return invs.Concat(crns).ToList();
    }

    private async Task<List<EInvoiceCandidate>> GetPendingInvoicesInternalAsync(ITenantScope scope, string docType, CancellationToken ct)
    {
        var qryHeader = scope.Query<SalesInvoiceHeader>()
            .Where(h => h.EInvSkip == 0)
            .Where(h => h.EInvIRNNo == "")
            .Where(h => h.GSTCustomerType == 1);
        
        var headers = await qryHeader.ToArrayAsync(ct).ConfigureAwait(false);
        if (headers.Length == 0) return new List<EInvoiceCandidate>();

        return await BuildCandidatesForInvoicesAsync(scope, docType, headers, ct).ConfigureAwait(false);
    }

    private async Task<List<EInvoiceCandidate>> GetPendingCrMemosInternalAsync(ITenantScope scope, string docType, CancellationToken ct)
    {
        var qryHeader = scope.Query<SalesCrMemoHeader>()
            .Where(h => h.EInvSkip == 0)
            .Where(h => h.EInvIRNNo == "")
            .Where(h => h.GSTCustomerType == 1);
        
        var headers = await qryHeader.ToArrayAsync(ct).ConfigureAwait(false);
        if (headers.Length == 0) return new List<EInvoiceCandidate>();

        return await BuildCandidatesForCrMemosAsync(scope, docType, headers, ct).ConfigureAwait(false);
    }

    private async Task<List<EInvoiceCandidate>> BuildCandidatesForInvoicesAsync(ITenantScope scope, string docType, SalesInvoiceHeader[] headers, CancellationToken ct)
    {
        var rcCodes = headers.Select(h => h.ResponsibilityCenter).Distinct().ToArray();
        var rcs = await WhereResponsibilityCenterCodesIn(scope.Query<ResponsibilityCenter>(), rcCodes)
            .Where(r => r.EINVUsername != "")
            .ToArrayAsync(ct).ConfigureAwait(false);

        var validRcs = rcs.Select(r => r.Code).ToHashSet();
        headers = headers.Where(h => validRcs.Contains(h.ResponsibilityCenter)).ToArray();
        if (headers.Length == 0) return new List<EInvoiceCandidate>();

        var custNos = headers.Select(h => h.SellToCustomerNo).Distinct().ToArray();
        var customers = await WhereCustomerNosIn(scope.Query<Customer>(), custNos)
            .Where(c => c.GSTCustomerType == 1)
            .Where(c => c.GenBusPostingGroup != "IC-WS")
            .ToArrayAsync(ct).ConfigureAwait(false);

        var validCusts = customers.Select(c => c.No).ToHashSet();
        headers = headers.Where(h => validCusts.Contains(h.SellToCustomerNo)).ToArray();
        if (headers.Length == 0) return new List<EInvoiceCandidate>();

        var stateCodes = rcs.Select(r => r.State).Where(c => !string.IsNullOrEmpty(c)).Distinct().ToArray();
        var states = stateCodes.Length > 0 ? await FetchStatesByCodesAsync(scope, stateCodes, ct).ConfigureAwait(false) : Array.Empty<State>();

        var buyerStateCodes = customers.Select(c => c.StateCode).Where(c => !string.IsNullOrEmpty(c)).Distinct().ToArray();
        var buyerStates = buyerStateCodes.Length > 0 ? await FetchStatesByCodesAsync(scope, buyerStateCodes, ct).ConfigureAwait(false) : Array.Empty<State>();

        var docNos = headers.Select(h => h.No).Distinct().ToArray();
        var lines = await WhereSalesInvoiceLineDocumentNosIn(scope.Query<SalesInvoiceLine>(), docNos)
            .Where(l => l.No != "9400")
            .Where(l => l.LineAmount != 0)
            .Where(l => l.GSTBaseAmount != 0)
            .ToArrayAsync(ct).ConfigureAwait(false);

        var rcMap = rcs.ToDictionary(r => r.Code);
        var stateMap = states.ToDictionary(s => s.Code);
        var bsMap = buyerStates.ToDictionary(s => s.Code);
        var custMap = customers.ToDictionary(c => c.No);
        var lineMap = lines.ToLookup(l => l.DocumentNo);

        var candidates = new List<EInvoiceCandidate>();
        foreach (var h in headers)
        {
            var docLines = lineMap[h.No].ToList();
            if (docLines.Count == 0) continue;

            rcMap.TryGetValue(h.ResponsibilityCenter, out var rc);
            custMap.TryGetValue(h.SellToCustomerNo, out var cust);
            if (rc == null || cust == null) continue;

            stateMap.TryGetValue(rc.State ?? "", out var fromState);
            bsMap.TryGetValue(cust.StateCode ?? "", out var buyerState);

            candidates.Add(MapCandidate(h.No, h.PostingDate, docType, h.GSTRegistrationNo, rc, fromState, cust, buyerState, docLines.Cast<dynamic>()));
        }
        return candidates;
    }

    private async Task<List<EInvoiceCandidate>> BuildCandidatesForCrMemosAsync(ITenantScope scope, string docType, SalesCrMemoHeader[] headers, CancellationToken ct)
    {
        var rcCodes = headers.Select(h => h.ResponsibilityCenter).Distinct().ToArray();
        var rcs = await WhereResponsibilityCenterCodesIn(scope.Query<ResponsibilityCenter>(), rcCodes)
            .Where(r => r.EINVUsername != "")
            .ToArrayAsync(ct).ConfigureAwait(false);

        var validRcs = rcs.Select(r => r.Code).ToHashSet();
        headers = headers.Where(h => validRcs.Contains(h.ResponsibilityCenter)).ToArray();
        if (headers.Length == 0) return new List<EInvoiceCandidate>();

        var custNos = headers.Select(h => h.SellToCustomerNo).Distinct().ToArray();
        var customers = await WhereCustomerNosIn(scope.Query<Customer>(), custNos)
            .Where(c => c.GSTCustomerType == 1)
            .Where(c => c.GenBusPostingGroup != "IC-WS")
            .ToArrayAsync(ct).ConfigureAwait(false);

        var validCusts = customers.Select(c => c.No).ToHashSet();
        headers = headers.Where(h => validCusts.Contains(h.SellToCustomerNo)).ToArray();
        if (headers.Length == 0) return new List<EInvoiceCandidate>();

        var stateCodes = rcs.Select(r => r.State).Where(c => !string.IsNullOrEmpty(c)).Distinct().ToArray();
        var states = stateCodes.Length > 0 ? await FetchStatesByCodesAsync(scope, stateCodes, ct).ConfigureAwait(false) : Array.Empty<State>();

        var buyerStateCodes = customers.Select(c => c.StateCode).Where(c => !string.IsNullOrEmpty(c)).Distinct().ToArray();
        var buyerStates = buyerStateCodes.Length > 0 ? await FetchStatesByCodesAsync(scope, buyerStateCodes, ct).ConfigureAwait(false) : Array.Empty<State>();

        var docNos = headers.Select(h => h.No).Distinct().ToArray();
        var lines = await WhereSalesCrMemoLineDocumentNosIn(scope.Query<SalesCrMemoLine>(), docNos)
            .Where(l => l.No != "9400")
            .Where(l => l.LineAmount != 0)
            .Where(l => l.GSTBaseAmount != 0)
            .Where(l => l.TotalGSTAmount != 0)
            .ToArrayAsync(ct).ConfigureAwait(false);

        var rcMap = rcs.ToDictionary(r => r.Code);
        var stateMap = states.ToDictionary(s => s.Code);
        var bsMap = buyerStates.ToDictionary(s => s.Code);
        var custMap = customers.ToDictionary(c => c.No);
        var lineMap = lines.ToLookup(l => l.DocumentNo);

        var candidates = new List<EInvoiceCandidate>();
        foreach (var h in headers)
        {
            var docLines = lineMap[h.No].ToList();
            if (docLines.Count == 0) continue;

            rcMap.TryGetValue(h.ResponsibilityCenter, out var rc);
            custMap.TryGetValue(h.SellToCustomerNo, out var cust);
            if (rc == null || cust == null) continue;

            stateMap.TryGetValue(rc.State ?? "", out var fromState);
            bsMap.TryGetValue(cust.StateCode ?? "", out var buyerState);

            candidates.Add(MapCandidate(h.No, h.PostingDate, docType, h.GSTRegistrationNo, rc, fromState, cust, buyerState, docLines.Cast<dynamic>()));
        }
        return candidates;
    }

    private EInvoiceCandidate MapCandidate(string docNo, DateTime? postingDate, string docType, string hGstin, ResponsibilityCenter rc, State fromState, Customer cust, State buyerState, IEnumerable<dynamic> dLines)
    {
        var docLines = dLines.ToList();
        
        // Convert dynamic to mapped fields
        var mappedLines = docLines.Select((l, idx) => {
            bool isIntra = (int)l.GSTJurisdictionType == 0;
            return new Tyresoles.Protean.Models.EInvoice.InvoiceItem
            {
                SlNo = (idx + 1).ToString(),
                PrdDesc = !string.IsNullOrEmpty(l.Description) ? l.Description : l.No,
                IsServc = (l.Type == 1 || (l.GetType().GetProperty("IsService") != null && l.IsService)) ? "Y" : "N",
                HsnCd = l.HSNSACCode,
                Unit = l.UnitOfMeasureCode,
                Qty = l.Quantity,
                UnitPrice = Math.Round(l.UnitPrice, 2),
                TotAmt = l.LineAmount + l.Freight,
                Discount = l.LineDiscountAmount,
                AssAmt = l.GSTBaseAmount,
                GstRt = Math.Round(l.GST, 0),
                CgstAmt = isIntra ? Math.Round(l.TotalGSTAmount / 2m, 2) : 0,
                SgstAmt = isIntra ? Math.Round(l.TotalGSTAmount / 2m, 2) : 0,
                IgstAmt = !isIntra ? Math.Round(l.TotalGSTAmount, 2) : 0,
                Otherchrg = l.TCSOnSales,
                TotItemVal = l.AmountToCustomer - l.TCSOnSales
            };
        }).ToList();

        decimal assVal = mappedLines.Sum(l => l.AssAmt);
        decimal cgstVal = mappedLines.Sum(l => l.CgstAmt);
        decimal sgstVal = mappedLines.Sum(l => l.SgstAmt);
        decimal igstVal = mappedLines.Sum(l => l.IgstAmt);
        decimal othChrg = docLines.Sum(l => (decimal)l.TCSOnSales);
        decimal totInvVal = Math.Round(docLines.Sum(l => (decimal)l.AmountToCustomer), 0);
        decimal rndOffAmt = Math.Round(totInvVal - (assVal + cgstVal + sgstVal + igstVal + othChrg), 2);

        return new EInvoiceCandidate
        {
            DocumentNo = docNo,
            DocumentType = docType,
            PostingDate = postingDate?.ToString("dd/MM/yyyy") ?? "",

            Gstin = rc.GSTNo ?? "",
            UserName = rc.EINVUsername ?? "",
            Password = rc.EINVPassword ?? "",
            GSTTradeName = rc.Name ?? "",
            Address1 = rc.Address ?? "",
            Address2 = rc.Address2,
            City = rc.City ?? "",
            Pincode = rc.PostCode?.Replace(" ", "") ?? "",
            StateCode = fromState?.StateCodeGSTRegNo ?? "",

            BuyerGstin = cust.GSTRegistrationNo ?? "",
            BuyerName = cust.Name ?? "",
            BuyerAddr1 = cust.Address ?? "",
            BuyerAddr2 = cust.Address2,
            BuyerCity = cust.City ?? "",
            BuyerPincode = cust.PostCode?.Replace(" ", "") ?? "",
            BuyerState = buyerState?.StateCodeGSTRegNo ?? "",

            TotalValue = assVal,
            CgstValue = cgstVal,
            SgstValue = sgstVal,
            IgstValue = igstVal,
            TotInvValue = totInvVal,
            OthChrg = othChrg,
            RndOffAmt = rndOffAmt,

            EInvSkip = false,
            ExistingIrn = null,

            Lines = mappedLines
        };
    }

    // ──────────────────────────────────────────────────────────────────────────
    // E-Invoice: Write result (mirrors Nav Connector.InsertEInvoiceDetails)
    // ──────────────────────────────────────────────────────────────────────────

    /// <inheritdoc/>
    public async Task WriteEInvoiceResultAsync(
        ITenantScope scope,
        string documentType,
        string documentNo,
        string irn,
        string ackNo,
        DateTime ackDate,
        byte[] qrImageBytes,
        string jsonText,
        CancellationToken ct = default)
    {
        if (string.Equals(documentType, "CRN", StringComparison.OrdinalIgnoreCase))
        {
            var memo = await scope.Query<SalesCrMemoHeader>()
                .Where(h => h.No == documentNo)
                .FirstOrDefaultAsync(ct).ConfigureAwait(false);
            if (memo is null) return;
            memo.EInvIRNNo        = irn;
            memo.EInvAckNo        = ackNo;
            memo.EInvAckDate      = ackDate;
            memo.EInvSignedQrcode = qrImageBytes;
            memo.EInvJson         = System.Text.Encoding.UTF8.GetBytes(jsonText);
            await scope.UpdateAsync(memo, ct).ConfigureAwait(false);
            return;
        }

        var header = await scope.Query<SalesInvoiceHeader>()
            .Where(h => h.No == documentNo)
            .FirstOrDefaultAsync(ct).ConfigureAwait(false);
        if (header is null) return;

        header.EInvIRNNo        = irn;
        header.EInvAckNo        = ackNo;
        header.EInvAckDate      = ackDate;
        header.EInvSignedQrcode = qrImageBytes;
        header.EInvJson         = System.Text.Encoding.UTF8.GetBytes(jsonText);

        await scope.UpdateAsync(header, ct).ConfigureAwait(false);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // GST API Log (mirrors Nav Connector.InsertGstApiLog)
    // ──────────────────────────────────────────────────────────────────────────

    /// <inheritdoc/>
    public async Task WriteGstApiLogAsync(
        ITenantScope scope,
        string documentType,
        string documentNo,
        string errorCode,
        string errorMessage,
        string source,
        string respCenter = "",
        CancellationToken ct = default)
    {
        await scope.InsertAsync(new GSTApiLog
        {
            DocumentType  = MapGstApiLogDocumentType(documentType),
            DocumentNo    = documentNo,
            ErrorCode     = errorCode,
            ErrorMessage  = errorMessage,
            Source        = source,
            Date          = DateTime.Now,
            RespCenter    = respCenter ?? "",
        }, ct).ConfigureAwait(false);
    }

    /// <summary>Nav GST Api Log.[Document Type]: 0 = posted sales invoice, 1 = posted sales credit memo; other tags default to 0.</summary>
    private static int MapGstApiLogDocumentType(string documentType)
    {
        if (string.Equals(documentType, "CRN", StringComparison.OrdinalIgnoreCase)) return 1;
        return 0;
    }

    // ──────────────────────────────────────────────────────────────────────────
    // E-Waybill: Pending outward candidates
    // ──────────────────────────────────────────────────────────────────────────

    /// <inheritdoc/>
    public async Task<IReadOnlyList<EWaybillCandidate>> GetPendingEWaybillsAsync(
        ITenantScope scope,
        CancellationToken ct = default)
    {
        // Mirrors Db.Protean.cs GetSalesLinesForEwbOutWard():
        // Headers with EWbillStatus in (1=Generate, 3=Cancel)
        // whose RC has EwaybillEnabled = 1
        // and linked DC header/line does NOT have Skip E Way Bill set.

        // Step 1: Headers in correct status
        var pendingHeaders = await scope.Query<SalesInvoiceHeader>()
            .Where(h => h.EWbillStatus == 1 || h.EWbillStatus == 3)
            .ToArrayAsync(ct).ConfigureAwait(false);

        if (pendingHeaders.Length == 0)
            return Array.Empty<EWaybillCandidate>();

        // Step 2: Responsibility Centres (EwaybillEnabled = 1 filter)
        var qryPendingRcs = scope.Query<SalesInvoiceHeader>()
            .Where(h => h.EWbillStatus == 1 || h.EWbillStatus == 3)
            .Select(h => new { h.ResponsibilityCenter });

        var enabledRcs = await scope.Query<ResponsibilityCenter>()
            .Where(r => r.Code, qryPendingRcs, SubqueryOperator.In)
            .Where(r => r.EwaybillEnabled == 1)
            .ToArrayAsync(ct).ConfigureAwait(false);

        var enabledRcCodes = enabledRcs.Select(r => r.Code).ToHashSet(StringComparer.OrdinalIgnoreCase);

        // Step 3: Filter headers to enabled RCs
        var filteredHeaders = pendingHeaders
            .Where(h => enabledRcCodes.Contains(h.ResponsibilityCenter))
            .ToArray();

        if (filteredHeaders.Length == 0)
            return Array.Empty<EWaybillCandidate>();

        // Sell-to customers (Live GetSalesLinesForEwbOutWard joins Customer for toTrdName: GST Trade Name when GST Customer Type = 1)
        var sellToNos = filteredHeaders.Select(h => h.SellToCustomerNo).Where(n => !string.IsNullOrEmpty(n)).Distinct().ToArray();
        var customerRows = sellToNos.Length > 0
            ? await WhereCustomerNosIn(scope.Query<Customer>(), sellToNos).ToArrayAsync(ct).ConfigureAwait(false)
            : Array.Empty<Customer>();
        var customerByNo = new Dictionary<string, Customer>(StringComparer.OrdinalIgnoreCase);
        foreach (var c in customerRows)
            customerByNo[c.No] = c;

        // Step 4: DC Lines (skipped invoices)
        var qryDocNos = scope.Query<SalesInvoiceHeader>()
            .Where(h => h.EWbillStatus == 1 || h.EWbillStatus == 3)
            .Select(h => new { h.No });

        var dcLines = await scope.Query<DCLinePosted>()
            .Where(l => l.InvoiceNo, qryDocNos, SubqueryOperator.In)
            .ToArrayAsync(ct).ConfigureAwait(false);

        var skippedInvoicesViaDCL = dcLines
            .Where(l => l.SkipEWayBill == 1)
            .Select(l => l.InvoiceNo)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        // DC Headers (for transport data)
        var dcHeaderNos = dcLines.Select(l => l.DocumentNo).Distinct().ToArray();
        var dcHeaders = dcHeaderNos.Length > 0
            ? await scope.Query<DCHeaderPosted>()
                .Where(h => h.SkipEWayBill == 0)
                .ToArrayAsync(ct).ConfigureAwait(false)
            : Array.Empty<DCHeaderPosted>();

        var skippedInvoicesViaDCH = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var dcl in dcLines)
        {
            var dch = dcHeaders.FirstOrDefault(d => d.No == dcl.DocumentNo);
            if (dch?.SkipEWayBill == 1)
                skippedInvoicesViaDCH.Add(dcl.InvoiceNo);
        }

        // Step 5: Aggregate lines per header
        var qryLineNos = scope.Query<SalesInvoiceHeader>()
            .Where(h => h.EWbillStatus == 1 || h.EWbillStatus == 3)
            .Select(h => new { h.No });

        var lines = await scope.Query<SalesInvoiceLine>()
            .Where(l => l.DocumentNo, qryLineNos, SubqueryOperator.In)
            .ToArrayAsync(ct).ConfigureAwait(false);

        // Step 6: States for from/to codes
        var rcMap   = enabledRcs.ToDictionary(r => r.Code);
        var lineMap = lines.ToLookup(l => l.DocumentNo);
        var dcLinesByInv = dcLines.ToLookup(l => l.InvoiceNo);
        var dcHeaderMap  = dcHeaders.ToDictionary(d => d.No);

        // Fetch states for RC.State values
        var rcStates = enabledRcs.Select(r => r.State).Where(s => !string.IsNullOrEmpty(s)).Distinct().ToArray();
        var stateRows = rcStates.Length > 0
            ? await FetchStatesByCodesAsync(scope, rcStates, ct).ConfigureAwait(false)
            : Array.Empty<State>();
        var stateMap = stateRows.ToDictionary(s => s.Code, StringComparer.OrdinalIgnoreCase);

        var buyerStateCodes = filteredHeaders.Select(h => h.GSTBillToStateCode).Where(s => !string.IsNullOrEmpty(s)).Distinct().ToArray();
        var shipStateCodes  = filteredHeaders.Select(h => h.GSTShipToStateCode).Where(s => !string.IsNullOrEmpty(s)).Distinct().ToArray();
        var allStateCodes = buyerStateCodes.Concat(shipStateCodes).Distinct().ToArray();
        var allBuyerStates = allStateCodes.Length > 0
            ? await FetchStatesByCodesAsync(scope, allStateCodes, ct).ConfigureAwait(false)
            : Array.Empty<State>();
        var buyerStateMap = allBuyerStates.ToDictionary(s => s.Code, StringComparer.OrdinalIgnoreCase);

        var results = new List<EWaybillCandidate>();

        foreach (var h in filteredHeaders)
        {
            // Skip if DC Line or DC Header flags it
            if (skippedInvoicesViaDCL.Contains(h.No) || skippedInvoicesViaDCH.Contains(h.No))
                continue;

            if (!rcMap.TryGetValue(h.ResponsibilityCenter, out var rc)) continue;
            stateMap.TryGetValue(rc.State ?? "", out var fromState);

            var hasShipTo  = !string.IsNullOrWhiteSpace(h.GSTShipToStateCode);
            var toStateKey = hasShipTo ? h.GSTShipToStateCode : h.GSTBillToStateCode;
            buyerStateMap.TryGetValue(toStateKey ?? "", out var toState);

            var docLines = lineMap[h.No].ToList();
            if (docLines.Count == 0) continue;

            // Determine transport from DC sheet (first match)
            var relatedDcl = dcLinesByInv[h.No].FirstOrDefault();
            DCHeaderPosted? dch = relatedDcl is not null &&
                                  dcHeaderMap.TryGetValue(relatedDcl.DocumentNo, out var d) ? d : null;

            // Get a representative line for HSN/UOM/GST%
            var repLine = docLines.First();

            // Build item list (same logic as Db.Protean.cs lines 430-466)
            var ecomileLines = docLines.Where(l =>
                l.ItemCategoryCode == "ECOMILE" || l.ItemCategoryCode == "RETD").ToList();

            var itemList = new List<EWaybillItem>();
            if (ecomileLines.Count > 0)
            {
                var el = ecomileLines.First();
                bool isIgst = el.GSTJurisdictionType == 1;
                itemList.Add(new EWaybillItem
                {
                    productName   = "Tyre Retreading",
                    productDesc   = "Retreading",
                    hsnCode       = int.TryParse(el.HSNSACCode, out var hsn) ? hsn : 0,
                    quantity      = (int)ecomileLines.Sum(l => l.Quantity),
                    qtyUnit       = el.UnitOfMeasureCode ?? "",
                    taxableAmount = Math.Round(ecomileLines.Sum(l => (decimal)l.GSTBaseAmount), 2),
                    igstRate      = isIgst ? Math.Round(el.GST, 2) : 0,
                    cgstRate      = isIgst ? 0 : Math.Round(el.GST / 2, 2),
                    sgstRate      = isIgst ? 0 : Math.Round(el.GST / 2, 2),
                    cessRate      = 0,
                    cessNonadvol  = 0,
                });
                // RETD adds second line — Live Db.Protean placeholder (qty 0, empty UOM)
                if (el.ItemCategoryCode == "RETD")
                {
                    itemList.Add(new EWaybillItem
                    {
                        hsnCode         = 40121200,
                        productName     = "Tyre Retreading",
                        productDesc     = "Retreading",
                        quantity        = 0,
                        qtyUnit         = "",
                        taxableAmount   = 0,
                        igstRate        = 0,
                        cgstRate        = 0,
                        sgstRate        = 0,
                        cessRate        = 0,
                        cessNonadvol    = 0,
                    });
                }
            }

            var cgstVal = docLines.Where(l => l.GSTJurisdictionType == 0).Sum(l => l.TotalGSTAmount) / 2m;
            var igstVal = docLines.Where(l => l.GSTJurisdictionType == 1).Sum(l => l.TotalGSTAmount);

            var fromStateCode = ResolveEwbGstStateCode(fromState, rc.GSTNo);
            var toGstinWire   = h.GSTCustomerType == 1 ? (h.GSTRegistrationNo ?? "URP") : "URP";
            var toStateCode   = ResolveEwbGstStateCode(toState, string.Equals(toGstinWire, "URP", StringComparison.OrdinalIgnoreCase) ? null : toGstinWire);
            System.Console.WriteLine($"doc: {h.No}, toState: {toState}, {toStateCode}");
            // Intra-state B2C: buyer State row may omit GST/TIN codes; place of supply matches seller state.
            if (string.IsNullOrEmpty(toStateCode) && !string.IsNullOrEmpty(fromStateCode) && igstVal == 0 && cgstVal > 0)
                toStateCode = fromStateCode;

            var payload = new EWaybillGeneratePayload
            {
                supplyType       = "O",
                subSupplyType    = "1",
                subSupplyDesc    = "",
                docType          = "INV",
                docNo            = h.No,
                docDate          = h.PostingDate?.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture) ?? "",
                fromGstin        = rc.GSTNo ?? "",
                fromTrdName      = rc.GSTTradeName ?? "",
                fromAddr1        = rc.Address ?? "",
                fromAddr2        = rc.Address2 ?? "",
                fromPlace        = rc.City ?? "",
                fromPincode      = rc.PostCode?.Replace(" ", "") ?? "",
                fromStateCode    = fromStateCode,
                actFromStateCode = fromStateCode,
                toGstin          = toGstinWire,
                toTrdName        = h.GSTCustomerType == 1 && customerByNo.TryGetValue(h.SellToCustomerNo, out var cust)
                    ? (string.IsNullOrWhiteSpace(cust.GSTTradeName) ? h.SellToCustomerName : cust.GSTTradeName)
                    : h.SellToCustomerName,
                toAddr1          = (hasShipTo ? h.ShipToAddress  : h.SellToAddress) ?? "",
                toAddr2          = (hasShipTo ? h.ShipToAddress2 : h.SellToAddress2) ?? "",
                toPlace          = (hasShipTo ? h.ShipToCity     : h.SellToCity) ?? "",
                toPincode        = (hasShipTo ? h.ShipToPostCode : h.SellToPostCode)?.Replace(" ", "") ?? "",
                toStateCode      = toStateCode,
                actToStateCode   = toStateCode,
                transactionType  = hasShipTo ? "2" : "1",
                totalValue       = Math.Round(docLines.Sum(l => (decimal)l.GSTBaseAmount), 2),
                cgstValue        = Math.Round(cgstVal, 2),
                sgstValue        = Math.Round(cgstVal, 2),
                igstValue        = Math.Round(igstVal, 2),
                totInvValue      = Math.Round(docLines.Sum(l => (decimal)l.AmountToCustomer), 2),
                transMode        = "1",
                transporterName  = dch?.VehicleOwner ?? "",
                transporterId    = dch?.TranspGSTIN ?? "",
                transDocNo       = dch?.TranspDocNo ?? "",
                transDocDate     = dch?.TranspDocDate != null && dch.TranspDocDate != new DateTime(1753, 1, 1)
                    ? dch.TranspDocDate.Value.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)
                    : "",
                vehicleNo        = dch?.VehicleNo?.Replace(" ", "") ?? "",
                vehicleType      = "R",
                itemList         = itemList,
            };

            var requestType = h.EWbillStatus switch
            {
                1 => "Generate",
                2 => "Consolidate",
                3 => "Cancel",
                _ => "Generate"
            };

            results.Add(new EWaybillCandidate
            {
                DocumentNo        = h.No,
                PostingDate       = h.PostingDate?.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture) ?? "",
                EWbillStatus      = h.EWbillStatus,
                RequestType       = requestType,
                Gstin             = rc.GSTNo,
                UserName          = rc.EINVUsername,
                Password          = rc.EINVPassword,
                TransporterGstin  = dch?.TranspGSTIN,
                TransporterName   = dch?.VehicleOwner,
                TransDocNo        = dch?.TranspDocNo,
                TransDocDate      = dch?.TranspDocDate?.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture),
                VehicleNo         = dch?.VehicleNo?.Replace(" ", ""),
                TotalValue        = payload.totalValue,
                CgstValue         = payload.cgstValue,
                SgstValue         = payload.sgstValue,
                IgstValue         = payload.igstValue,
                TotInvValue       = payload.totInvValue,
                Payload           = payload,
            });
        }

        return results;
    }

    // ──────────────────────────────────────────────────────────────────────────
    // E-Waybill: Write result back to Sales Invoice Header
    // ──────────────────────────────────────────────────────────────────────────

    /// <inheritdoc/>
    public async Task WriteEWaybillResultAsync(
        ITenantScope scope,
        EWaybillWriteback wb,
        CancellationToken ct = default)
    {
        var header = await scope.Query<SalesInvoiceHeader>()
            .Where(h => h.No == wb.InvoiceNo)
            .FirstOrDefaultAsync(ct).ConfigureAwait(false);

        if (header is null) return;

        var navEpoch = new DateTime(1753, 1, 1); // Nav "zero date"

        switch (wb.Type)
        {
            case EWaybillWritebackType.Generate:
                header.EWbillNo     = wb.EwbNo ?? "";
                header.EWbillDate   = wb.EwbDate.HasValue() ? ParseNavDate(wb.EwbDate!) : navEpoch;
                header.EWbillExpiry = wb.ValidUpto.HasValue()
                    ? ParseNavDateFlex(wb.ValidUpto!)
                    : navEpoch;
                header.EWbillStatus = 5; // Done
                break;

            case EWaybillWritebackType.Consolidate:
                header.EWbillConsNo   = wb.EwbNo ?? "";
                header.EWbillConsDate = wb.EwbDate.HasValue() ? ParseNavDate(wb.EwbDate!) : navEpoch;
                header.EWbillStatus   = 5;
                break;

            case EWaybillWritebackType.Cancel:
                header.EWbillNo         = "";
                header.EWbillDate       = navEpoch;
                header.EWbillExpiry     = navEpoch;
                header.EWbillConsNo     = "";
                header.EWbillConsDate   = navEpoch;
                header.EWbillStatus     = 0;
                break;

            case EWaybillWritebackType.Error:
                header.EWbillNo         = "";
                header.EWbillDate       = navEpoch;
                header.EWbillExpiry     = navEpoch;
                header.EWbillConsNo     = "";
                header.EWbillConsDate   = navEpoch;
                header.EWbillStatus     = 4; // Error
                break;
        }

        await scope.UpdateAsync(header, ct).ConfigureAwait(false);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Credentials lookup for GetByDoc retries
    // ──────────────────────────────────────────────────────────────────────────

    /// <inheritdoc/>
    public async Task<(string Gstin, string UserName, string Password)?> GetCredentialsForInvoiceAsync(
        ITenantScope scope,
        string invoiceNo,
        CancellationToken ct = default)
    {
        // Get the header's RC code
        var qryRcCode = scope.Query<SalesInvoiceHeader>()
            .Where(h => h.No == invoiceNo)
            .Select(h => new { h.ResponsibilityCenter });

        var rc = await scope.Query<ResponsibilityCenter>()
            .Where(r => r.Code, qryRcCode, SubqueryOperator.In)
            .FirstOrDefaultAsync(ct).ConfigureAwait(false);

        if (rc is null) return null;
        return (rc.GSTNo, rc.EINVUsername, rc.EINVPassword);
    }

    /// <inheritdoc/>
    public async Task<string?> GetPartyNameAsync(
        ITenantScope scope,
        string type,
        string code,
        CancellationToken ct = default)
    {
        if (string.Equals(type, "Customer", StringComparison.OrdinalIgnoreCase))
        {
            return await scope.Query<Customer>()
                .Where(c => c.No == code)
                .Select(c => c.Name)
                .FirstOrDefaultAsync(ct)
                .ConfigureAwait(false);
        }
        else if (string.Equals(type, "Vendor", StringComparison.OrdinalIgnoreCase) || 
                 string.Equals(type, "Transporter", StringComparison.OrdinalIgnoreCase))
        {
            var query = scope.Query<Vendor>().Where(v => v.No == code);
            if (string.Equals(type, "Transporter", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(v => v.Transporter == 1);
            }

            return await query
                .Select(v => v.Name)
                .FirstOrDefaultAsync(ct)
                .ConfigureAwait(false);
        }

        return null;
    }

    /// <inheritdoc/>
    public async Task UpdateGstinMasterAsync(
        ITenantScope scope,
        string type,
        string code,
        string gstin,
        string tradeName,
        string legalName,
        string status,
        string blockStatus,
        CancellationToken ct = default)
    {
        int gstStatusValue = (status ?? "").Contains("Active", StringComparison.OrdinalIgnoreCase) ? 1 : 2;
        int blockStatusValue = (blockStatus ?? "").Contains("Blocked", StringComparison.OrdinalIgnoreCase) ? 1 : 0;

        if (string.Equals(type, "Customer", StringComparison.OrdinalIgnoreCase))
        {
            var customer = await scope.Query<Customer>()
                .Where(c => c.No == code)
                .FirstOrDefaultAsync(ct)
                .ConfigureAwait(false);

            if (customer != null)
            {
                customer.GSTRegistrationNo = gstin;
                customer.GSTTradeName = tradeName;
                customer.GSTLegalName = legalName;
                customer.GSTStatus = 1;
                customer.GSTRegistrationType = 0;
                customer.GSTBlockStatus = 1;
                customer.GSTSupplyType = 2;
                customer.GSTCustomerType = 1;

                await scope.UpdateAsync(customer, ct).ConfigureAwait(false);
            }
        }
        else if (string.Equals(type, "Vendor", StringComparison.OrdinalIgnoreCase))
        {
            var vendor = await scope.Query<Vendor>()
                .Where(v => v.No == code)
                .FirstOrDefaultAsync(ct)
                .ConfigureAwait(false);

            if (vendor != null)
            {
                vendor.GSTRegistrationNo = gstin;
                vendor.GSTVendorType = 1;
                vendor.GSTTradeName = tradeName;
                vendor.GSTLegalName = legalName;
                vendor.GSTStatus = 1;
                vendor.GSTBlockStatus = 1;
                vendor.GSTSupplyType = 2;               

                await scope.UpdateAsync(vendor, ct).ConfigureAwait(false);
            }
        }
        else if (string.Equals(type, "Transporter", StringComparison.OrdinalIgnoreCase))
        {
            var vehicle = await scope.Query<Vehicles>()
                .Where(v => v.No == code)
                .FirstOrDefaultAsync(ct)
                .ConfigureAwait(false);

            if (vehicle != null)
            {
                vehicle.GstNo = gstin;              

                await scope.UpdateAsync(vehicle, ct).ConfigureAwait(false);
            }
        }
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Helpers
    // ──────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Parameterized <c>IN</c> for captured arrays. On modern .NET, <c>array.Contains(x)</c> in expression
    /// trees can lower to span/MemoryExtensions paths that Tyresoles.Sql's expression evaluator cannot invoke
    /// (NotSupportedException). Raw SQL avoids that.
    /// </summary>
    private static IQuery<ResponsibilityCenter> WhereResponsibilityCenterCodesIn(
        IQuery<ResponsibilityCenter> query,
        IReadOnlyList<string> codes)
    {
        if (codes.Count == 0) return query.Where("1=0", null);
        var dict = new Dictionary<string, object>(codes.Count);
        var parts = new List<string>(codes.Count);
        for (var i = 0; i < codes.Count; i++)
        {
            var name = $"@rc{i}";
            dict[name] = codes[i]!;
            parts.Add(name);
        }
        return query.Where($"[Code] IN ({string.Join(", ", parts)})", dict);
    }

    private static IQuery<Customer> WhereCustomerNosIn(IQuery<Customer> query, IReadOnlyList<string> nos)
    {
        if (nos.Count == 0) return query.Where("1=0", null);
        var dict = new Dictionary<string, object>(nos.Count);
        var parts = new List<string>(nos.Count);
        for (var i = 0; i < nos.Count; i++)
        {
            var name = $"@cu{i}";
            dict[name] = nos[i]!;
            parts.Add(name);
        }
        return query.Where($"[No_] IN ({string.Join(", ", parts)})", dict);
    }

    private static IQuery<SalesInvoiceLine> WhereSalesInvoiceLineDocumentNosIn(
        IQuery<SalesInvoiceLine> query,
        IReadOnlyList<string> docNos)
    {
        if (docNos.Count == 0) return query.Where("1=0", null);
        var dict = new Dictionary<string, object>(docNos.Count);
        var parts = new List<string>(docNos.Count);
        for (var i = 0; i < docNos.Count; i++)
        {
            var name = $"@dinv{i}";
            dict[name] = docNos[i]!;
            parts.Add(name);
        }
        return query.Where($"[Document No_] IN ({string.Join(", ", parts)})", dict);
    }

    private static IQuery<SalesCrMemoLine> WhereSalesCrMemoLineDocumentNosIn(
        IQuery<SalesCrMemoLine> query,
        IReadOnlyList<string> docNos)
    {
        if (docNos.Count == 0) return query.Where("1=0", null);
        var dict = new Dictionary<string, object>(docNos.Count);
        var parts = new List<string>(docNos.Count);
        for (var i = 0; i < docNos.Count; i++)
        {
            var name = $"@dcr{i}";
            dict[name] = docNos[i]!;
            parts.Add(name);
        }
        return query.Where($"[Document No_] IN ({string.Join(", ", parts)})", dict);
    }

    /// <summary>GST e-waybill state code: prefer State master, then GSTIN first two digits.</summary>
    private static string ResolveEwbGstStateCode(State? state, string? gstinFallback)
    {
        
        if (state is not null)
        {
            //Console.WriteLine($"state {state.StateCodeGSTRegNo}, {state.Code}");
            var gst = state.StateCodeGSTRegNo?.Trim();
            if (!string.IsNullOrEmpty(gst)) return gst;
            var fromTin = TryTwoDigitGstStateFromNavField(state.StateCodeForTIN);
            if (!string.IsNullOrEmpty(fromTin)) return fromTin;
            var fromEtds = TryTwoDigitGstStateFromNavField(state.StateCodeForEtdsTCS);
            if (!string.IsNullOrEmpty(fromEtds)) return fromEtds;
        }
        return TryTwoDigitStateCodeFromGstin(gstinFallback);
    }

    /// <summary>Nav may store 2-digit code or longer numeric strings; extract first two consecutive digits.</summary>
    private static string TryTwoDigitGstStateFromNavField(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return "";
        var s = raw.Trim();
        if (s.Length <= 2 && s.All(char.IsDigit))
            return s.Length == 1 ? "0" + s : s;
        for (var i = 0; i <= s.Length - 2; i++)
        {
            if (char.IsDigit(s[i]) && char.IsDigit(s[i + 1]))
                return s.Substring(i, 2);
        }
        return "";
    }

    private static string TryTwoDigitStateCodeFromGstin(string? gstin)
    {
        if (string.IsNullOrEmpty(gstin) || gstin.Length < 2) return "";
        return char.IsDigit(gstin[0]) && char.IsDigit(gstin[1]) ? gstin[..2] : "";
    }

    /// <summary>Fetches State rows by code list in a single round trip (IN clause).</summary>
    private static async ValueTask<State[]> FetchStatesByCodesAsync(ITenantScope scope, string[] stateCodes, CancellationToken ct)
    {
        if (stateCodes == null || stateCodes.Length == 0) return Array.Empty<State>();
        var stateT = scope.GetQualifiedTableName("State", isShared: true);
        // Dapper maps by result column name → property name. SELECT * returns Navision labels like
        // [State Code (GST Reg_ No_)] which do not match StateCodeGSTRegNo; alias to property names.
        var sql = $"""
            SELECT
                [Code] AS Code,
                [Description] AS Description,
                [State Code (GST Reg_ No_)] AS StateCodeGSTRegNo,
                [State Code for TIN] AS StateCodeForTIN,
                [State Code for eTDS_TCS] AS StateCodeForEtdsTCS
            FROM {stateT}
            WHERE [Code] IN @stateCodes
            """;
        return await scope.RawQueryToArrayAsync<State>(sql, new { stateCodes }, ct).ConfigureAwait(false);
    }

    private static DateTime ParseNavDate(string s)
    {
        if (DateTime.TryParseExact(s, "dd/MM/yyyy", null,
                System.Globalization.DateTimeStyles.None, out var dt))
            return dt;
        return DateTime.TryParse(s, out var dt2) ? dt2 : new DateTime(1753, 1, 1);
    }

    private static DateTime ParseNavDateFlex(string s)
    {
        // Protean returns either "dd/MM/yyyy hh:mm:ss tt" (AM/PM) or "dd/MM/yyyy HH:mm:ss"
        var formats = new[] { "dd/MM/yyyy hh:mm:ss tt", "dd/MM/yyyy HH:mm:ss", "dd/MM/yyyy" };
        foreach (var fmt in formats)
            if (DateTime.TryParseExact(s, fmt, null,
                    System.Globalization.DateTimeStyles.None, out var dt))
                return dt;
        return DateTime.TryParse(s, out var fallback) ? fallback : new DateTime(1753, 1, 1);
    }
}

/// <summary>Extension to check non-null/empty strings inline (mirrors StringExtensions).</summary>
file static class StringHelpers
{
    internal static bool HasValue(this string? s) => !string.IsNullOrWhiteSpace(s);
}
