using Microsoft.Extensions.Logging;
using Microsoft.ReportingServices.Diagnostics.Utilities;
using System;
using System.Buffers;
using System.Buffers.Text;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using ZXing;
using ZXing.Common;
using PuppeteerSharp;
using Tyresoles.Protean;
using Tyresoles.Protean.Models.EInvoice;
using Tyresoles.Protean.Models.EWaybill;
using Tyresoles.Protean.Session;
using Tyresoles.Sql.Abstractions;

namespace Tyresoles.Data.Features.Sales;

public class ProteanService: IProteanService
{
    private readonly IDataverse _dataverse;
    private readonly Tyresoles.Data.Features.Protean.IProteanDataService _proteanDataService;
    private readonly Tyresoles.Data.Features.Common.Connector _connector;
    private readonly IProteanSessionService _proteanSession;
    private readonly ILogger<ProteanService> _logger;

    public ProteanService(
        IDataverse dataverse,
        Tyresoles.Data.Features.Protean.IProteanDataService proteanDataService,
        Tyresoles.Data.Features.Common.Connector connector,
        IProteanSessionService proteanSession,
        ILogger<ProteanService> logger)
    {
        _dataverse = dataverse ?? throw new ArgumentNullException(nameof(dataverse));
        _proteanDataService = proteanDataService;
        _connector = connector ?? throw new ArgumentNullException(nameof(connector));
        _proteanSession = proteanSession ?? throw new ArgumentNullException(nameof(proteanSession));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>Same flow as Tyresoles.Live <c>Tyresoles.One.Data.Navision.Database.VerifyEInoice</c> (Db.Protean-old).</summary>
    public async Task<string?> VerifyEInvoiceAsync(ITenantScope scope, string type, string no, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(type))
            throw new Exception("Document Type is required.");
        if (string.IsNullOrWhiteSpace(no))
            throw new Exception("Document No. is required.");

        var headerT = scope.GetQualifiedTableName(type == "Invoice" ? "Sales Invoice Header" : "Sales Cr_Memo Header");
        var sql = $"SELECT [E-Inv Json] FROM {headerT} WHERE [No_] = @no";
        var rows = (await scope.QueryAsync<byte[]?>(sql, new { no }, ct).ConfigureAwait(false)).ToList();
        var jsonBytes = rows.Count > 0 ? rows[0] : null;
        if (jsonBytes == null || jsonBytes.Length == 0)
            throw new Exception("There is no saved json for this document in ERP.");

        int startIndex = 0;
        for (int i = 0; i < 16; i++)
            if (jsonBytes[i] == 123)
                startIndex = i;
        if (startIndex > 0)
            jsonBytes = jsonBytes[startIndex..];

        Encoding encoding = DetectEncoding(jsonBytes);
        System.Console.WriteLine(encoding.ToString());
        string json = encoding.GetString(jsonBytes);
        var options = new JsonSerializerOptions();
        options.Converters.Add(new EInvoiceLongToStringJsonConverter());

        EInvoiceResult? response = JsonSerializer.Deserialize<EInvoiceResult>(json, options);
        return await VerifyEInvoiceBrowserAsync(response, ct).ConfigureAwait(false);
    }
    
    public async Task<List<SalesLineForEInvoice>> GetPendingRecords(ITenantScope scope, CancellationToken ct = default)
    {
        List<SalesLineForEInvoice> lines = new List<SalesLineForEInvoice>();
        lines.AddRange(await GetSalesCrnLinesForInvoiceAsync(scope, ct));
        lines.AddRange(await GetSalesInvLinesForInvoiceAsync(scope, ct));
        return lines;
    }
    public async Task<List<SalesLineForEInvoice>> GetSalesInvLinesForInvoiceAsync(ITenantScope scope, CancellationToken ct = default)
    {
        var headerT = scope.GetQualifiedTableName("Sales Invoice Header");
        var lineT = scope.GetQualifiedTableName("Sales Invoice Line");
        var rcT = scope.GetQualifiedTableName("Responsibility Center");
        var custT = scope.GetQualifiedTableName("Customer");
        var hsnT = scope.GetQualifiedTableName("HSN_SAC");
        var stateT = scope.GetQualifiedTableName("State", isShared: true);

        string sql = $@"
SELECT DISTINCT
    Header.[No_] as DocNo, 
    Header.[Posting Date] as DocDate,
    RespCenter.[Code] as RespCenter, 
    Cust.[GST Supply Type] as GSTSupplyType,
    Cust.[Address] as BuyerAddress1, 
    Cust.[Address 2] as BuyerAddress2,
    Cust.[City] as BuyerLocation, 
    Cust.[Name] as BuyerName,
    Cust.[Post Code] as BuyerPinTxt, 
    BuyerState.[State Code (GST Reg_ No_)] as BuyerState,
    Cust.[GST Registration No_] as BuyerGSTN, 
    RespCenter.[Name] as SellerName,
    RespCenter.[Address] as SellerAddress1, 
    RespCenter.[Address 2] as SellerAddress2,
    RespCenter.[EINV Username] as EInvUsername, 
    RespCenter.[EINV Password] as EInvPassword,
    RespCenter.[City] as SellerLocation, 
    RespCenter.[Post Code] as SellerPinTxt,
    SellerState.[State Code (GST Reg_ No_)] as SellerStateCode,
    Line.[Description], 
    HsnSac.[Is Service] as IsService,
    Line.[HSN_SAC Code] as HSNCode, 
    RespCenter.[GST No] as SellerGSTN,
    Line.[GST Jurisdiction Type] as Interstate,
    Line.[Unit of Measure Code] as UnitOfMeasure,
    (Line.[Line No_]/100) as [LineNo],
    'INV' as DocType,
    convert(decimal(10,2), Line.[Unit Price]) as UnitPrice,
    convert(decimal(10,2), Line.[Quantity]) as Quantity,
    convert(decimal(10,2), (Line.[Quantity] * Line.[Unit Price])) as LineAmount,
    convert(decimal(10,2), (Line.[Line Discount Amount])) as DiscountAmount,
    convert(decimal(10,2), Line.[GST Base Amount]) as GstBaseAmount,
    convert(decimal(10,2), round(Line.[GST _],0)) as GstPercent,
    convert(decimal(10,2), Line.[Total GST Amount]) as GstAmount,
    iif(Line.[GST Jurisdiction Type] = 1, convert(decimal(10,2), Line.[Total GST Amount]), 0) as IGSTAmount,
    iif(Line.[GST Jurisdiction Type] = 0, convert(decimal(10,2), Line.[Total GST Amount] / 2), 0) as CGSTAmount,
    iif(Line.[GST Jurisdiction Type] = 0, convert(decimal(10,2), Line.[Total GST Amount] / 2), 0) as SGSTAmount,
    convert(decimal(10,2), Line.[TCS on Sales]) as TCSAmount,
    convert(decimal(10,2), Line.[Freight]) as FreightAmount,
    convert(decimal(10,2), Line.[Amount To Customer]) as TotalAmount
FROM {lineT} as Line
LEFT JOIN {headerT} as Header ON Header.[No_] = Line.[Document No_]
LEFT JOIN {rcT} as RespCenter ON RespCenter.[Code] = Header.[Responsibility Center]
LEFT JOIN {custT} as Cust ON Cust.[No_] = Header.[Sell-to Customer No_]
LEFT JOIN {stateT} as BuyerState ON BuyerState.[Code] = Cust.[State Code]
LEFT JOIN {stateT} as SellerState ON SellerState.[Code] = RespCenter.[State]
LEFT JOIN {hsnT} as HsnSac ON HsnSac.[Code] = Line.[HSN_SAC Code] AND HsnSac.[GST Group Code] = Line.[GST Group Code]
WHERE Line.[No_] <> '9400'
  AND Header.[E-Inv IRN No_] = ''
  AND Header.[E-Inv Skip] = 0
  AND Header.[GST Customer Type] = 1
  AND Cust.[GST Customer Type] = 1
  AND Line.[Line Amount] <> 0
  AND Line.[GST Base Amount] <> 0
  AND RespCenter.[EINV Username] <> ''
  AND Cust.[Gen_ Bus_ Posting Group] <> 'IC-WS'
ORDER BY Header.[No_], (Line.[Line No_]/100)";

        var result = await scope.QueryAsync<SalesLineForEInvoice>(sql, null, ct);
        return result.ToList();
    }

    public async Task<List<SalesLineForEInvoice>> GetSalesCrnLinesForInvoiceAsync(ITenantScope scope, CancellationToken ct = default)
    {
        var headerT = scope.GetQualifiedTableName("Sales Cr_Memo Header");
        var lineT = scope.GetQualifiedTableName("Sales Cr_Memo Line");
        var rcT = scope.GetQualifiedTableName("Responsibility Center");
        var custT = scope.GetQualifiedTableName("Customer");
        var hsnT = scope.GetQualifiedTableName("HSN_SAC");
        var stateT = scope.GetQualifiedTableName("State", isShared: true);

        string sql = $@"
SELECT DISTINCT
    Header.[No_] as DocNo, 
    Header.[Posting Date] as DocDate,
    Line.[Type] as Type,
    Line.[No_] as ItemNo,
    RespCenter.[Code] as RespCenter, 
    Cust.[GST Supply Type] as GSTSupplyType,
    Cust.[Address] as BuyerAddress1, 
    Cust.[Address 2] as BuyerAddress2,
    Cust.[City] as BuyerLocation, 
    Cust.[Name] as BuyerName,
    Cust.[Post Code] as BuyerPinTxt, 
    BuyerState.[State Code (GST Reg_ No_)] as BuyerState,
    Cust.[GST Registration No_] as BuyerGSTN, 
    RespCenter.[Name] as SellerName,
    RespCenter.[Address] as SellerAddress1, 
    RespCenter.[Address 2] as SellerAddress2,
    RespCenter.[EINV Username] as EInvUsername, 
    RespCenter.[EINV Password] as EInvPassword,
    RespCenter.[City] as SellerLocation, 
    RespCenter.[Post Code] as SellerPinTxt,
    SellerState.[State Code (GST Reg_ No_)] as SellerStateCode,
    HsnSac.[Is Service] as IsService,
    Line.[HSN_SAC Code] as HSNCode, 
    RespCenter.[GST No] as SellerGSTN,
    Line.[GST Jurisdiction Type] as Interstate,
    Line.[Unit of Measure Code] as UnitOfMeasure,
    (Line.[Line No_]/100) as [LineNo],
    'CRN' as DocType,
    convert(decimal(10,2), Line.[Unit Price]) as UnitPrice,
    convert(decimal(10,2), Line.[Quantity]) as Quantity,
    convert(decimal(10,2), (Line.[Quantity] * Line.[Unit Price])) as LineAmount,
    convert(decimal(10,2), (Line.[Line Discount Amount])) as DiscountAmount,
    convert(decimal(10,2), Line.[GST Base Amount]) as GstBaseAmount,
    convert(decimal(10,2), round(Line.[GST _],0)) as GstPercent,
    convert(decimal(10,2), Line.[Total GST Amount]) as GstAmount,
    iif(Line.[Description] <> '', Line.[Description], Line.[No_]) as Description,
    iif(Line.[GST Jurisdiction Type] = 1, convert(decimal(10,2), Line.[Total GST Amount]), 0) as IGSTAmount,
    iif(Line.[GST Jurisdiction Type] = 0, convert(decimal(10,2), Line.[Total GST Amount] / 2), 0) as CGSTAmount,
    iif(Line.[GST Jurisdiction Type] = 0, convert(decimal(10,2), Line.[Total GST Amount] / 2), 0) as SGSTAmount,
    convert(decimal(10,2), Line.[TCS on Sales]) as TCSAmount,
    convert(decimal(10,2), Line.[Freight]) as FreightAmount,
    convert(decimal(10,2), Line.[Amount To Customer]) as TotalAmount
FROM {lineT} as Line
LEFT JOIN {headerT} as Header ON Header.[No_] = Line.[Document No_]
LEFT JOIN {rcT} as RespCenter ON RespCenter.[Code] = Header.[Responsibility Center]
LEFT JOIN {custT} as Cust ON Cust.[No_] = Header.[Sell-to Customer No_]
LEFT JOIN {stateT} as BuyerState ON BuyerState.[Code] = Cust.[State Code]
LEFT JOIN {stateT} as SellerState ON SellerState.[Code] = RespCenter.[State]
LEFT JOIN {hsnT} as HsnSac ON HsnSac.[Code] = Line.[HSN_SAC Code] AND HsnSac.[GST Group Code] = Line.[GST Group Code]
WHERE Line.[No_] <> '9400'
  AND Header.[E-Inv IRN No_] = ''
  AND Header.[E-Inv Skip] = 0
  AND Header.[GST Customer Type] = 1
  AND Cust.[GST Customer Type] = 1
  AND RespCenter.[EINV Username] <> ''
  AND Cust.[Gen_ Bus_ Posting Group] <> 'IC-WS'
  AND Line.[GST Base Amount] <> 0
  AND Line.[Total GST Amount] <> 0
  AND Line.[Line Amount] <> 0
ORDER BY Header.[No_], (Line.[Line No_]/100)";

        var result = await scope.QueryAsync<SalesLineForEInvoice>(sql, null, ct);
        return result.ToList();
    }

    public async Task<List<Tyresoles.Data.Features.Protean.EInvoiceCandidate>> GetSalesLinesForEInvoiceAsync(ITenantScope scope, CancellationToken ct = default)
    {
        var results = new List<Tyresoles.Data.Features.Protean.EInvoiceCandidate>();
        var lines = new List<SalesLineForEInvoice>();
        lines.AddRange(await GetSalesInvLinesForInvoiceAsync(scope, ct));
        lines.AddRange(await GetSalesCrnLinesForInvoiceAsync(scope, ct));
        
        if (lines.Any())
        {
            var headers = lines.GroupBy(c => c.DocNo).Select(c => c.FirstOrDefault()).ToList();
            foreach (var head in headers)
            {
                if (head == null) continue;
                
                var errors = head.Validate();                    
                if (!errors.Any())
                {
                    var itemLines = lines.Where(c => c.DocNo == head.DocNo).ToList();
                    
                    var itemList = new List<Tyresoles.Protean.Models.EInvoice.InvoiceItem>();
                    foreach (var line in itemLines)
                    {
                        itemList.Add(new Tyresoles.Protean.Models.EInvoice.InvoiceItem
                        {
                            AssAmt = line.GstBaseAmount,
                            CgstAmt = line.CGSTAmount,
                            Discount = line.DiscountAmount,
                            GstRt = line.GstPercent,
                            HsnCd = line.HSNCode,
                            IgstAmt = line.IGSTAmount,
                            IsServc = line.IsService ? "Y" : "N",
                            PrdDesc = line.Description,
                            Qty = line.Quantity,
                            UnitPrice = line.UnitPrice,
                            TotAmt = line.LineAmount + line.FreightAmount,
                            SgstAmt = line.SGSTAmount,
                            SlNo = line.LineNo.ToString(),
                            TotItemVal = line.TotalAmount - line.TCSAmount,
                            Unit = line.UnitOfMeasure
                        });
                    }

                    decimal assVal = itemLines.Sum(c => c.GstBaseAmount);
                    decimal othChrg = itemLines.Sum(c => c.TCSAmount);
                    decimal cgstVal = itemLines.Sum(c => c.CGSTAmount);
                    decimal sgstVal = itemLines.Sum(c => c.SGSTAmount);
                    decimal igstVal = itemLines.Sum(c => c.IGSTAmount);
                    decimal totInvVal = Math.Round(itemLines.Sum(c => c.TotalAmount), 0);
                    decimal rndOffAmt = Math.Round(totInvVal - (assVal + cgstVal + sgstVal + igstVal + othChrg), 2);

                    System.Console.WriteLine(head.DocDate.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture));

                    results.Add(new Tyresoles.Data.Features.Protean.EInvoiceCandidate
                    {
                        DocumentNo = head.DocNo,
                        DocumentType = head.DocType,
                        PostingDate = head.DocDate.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture),
                        RespCenter = head.RespCenter,

                        Gstin = head.SellerGSTN,
                        UserName = head.EInvUsername,
                        Password = head.EInvPassword,
                        GSTTradeName = head.SellerName,
                        Address1 = head.SellerAddress1,
                        Address2 = head.SellerAddress2,
                        City = head.SellerLocation,
                        Pincode = head.SellerPin.ToString(),
                        StateCode = head.SellerStateCode,
                        
                        BuyerGstin = head.BuyerGSTN,
                        BuyerName = head.BuyerName,
                        BuyerAddr1 = head.BuyerAddress1,
                        BuyerAddr2 = head.BuyerAddress2,
                        BuyerCity = head.BuyerLocation,
                        BuyerPincode = head.BuyerPin.ToString(),
                        BuyerState = head.BuyerState,

                        TotalValue = assVal,
                        CgstValue = cgstVal,
                        SgstValue = sgstVal,
                        IgstValue = igstVal,
                        TotInvValue = totInvVal,
                        OthChrg = othChrg,
                        RndOffAmt = rndOffAmt,
                        Lines = itemList,

                        EInvSkip = false
                    });
                }
                else
                {
                    foreach (var err in errors)
                    {
                        var apiLog = new Tyresoles.Data.Features.Common.GSTApiLog
                        {
                            DocumentType = head.DocType,
                            DocumentNo = head.DocNo,
                            Source = "GST-EINV",
                            ErrorCode = "Validation",
                            ErrorMessage = err
                        };
                        await _connector.InsertGstApiLogAsync(apiLog).ConfigureAwait(false);
                    }
                }
            }
        }
        return results;
    }

    public async Task<(int Processed, int Errors)> RunEInvProcessAsync(ITenantScope scope, Tyresoles.Protean.Services.IEInvoiceService eInvoiceService, CancellationToken ct = default)
    {
        var candidates = await GetSalesLinesForEInvoiceAsync(scope, ct);
        if (candidates == null || candidates.Count == 0) return (0, 0);

        int processed = 0;
        int errors = 0;

        await Parallel.ForEachAsync(candidates, new ParallelOptions { MaxDegreeOfParallelism = 5, CancellationToken = ct }, async (candidate, token) =>
        {
            if (candidate.EInvSkip || !string.IsNullOrWhiteSpace(candidate.ExistingIrn)) return;

            await using var workScope = _dataverse.ForTenant(scope.TenantKey);
            try
            {
                var request = new EInvoiceRequest
                {
                    UserName = candidate.UserName,
                    Password = candidate.Password,
                    Gstin = candidate.Gstin,
                    Request = candidate.ToPayload(),
                    Document = new DocumentRef
                    {
                        Type = candidate.DocumentType,
                        No = candidate.DocumentNo,
                        Date = DateTime.ParseExact(candidate.PostingDate, "dd/MM/yyyy", CultureInfo.InvariantCulture)
                    }
                };

                System.Console.WriteLine(candidate.ToPayload().ToString());

                var response = await eInvoiceService.GenerateIrnAsync(request, token);

                if (response != null && !string.IsNullOrWhiteSpace(response.Irn))
                {
                    byte[]? qrCodeBytes = null;
                    if (!string.IsNullOrWhiteSpace(response.SignedQRCode))
                        qrCodeBytes = GenerateQRCode(response.SignedQRCode);

                    await _connector.InsertEInvoiceAsync(new Tyresoles.Data.Features.Common.EInvoice
                    {
                        DocumentType = candidate.DocumentType,
                        DocumentNo = candidate.DocumentNo,
                        IRN = response.Irn,
                        AckNo = response.AckNo ?? "",
                        AckDate = ParseEInvAckDate(response.AckDt),
                        QRImage = qrCodeBytes != null ? Convert.ToBase64String(qrCodeBytes) : "",
                        JsonText = JsonSerializer.Serialize(response)
                    }).ConfigureAwait(false);

                    Interlocked.Increment(ref processed);
                }
            }
            catch (Tyresoles.Protean.DuplicateIrnException dupEx)
            {
                // Mirror Live rqEInvRequestsByDoc: if IRP returns IRN in 2150 InfoDtls, fetch by IRN (no 2-day doc limit).
                // GetIrnByDocumentAsync alone fails for posting dates older than 2 days even when duplicate payload includes Irn.
                try
                {
                    var docRef = new DocumentRef
                    {
                        Type = candidate.DocumentType,
                        No = candidate.DocumentNo,
                        Date = DateTime.ParseExact(candidate.PostingDate, "dd/MM/yyyy", CultureInfo.InvariantCulture)
                    };

                    EInvoiceResult response;
                    if (!string.IsNullOrWhiteSpace(dupEx.IrnInfo?.Irn))
                    {
                        response = await eInvoiceService.GetIrnAsync(new EInvoiceRequest
                        {
                            UserName = candidate.UserName,
                            Password = candidate.Password,
                            Gstin = candidate.Gstin,
                            Request = candidate.ToPayload(),
                            Document = docRef,
                            Irn = dupEx.IrnInfo.Irn
                        }, token).ConfigureAwait(false);
                    }
                    else
                    {
                        response = await eInvoiceService.GetIrnByDocumentAsync(new EInvoiceRequest
                        {
                            UserName = candidate.UserName,
                            Password = candidate.Password,
                            Gstin = candidate.Gstin,
                            Request = candidate.ToPayload(),
                            Document = docRef
                        }, token).ConfigureAwait(false);
                    }
                    if (response != null && !string.IsNullOrWhiteSpace(response.Irn))
                    {
                        byte[]? qrCodeBytes = null;
                        if (!string.IsNullOrWhiteSpace(response.SignedQRCode))
                            qrCodeBytes = GenerateQRCode(response.SignedQRCode);

                        await _connector.InsertEInvoiceAsync(new Tyresoles.Data.Features.Common.EInvoice
                        {
                            DocumentType = candidate.DocumentType,
                            DocumentNo = candidate.DocumentNo,
                            IRN = response.Irn,
                            AckNo = response.AckNo ?? "",
                            AckDate = ParseEInvAckDate(response.AckDt),
                            QRImage = qrCodeBytes != null ? Convert.ToBase64String(qrCodeBytes) : "",
                            JsonText = JsonSerializer.Serialize(response)
                        }).ConfigureAwait(false);

                        Interlocked.Increment(ref processed);
                    }
                    else
                    {
                        Interlocked.Increment(ref errors);
                    }
                }
                catch (Exception ex)
                {
                    Interlocked.Increment(ref errors);                    
                    ErrorDetail errorDetail = new ErrorDetail();
                    if (ex.Message.Contains("ErrorCode"))
                        errorDetail = JsonSerializer.Deserialize<ErrorDetail>(ex.Message);                      
                    
                    
                    await _connector.InsertGstApiLogAsync(new Tyresoles.Data.Features.Common.GSTApiLog
                    {
                        DocumentType = candidate.DocumentType,
                        DocumentNo = candidate.DocumentNo,
                        Source = "GST-EINV",
                        ErrorCode = errorDetail != null && !errorDetail.ErrorCode.IsWhiteSpace() ? errorDetail.ErrorCode : "RECOVERY-ERR",
                        ErrorMessage = errorDetail != null && !errorDetail.ErrorMessage.IsWhiteSpace() ? errorDetail.ErrorMessage : ex.Message
                    }).ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                Interlocked.Increment(ref errors);

                ErrorDetail errorDetail = new ErrorDetail();
                if (ex.Message.Contains("ErrorCode"))
                    errorDetail = JsonSerializer.Deserialize<ErrorDetail>(ex.Message);

                await _connector.InsertGstApiLogAsync(new Tyresoles.Data.Features.Common.GSTApiLog
                {
                    DocumentType = candidate.DocumentType,
                    DocumentNo = candidate.DocumentNo,
                    Source = "GST-EINV",
                    ErrorCode = errorDetail != null && !errorDetail.ErrorCode.IsWhiteSpace() ? errorDetail.ErrorCode : "RECOVERY-ERR",
                    ErrorMessage = errorDetail != null && !errorDetail.ErrorMessage.IsWhiteSpace() ? errorDetail.ErrorMessage : ex.Message
                }).ConfigureAwait(false);
            }
        });

        return (processed, errors);
    }

    /// <summary>
    /// Ports <c>Db.Protean.RunEWBProcess</c> + <c>rqEwbRequests</c> queue handler (Tyresoles.Live):
    /// Generate → <see cref="Tyresoles.Protean.Services.IEWaybillService.GenerateAsync"/>;
    /// <see cref="EWaybillGenerateException"/> with errors → 604 / 238 / default (GST log + Error response);
    /// empty error dictionary → swallowed (Live: <c>if (ex.Errors.IsValid())</c> only);
    /// other exceptions → log + GST log + Error writeback (Live logged only; we persist for ops).
    /// Cancel (EWbillStatus 3): <c>UpdateEWB</c> Cancel branch — no Cancel API call.
    /// </summary>
    public async Task<(int Processed, int Errors)> RunEWBProcessAsync(ITenantScope scope, Tyresoles.Protean.Services.IEWaybillService eWaybillService, CancellationToken ct = default)
    {
        var candidates = await _proteanDataService.GetPendingEWaybillsAsync(scope, ct);
        if (candidates == null || candidates.Count == 0) return (0, 0);

        int processed = 0;
        int errors = 0;

        await Parallel.ForEachAsync(candidates, new ParallelOptions { MaxDegreeOfParallelism = 5, CancellationToken = ct }, async (candidate, token) =>
        {
            await using var workScope = _dataverse.ForTenant(scope.TenantKey);
            try
            {
                if (candidate.EWbillStatus == 1 && candidate.Payload != null)
                {
                    // Generate — Live rqEwbRequests: only Action "Generate" calls Processor.GenerateEwaybill
                    try
                    {
                        var result = await eWaybillService.GenerateAsync(new EWaybillRequest
                        {
                            Gstin = candidate.Gstin,
                            UserName = candidate.UserName,
                            Password = candidate.Password,
                            Action = "Generate",
                            Request = candidate.Payload
                        }, token).ConfigureAwait(false);

                        if (!string.IsNullOrEmpty(result.ewayBillNo))
                        {
                            await _proteanDataService.WriteEWaybillResultAsync(workScope, new Tyresoles.Data.Features.Protean.EWaybillWriteback
                            {
                                InvoiceNo = candidate.DocumentNo,
                                Type = Tyresoles.Data.Features.Protean.EWaybillWritebackType.Generate,
                                EwbNo = result.ewayBillNo,
                                EwbDate = result.ewayBillDate,
                                ValidUpto = result.validUpto
                            }, token).ConfigureAwait(false);

                            Interlocked.Increment(ref processed);
                        }
                    }
                    catch (EWaybillGenerateException ex) when (ex.Errors.Count > 0)
                    {
                        foreach (var error in ex.Errors)
                        {
                            switch (error.Key)
                            {
                                case "604": // Already exists — Live GetEwbByDocNo → rqEwbResponses
                                    if (await GetEwbByDocNoAsync(workScope, "INV", candidate.DocumentNo, eWaybillService, token).ConfigureAwait(false) > 0)
                                        Interlocked.Increment(ref processed);
                                    else
                                        Interlocked.Increment(ref errors);
                                    break;

                                case "238": // Invalid auth — Live RemoveExpiredSession + Log.Error(error.Value)
                                    _proteanSession.InvalidateEWaybillSession(candidate.Gstin);
                                    _logger.LogError("{EwbError}", error.Value);
                                    break;

                                default:
                                    Interlocked.Increment(ref errors);
                                    await _connector.InsertGstApiLogAsync(new Tyresoles.Data.Features.Common.GSTApiLog
                                    {
                                        DocumentType = "INV",
                                        DocumentNo = candidate.DocumentNo,
                                        Source = "GST-EWB",
                                        ErrorCode = error.Key,
                                        ErrorMessage = error.Value
                                    }).ConfigureAwait(false);
                                    await _proteanDataService.WriteEWaybillResultAsync(workScope, new Tyresoles.Data.Features.Protean.EWaybillWriteback
                                    {
                                        InvoiceNo = candidate.DocumentNo,
                                        Type = Tyresoles.Data.Features.Protean.EWaybillWritebackType.Error
                                    }, token).ConfigureAwait(false);
                                    break;
                            }
                        }
                    }
                    catch (EWaybillGenerateException)
                    {
                        // Live: catch (EWBGenerateException ex) { if (ex.Errors.IsValid()) { ... } } — no rethrow; swallow when no valid errors
                    }
                }
                else if (candidate.EWbillStatus == 3)
                {
                    // Cancel — Live UpdateEWB "Cancel": clear E-WBill fields; queue never called Cancel API
                    await _proteanDataService.WriteEWaybillResultAsync(workScope, new Tyresoles.Data.Features.Protean.EWaybillWriteback
                    {
                        InvoiceNo = candidate.DocumentNo,
                        Type = Tyresoles.Data.Features.Protean.EWaybillWritebackType.Cancel
                    }, token).ConfigureAwait(false);

                    Interlocked.Increment(ref processed);
                }
            }
            catch (Exception ex)
            {
                Interlocked.Increment(ref errors);
                _logger.LogError(ex, "EWB run failed for {DocumentNo}", candidate.DocumentNo);
                await _connector.InsertGstApiLogAsync(new Tyresoles.Data.Features.Common.GSTApiLog
                {
                    DocumentType = "EWB",
                    DocumentNo = candidate.DocumentNo,
                    Source = "EWB-RUN",
                    ErrorCode = "EXCEPTION",
                    ErrorMessage = ex.Message
                }).ConfigureAwait(false);

                await _proteanDataService.WriteEWaybillResultAsync(workScope, new Tyresoles.Data.Features.Protean.EWaybillWriteback
                {
                    InvoiceNo = candidate.DocumentNo,
                    Type = Tyresoles.Data.Features.Protean.EWaybillWritebackType.Error
                }, token).ConfigureAwait(false);
            }
        });

        return (processed, errors);
    }

    /// <inheritdoc/>
    public async Task<int> GetEwbByDocNoAsync(ITenantScope scope, string docType, string docNo, Tyresoles.Protean.Services.IEWaybillService eWaybillService, CancellationToken ct = default)
    {
        try
        {
            var creds = await _proteanDataService.GetCredentialsForInvoiceAsync(scope, docNo, ct).ConfigureAwait(false);
            if (creds == null)
            {
                _logger.LogWarning("No credentials found for EWB lookup on {DocNo}", docNo);
                return 0;
            }

            var result = await eWaybillService.GetByDocumentAsync(new EWaybillRequest
            {
                Gstin = creds.Value.Gstin,
                UserName = creds.Value.UserName,
                Password = creds.Value.Password,
                Action = "GetByDoc",
                Request = new EWaybillByDocPayload { docType = string.IsNullOrEmpty(docType) ? "INV" : docType, docNo = docNo }
            }, ct).ConfigureAwait(false);

            if (!string.IsNullOrEmpty(result.ewayBillNo))
            {
                await _proteanDataService.WriteEWaybillResultAsync(scope, new Tyresoles.Data.Features.Protean.EWaybillWriteback
                {
                    InvoiceNo = docNo,
                    Type = Tyresoles.Data.Features.Protean.EWaybillWritebackType.Generate,
                    EwbNo = result.ewayBillNo,
                    EwbDate = result.ewayBillDate,
                    ValidUpto = result.validUpto
                }, ct).ConfigureAwait(false);
                return 1;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetEwbByDocNoAsync failed for {DocNo}", docNo);
        }
        return 0;
    }

    /// <summary>Matches Tyresoles.Live <c>Utilities.WebScraping.VerifyEInvoice</c>.</summary>
    private static async Task<string?> VerifyEInvoiceBrowserAsync(EInvoiceResult? irn, CancellationToken ct)
    {
        if (irn == null)
            return null;

        string? chromeExe = null;
        if (string.IsNullOrWhiteSpace(chromeExe) || !File.Exists(chromeExe))
        {
            if (File.Exists(@"C:\Program Files\Google\Chrome\Application\chrome.exe"))
                chromeExe = @"C:\Program Files\Google\Chrome\Application\chrome.exe";
            else if (File.Exists(@"C:\Program Files (x86)\Google\Chrome\Application\chrome.exe"))
                chromeExe = @"C:\Program Files (x86)\Google\Chrome\Application\chrome.exe";
        }

        string fileDirectory = Path.GetTempPath();

        string fileName = irn.Irn!.Substring(0, 8);
        string inputFile = Path.Combine(fileDirectory, $"{fileName}.json");
        string outputFile = Path.Combine(fileDirectory, $"{fileName}.pdf");
        await using (FileStream createStream = File.Create(inputFile))
        {
            await JsonSerializer.SerializeAsync(createStream, irn, cancellationToken: ct).ConfigureAwait(false);
        }

        var launchOptions = new LaunchOptions
        {
            Headless = true,
            ExecutablePath = chromeExe
        };

        await using (var browser = await Puppeteer.LaunchAsync(launchOptions).ConfigureAwait(false))
        await using (var page = await browser.NewPageAsync().ConfigureAwait(false))
        {
            await page.GoToAsync(Tyresoles.Protean.Constants.UrlVerifySignedInvoice).WaitAsync(ct).ConfigureAwait(false);
            var fileInput = await page.QuerySelectorAsync("input[type='file']").ConfigureAwait(false);
            System.Console.WriteLine($"File uploading {inputFile}");
            if (fileInput == null)
                throw new InvalidOperationException("NIC verify page: file input not found.");
            await fileInput.UploadFileAsync(inputFile).ConfigureAwait(false);
            var submitButton = await page.MainFrame.QuerySelectorAsync("button[type=submit]").ConfigureAwait(false);
            if (submitButton == null)
                throw new InvalidOperationException("NIC verify page: submit button not found.");
            await submitButton.ClickAsync().ConfigureAwait(false);
            await page.WaitForNavigationAsync().ConfigureAwait(false);
            System.Console.WriteLine("File Uploaded");
            await page.WaitForSelectorAsync("#tblsignedJson").ConfigureAwait(false);
            try
            {
                File.Delete(inputFile);
            }
            catch
            {
                // match Live: best-effort delete
            }

            System.Console.WriteLine("#tblsignedJson");
            await page.PdfAsync(outputFile).ConfigureAwait(false);
            return outputFile;
        }
    }

    /// <summary>Matches Tyresoles.Live <c>Database.DetectEncoding</c> in Db.Protean-old.</summary>
    private static Encoding DetectEncoding(byte[] data)
    {
        if (data.Length >= 3 && data[0] == 0xEF && data[1] == 0xBB && data[2] == 0xBF)
            return new UTF8Encoding(true);

        if (data.Length >= 2 && data[0] == 0xFF && data[1] == 0xFE)
            return Encoding.Unicode;

        if (data.Length >= 2 && data[0] == 0xFE && data[1] == 0xFF)
            return Encoding.BigEndianUnicode;

        return Encoding.ASCII;
    }

    private static DateTime ParseEInvAckDate(string? ackDt)
    {
        if (string.IsNullOrWhiteSpace(ackDt)) return DateTime.Now;
        if (DateTime.TryParse(ackDt, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
            return dt;
        return DateTime.TryParse(ackDt, out var dt2) ? dt2 : DateTime.Now;
    }

    /// <summary>
    /// Matches Tyresoles.Live <c>Utilities.Helper.MakeQRCode</c>: ZXing QR, fixed 250×250, BMP bytes (for NAV <c>QRImage</c> base64).
    /// </summary>
    private static byte[] GenerateQRCode(string data)
    {
        var qrCodeWriter = new BarcodeWriterPixelData
        {
            Format = BarcodeFormat.QR_CODE,
            Options = new EncodingOptions
            {
                Height = 250,
                Width = 250,
                Margin = 0,
                NoPadding = true,
            },
        };

        var pixelData = qrCodeWriter.Write(data);
        using var bitmap = new Bitmap(pixelData.Width, pixelData.Height, PixelFormat.Format32bppRgb);
        using var ms = new MemoryStream();
        var bitmapData = bitmap.LockBits(
            new Rectangle(0, 0, pixelData.Width, pixelData.Height),
            ImageLockMode.WriteOnly,
            PixelFormat.Format32bppRgb);
        try
        {
            Marshal.Copy(pixelData.Pixels, 0, bitmapData.Scan0, pixelData.Pixels.Length);
        }
        finally
        {
            bitmap.UnlockBits(bitmapData);
        }

        bitmap.Save(ms, ImageFormat.Bmp);
        return ms.ToArray();
    }
}

/// <summary>Matches Tyresoles.Live <c>Tyresoles.One.Data.Utilities.LongToStringJsonConverter</c> (WebScraping).</summary>
internal sealed class EInvoiceLongToStringJsonConverter : JsonConverter<string?>
{
    public override string? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
            return reader.GetString();
        if (reader.TokenType == JsonTokenType.Number)
        {
            var span = reader.HasValueSequence ? reader.ValueSequence.ToArray() : reader.ValueSpan;
            if (Utf8Parser.TryParse(span, out long number, out var bytesConsumed) && span.Length == bytesConsumed)
                return number.ToString();
            return reader.GetInt64().ToString();
        }

        throw new JsonException($"Unexpected JSON token {reader.TokenType} for a string property.")
        {
            Source = nameof(EInvoiceLongToStringJsonConverter)
        };
    }

    public override void Write(Utf8JsonWriter writer, string? value, JsonSerializerOptions options)
        => writer.WriteStringValue(value?.ToString());
}

public class ErrorDetail
{
    public string ErrorCode { get; set; }
    public string ErrorMessage { get; set; }
    public override string ToString()
    {
        return JsonSerializer.Serialize(this);
    }
}

public class SalesLineForEInvoice
{
    public string DocNo { get; set; } = string.Empty;
    public DateTime DocDate { get; set; }
    public int Type { get; set; }
    public string ItemNo { get; set; } = string.Empty;
    public string RespCenter { get; set; } = string.Empty;
    public int GSTSupplyType { get; set; }
    public string BuyerAddress1 { get; set; } = string.Empty;
    public string BuyerAddress2 { get; set; } = string.Empty;
    public string BuyerLocation { get; set; } = string.Empty;
    public string BuyerName { get; set; } = string.Empty;
    public string BuyerPinTxt { get; set; } = string.Empty;
    public int BuyerPin
    {
        get
        {
            int pin = 0;
            if (!BuyerPinTxt.IsWhiteSpace())
            {
                BuyerPinTxt = BuyerPinTxt.Replace(" ", "");
                pin = int.Parse(BuyerPinTxt);
            }
            return pin;
        }
    }
    public string BuyerState { get; set; } = string.Empty;
    public string BuyerGSTN { get; set; } = string.Empty;
    public string SellerName { get; set; } = string.Empty;
    public string SellerAddress1 { get; set; } = string.Empty;
    public string SellerAddress2 { get; set; } = string.Empty;
    public string EInvUsername { get; set; } = string.Empty;
    public string EInvPassword { get; set; } = string.Empty;
    public string SellerLocation { get; set; } = string.Empty;
    public string SellerPinTxt { get; set; } = string.Empty;
    public int SellerPin
    {
        get
        {
            int pin = 0;
            if (!SellerPinTxt.IsWhiteSpace())
            {
                SellerPinTxt = SellerPinTxt.Replace(" ", "");
                pin = int.Parse(SellerPinTxt);
            }
            return pin;
        }
    }
    public string SellerStateCode { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool IsService { get; set; }
    public string HSNCode { get; set; } = string.Empty;
    public string SellerGSTN { get; set; } = string.Empty;
    public int Interstate { get; set; }
    public string UnitOfMeasure { get; set; } = string.Empty;
    public int LineNo { get; set; }
    public string DocType { get; set; } = string.Empty;
    public decimal UnitPrice { get; set; }
    public decimal Quantity { get; set; }
    public decimal LineAmount { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal GstBaseAmount { get; set; }
    public decimal GstPercent { get; set; }
    public decimal GstAmount { get; set; }
    public decimal IGSTAmount { get; set; }
    public decimal CGSTAmount { get; set; }
    public decimal SGSTAmount { get; set; }
    public decimal TCSAmount { get; set; }
    public decimal FreightAmount { get; set; }
    public decimal TotalAmount { get; set; }

    public List<string> Validate()
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(SellerGSTN)) errors.Add("Seller GSTIN is missing.");
        if (string.IsNullOrWhiteSpace(BuyerGSTN)) errors.Add("Buyer GSTIN is missing.");
        if (string.IsNullOrWhiteSpace(EInvUsername)) errors.Add("EINV Username is missing.");
        return errors;
    }
}
