namespace Tyresoles.Data.Features.Sales;

/// <summary>
/// Sales hierarchy roles matching Dynamics NAV Team Salesperson [Type] values.
/// UnitHead (4) -> RegionManager (3) -> ZoneManager (2) -> AreaManager (1) -> Salesman (0).
/// Note: Types 5 (Zone code) and 6 (Region code) are territory geography codes, not personnel.
/// </summary>
public enum SalesRoleType
{
    Salesman = 0,
    AreaManager = 1,
    ZoneManager = 2,
    RegionManager = 3,
    UnitHead = 4
}

public static class SalesRoleHelper
{
    public static string GetRoleName(int roleType) => roleType switch
    {
        0 => "Salesman",
        1 => "AreaManager",
        2 => "ZoneManager",
        3 => "RegionManager",
        4 => "UnitHead",
        _ => "Salesperson"
    };

    public static string GetDisplayTitle(int roleType) => roleType switch
    {
        0 => "Sales Representative",
        1 => "Area Sales Manager",
        2 => "Zonal Sales Manager",
        3 => "Regional Sales Manager",
        4 => "Unit Head",
        _ => "Sales Staff"
    };
}

/// <summary>
/// Detail of an individual subordinate salesperson.
/// </summary>
public class SubordinateSalespersonDto
{
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public int RoleType { get; set; }
    public string RoleName { get; set; } = "";
    public string DisplayTitle { get; set; } = "";
    public string? JobTitle { get; set; }
    public string? MobilePhoneNo { get; set; }
    public string? CompanyEmail { get; set; }
    public int SharedTeamsCount { get; set; }
    public List<string> SharedTeamCodes { get; set; } = new();
}

/// <summary>
/// Full hierarchy resolution result for a supervisor.
/// </summary>
public class SalesHierarchySummaryDto
{
    public string SupervisorCode { get; set; } = "";
    public string SupervisorName { get; set; } = "";
    public int SupervisorMaxRoleType { get; set; }
    public string SupervisorRoleName { get; set; } = "";
    public string SupervisorDisplayTitle { get; set; } = "";
    public int TotalSubordinatesCount { get; set; }
    public List<string> SubordinateCodes { get; set; } = new();
    public List<SubordinateSalespersonDto> Subordinates { get; set; } = new();
}
