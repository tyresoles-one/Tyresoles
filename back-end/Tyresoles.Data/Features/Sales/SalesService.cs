using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Dataverse.NavLive;
using Tyresoles.Data.Constants;
using Tyresoles.Data.Features.Common;
using Tyresoles.Data.Features.Crm;
using Tyresoles.Data.Features.Crm.Entities;
using Tyresoles.Data.Infrastructure;
using Tyresoles.Sql;
using Tyresoles.Sql.Abstractions;
using Tyresoles.Sql.GraphQL;
using Tyresoles.Data.Features.Sales.Reports;
using Tyresoles.Data;
using NavLiveVendor = Dataverse.NavLive.Vendor;
using NavLiveVehicles = Dataverse.NavLive.Vehicles;

namespace Tyresoles.Data.Features.Sales;

public sealed class SalesService : ISalesService
{
    private readonly GlobalQueryCache _cache;
    private readonly ILogger<SalesService> _logger;
    private readonly Connector _connector;
    private readonly CrmDbContext _crmDb;
    private readonly ISalesReportService _salesReportService;

    // Typical NAV/BC nvarchar caps on Salesperson Purchaser (Table 13 / 5714); prevents SQL 8152 on MERGE.
    // Name / Dealership Name are often Text[30] in older DBs; BC may allow 50—truncate to 30 to match strict SQL.
    private const int NavSalespersonCodeMax = 20;
    private const int NavSalespersonNameMax = 30;
    private const int NavDealershipNameMax = 30;
    private const int NavPrimaryCustomerNoMax = 20;
    private const int NavPhoneMax = 30;
    private const int NavRespCenterMax = 10;
    private const int NavEmailMax = 80;

    private static string TruncateNavField(string? value, int maxLen)
    {
        if (string.IsNullOrEmpty(value)) return "";
        var s = value.Trim();
        return s.Length <= maxLen ? s : s[..maxLen];
    }

    public SalesService(
        GlobalQueryCache cache,
        ILogger<SalesService> logger,
        Connector connector,
        CrmDbContext crmDb,
        ISalesReportService salesReportService)
    {
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _connector = connector ?? throw new ArgumentNullException(nameof(connector));
        _crmDb = crmDb ?? throw new ArgumentNullException(nameof(crmDb));
        _salesReportService = salesReportService ?? throw new ArgumentNullException(nameof(salesReportService));
    }

    /// <inheritdoc />
    public async Task SaveDealerAsync(ITenantScope scope, SaveDealerInput input, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (string.IsNullOrWhiteSpace(input.Code))
            throw new ArgumentException("Dealer code is required.", nameof(input));

        var code = input.Code.Trim();
        _logger.LogInformation("Saving dealer (NAV CreateDealer + SQL): {Code}", code);

        // Ported from Tyresoles.Live SalesController.UpdateDealerRecord → Navision.Database.UpdateDealerRecord → Connector.CreateDealer.
        var customerNo = await ResolveCustomerNoForDealerAsync(scope, code, ct).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(customerNo))
        {
            throw new InvalidOperationException(
                "Cannot resolve customer number for this dealer. Set Primary Customer No_ on the salesperson, or link a Customer with this Dealer Code.");
        }

        var navDealer = MapSaveInputToNavCreateDealer(input, customerNo);
        await _connector.CreateDealerAsync(navDealer).ConfigureAwait(false);

        var existingDealer = await scope.Query<SalespersonPurchaser>()
            .Where(s => s.Code == code)
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);

        // Direct SQL update for full field set (SOAP CreateDealer does not include Mobile, Status, etc.).
        var dealer = new SalespersonPurchaser
        {
            Code = code,
            Name = input.Name ?? "",
            EMail = input.EMail ?? "",
            MobileNo = input.MobileNo ?? "",
            DealershipName = input.DealershipName ?? "",
            Status = input.Status,
            BusinessModel = input.BusinessModel,
            Product = input.Product,
            InvestmentAmount = input.InvestmentAmount,
            DateOfBirth = input.DateOfBirth,
            DateOfAniversary = input.DateOfAniversary,
            DealershipExpDate = input.DealershipExpDate,
            DealershipStartDate = input.DealershipStartDate,
            BrandedShop = input.BrandedShop,
            PANNo = input.PanNo ?? "",
            GSTNo = input.GstNo ?? "",
            AadharNo = input.AadharNo ?? "",
            BankName = input.BankName ?? "",
            BankACNo = input.BankACNo ?? "",
            BankBranch = input.BankBranch ?? "",
            BankIFSC = input.BankIFSC ?? "",
            ResponsibilityCenter = existingDealer?.ResponsibilityCenter ?? "",
            Group = existingDealer?.Group ?? "",
            Depot = existingDealer?.Depot ?? "",
            PrimaryCustomerNo = existingDealer?.PrimaryCustomerNo ?? "",
            BasePriceMaster = existingDealer?.BasePriceMaster ?? "",
            GlobalDimension1Code = existingDealer?.GlobalDimension1Code ?? "",
            GlobalDimension2Code = existingDealer?.GlobalDimension2Code ?? ""
        };

        await scope.UpdateAsync(dealer, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Resolves NAV <c>CustomerNo</c> for CreateDealer: Primary Customer No_ on salesperson, else Customer.No_ where Dealer Code matches.
    /// </summary>
    private static async Task<string?> ResolveCustomerNoForDealerAsync(
        ITenantScope scope,
        string dealerCode,
        CancellationToken ct)
    {
        var sp = await scope.Query<SalespersonPurchaser>()
            .Where(s => s.Code == dealerCode)
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);
        if (sp != null && !string.IsNullOrWhiteSpace(sp.PrimaryCustomerNo))
            return sp.PrimaryCustomerNo.Trim();

        var cust = await scope.Query<Customer>()
            .Where(c => c.DealerCode == dealerCode)
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);
        if (cust == null || string.IsNullOrWhiteSpace(cust.No))
            return null;
        return cust.No.Trim();
    }

    /// <summary>Maps <see cref="SaveDealerInput"/> to NAV SOAP <see cref="CreateDealer"/> (same shape as Tyresoles.Live Navision.Models.CreateDealer).</summary>
    private static CreateDealer MapSaveInputToNavCreateDealer(SaveDealerInput input, string customerNo)
    {
        return new CreateDealer
        {
            CustomerNo = customerNo,
            DealerCode = input.Code?.Trim() ?? "",
            Product = input.Product,
            BusModel = input.BusinessModel,
            Name = input.Name ?? "",
            Email = input.EMail ?? "",
            DlrshipName = input.DealershipName ?? "",
            InvAmt = input.InvestmentAmount,
            DoB = input.DateOfBirth,
            DoA = input.DateOfAniversary,
            DoE = input.DealershipExpDate,
            DoJ = input.DealershipStartDate,
            BrdShop = input.BrandedShop,
            Pan = input.PanNo ?? "",
            Gst = input.GstNo ?? "",
            Adhar = input.AadharNo ?? "",
            BkName = input.BankName ?? "",
            BkAcNo = input.BankACNo ?? "",
            BkBrch = input.BankBranch ?? "",
            BkIfsc = input.BankIFSC ?? "",
            Comments = ""
        };
    }

    /// <inheritdoc />
    public async Task<CreateDealerResult> CreateDealerAsync(ITenantScope scope, string customerNo, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(scope);
        if (string.IsNullOrWhiteSpace(customerNo))
            return new CreateDealerResult { Success = false, Message = "Customer number is required." };

        var noKey = customerNo.Trim();
        var customer = await scope.Query<Customer>()
            .Where(c => c.No == noKey)
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);

        if (customer == null)
            return new CreateDealerResult { Success = false, Message = "Customer not found." };

        if (!string.IsNullOrWhiteSpace(customer.DealerCode))
        {
            return new CreateDealerResult
            {
                Success = false,
                Message = "Customer already has a dealer code assigned.",
                DealerCode = customer.DealerCode
            };
        }

        if (!TryNormalizeIndianMobile(customer.PhoneNo, out var mobile, out var mobileError))
            return new CreateDealerResult { Success = false, Message = mobileError ?? "Invalid mobile number." };

        var custNo = customer.No ?? "";
        var codeA = BuildDealerCodeCandidate(custNo, rightLen: 5);
        var codeB = BuildDealerCodeCandidate(custNo, rightLen: 6);

        if (string.IsNullOrEmpty(codeA) && string.IsNullOrEmpty(codeB))
            return new CreateDealerResult { Success = false, Message = "Cannot derive dealer code from customer number." };

        string? chosenCode;
        if (string.Equals(codeA, codeB, StringComparison.Ordinal))
        {
            var taken = await scope.Query<SalespersonPurchaser>().Where(s => s.Code == codeA).AnyAsync(ct).ConfigureAwait(false);
            if (taken)
                return new CreateDealerResult { Success = false, Message = $"Dealer code {codeA} is already in use." };
            chosenCode = codeA;
        }
        else
        {
            var existsA = !string.IsNullOrEmpty(codeA) && await scope.Query<SalespersonPurchaser>().Where(s => s.Code == codeA).AnyAsync(ct).ConfigureAwait(false);
            var existsB = !string.IsNullOrEmpty(codeB) && await scope.Query<SalespersonPurchaser>().Where(s => s.Code == codeB).AnyAsync(ct).ConfigureAwait(false);
            if (existsA && existsB)
            {
                return new CreateDealerResult
                {
                    Success = false,
                    Message = $"Dealer codes {codeA} and {codeB} are already in use."
                };
            }

            if (!existsA && !string.IsNullOrEmpty(codeA))
                chosenCode = codeA;
            else if (!existsB && !string.IsNullOrEmpty(codeB))
                chosenCode = codeB;
            else
                return new CreateDealerResult { Success = false, Message = "No available dealer code could be assigned." };
        }

        // NAV/Business Central nvarchar limits on Salesperson/Purchaser vary; long customer names cause SQL 8152.
        var dealer = new SalespersonPurchaser
        {
            Code = TruncateNavField(chosenCode!, NavSalespersonCodeMax),
            Name = TruncateNavField(customer.Name, NavSalespersonNameMax),
            DealershipName = TruncateNavField(customer.Name, NavDealershipNameMax),
            EMail = TruncateNavField(customer.EMail, NavEmailMax),
            MobileNo = TruncateNavField(mobile, NavPhoneMax),
            PrimaryCustomerNo = TruncateNavField(customer.No, NavPrimaryCustomerNoMax),
            DateOfBirth = new DateTime(1753, 1, 1).Date,
            DateOfAniversary = new DateTime(1753, 1, 1).Date,
            DealershipExpDate = new DateTime(1753, 1, 1).Date,
            DealershipStartDate = new DateTime(1753, 1, 1).Date,
            ResponsibilityCenter = customer.ResponsibilityCenter,
            //ResponsibilityCenter = TruncateNavField(customer.ResponsibilityCenter, NavRespCenterMax)
        };

        using var tx = await scope.BeginTransactionAsync(ct).ConfigureAwait(false);
        try
        {
            await scope.UpsertAsync(dealer, ct).ConfigureAwait(false);
            customer.DealerCode = TruncateNavField(chosenCode!, NavSalespersonCodeMax);
            await scope.UpdateAsync(customer, new Expression<Func<Customer, object>>[] { c => c.DealerCode! }, ct).ConfigureAwait(false);
            tx.Commit();
        }
        catch (Exception ex)
        {
            tx.Rollback();
            _logger.LogError(ex, "CreateDealer failed for customer {CustomerNo}", noKey);
            return new CreateDealerResult { Success = false, Message = ex.InnerException?.Message ?? ex.Message };
        }

        return new CreateDealerResult
        {
            Success = true,
            Message = "Dealer created and linked to customer.",
            DealerCode = chosenCode
        };
    }

    /// <summary>SQL-style <c>LEFT(No,4)+RIGHT(No,n)</c> for a dealer code candidate.</summary>
    private static string BuildDealerCodeCandidate(string customerNo, int rightLen)
    {
        if (string.IsNullOrEmpty(customerNo)) return "";
        var left = customerNo.Length <= 4 ? customerNo : customerNo[..4];
        var right = customerNo.Length <= rightLen ? customerNo : customerNo.Substring(customerNo.Length - rightLen);
        return left + right;
    }

    /// <summary>Normalize to 10-digit Indian mobile; rejects missing or invalid numbers.</summary>
    private static bool TryNormalizeIndianMobile(string? phone, out string normalized, out string? error)
    {
        normalized = "";
        error = null;
        if (string.IsNullOrWhiteSpace(phone))
        {
            error = "Customer phone number is missing.";
            return false;
        }

        var digits = new string(phone.Where(char.IsDigit).ToArray());
        while (digits.Length > 10 && digits.StartsWith("91", StringComparison.Ordinal))
            digits = digits[2..];
        if (digits.Length == 11 && digits[0] == '0')
            digits = digits[1..];
        if (digits.Length != 10)
        {
            error = "Phone must be a valid 10-digit mobile number.";
            return false;
        }

        if (digits[0] < '6' || digits[0] > '9')
        {
            error = "Invalid mobile number.";
            return false;
        }

        normalized = digits;
        return true;
    }

    /// <summary>Single-query result for PartnerGroup balance: dealer code, balance, and product (enum int).</summary>
    private sealed class PartnerGroupBalanceRow
    {
        public string Code { get; set; } = "";
        public decimal Balance { get; set; }
        public int Product { get; set; }
    }

    public async Task<List<EntityBalance>> GetMyBalanceAsync(
        ITenantScope scope,
        string? entityType,
        string? entityCode,
        string? respCenter = null,
        CancellationToken ct = default)
    {
        var code = entityCode ?? "";
        if (string.IsNullOrEmpty(code)) return new List<EntityBalance>();

        string cacheKey = $"balance:{scope.TenantKey}:{entityType}:{code}:{respCenter}";
        
        return await _cache.GetOrAddAsync(cacheKey, async () => 
        {
            switch (entityType)
            {
                case EntityTypes.Customer:
                {
                    var query = scope.Query<DetailedCustLedgEntry>().Where(l => l.CustomerNo == entityCode);
                    var sum = await query.SumAsync(l => l.Amount, ct).ConfigureAwait(false);
                    return new List<EntityBalance> { new EntityBalance { Code = code, Balance = sum } };
                }
                case EntityTypes.Partner:
                {
                        var spT = scope.GetQualifiedTableName("Salesperson_Purchaser", isShared: false);
                        var custT = scope.GetQualifiedTableName("Customer", isShared: false);
                        var detLedgerT = scope.GetQualifiedTableName("Detailed Cust_ Ledg_ Entry", isShared: false);
                        var sql = $@"
    SELECT sp.[Code], sp.[Product], ISNULL(SUM(d.[Amount]),0) AS Balance
    FROM {spT} sp
    LEFT JOIN {custT} c ON c.[Dealer Code] = sp.[Code]
    LEFT JOIN {detLedgerT} d ON d.[Customer No_] = c.[No_]
    WHERE sp.[Code] = @entityCode
    GROUP BY sp.[Code], sp.[Product]";
                        var rows = await scope.RawQueryToArrayAsync<PartnerGroupBalanceRow>(sql, new { entityCode }, ct).ConfigureAwait(false);
                        return rows.Select(r => new EntityBalance
                        {
                            Code = r.Code ?? "",
                            Balance = r.Balance,
                            Product = ((SalepersonPurchaserProductType)r.Product).ToString()
                        }).ToList();
                    }
                case EntityTypes.PartnerGroup:
                {
                    var spT = scope.GetQualifiedTableName("Salesperson_Purchaser", isShared: false);
                    var custT = scope.GetQualifiedTableName("Customer", isShared: false);
                    var detLedgerT = scope.GetQualifiedTableName("Detailed Cust_ Ledg_ Entry", isShared: false);
                    var sql = $@"
    SELECT sp.[Code], sp.[Product], ISNULL(SUM(d.[Amount]),0) AS Balance
    FROM {spT} sp
    LEFT JOIN {custT} c ON c.[Dealer Code] = sp.[Code]
    LEFT JOIN {detLedgerT} d ON d.[Customer No_] = c.[No_]
    WHERE sp.[Group] = @entityCode
    GROUP BY sp.[Code], sp.[Product]";
                    var rows = await scope.RawQueryToArrayAsync<PartnerGroupBalanceRow>(sql, new { entityCode }, ct).ConfigureAwait(false);
                    return rows.Select(r => new EntityBalance
                    {
                        Code = r.Code ?? "",
                        Balance = r.Balance,
                        Product = ((SalepersonPurchaserProductType)r.Product).ToString()
                    }).ToList();
                }
                default:
                    return new List<EntityBalance>();
            }
        }, TimeSpan.FromMinutes(5)); // Cache for 5 mins
    }

    public IQueryable<AccountTransaction> GetMyTransactionsQuery(
        ITenantScope scope,
        string? entityType,
        string? entityCode,
        string? respCenter = null)
    {
        IQuery<DetailedCustLedgEntry> query;

        switch (entityType)
        {
            case EntityTypes.Partner:
            {
                var qryCust = scope.Query<Customer>()
                    .Where(c => c.DealerCode == entityCode)
                    .Select(c => new { c.No });
                query = scope.Query<DetailedCustLedgEntry>()
                    .Where(l => l.CustomerNo, qryCust, SubqueryOperator.In);
                break;
            }
            case EntityTypes.PartnerGroup:
            {
                var qryDealer = scope.Query<SalespersonPurchaser>()
                    .Where(d => d.Group == entityCode)
                    .Select(d => new { d.Code });
                var qryCust = scope.Query<Customer>()
                    .Where(c => c.DealerCode, qryDealer, SubqueryOperator.In)
                    .Select(c => new { c.No });
                query = scope.Query<DetailedCustLedgEntry>()
                    .Where(l => l.CustomerNo, qryCust, SubqueryOperator.In);
                break;
            }
            default:
                query = scope.Query<DetailedCustLedgEntry>().Where(l => l.EntryNo == -1);
                break;
        }

        IQuery<AccountTransaction> txQuery;
        switch (entityType)
        {
            case EntityTypes.Partner:
            case EntityTypes.PartnerGroup:
                string detailedLedgerT = scope.GetQualifiedTableName("Detailed Cust_ Ledg_ Entry", isShared: false);
                string custT = scope.GetQualifiedTableName("Customer", isShared: false);

                txQuery = query
                    .Join<Customer, AccountTransaction>(
                        l => l.CustomerNo,
                        c => c.No,
                        node => new AccountTransaction
                        {
                            Date = node.Left.PostingDate,
                            Type = node.Left.DocumentType,
                            DocumentNo = node.Left.DocumentNo,
                            Amount = node.Left.Amount,
                            CustomerNo = node.Left.CustomerNo,
                            CustomerName = node.Right.Name ?? "",
                            Balance = 0
                        },
                        JoinType.Left)
                    .SelectRaw($"SUM(t0.[Amount]) OVER (PARTITION BY t0.[Customer No_] ORDER BY t0.[Posting Date], t0.[Entry No_]) AS [Balance]");
                break;
            default:
                txQuery = query
                    .Select(l => new AccountTransaction
                    {
                        Date = l.PostingDate,
                        Type = l.DocumentType,
                        DocumentNo = l.DocumentNo,
                        Amount = l.Amount,
                        CustomerNo = l.CustomerNo,
                        CustomerName = "",
                        Balance = 0
                    });
                break;
        }

        return txQuery.AsQueryable(scope);
    }

    public SalespersonPurchaser GetDealerQuery(
        ITenantScope scope,
        string code,
        CancellationToken cancellationToken = default
        )
    {
        var query = scope.Query<SalespersonPurchaser>()
            .Where(l => l.Code == code);

        var row = query.FirstOrDefaultAsync(cancellationToken).Result;
        return row ?? new SalespersonPurchaser();
    }

    public IQueryable<Customer> GetMyCustomersQuery(
        ITenantScope scope,
        string? entityType,
        string? entityCode,
        string? department,
        string? respCenter = null,
        string? dealerCode = null)
    {
        var detailedLedger = scope.GetQualifiedTableName("Detailed Cust_ Ledg_ Entry", isShared: false);
        // Correlated subquery: same balance as GetVendorBalanceAsync (sum of Amount on Detailed Vendor Ledger).
        var balanceSql =
            $"(SELECT ISNULL(-SUM(d.[Amount]), 0) FROM {detailedLedger} d WITH (NOLOCK) WHERE d.[Customer No_] = t0.[No_]) AS [Balance]";

        var query = scope.Query<Customer>()
            .WhereIf(respCenter.HasValue(), a => a.ResponsibilityCenter == respCenter)
            .WhereIf(dealerCode.HasValue(), a => a.DealerCode == dealerCode)
            .SelectRaw(balanceSql);

        switch (entityType)
        {
            case EntityTypes.Partner:
                {
                    query = query.Where(c => c.DealerCode == entityCode);
                    break;
                }
            case EntityTypes.PartnerGroup:
                {
                    var qryDealer = scope.Query<SalespersonPurchaser>()
                        .Where(c => c.Group == entityCode)
                        .Select(c => new { c.Code });
                    query = query.Where(c => c.DealerCode, qryDealer, SubqueryOperator.In);
                    break;
                }
            case EntityTypes.Employee:
                {
                    if (department == Departments.Sales)
                    {
                        var qryTeam = scope.Query<TeamSalesperson>()
                                        .Where(t => t.Code == entityCode)
                                        .Select(t => new { t.TeamCode });
                        var qryArea = scope.Query<Area>()
                                            .Where(a => a.Team, qryTeam, SubqueryOperator.In)
                                            .Select(a => new { a.Code });
                        query = query.Where(c => c.AreaCode, qryArea, SubqueryOperator.In);

                    }
                    break;
                }
        }

        return query.AsQueryable(scope);
    }

    private static string[] NormalizeRespCentersFilter(IReadOnlyList<string>? respCenters)
    {
        if (respCenters is null || respCenters.Count == 0)
            return [];
        return respCenters
            .Select(s => s?.Trim())
            .Where(s => !string.IsNullOrEmpty(s))
            .Select(s => s!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public IQueryable<SalespersonPurchaser> GetMyDealersQuery(
        ITenantScope scope,
        string? entityType,
        string? entityCode,
        string? department,
        IReadOnlyList<string>? respCenters = null)
    {
        var rcFilter = NormalizeRespCentersFilter(respCenters);
        // Align with GetMyCustomersQuery / GetMyAreasQuery: when respCenters is supplied (GraphQL), filter SQL.
        // Previously non-sales employees skipped this filter so COUNT(*) ignored respCenter; explicit client requests
        // (e.g. reportsale MasterSelect respCenterOverride) must scope dealers by responsibility center.
        var query = scope.Query<SalespersonPurchaser>()
            .WhereIf(rcFilter.Length > 0, a => a.ResponsibilityCenter, rcFilter);

        switch (entityType)
        {
            case EntityTypes.Partner:
                {
                    query = query.Where(c => c.Code == entityCode);
                    break;
                }
            case EntityTypes.PartnerGroup:
                {
                    query = query.Where(c => c.Group == entityCode);
                    break;
                }
            case EntityTypes.Employee:
                {
                    if (department == Departments.Sales)
                    { 
                    var qryTeam = scope.Query<TeamSalesperson>()
                                        .Where(t => t.Code == entityCode)
                                        .Select(t => new { t.TeamCode });
                    var qryArea = scope.Query<Area>()
                                        .Where(a => a.Team, qryTeam, SubqueryOperator.In)
                                        .Select(a => new { a.Code });
                    var qryCust = scope.Query<Customer>()
                                        .Where(c => c.AreaCode, qryArea, SubqueryOperator.In)
                                        .Select(c => new { c.DealerCode });
                    query = query.Where(c => c.Code, qryCust, SubqueryOperator.In);

                    }
                    break; 
                }
        }
        return query.AsQueryable(scope);
    }

    public IQueryable<Area> GetMyAreasQuery(
        ITenantScope scope,
        string? entityType,
        string? entityCode,
        string? department,
        IReadOnlyList<string>? respCenters = null)
    {        

        ArgumentNullException.ThrowIfNull(scope);       
        var rcFilter = NormalizeRespCentersFilter(respCenters);
        IQuery<Area> query = scope.Query<Area>()
            .WhereIf(rcFilter.Length > 0, a => a.ResponsibilityCenter, rcFilter);

        switch (entityType)
        {
            case EntityTypes.Partner:
                {
                    var qryCust = scope.Query<Customer>()
                        .Where(c => c.DealerCode == entityCode)
                        .Select(c => new { c.AreaCode });

                    query = query.Where(a => a.Code, qryCust, SubqueryOperator.In);

                    break;
                }
            case EntityTypes.PartnerGroup:
                {
                    var qryDealer = scope.Query<SalespersonPurchaser>()
                                    .Where(c => c.Group == entityCode)
                                    .Select(c => new { c.Code });
                    var qryCust = scope.Query<Customer>()
                                    .Where(c => c.DealerCode, qryDealer, SubqueryOperator.In)
                                    .Select(c => new { c.AreaCode });
                    query = query.Where(c => c.Code, qryCust, SubqueryOperator.In);
                    break;
                }
            case EntityTypes.Employee:
                {
                    if (department == Departments.Sales)
                    {
                        var qryTeam = scope.Query<TeamSalesperson>()
                                        .Where(t => t.Code == entityCode)
                                        .Select(t => new { t.TeamCode });
                        query = query.Where(c=>c.Team, qryTeam, SubqueryOperator.In);
                    }
                    break;
                }
        }
        

        return query.AsQueryable(scope);
    }

    public IQueryable<Territory> GetMyRegionsQuery(
        ITenantScope scope,
        string? entityType,
        string? entityCode,
        string? department, IReadOnlyList<string>? respCenters = null)
    {
        var rcFilter = NormalizeRespCentersFilter(respCenters);
        var query = scope.Query<Territory>()
            .Where(c => c.Type == 1)
            .WhereIf(rcFilter.Length > 0, c => c.ResponsibilityCenter, rcFilter);

        switch (entityType)
        {
            case EntityTypes.Partner:
                {
                    var qryCust = scope.Query<Customer>()
                        .Where(c => c.DealerCode == entityCode)
                        .Select(c => new { c.AreaCode });

                    var qryArea = scope.Query<Area>()
                        .Where(c => c.Code, qryCust, SubqueryOperator.In)
                        .Select(c => new { c.Team });

                    var qryTeam = scope.Query<TeamSalesperson>()
                        .Where(c => c.TeamCode, qryArea, SubqueryOperator.In)
                        .Where(c => c.Type == 6)
                        .Select(c => new { c.Code });

                    query = query.Where(a => a.Code, qryTeam, SubqueryOperator.In);

                    break;
                }
            case EntityTypes.PartnerGroup:
                {
                    var qryDealer = scope.Query<SalespersonPurchaser>()
                                    .Where(c => c.Group == entityCode)
                                    .Select(c => new { c.Code });

                    var qryCust = scope.Query<Customer>()
                        .Where(c => c.DealerCode, qryDealer, SubqueryOperator.In)
                        .Select(c => new { c.AreaCode });

                    var qryArea = scope.Query<Area>()
                        .Where(c => c.Code, qryCust, SubqueryOperator.In)
                        .Select(c => new { c.Team });

                    var qryTeam = scope.Query<TeamSalesperson>()
                        .Where(c => c.TeamCode, qryArea, SubqueryOperator.In)
                        .Where(c => c.Type == 6)
                        .Select(c => new { c.Code });

                    query = query.Where(c => c.Code, qryTeam, SubqueryOperator.In);
                    break;
                }
            case EntityTypes.Employee:
                {
                    if (department == Departments.Sales)
                    {
                        var qryTeam = scope.Query<TeamSalesperson>()
                                        .Where(t => t.Code == entityCode)
                                        .Select(t => new { t.TeamCode });
                        var qryTeam2 = scope.Query<TeamSalesperson>()
                            .Where(c => c.TeamCode, qryTeam, SubqueryOperator.In)
                            .Where(t => t.Type == 6)
                            .Select(t => new { t.Code });

                        query = query.Where(c => c.Code, qryTeam2, SubqueryOperator.In);
                    }
                    break;
                }
        }
        return query.AsQueryable(scope);
    }


    public IQueryable<ResponsibilityCenter> GetMyRespCentersQuery(
        ITenantScope scope,
        string userid,
        string type)
    {
        var qryAllowed = scope.Query<RespCenterUserSetup>()
            .Where(r => r.UserID == userid)
            .Select(r => r.RespCenter);

        var query = scope.Query<ResponsibilityCenter>()
            .Where(rc => rc.Code, qryAllowed, SubqueryOperator.In);

        var types = type.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (types.Length > 0)
        {
            var hasSale = types.Contains("Sale");
            var hasPayroll = types.Contains("Payroll");
            var hasProduction = types.Contains("Production");
            var hasPurchase = types.Contains("Purchase");

            if (!(hasSale && hasPayroll && hasProduction && hasPurchase))
            {
                query = query.Where(rc => 
                    (hasSale==true && rc.Sale != 0) ||
                    (hasPayroll==true && rc.Payroll != 0) ||
                    (hasProduction==true && rc.Production != 0) ||
                    (hasPurchase == true && rc.Purchase != 0)
                );
            }
        }

        return query
            .AsQueryable(scope);
    }

    public IQueryable<NavLiveVehicles> GetMyVehiclesQuery(
        ITenantScope scope,
        string? entityType,
        string? entityCode,
        string? department,
        string? respCenter = null)
    {
        ArgumentNullException.ThrowIfNull(scope);

        var query = scope.Query<NavLiveVehicles>()
            .WhereIf(respCenter != null, v => v.ResponsibilityCenter == respCenter);

        return query.AsQueryable(scope);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<DealerDocumentImageDto>> GetDealerDocumentImagesAsync(
        ITenantScope scope,
        string documentNo,
        int docType,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(scope);
        if (string.IsNullOrWhiteSpace(documentNo))
            throw new ArgumentException("Document number is required.", nameof(documentNo));

        var docNo = documentNo.Trim();
        var rows = await scope.Query<Images>()
            .Where(i => i.DocumentNo == docNo && i.DocType == docType)
            .OrderBy(i => i.Lineno)
            .ToArrayAsync(ct)
            .ConfigureAwait(false);

        var list = new List<DealerDocumentImageDto>(rows.Length);
        foreach (var r in rows)
        {
            var bytes = r.Imagedata is { Length: > 0 } ? r.Imagedata : r.Image;
            var b64 = bytes is { Length: > 0 } ? Convert.ToBase64String(bytes) : "";
            list.Add(new DealerDocumentImageDto { LineNo = r.Lineno, ImageBase64 = b64 });
        }

        return list;
    }

    /// <inheritdoc />
    public async Task UploadDealerDocumentImagesAsync(
        ITenantScope scope,
        string documentNo,
        int docType,
        IReadOnlyList<string> imageBase64Payloads,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(scope);
        if (string.IsNullOrWhiteSpace(documentNo))
            throw new ArgumentException("Document number is required.", nameof(documentNo));
        if (imageBase64Payloads == null || imageBase64Payloads.Count == 0)
            return;

        var docNo = documentNo.Trim();

        // Use MaxAsync to find the next line number without fetching existing BLOBs into memory.
        var maxLine = await scope.Query<Images>()
            .Where(i => i.DocumentNo == docNo && i.DocType == docType)
            .MaxAsync(i => (int?)i.Lineno, ct)
            .ConfigureAwait(false);

        var nextLine = (maxLine ?? 0) + 1;

        // Insert into the same Images table the app reads from (avoids slow NAV SOAP AddUpdateImage).
        foreach (var raw in imageBase64Payloads)
        {
            var b64 = NormalizeDocumentImageBase64(raw);
            if (string.IsNullOrEmpty(b64))
                continue;

            byte[] bytes;
            try
            {
                bytes = Convert.FromBase64String(b64);
            }
            catch (FormatException)
            {
                continue;
            }

            var lineNo = nextLine++;
            var row = new Images
            {
                DocumentNo = docNo,
                Lineno = lineNo,
                DocType = docType,
                Image = bytes,
                Imagedata = Array.Empty<byte>()
            };
            await scope.InsertAsync(row, ct).ConfigureAwait(false);
        }
    }

    /// <summary>Strips <c>data:image/...;base64,</c> prefix if present.</summary>
    private static string NormalizeDocumentImageBase64(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return "";
        var s = raw.Trim();
        var idx = s.IndexOf("base64,", StringComparison.OrdinalIgnoreCase);
        if (idx >= 0)
            s = s[(idx + "base64,".Length)..].Trim();
        return s;
    }

    /// <inheritdoc />
    public async Task<int> SyncClaimPostedMobileNumbersAsync(ITenantScope scope, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(scope);

        var claimT = scope.GetQualifiedTableName("Claim & Failure Posted", isShared: false);
        var headerT = scope.GetQualifiedTableName("Sales Invoice Header", isShared: false);

        // 1. Ensure Mobile No_ column exists on Claim & Failure Posted table if missing
        var sqlCheckCol = $@"
            IF OBJECT_ID('{claimT}', 'U') IS NOT NULL AND COL_LENGTH('{claimT}', 'Mobile No_') IS NULL
            BEGIN
                ALTER TABLE {claimT} ADD [Mobile No_] NVARCHAR(50) NULL;
            END";
        await scope.ExecuteNonQueryAsync(sqlCheckCol, null, ct).ConfigureAwait(false);

        // 2. Sync Mobile No_ from Sales Invoice Header to Claim & Failure Posted mapping Invoice No_ to No_
        // filling all empty/NULL Mobile No_ fields in Claim & Failure Posted table
        var sqlSync = $@"
            UPDATE c
            SET c.[Mobile No_] = h.[Mobile No_]
            FROM {claimT} c
            INNER JOIN {headerT} h ON c.[Invoice No_] = h.[No_]
            WHERE (c.[Mobile No_] IS NULL OR LTRIM(RTRIM(c.[Mobile No_])) = '')
              AND h.[Mobile No_] IS NOT NULL 
              AND LTRIM(RTRIM(h.[Mobile No_])) <> ''";

        var count = await scope.ExecuteNonQueryAsync(sqlSync, null, ct).ConfigureAwait(false);
        _logger.LogInformation("SyncClaimPostedMobileNumbersAsync: updated {UpdatedCount} Claim & Failure Posted records with mobile numbers.", count);
        return count;
    }

    /// <inheritdoc />
    public async Task<int> ImportUniqueCrmContactsFromInvoicesAsync(ITenantScope scope, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(scope);

        // Sanitize sales invoice header mobile numbers and sync claim mobile numbers prior to import
        await SanitizeSalesInvoiceHeaderMobileNumbersAsync(scope, ct).ConfigureAwait(false);
        await SyncClaimPostedMobileNumbersAsync(scope, ct).ConfigureAwait(false);

        var headerT = scope.GetQualifiedTableName("Sales Invoice Header", isShared: false);
        var lineT = scope.GetQualifiedTableName("Sales Invoice Line", isShared: false);
        var custT = scope.GetQualifiedTableName("Customer", isShared: false);

        // Fetch distinct mobile rows from Sales Invoice Header (latest invoice per mobile)

        var sqlSelect = $@"
            WITH RankedInvoices AS (
                SELECT
                    h.[Mobile No_]                          AS MobileNo,
                    h.[Sell-to Customer Name]               AS FullName,
                    h.[Sell-to Address]                     AS Address,
                    h.[Sell-to City]                        AS City,
                    h.[Responsibility Center]               AS RespCenter,
                    h.[Sell-to Customer No_]                AS ERPCustomerNo,
                    c.[Phone No_]                           AS CustomerPhoneNo,
                    COALESCE(NULLIF(h.[GST Bill-to State Code], ''), NULLIF(c.[State Code], '')) AS State,
                    c.[Area Code]                           AS ERPAreaCode,
                    ROW_NUMBER() OVER (
                        PARTITION BY LTRIM(RTRIM(h.[Mobile No_]))
                        ORDER BY h.[Posting Date] DESC
                    ) AS rn
                FROM {headerT} h
                LEFT JOIN {custT} c ON c.[No_] = h.[Sell-to Customer No_]
                WHERE LTRIM(RTRIM(ISNULL(h.[Mobile No_], ''))) <> ''
            )
            SELECT 
                r.MobileNo, r.FullName, r.Address, r.City, r.RespCenter, r.ERPCustomerNo, r.CustomerPhoneNo, r.State, r.ERPAreaCode,
                (
                    SELECT STUFF((
                        SELECT ', ' + l.[No_]
                        FROM (
                            SELECT DISTINCT line.[No_]
                            FROM {lineT} line
                            INNER JOIN {headerT} inv ON line.[Document No_] = inv.[No_]
                            WHERE inv.[Mobile No_] = r.MobileNo 
                              AND line.[Item Category Code] IN ('ECOMILE', 'RETD')
                              AND line.[No_] <> ''
                        ) l
                        ORDER BY l.[No_]
                        FOR XML PATH(''), TYPE
                    ).value('.', 'NVARCHAR(MAX)'), 1, 2, '')
                ) AS Products
            FROM RankedInvoices r
            WHERE r.rn = 1";

        var rows = await scope.QueryAsync<CrmImportRow>(sqlSelect, null, ct).ConfigureAwait(false);

        // Load existing contacts from CrmContacts
        var existingContacts = await _crmDb.CrmContacts
            .ToListAsync(ct)
            .ConfigureAwait(false);

        var existingMap = new Dictionary<string, CrmContact>(StringComparer.OrdinalIgnoreCase);
        foreach (var c in existingContacts)
        {
            if (string.IsNullOrWhiteSpace(c.MobileNo)) continue;
            
            if (TryNormalizeIndianMobile(c.MobileNo, out var norm, out _))
                existingMap[norm] = c;
            else
                existingMap[c.MobileNo.Trim()] = c;
        }

        int imported = 0;
        int updated = 0;
        foreach (var row in rows)
        {
            // Normalize the mobile number
            string? mobile = null;
            if (!string.IsNullOrWhiteSpace(row.MobileNo))
            {
                if (TryNormalizeIndianMobile(row.MobileNo, out var norm, out _))
                    mobile = norm;
                else
                    mobile = ExtractValidMobileNumber(row.MobileNo);
            }

            if (string.IsNullOrEmpty(mobile)) continue;

            var productsValue = !string.IsNullOrWhiteSpace(row.Products) ? row.Products.Trim() : null;
            var stateValue = !string.IsNullOrWhiteSpace(row.State) ? row.State.Trim() : null;
            var areaCodesValue = !string.IsNullOrWhiteSpace(row.ERPAreaCode) ? row.ERPAreaCode.Trim() : null;
            var customerNosValue = !string.IsNullOrWhiteSpace(row.ERPCustomerNo) ? row.ERPCustomerNo.Trim() : null;
            var city = !string.IsNullOrWhiteSpace(row.City) ? row.City.Trim() : null;

            if (existingMap.TryGetValue(mobile, out var contact))
            {
                bool modified = false;

                // Update products on existing contact in either case (empty or already populated)
                if (!string.IsNullOrWhiteSpace(productsValue))
                {
                    if (string.IsNullOrWhiteSpace(contact.Products))
                    {
                        contact.Products = productsValue;
                        modified = true;
                    }
                    else
                    {
                        var existingList = contact.Products.Split(',')
                            .Select(p => p.Trim())
                            .Where(p => !string.IsNullOrEmpty(p));
                        var newList = productsValue.Split(',')
                            .Select(p => p.Trim())
                            .Where(p => !string.IsNullOrEmpty(p));

                        var mergedStr = string.Join(", ", existingList.Concat(newList).Distinct(StringComparer.OrdinalIgnoreCase));
                        if (!string.Equals(contact.Products, mergedStr, StringComparison.OrdinalIgnoreCase))
                        {
                            contact.Products = mergedStr;
                            modified = true;
                        }
                    }
                }
                if (string.IsNullOrWhiteSpace(contact.State) && !string.IsNullOrWhiteSpace(stateValue))
                {
                    contact.State = stateValue;
                    modified = true;
                }
                if (string.IsNullOrWhiteSpace(contact.ERPAreaCodes) && !string.IsNullOrWhiteSpace(areaCodesValue))
                {
                    contact.ERPAreaCodes = areaCodesValue;
                    modified = true;
                }
                if (string.IsNullOrWhiteSpace(contact.ERPCustomerNos) && !string.IsNullOrWhiteSpace(customerNosValue))
                {
                    contact.ERPCustomerNos = customerNosValue;
                    modified = true;
                }
                if (string.IsNullOrWhiteSpace(contact.City) && !string.IsNullOrWhiteSpace(city))
                {
                    contact.City = city;
                    modified = true;
                }

                if (modified)
                {
                    updated++;
                }
            }
            else
            {
                contact = new CrmContact
                {
                    Id = Guid.NewGuid(),
                    ContactType = "Customer",
                    FullName = !string.IsNullOrWhiteSpace(row.FullName)
                        ? row.FullName.Trim()
                        : mobile,
                    MobileNo = mobile,
                    Address = !string.IsNullOrWhiteSpace(row.Address) ? row.Address.Trim() : null,
                    City = city,
                    State = stateValue,
                    RespCenter = !string.IsNullOrWhiteSpace(row.RespCenter) ? row.RespCenter.Trim() : null,
                    ERPCustomerNos = customerNosValue,
                    ERPAreaCodes = areaCodesValue,
                    Products = productsValue,
                    IsActive = true,
                    CreatedBy = "System Import",
                    CreatedAt = DateTime.UtcNow
                };

                _crmDb.CrmContacts.Add(contact);
                existingMap[mobile] = contact; // prevent duplicates within same batch
                imported++;
            }
        }

        if (imported > 0 || updated > 0)
            await _crmDb.SaveChangesAsync(ct).ConfigureAwait(false);

        _logger.LogInformation("ImportUniqueCrmContactsFromInvoicesAsync: imported {ImportedCount} new contacts, updated {UpdatedCount} existing contacts.", imported, updated);
        return imported + updated;
    }

    /// <inheritdoc />
    public async Task SanitizeSalesInvoiceHeaderMobileNumbersAsync(ITenantScope scope, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(scope);

        var headerT = scope.GetQualifiedTableName("Sales Invoice Header", isShared: false);
        var custT = scope.GetQualifiedTableName("Customer", isShared: false);

        var sqlSelect = $@"
            SELECT h.[No_] AS No, 
                   h.[Sell-to Customer Name] AS SellToCustomerName, 
                   h.[Sell-to Address] AS SellToAddress, 
                   h.[Sell-to Address 2] AS SellToAddress2,
                   c.[Phone No_] AS CustomerPhoneNo
            FROM {headerT} h
            LEFT JOIN {custT} c ON c.[No_] = h.[Sell-to Customer No_]
            WHERE h.[Mobile No_] IS NULL OR LTRIM(RTRIM(h.[Mobile No_])) = ''";

        var rows = await scope.QueryAsync<SalesInvoiceHeaderMobileSanitationRow>(sqlSelect, null, ct).ConfigureAwait(false);

        var sqlUpdate = $@"
            UPDATE {headerT}
            SET [Mobile No_] = @MobileNo
            WHERE [No_] = @No";

        foreach (var row in rows)
        {
            string? mobile = ExtractValidMobileNumber(row.SellToCustomerName)
                             ?? ExtractValidMobileNumber(row.SellToAddress)
                             ?? ExtractValidMobileNumber(row.SellToAddress2);

            if (string.IsNullOrEmpty(mobile) && !string.IsNullOrEmpty(row.CustomerPhoneNo))
            {
                if (TryNormalizeIndianMobile(row.CustomerPhoneNo, out var normMobile, out _))
                {
                    mobile = normMobile;
                }
                else
                {
                    mobile = ExtractValidMobileNumber(row.CustomerPhoneNo);
                }
            }

            if (!string.IsNullOrEmpty(mobile))
            {
                await scope.ExecuteNonQueryAsync(sqlUpdate, new { MobileNo = mobile, No = row.No }, ct).ConfigureAwait(false);
            }
        }
    }

    private static string? ExtractValidMobileNumber(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;

        var matches = System.Text.RegularExpressions.Regex.Matches(text, @"(?:\+?91|0)?[6-9]\d{9}");
        foreach (System.Text.RegularExpressions.Match match in matches)
        {
            if (TryNormalizeIndianMobile(match.Value, out var normalized, out _))
            {
                return normalized;
            }
        }

        var onlyDigits = new string(text.Where(char.IsDigit).ToArray());
        for (int i = 0; i <= onlyDigits.Length - 10; i++)
        {
            for (int len = 10; len <= 12 && i + len <= onlyDigits.Length; len++)
            {
                var candidate = onlyDigits.Substring(i, len);
                if (TryNormalizeIndianMobile(candidate, out var normalized, out _))
                {
                    return normalized;
                }
            }
        }

        return null;
    }

    private sealed class SalesInvoiceHeaderMobileSanitationRow
    {
        public string No { get; set; } = "";
        public string? SellToCustomerName { get; set; }
        public string? SellToAddress { get; set; }
        public string? SellToAddress2 { get; set; }
        public string? CustomerPhoneNo { get; set; }
    }

    private sealed class CrmImportRow
    {
        public string? MobileNo { get; set; }
        public string? FullName { get; set; }
        public string? Address { get; set; }
        public string? City { get; set; }
        public string? RespCenter { get; set; }
        public string? ERPCustomerNo { get; set; }
        public string? CustomerPhoneNo { get; set; }
        public string? State { get; set; }
        public string? Products { get; set; }
        public string? ERPAreaCode { get; set; }
    }

    public async Task<SalesHierarchySummaryDto> GetSubordinateSalespersonsAsync(
        ITenantScope scope,
        string supervisorEmployeeCode,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(scope);

        var cleanCode = (supervisorEmployeeCode ?? "").Trim().ToUpperInvariant();
        if (string.IsNullOrEmpty(cleanCode))
        {
            return new SalesHierarchySummaryDto();
        }

        var teamSalespersonTable = scope.GetQualifiedTableName("Team Salesperson", isShared: false);
        var employeeTable = scope.GetQualifiedTableName("Employee", isShared: false);

        // 1. Get Supervisor Info and Max Role Type
        var supervisorSql = $@"
            SELECT 
                MAX(ts.[Type]) AS SupervisorMaxRoleType,
                MAX(ts.[Name]) AS SupervisorName
            FROM {teamSalespersonTable} ts
            WHERE ts.[Code] = @cleanCode
              AND ts.[Type] IN (0, 1, 2, 3, 4);";

        var supRows = await scope.RawQueryToArrayAsync<SupervisorInfoRow>(supervisorSql, new { cleanCode }, ct).ConfigureAwait(false);
        var supervisorInfo = supRows.FirstOrDefault();

        var maxRoleType = supervisorInfo?.SupervisorMaxRoleType ?? 0;
        var supName = (supervisorInfo?.SupervisorName ?? "").Trim();

        if (string.IsNullOrEmpty(supName))
        {
            // Fallback: check Employee table for supervisor's name
            var empNameSql = $"SELECT [First Name] + ' ' + [Last Name] AS SupervisorName FROM {employeeTable} WHERE [No_] = @cleanCode;";
            var empNameRows = await scope.RawQueryToArrayAsync<SupervisorInfoRow>(empNameSql, new { cleanCode }, ct).ConfigureAwait(false);
            supName = (empNameRows.FirstOrDefault()?.SupervisorName ?? cleanCode).Trim();
        }

        var result = new SalesHierarchySummaryDto
        {
            SupervisorCode = cleanCode,
            SupervisorName = supName,
            SupervisorMaxRoleType = maxRoleType,
            SupervisorRoleName = SalesRoleHelper.GetRoleName(maxRoleType),
            SupervisorDisplayTitle = SalesRoleHelper.GetDisplayTitle(maxRoleType)
        };

        // If the supervisor is a pure salesman (Type 0) or has no roles above salesman, they have no subordinates.
        if (maxRoleType <= 0)
        {
            return result;
        }

        // 2. Query Subordinates who have a lower role than supervisor in shared teams
        var subordinatesSql = $@"
            SELECT 
                tsSub.[Code] AS Code,
                MAX(tsSub.[Name]) AS Name,
                MAX(tsSub.[Type]) AS RoleType,
                MAX(e.[Job Title]) AS JobTitle,
                MAX(e.[Mobile Phone No_]) AS MobilePhoneNo,
                MAX(e.[Company E-Mail]) AS CompanyEmail,
                COUNT(DISTINCT tsSub.[Team Code]) AS SharedTeamsCount
            FROM {teamSalespersonTable} tsSup
            INNER JOIN {teamSalespersonTable} tsSub
                ON tsSup.[Team Code] = tsSub.[Team Code]
            LEFT JOIN {employeeTable} e
                ON tsSub.[Code] = e.[No_]
            WHERE tsSup.[Code] = @cleanCode
              AND tsSub.[Type] IN (0, 1, 2, 3, 4)
              AND tsSub.[Type] < tsSup.[Type]
              AND tsSub.[Code] <> @cleanCode
              AND (e.[Status] IS NULL OR e.[Status] = 0)
            GROUP BY tsSub.[Code]
            ORDER BY RoleType DESC, Name ASC;";

        var subRows = await scope.RawQueryToArrayAsync<SubordinateRawRow>(subordinatesSql, new { cleanCode }, ct).ConfigureAwait(false);

        // 3. Query shared team codes for each subordinate
        var teamsSql = $@"
            SELECT DISTINCT
                tsSub.[Code] AS Code,
                tsSub.[Team Code] AS TeamCode
            FROM {teamSalespersonTable} tsSup
            INNER JOIN {teamSalespersonTable} tsSub
                ON tsSup.[Team Code] = tsSub.[Team Code]
            WHERE tsSup.[Code] = @cleanCode
              AND tsSub.[Type] IN (0, 1, 2, 3, 4)
              AND tsSub.[Type] < tsSup.[Type]
              AND tsSub.[Code] <> @cleanCode;";

        var teamRows = await scope.RawQueryToArrayAsync<SubordinateSharedTeamRow>(teamsSql, new { cleanCode }, ct).ConfigureAwait(false);

        var teamsLookup = teamRows
            .Where(r => !string.IsNullOrEmpty(r.Code) && !string.IsNullOrEmpty(r.TeamCode))
            .GroupBy(r => r.Code.Trim().ToUpperInvariant())
            .ToDictionary(g => g.Key, g => g.Select(x => x.TeamCode.Trim()).Distinct().ToList(), StringComparer.OrdinalIgnoreCase);

        var subordinates = new List<SubordinateSalespersonDto>(subRows.Length);
        foreach (var r in subRows)
        {
            var code = (r.Code ?? "").Trim();
            if (string.IsNullOrEmpty(code)) continue;

            teamsLookup.TryGetValue(code, out var sharedTeams);

            subordinates.Add(new SubordinateSalespersonDto
            {
                Code = code,
                Name = (r.Name ?? "").Trim(),
                RoleType = r.RoleType,
                RoleName = SalesRoleHelper.GetRoleName(r.RoleType),
                DisplayTitle = SalesRoleHelper.GetDisplayTitle(r.RoleType),
                JobTitle = (r.JobTitle ?? "").Trim(),
                MobilePhoneNo = (r.MobilePhoneNo ?? "").Trim(),
                CompanyEmail = (r.CompanyEmail ?? "").Trim(),
                SharedTeamsCount = r.SharedTeamsCount,
                SharedTeamCodes = sharedTeams ?? new List<string>()
            });
        }

        result.Subordinates = subordinates;
        result.SubordinateCodes = subordinates.Select(s => s.Code).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        result.TotalSubordinatesCount = subordinates.Count;

        return result;
    }

    public async Task<List<string>> GetSubordinateEmployeeCodesAsync(
        ITenantScope scope,
        string supervisorEmployeeCode,
        CancellationToken ct = default)
    {
        var hierarchy = await GetSubordinateSalespersonsAsync(scope, supervisorEmployeeCode, ct).ConfigureAwait(false);
        return hierarchy.SubordinateCodes;
    }

    private sealed class SupervisorInfoRow
    {
        public int? SupervisorMaxRoleType { get; set; }
        public string? SupervisorName { get; set; }
    }

    private sealed class SubordinateRawRow
    {
        public string Code { get; set; } = "";
        public string? Name { get; set; }
        public int RoleType { get; set; }
        public string? JobTitle { get; set; }
        public string? MobilePhoneNo { get; set; }
        public string? CompanyEmail { get; set; }
        public int SharedTeamsCount { get; set; }
    }

    private sealed class SubordinateSharedTeamRow
    {
        public string Code { get; set; } = "";
        public string TeamCode { get; set; } = "";
    }

    private sealed class TeamRecord
    {
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string RespCenter { get; set; } = string.Empty;
        public decimal ExistingTarget { get; set; }
        public DateTime? ExistingTargetEndDate { get; set; }
    }

    /// <summary>
    /// Calculates proposed target rounded up to the specified step (e.g. 50,000 for 0.5 Lakh)
    /// to form a meaningful, clean sales target and stretch growth on the grand total.
    /// </summary>
    private static decimal CalculateRoundedProposedTarget(decimal currentSale, decimal multiplier, decimal roundingStep)
    {
        if (currentSale <= 0m) return 0m;
        decimal effectiveMultiplier = multiplier == 0m ? 1m : multiplier;
        decimal rawTarget = currentSale * effectiveMultiplier;
        if (rawTarget <= 0m) return 0m;

        if (roundingStep == -1m) // Smart Adaptive
        {
            if (rawTarget >= 200000m) // >= 2 Lakhs: round up to nearest 0.5 Lakh (50,000)
                return Math.Ceiling(rawTarget / 50000m) * 50000m;
            if (rawTarget >= 50000m) // 50k - 2 Lakhs: round up to nearest 0.25 Lakh (25,000)
                return Math.Ceiling(rawTarget / 25000m) * 25000m;
            return Math.Ceiling(rawTarget / 5000m) * 5000m; // < 50k: round up to nearest 5,000
        }

        if (roundingStep > 0m)
        {
            return Math.Ceiling(rawTarget / roundingStep) * roundingStep;
        }

        return Math.Max(0m, Math.Round(rawTarget, 2));
    }

    /// <inheritdoc />
    public async Task<TeamSalesTargetsPreviewResult> PreviewTeamSalesTargetsAsync(
        ITenantScope scope,
        PreviewTeamSalesTargetsRequest request,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(request);

        DateTime fromDt;
        if (!DateTime.TryParse(request.FromDate, out fromDt))
        {
            fromDt = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        }

        DateTime toDt;
        if (!DateTime.TryParse(request.ToDate, out toDt))
        {
            toDt = DateTime.Today;
        }

        DateTime targetEndDate;
        if (!string.IsNullOrWhiteSpace(request.TargetEndDate) && DateTime.TryParse(request.TargetEndDate, out var parsedEndDate))
        {
            targetEndDate = parsedEndDate.Date;
        }
        else
        {
            // Default to last day of next month after toDt
            var nextMonthStart = new DateTime(toDt.Year, toDt.Month, 1).AddMonths(1);
            targetEndDate = nextMonthStart.AddMonths(1).AddDays(-1);
        }

        decimal roundingStep = request.RoundingStep ?? 50000m;

        string rcTable = scope.GetQualifiedTableName("Responsibility Center", isShared: false);
        string teamTable = scope.GetQualifiedTableName("Team", isShared: false);

        // Fetch Responsibility Center multipliers
        var rcSql = $"SELECT [Code], [Name], ISNULL([Target Multiplier], 0) AS TargetMultiplier FROM {rcTable} ORDER BY [Code]";
        var rcRows = await scope.RawQueryToArrayAsync<ResponsibilityCenterTargetMultiplierDto>(rcSql, null, ct).ConfigureAwait(false);
        var rcLookup = rcRows.ToDictionary(r => r.Code, r => r, StringComparer.OrdinalIgnoreCase);

        decimal respCenterMultiplier = 0m;
        if (!string.IsNullOrWhiteSpace(request.RespCenter) && !string.Equals(request.RespCenter, "ALL", StringComparison.OrdinalIgnoreCase))
        {
            if (rcLookup.TryGetValue(request.RespCenter.Trim(), out var selectedRc))
            {
                respCenterMultiplier = selectedRc.TargetMultiplier;
            }
        }

        // Fetch teams
        var teamWhere = new List<string> { "1=1" };
        var teamParams = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        if (!string.IsNullOrWhiteSpace(request.RespCenter) && !string.Equals(request.RespCenter, "ALL", StringComparison.OrdinalIgnoreCase))
        {
            teamParams["respCenter"] = request.RespCenter.Trim();
            teamWhere.Add("[Responsibility Center] = @respCenter");
        }

        var teamSql = $@"
        SELECT 
            [Code], 
            [Name], 
            [Responsibility Center] AS RespCenter, 
            ISNULL([Target (Sale)], 0) AS ExistingTarget, 
            [Target End Date] AS ExistingTargetEndDate 
        FROM {teamTable}
        WHERE {string.Join(" AND ", teamWhere)}
        ORDER BY [Responsibility Center], [Code]
        ";
        var teams = await scope.RawQueryToArrayAsync<TeamRecord>(teamSql, teamParams, ct).ConfigureAwait(false);

        // Fetch sales using GetSalesAndBalanceRowsAsync
        var reportParams = new SalesReportParams
        {
            From = fromDt.ToString("yyyy-MM-dd"),
            To = toDt.ToString("yyyy-MM-dd"),
            RespCenters = (!string.IsNullOrWhiteSpace(request.RespCenter) && !string.Equals(request.RespCenter, "ALL", StringComparison.OrdinalIgnoreCase))
                ? new[] { request.RespCenter.Trim() }
                : null,
            Type = request.SaleType ?? "retread-ecomile",
            View = "All"
        };
        var salesRows = await _salesReportService.GetSalesAndBalanceRowsAsync(scope, reportParams, ct).ConfigureAwait(false);

        // Group sales by TeamCode (which was mapped from Customer."Area Code" -> Area.Team)
        var salesByTeam = salesRows
            .Where(r => !string.IsNullOrWhiteSpace(r.TeamCode))
            .GroupBy(r => r.TeamCode!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.Sum(r => r.TotalSale), StringComparer.OrdinalIgnoreCase);

        var items = new List<TeamSalesTargetDto>();
        decimal totalCurrentSale = 0m;
        decimal totalNextTarget = 0m;

        foreach (var t in teams)
        {
            salesByTeam.TryGetValue(t.Code, out var currentSale);

            decimal rawMultiplier = 0m;
            if (rcLookup.TryGetValue(t.RespCenter, out var rcInfo))
            {
                rawMultiplier = rcInfo.TargetMultiplier;
            }

            // Next target formula: current sale * respCenter."Target Multiplier" [if 0 then 1], rounded UP to step
            decimal nextTarget = CalculateRoundedProposedTarget(currentSale, rawMultiplier, roundingStep);

            totalCurrentSale += currentSale;
            totalNextTarget += nextTarget;

            items.Add(new TeamSalesTargetDto
            {
                TeamCode = t.Code,
                TeamName = t.Name,
                RespCenter = t.RespCenter,
                CurrentSale = Math.Round(currentSale, 2),
                TargetMultiplier = rawMultiplier,
                NextTarget = nextTarget,
                ExistingTarget = Math.Round(t.ExistingTarget, 2),
                ExistingTargetEndDate = t.ExistingTargetEndDate > new DateTime(2000, 1, 1) ? t.ExistingTargetEndDate : null,
                NextTargetEndDate = targetEndDate
            });
        }

        // Sort: teams with sales first descending, then team code
        items = items.OrderByDescending(i => i.CurrentSale).ThenBy(i => i.TeamCode).ToList();

        decimal effectiveGrowthPercent = totalCurrentSale > 0m
            ? Math.Round(((totalNextTarget / totalCurrentSale) - 1m) * 100m, 2)
            : 0m;

        return new TeamSalesTargetsPreviewResult
        {
            Success = true,
            Message = $"Calculated sales targets for {items.Count} team(s).",
            RespCenter = request.RespCenter,
            RespCenterMultiplier = respCenterMultiplier,
            FromDate = fromDt,
            ToDate = toDt,
            NextTargetEndDate = targetEndDate,
            TotalCurrentSale = Math.Round(totalCurrentSale, 2),
            TotalNextTarget = Math.Round(totalNextTarget, 2),
            RoundingStep = roundingStep,
            EffectiveGrowthPercent = effectiveGrowthPercent,
            Items = items
        };
    }

    /// <inheritdoc />
    public async Task<GenerateTeamSalesTargetsResult> GenerateTeamSalesTargetsAsync(
        ITenantScope scope,
        GenerateTeamSalesTargetsRequest request,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(request);

        var preview = await PreviewTeamSalesTargetsAsync(scope, new PreviewTeamSalesTargetsRequest
        {
            RespCenter = request.RespCenter,
            FromDate = request.FromDate,
            ToDate = request.ToDate,
            TargetEndDate = request.TargetEndDate,
            SaleType = request.SaleType,
            RoundingStep = request.RoundingStep
        }, ct).ConfigureAwait(false);

        var overrides = request.TargetOverrides?.ToDictionary(o => o.TeamCode, o => o.Target, StringComparer.OrdinalIgnoreCase);

        string teamTable = scope.GetQualifiedTableName("Team", isShared: false);
        int updatedCount = 0;
        decimal totalGenerated = 0m;

        foreach (var item in preview.Items)
        {
            decimal targetToSet = item.NextTarget;
            if (overrides != null && overrides.TryGetValue(item.TeamCode, out var ovr))
            {
                targetToSet = Math.Max(0m, Math.Round(ovr, 2));
                item.NextTarget = targetToSet;
            }

            var sql = $@"
            UPDATE {teamTable}
            SET [Target (Sale)] = @targetSale,
                [Target End Date] = @targetEndDate
            WHERE [Code] = @teamCode
            ";

            var param = new Dictionary<string, object?>
            {
                ["targetSale"] = targetToSet,
                ["targetEndDate"] = preview.NextTargetEndDate,
                ["teamCode"] = item.TeamCode
            };

            var affected = await scope.ExecuteNonQueryAsync(sql, param, ct).ConfigureAwait(false);
            if (affected > 0)
            {
                updatedCount += affected;
                totalGenerated += targetToSet;
            }
        }

        decimal effectiveGrowth = preview.TotalCurrentSale > 0m
            ? Math.Round(((totalGenerated / preview.TotalCurrentSale) - 1m) * 100m, 2)
            : 0m;

        return new GenerateTeamSalesTargetsResult
        {
            Success = true,
            Message = $"Successfully generated and saved sales targets for {updatedCount} team(s) with total target of ₹{totalGenerated:N2}.",
            UpdatedCount = updatedCount,
            TotalGeneratedTarget = Math.Round(totalGenerated, 2),
            RoundingStep = preview.RoundingStep,
            EffectiveGrowthPercent = effectiveGrowth,
            Items = preview.Items
        };
    }

    /// <inheritdoc />
    public async Task<bool> UpdateRespCenterTargetMultiplierAsync(
        ITenantScope scope,
        string respCenterCode,
        decimal targetMultiplier,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(scope);
        if (string.IsNullOrWhiteSpace(respCenterCode))
            throw new ArgumentException("Responsibility center code is required.", nameof(respCenterCode));

        string rcTable = scope.GetQualifiedTableName("Responsibility Center", isShared: false);
        var sql = $@"
        UPDATE {rcTable}
        SET [Target Multiplier] = @multiplier
        WHERE [Code] = @code
        ";

        var param = new Dictionary<string, object?>
        {
            ["multiplier"] = targetMultiplier,
            ["code"] = respCenterCode.Trim()
        };

        var affected = await scope.ExecuteNonQueryAsync(sql, param, ct).ConfigureAwait(false);
        return affected > 0;
    }

    /// <inheritdoc />
    public async Task<decimal> GetRespCenterTargetMultiplierAsync(
        ITenantScope scope,
        string respCenterCode,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(scope);
        if (string.IsNullOrWhiteSpace(respCenterCode)) return 0m;

        string rcTable = scope.GetQualifiedTableName("Responsibility Center", isShared: false);
        var sql = $"SELECT ISNULL([Target Multiplier], 0) FROM {rcTable} WHERE [Code] = @code";
        var res = await scope.ExecuteScalarAsync<decimal?>(sql, new { code = respCenterCode.Trim() }, ct).ConfigureAwait(false);
        return res ?? 0m;
    }

    /// <inheritdoc />
    public async Task<List<ResponsibilityCenterTargetMultiplierDto>> GetAllRespCentersWithMultipliersAsync(
        ITenantScope scope,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(scope);
        string rcTable = scope.GetQualifiedTableName("Responsibility Center", isShared: false);
        var sql = $"SELECT [Code], [Name], ISNULL([Target Multiplier], 0) AS TargetMultiplier FROM {rcTable} ORDER BY [Code]";
        var rows = await scope.RawQueryToArrayAsync<ResponsibilityCenterTargetMultiplierDto>(sql, null, ct).ConfigureAwait(false);
        return rows.ToList();
    }
}
