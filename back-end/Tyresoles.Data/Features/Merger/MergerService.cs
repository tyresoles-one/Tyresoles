using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Tyresoles.Data.Features.Common;
using Tyresoles.Sql.Abstractions;

namespace Tyresoles.Data.Features.Merger;

public sealed class MergerService : IMergerService
{
    private readonly ILogger<MergerService> _logger;
    private readonly Connector _connector;

    public MergerService(ILogger<MergerService> logger, Connector connector)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _connector = connector ?? throw new ArgumentNullException(nameof(connector));
    }

    public List<string> GetOldCompanies(string business = "")
    {
        var companies = new List<string>
        {
            "Manjunath Tyresoles Treads Pvt",
            "Ecoflex Surfaces Pvt. Ltd.",
            "Tyresoles (India) Pvt. Ltd.",
            "Presto Tyresoles Retd. P. Ltd",
            "Tyresoles Retd.(Gujarat)P.Ltd"
        };
        if (string.Equals(business, "tyre", StringComparison.OrdinalIgnoreCase))
        {
            companies = new List<string>
            {
                "Tyresoles (India) Pvt. Ltd.",
                "Presto Tyresoles Retd. P. Ltd",
                "Tyresoles Retd.(Gujarat)P.Ltd"
            };
        }
        return companies;
    }

    public async Task<List<string>> GetOldRespCentersAsync(ITenantScope scope, string company, CancellationToken ct = default)
    {
        var entityMapperTable = scope.GetQualifiedTableName("Entity Mapper", isShared: false);
        string sql = $"SELECT DISTINCT [Old Resp_ Center] FROM {entityMapperTable} WHERE [Old Company] = @company";
        var result = await scope.QueryAsync<string>(sql, new { company }, ct);
        return result.Where(r => !string.IsNullOrEmpty(r)).ToList();
    }

    public async Task<EntityMapper?> GetMergerEntityAsync(ITenantScope scope, ITenantScope oldScope, EntityMapper request, CancellationToken ct = default)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));
        if (string.IsNullOrWhiteSpace(request.OldCode)) throw new ArgumentException("Old Code is required.");
        if (string.IsNullOrWhiteSpace(request.OldCompany)) throw new ArgumentException("Old Company is required.");
        if (string.IsNullOrWhiteSpace(request.Type)) throw new ArgumentException("Type is required.");

        var entityMapperTable = scope.GetQualifiedTableName("Entity Mapper", isShared: false);
        string sql = $@"
SELECT 
    CASE [Type] WHEN 1 THEN 'Customer' WHEN 2 THEN 'Vendor' END AS [Type],
    [Old Company] AS OldCompany,
    [Old Code] AS OldCode,
    [Old Name] AS Name,
    [Old Resp_ Center] AS OldRespCenter,
    [New Code] AS NewCode
FROM {entityMapperTable}
WHERE [Type] = @typeVal
  AND [Old Company] = @oldCompany
  AND [Old Code] = @oldCode
  AND [Old Resp_ Center] = @oldRespCenter";

        int typeVal = string.Equals(request.Type, "Customer", StringComparison.OrdinalIgnoreCase) ? 1 : 2;
        var record = (await scope.QueryAsync<EntityMapper>(sql, new
        {
            typeVal,
            oldCompany = request.OldCompany,
            oldCode = request.OldCode,
            oldRespCenter = request.OldRespCenter
        }, ct)).FirstOrDefault();

        if (record != null) return record;

        string safeCompany = request.OldCompany.Replace('.', '_');
        string targetTable = string.Equals(request.Type, "Customer", StringComparison.OrdinalIgnoreCase)
            ? $"[{safeCompany}$Customer]"
            : $"[{safeCompany}$Vendor]";

        string sqlOld = $"SELECT [No_] AS OldCode, [Responsibility Center] AS OldRespCenter, [Name] FROM {targetTable} WHERE [No_] = @oldCode";
        var recOld = (await oldScope.QueryAsync<EntityMapper>(sqlOld, new { oldCode = request.OldCode }, ct)).FirstOrDefault();
        if (recOld != null)
        {
            request.Name = recOld.Name;
        }

        return request;
    }

    public async Task<string> PrepareEntityAsync(ITenantScope scope, ITenantScope oldScope, EntityMapper request, CancellationToken ct = default)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));

        var mapperRecord = await GetMergerEntityAsync(scope, oldScope, request, ct);
        System.Console.WriteLine(mapperRecord.ToString());

        bool canAdd = mapperRecord != null && string.IsNullOrWhiteSpace(mapperRecord.NewCode);
        System.Console.WriteLine($"canAdd {canAdd}");

        if (canAdd)
        {
            var entityMapperTable = scope.GetQualifiedTableName("Entity Mapper", isShared: false);
            string insertSql = $@"
INSERT INTO {entityMapperTable} 
([Type], [Old Code], [Old Company], [Old Resp_ Center], [Old Name], [New Resp_ Center], [New Code], [Balance])
VALUES (@typeVal, @oldCode, @oldCompany, @oldRespCenter, @name, '', '', 0)";

            int typeVal = string.Equals(request.Type, "Customer", StringComparison.OrdinalIgnoreCase) ? 1 : 2;
            int inserted = await scope.ExecuteNonQueryAsync(insertSql, new
            {
                typeVal,
                oldCode = request.OldCode,
                oldCompany = request.OldCompany,
                oldRespCenter = request.OldRespCenter,
                name = mapperRecord?.Name ?? request.Name
            }, ct);

            if (inserted < 1)
                throw new InvalidOperationException("Entity Mapper record not inserted.");

            var completedRecord = await FillMergerEntityAsync(scope, new MergerEntity
            {
                OldCode = request.OldCode,
                OldCompany = request.OldCompany,
                Type = request.Type,
                OldRespCenter = request.OldRespCenter
            }, ct);

            if (completedRecord != null)
            {
                return await _connector.InsertEntityAsync(completedRecord);
            }
        }
        return string.Empty;
    }

    private async Task<MergerEntity?> FillMergerEntityAsync(ITenantScope scope, MergerEntity entity, CancellationToken ct = default)
    {
        if (entity == null) return null;
        string safeCompany = entity.OldCompany.Replace('.', '_');

        if (string.Equals(entity.Type, "Customer", StringComparison.OrdinalIgnoreCase))
        {
            string sql = $@"
SELECT 
    Cust.[Name], Cust.[Address], Cust.[Address 2] AS Address2, Cust.[City], Cust.[Post Code] AS PostCode, Cust.[State Code] AS State,
    RespCenter.[New Code] AS NewRespCenter, Regions.[New Resp Code] AS SerRespCenter, Areas.[New Code] AS AreaCode,
    Cust.[Phone No_] AS PhoneNo, Cust.[Mobile No] AS MobileNo, Cust.[GST Registration No_] AS GSTIN, Cust.[P_A_N_ No_] AS PAN, Dealers.[Primary Cust No_] AS PrimaryNo,
    Prices.[Code] AS PriceCode,
    CASE Prices.[Type] WHEN 1 THEN 'General' WHEN 2 THEN 'Base' ELSE 'Skip' END AS PriceType
FROM [{safeCompany}$Customer] Cust
LEFT JOIN [{safeCompany}$Responsibility Center] RespCenter ON RespCenter.[Code] = Cust.[Responsibility Center]
LEFT JOIN [{safeCompany}$Area] Areas ON Areas.[Code] = Cust.[Area Code]
LEFT JOIN [{safeCompany}$Team Salesperson] Teams ON Teams.[Team Code] = Areas.[Team]
LEFT JOIN [{safeCompany}$Territory] Regions ON Regions.[Code] = Teams.[Region Code]
LEFT JOIN [{safeCompany}$Salesperson_Purchaser] Dealers ON Dealers.[Code] = Cust.[Dealer Code]
LEFT JOIN [{safeCompany}$Customer Price Group] Prices ON Prices.[Code] = Cust.[Customer Price Group]
WHERE Cust.[No_] = @oldCode";

            var result = (await scope.QueryAsync<MergerEntity>(sql, new { oldCode = entity.OldCode }, ct)).FirstOrDefault();
            if (result != null)
            {
                entity.Name = result.Name;
                entity.Address = result.Address;
                entity.Address2 = result.Address2;
                entity.City = result.City;
                entity.PostCode = result.PostCode;
                entity.State = result.State;
                entity.NewRespCenter = result.NewRespCenter;
                entity.SerRespCenter = result.SerRespCenter;
                entity.AreaCode = result.AreaCode;
                entity.MobileNo = result.MobileNo;
                entity.PrimaryNo = result.PrimaryNo;
                entity.GSTIN = result.GSTIN;
                entity.PAN = result.PAN;
                entity.PriceType = result.PriceType;
            }
        }
        else if (string.Equals(entity.Type, "Vendor", StringComparison.OrdinalIgnoreCase))
        {
            string sql = $@"
SELECT 
    Vend.[Name], Vend.[Address], Vend.[Address 2] AS Address2, Vend.[City], Vend.[Post Code] AS PostCode, Vend.[State Code] AS State,
    RespCenter.[New Code] AS NewRespCenter,
    Vend.[Phone No_] AS MobileNo, Vend.[P_A_N_ No_] AS PAN, Vend.[GST Registration No_] AS GSTIN
FROM [{safeCompany}$Vendor] Vend
LEFT JOIN [{safeCompany}$Responsibility Center] RespCenter ON RespCenter.[Code] = Vend.[Responsibility Center]
WHERE Vend.[No_] = @oldCode";

            var result = (await scope.QueryAsync<MergerEntity>(sql, new { oldCode = entity.OldCode }, ct)).FirstOrDefault();
            if (result != null)
            {
                entity.Name = result.Name;
                entity.Address = result.Address;
                entity.Address2 = result.Address2;
                entity.City = result.City;
                entity.PostCode = result.PostCode;
                entity.State = result.State;
                entity.NewRespCenter = result.NewRespCenter;
                entity.MobileNo = result.MobileNo;
                entity.PAN = result.PAN;
                entity.GSTIN = result.GSTIN;
            }
        }
        return entity;
    }

    public async Task<List<InvLineMapper>> GetOldInvLinesAsync(ITenantScope scope, InvoiceMapper invoice, CancellationToken ct = default)
    {
        if (invoice == null) throw new ArgumentNullException(nameof(invoice));
        if (string.IsNullOrWhiteSpace(invoice.OldCompany)) throw new ArgumentException("Invalid Company Name.");
        if (string.IsNullOrWhiteSpace(invoice.InvoiceNo)) throw new ArgumentException("Invalid Invoice No.");

        string companyName = invoice.OldCompany.Trim();
        string invNo = invoice.InvoiceNo.Trim();
        string safeCompany = companyName.Replace('.', '_');
        string lineTable = $"[{safeCompany}$Sales Invoice Line]";
        string locationTable = $"[{safeCompany}$Location]";

        string sql = $@"
SELECT 
    Line.[Document No_] AS [InvNo],
    Line.[Line No_] AS [LineNo],
    Line.[No_] AS [Tyre],
    ISNULL(Line.[Tyre Serial No], '') AS [SerialNo],
    @companyName AS [OldCompany],
    FORMAT(Line.[Posting Date], 'dd/MM/yyyy') AS [Date],
    CASE Line.[Tyre Make]
        WHEN 0 THEN ''
        WHEN 1 THEN 'Apollo'
        WHEN 2 THEN 'Birla'
        WHEN 3 THEN 'JK'
        WHEN 4 THEN 'Ceat'
        WHEN 5 THEN 'MRF'
        WHEN 6 THEN 'Kaizen'
        WHEN 7 THEN 'Bridgeston'
        WHEN 8 THEN 'Good Year'
        WHEN 9 THEN 'BKT'
        WHEN 10 THEN 'Dunlop'
        WHEN 11 THEN 'Michelin'
        WHEN 12 THEN 'TVS'
        WHEN 13 THEN 'JCB'
        WHEN 14 THEN 'Harisance'
        WHEN 15 THEN 'China'
        WHEN 16 THEN 'Modi Continental'
        WHEN 17 THEN 'Vikrant'
        WHEN 18 THEN 'Others'
        ELSE ''
    END AS [Make]
FROM {lineTable} Line
LEFT JOIN {locationTable} Locations ON Locations.[Code] = Line.[Location Code]
WHERE Line.[Document No_] = @invNo
  AND Line.[No_] IS NOT NULL 
  AND Line.[No_] <> ''
  AND Line.[Item Category Code] IN ('FIN-GOODS', 'CASING')
  AND Locations.[Location Type] IN (0, 2, 5)";

        var result = await scope.QueryAsync<InvLineMapper>(sql, new { companyName, invNo }, ct);
        return result.ToList();
    }

    public async Task<string> CreateClaimOnOldInvAsync(ITenantScope scope, ITenantScope oldScope, InvLineMapper invoice, CancellationToken ct = default)
    {
        if (invoice == null) throw new ArgumentNullException(nameof(invoice));
        if (string.IsNullOrWhiteSpace(invoice.OldCompany)) throw new ArgumentException("Invalid Company Name.");
        if (string.IsNullOrWhiteSpace(invoice.InvNo)) throw new ArgumentException("Invalid Invoice No.");

        string companyName = invoice.OldCompany.Trim();
        string invNo = invoice.InvNo.Trim();
        string safeCompany = companyName.Replace('.', '_');
        string lineTable = $"[{safeCompany}$Sales Invoice Line]";
        string itemTable = $"[{safeCompany}$Item]";

        string sql = $@"
SELECT 
    Items.[New Code] AS [Tyre],
    ISNULL(Line.[Tyre Serial No], '') AS [SerialNo],
    Line.[Sell-to Customer No_] AS [CustomerNo],
    Line.[Document No_] AS [InvoiceNo],
    Line.[Line No_] AS [LineNo],
    Line.[Unit Price] AS [UnitPrice],
    Line.[Discount to Dealer] AS [DealerDisc],
    Line.[Line Discount Amount] AS [LineDisc],
    Line.[Variant Code] AS [Variant],
    Line.[Inspection Report] AS [InspReport],
    Line.[Owner Risk] AS [OwnerRisk],
    Line.[TWD Amount] AS [TWDAmount],
    Line.[Responsibility Center] AS [OldRespCenter],
    FORMAT(Line.[Posting Date], 'dd.MM.yyyy') AS [Date],
    CASE Line.[Tyre Make]
        WHEN 0 THEN ''
        WHEN 1 THEN 'APOLLO'
        WHEN 2 THEN 'BIRLA'
        WHEN 3 THEN 'JK'
        WHEN 4 THEN 'CEAT'
        WHEN 5 THEN 'MRF'
        WHEN 6 THEN 'KAIZEN'
        WHEN 7 THEN 'BRIDGESTON'
        WHEN 8 THEN 'GOOD YEAR'
        WHEN 9 THEN 'BKT'
        WHEN 10 THEN 'DUNLOP'
        WHEN 11 THEN 'MICHELIN'
        WHEN 12 THEN 'TVS'
        WHEN 13 THEN 'JCB'
        WHEN 14 THEN 'HARISANCE'
        WHEN 15 THEN 'CHINA'
        WHEN 16 THEN 'MODI CONTINENTAL'
        WHEN 17 THEN 'VIKRANT'
        WHEN 18 THEN 'OTHERS'
        ELSE ''
    END AS [Make],
    @companyName AS [OldCompany]
FROM {lineTable} Line
LEFT JOIN {itemTable} Items ON Items.[No_] = Line.[No_]
WHERE Line.[Document No_] = @invNo
  AND Line.[Line No_] = @lineNo";

        var rows = await oldScope.QueryAsync<ClaimRequest>(sql, new { companyName, invNo, lineNo = invoice.LineNo }, ct);
        var record = rows.FirstOrDefault();

        if (record != null)
        {
            await PrepareEntityAsync(scope, oldScope, new EntityMapper
            {
                OldCompany = record.OldCompany,
                OldCode = record.CustomerNo,
                OldRespCenter = record.OldRespCenter,
                Type = "Customer"
            }, ct);

            _logger.LogInformation("Started CreateClaim request to NAV for invoice {InvoiceNo}", record.InvoiceNo);
            return await _connector.CreateClaimAsync(record);
        }

        return string.Empty;
    }
}
