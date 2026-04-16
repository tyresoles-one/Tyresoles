using Dataverse.NavLive;
using Tyresoles.Sql;
using Tyresoles.Sql.Abstractions;
using Tyresoles.Sql.GraphQL;

namespace Tyresoles.Data.Features.Payroll;

/// <summary>
/// Implementation of <see cref="IPayrollService"/>.
/// Uses Tyresoles.Sql fluent API for queryable access to NAV Employee table.
/// </summary>
public sealed class PayrollService : IPayrollService
{
    /// <inheritdoc/>
    public IQueryable<Employee> GetEmployees(ITenantScope scope, ReportFetchParam param)
    {
        ArgumentNullException.ThrowIfNull(scope);
        param ??= new ReportFetchParam();

        IQuery<Employee> query = scope.Query<Employee>();

        // Filter by responsibility centers
        if (param.RespCenters is { Count: > 0 })
            query = query.WhereIf(true, e => e.ResponsibilityCenter, param.RespCenters);

        // Filter by specific employee numbers
        if (param.Nos is { Count: > 0 })
            query = query.WhereIf(true, e => e.No, param.Nos);

        // Filter by department
        if (!string.IsNullOrWhiteSpace(param.Department))
            query = query.Where(e => e.Department == param.Department);

        // Filter by status (0 = Active in NAV)
        if (param.Status.HasValue)
            query = query.Where(e => e.Status == param.Status.Value);

        // Search by name or number
        if (!string.IsNullOrWhiteSpace(param.Search))
        {
            var search = param.Search;
            query = query.Where($"(t0.[No_] LIKE @s OR t0.[First Name] LIKE @s OR t0.[Last Name] LIKE @s OR t0.[Search Name] LIKE @s)",
                new { s = $"%{search}%" });
        }

        // Type filter: "Salaried" = NotSalaried == 0, "Direct" = Direct == 1
        if (!string.IsNullOrWhiteSpace(param.Type))
        {
            if (param.Type.Equals("Salaried", StringComparison.OrdinalIgnoreCase))
                query = query.Where(e => e.NotSalaried == 0);
            else if (param.Type.Equals("Direct", StringComparison.OrdinalIgnoreCase))
                query = query.Where(e => e.Direct == 1);
        }

        return query.AsQueryable(scope);
    }
}
