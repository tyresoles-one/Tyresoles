using System.Text.Json.Serialization;

namespace Tyresoles.Data.Features.Sales.Reports;

/// <summary>
/// Parameters for sales report generation. Aligns with front-end ReportFetchParams and legacy FetchParams.
/// </summary>
public sealed class SalesReportParams
{
    /// <summary>Report logical name (e.g. "Posted Sales Invoice").</summary>
    public string? ReportName { get; set; }

    /// <summary>Output format: PDF, Excel, Word.</summary>
    public string? ReportOutput { get; set; }

    /// <summary>Date from (ISO or date string).</summary>
    public string? From { get; set; }

    /// <summary>Date to (ISO or date string).</summary>
    public string? To { get; set; }

    /// <summary>Specific document numbers (e.g. invoice nos).</summary>
    public IReadOnlyList<string>? Nos { get; set; }

    /// <summary>Customer numbers filter (comma-separated). REST may still send a JSON array; see <see cref="CustomersFilterJsonConverter"/>.</summary>
    [JsonConverter(typeof(CustomersFilterJsonConverter))]
    public string? Customers { get; set; }

    /// <summary>Splits <see cref="Customers"/> into individual customer numbers.</summary>
    public static string[] ParseCustomerNos(string? customersCsv)
    {
        if (string.IsNullOrWhiteSpace(customersCsv))
            return Array.Empty<string>();
        return customersCsv
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    /// <summary>Dealer codes filter.</summary>
    public IReadOnlyList<string>? Dealers { get; set; }

    /// <summary>Area codes filter.</summary>
    public IReadOnlyList<string>? Areas { get; set; }

    /// <summary>Region/territory codes filter.</summary>
    public IReadOnlyList<string>? Regions { get; set; }

    /// <summary>Responsibility center codes filter.</summary>
    public IReadOnlyList<string>? RespCenters { get; set; }
    /// <summary>Optional entity context (e.g. Partner, PartnerGroup). Not used by report logic; accepted for API compatibility.</summary>
    public string? EntityType { get; set; }
    /// <summary>Optional entity code. Not used by report logic; accepted for API compatibility.</summary>
    public string? EntityCode { get; set; }

    /// <summary>Product filter for Customer Trial Balance: none, ecomile, retread, all.</summary>
    public string? Type { get; set; }
    /// <summary>View filter: "Only Active", "Only Has Balance", or null for all.</summary>
    public string? View { get; set; }
    /// <summary>Entity department (e.g. Sales) for Employee filter.</summary>
    public string? EntityDepartment { get; set; }
    /// <summary>Work date override for calculations. Uses system date if null.</summary>
    public string? WorkDate { get; set; }

    /// <summary>Live search string for document numbers.</summary>
    public string? Search { get; set; }

    /// <summary>Number of items to skip for paging.</summary>
    public int? Skip { get; set; }

    /// <summary>Number of items to take for paging.</summary>
    public int? Take { get; set; }
}
