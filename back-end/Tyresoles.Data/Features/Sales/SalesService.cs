using System.Linq;
using System.Linq.Expressions;
using Microsoft.Extensions.Logging;
using Dataverse.NavLive;
using Tyresoles.Data.Constants;
using Tyresoles.Data.Features.Common;
using Tyresoles.Data.Infrastructure;
using Tyresoles.Sql.Abstractions;
using Tyresoles.Sql.GraphQL;
using Tyresoles.Data;
using NavLiveVendor = Dataverse.NavLive.Vendor;
using NavLiveVehicles = Dataverse.NavLive.Vehicles;

namespace Tyresoles.Data.Features.Sales;

public sealed class SalesService : ISalesService
{
    private readonly GlobalQueryCache _cache;
    private readonly ILogger<SalesService> _logger;
    private readonly Connector _connector;

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

    public SalesService(GlobalQueryCache cache, ILogger<SalesService> logger, Connector connector)
    {
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _connector = connector ?? throw new ArgumentNullException(nameof(connector));
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
            BankIFSC = input.BankIFSC ?? ""
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
            ResponsibilityCenter = TruncateNavField(customer.ResponsibilityCenter, NavRespCenterMax)
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

    public IQueryable<SalespersonPurchaser> GetMyDealersQuery(
        ITenantScope scope,
        string? entityType,
        string? entityCode,
        string? department,
        string? respCenter = null)
    {
        // Non-sales employees (e.g. Administration) must match dealers across responsibility centers when searching;
        // otherwise Responsibility Center = user's respCenter excludes dealers in other RCs before GraphQL where applies.
        var isEmployeeNonSales = entityType == EntityTypes.Employee
            && !string.IsNullOrWhiteSpace(department)
            && !string.Equals(department, Departments.Sales, StringComparison.OrdinalIgnoreCase);

        var narrowByRespCenter = respCenter.HasValue() && !isEmployeeNonSales;

        var query = scope.Query<SalespersonPurchaser>()
            .WhereIf(narrowByRespCenter, a => a.ResponsibilityCenter == respCenter);

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
        string? respCenter = null)
    {        

        ArgumentNullException.ThrowIfNull(scope);       
        IQuery<Area> query = scope.Query<Area>()
            .WhereIf(respCenter.HasValue(), a => a.ResponsibilityCenter == respCenter);

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
        string? department, string? respCenter = null)
    {
        var query = scope.Query<Territory>()
            .Where(c => c.Type == 1)
            .WhereIf(respCenter.HasValue(), c => c.ResponsibilityCenter == respCenter);

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
}
