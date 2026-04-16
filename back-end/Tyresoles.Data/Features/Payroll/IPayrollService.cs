using Dataverse.NavLive;
using Tyresoles.Sql;
using Tyresoles.Sql.Abstractions;
using Tyresoles.Sql.GraphQL;

namespace Tyresoles.Data.Features.Payroll;

/// <summary>
/// Payroll data service providing queryable methods for Navision payroll tables.
/// Returns <see cref="IQueryable{T}"/> so HotChocolate can push paging, filtering, and sorting to SQL.
/// </summary>
public interface IPayrollService
{
    /// <summary>
    /// Queryable list of employees with optional filters.
    /// Supports HotChocolate [UsePaging], [UseFiltering], [UseSorting], [UseProjection].
    /// </summary>
    IQueryable<Employee> GetEmployees(ITenantScope scope, ReportFetchParam param);
}

/// <summary>
/// Parameters for payroll report / data-fetch operations.
/// </summary>
public sealed class ReportFetchParam
{
    /// <summary>Comma-separated or list of responsibility center codes.</summary>
    public List<string> RespCenters { get; set; } = new();

    /// <summary>Employee number(s) to filter.</summary>
    public List<string> Nos { get; set; } = new();

    /// <summary>Department filter (e.g. "SALES", "PROD").</summary>
    public string? Department { get; set; }

    /// <summary>Status filter: 0 = Active, 1 = Inactive. null = all.</summary>
    public int? Status { get; set; }

    /// <summary>Search text for employee name/number.</summary>
    public string? Search { get; set; }

    /// <summary>From date filter.</summary>
    public string? From { get; set; }

    /// <summary>To date filter.</summary>
    public string? To { get; set; }

    /// <summary>Generic type discriminator (e.g. "Salaried", "Direct").</summary>
    public string? Type { get; set; }

    /// <summary>Generic view discriminator.</summary>
    public string? View { get; set; }
}
