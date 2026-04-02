using Tyresoles.Data.Features.Payroll.Models;
using Tyresoles.Data.Features.Sales.Reports;
using Tyresoles.Sql.Abstractions;

namespace Tyresoles.Data.Features.Payroll;

/// <summary>
/// Parameterized SQL builders for payroll reports (single round-trip, Tyresoles.Sql).
/// Leave register CTE matches legacy Navision <c>Database.LeaveRegister</c>; CL encash matches <c>EncashCL</c>.
/// </summary>
public sealed partial class PayrollReportService
{
    /// <summary>Legacy <c>Database.LeaveRegister</c>: aggregated PL/SL/CL from Emp_ Leave Register in one query.</summary>
    private (string? sql, object? prm) BuildLeaveRegisterSqlAndParams(ITenantScope scope, SalesReportParams p)
    {
        if (!DateTime.TryParse(p.From, out var from) || !DateTime.TryParse(p.To, out var to))
            return (null, null);

        var emp = scope.GetQualifiedTableName("Employee", false);
        var cat = scope.GetQualifiedTableName("Group Category", false);
        var det = scope.GetQualifiedTableName("Group Details", false);
        var leave = scope.GetQualifiedTableName("Emp_ Leave Register", false);
        var respCenters = p.RespCenters?.ToList() ?? new List<string>();
        var nos = p.Nos?.ToList() ?? new List<string>();

        var where = new List<string> { "e.[Status] IN (0, 1)" };
        if (respCenters.Count > 0) where.Add("e.[Responsibility Center] IN @respCenters");
        if (nos.Count > 0) where.Add("e.[No_] IN @nos");

        var sql = $@"
WITH LeaveReg AS (
  SELECT
    lr.[Employee No_] AS EmployeeNo,
    lr.[Responsibility Center] AS RespCenter,
    lr.[Cause of Absence Code] AS AbsenceCode,
    SUM(CASE WHEN lr.[Date] < @from THEN lr.[Qty] ELSE 0 END) AS OpnBal,
    SUM(CASE WHEN lr.[Date] >= @from AND lr.[Date] <= @to AND lr.[Entry Type] = 0 THEN lr.[Qty] ELSE 0 END) AS Earned,
    SUM(CASE WHEN lr.[Date] >= @from AND lr.[Date] <= @to AND lr.[Entry Type] = 1 THEN lr.[Qty] ELSE 0 END) AS Deducted,
    SUM(CASE WHEN lr.[Date] <= @to THEN lr.[Qty] ELSE 0 END) AS Balance
  FROM {leave} lr
  GROUP BY lr.[Employee No_], lr.[Responsibility Center], lr.[Cause of Absence Code]
)
SELECT
  e.[No_] AS EmpNo,
  e.[Initials] AS EmpName,
  cat.[Name] AS Department,
  det.[Name] AS Section,
  e.[Job Title] AS JobTitle,
  e.[Responsibility Center] AS RespCenter,
  CASE WHEN e.[Employment Date] IS NULL OR e.[Employment Date] <= '1753-01-02' THEN '' ELSE FORMAT(e.[Employment Date], 'dd-MMM-yy') END AS JoiningDate,
  CASE WHEN e.[Termination Date] IS NULL OR e.[Termination Date] <= '1753-01-02' THEN '' ELSE FORMAT(e.[Termination Date], 'dd-MMM-yy') END AS LeaveDate,
  CASE WHEN e.[Emp_ Confirmation Date] IS NULL OR e.[Emp_ Confirmation Date] <= '1753-01-02' THEN '' ELSE FORMAT(e.[Emp_ Confirmation Date], 'dd-MMM-yy') END AS ConfirmDate,
  ISNULL(PL.OpnBal, 0) AS PLOBal, ISNULL(PL.Earned, 0) AS PLEarn, ISNULL(PL.Deducted, 0) AS PLDeduct, ISNULL(PL.Balance, 0) AS PL,
  ISNULL(SL.OpnBal, 0) AS SLOBal, ISNULL(SL.Earned, 0) AS SLEarn, ISNULL(SL.Deducted, 0) AS SLDeduct, ISNULL(SL.Balance, 0) AS SL,
  ISNULL(CL.OpnBal, 0) AS CLOBal, ISNULL(CL.Earned, 0) AS CLEarn, ISNULL(CL.Deducted, 0) AS CLDeduct, ISNULL(CL.Balance, 0) AS CL
FROM {emp} e
LEFT JOIN {cat} cat ON cat.[Code] = e.[Department]
LEFT JOIN {det} det ON det.[Category] = e.[Department] AND det.[Code] = e.[Section]
LEFT JOIN LeaveReg PL ON PL.EmployeeNo = e.[No_] AND PL.RespCenter = e.[Responsibility Center] AND PL.AbsenceCode = 'PL'
LEFT JOIN LeaveReg SL ON SL.EmployeeNo = e.[No_] AND SL.RespCenter = e.[Responsibility Center] AND SL.AbsenceCode = 'SL'
LEFT JOIN LeaveReg CL ON CL.EmployeeNo = e.[No_] AND CL.RespCenter = e.[Responsibility Center] AND CL.AbsenceCode = 'CL'
WHERE " + string.Join(" AND ", where);

        object prm = respCenters.Count > 0 || nos.Count > 0
            ? new { from, to, respCenters = respCenters.Count > 0 ? respCenters : null, nos = nos.Count > 0 ? nos : null }
            : new { from, to };

        return (sql, prm);
    }

    /// <summary>Legacy <c>Database.EncashCL</c> — salary register + CL balance + job title (executive filter in C#).</summary>
    private (string? sql, object? prm) BuildCLEncashSqlAndParams(ITenantScope scope, SalesReportParams p)
    {
        if (!DateTime.TryParse(p.From, out var from) || !DateTime.TryParse(p.To, out var to))
            return (null, null);

        var sr = scope.GetQualifiedTableName("Emp_ Salary Register", false);
        var emp = scope.GetQualifiedTableName("Employee", false);
        var cat = scope.GetQualifiedTableName("Group Category", false);
        var det = scope.GetQualifiedTableName("Group Details", false);
        var leave = scope.GetQualifiedTableName("Emp_ Leave Register", false);
        var sal = scope.GetQualifiedTableName("Salutation", false);
        var rc = scope.GetQualifiedTableName("Responsibility Center", false);

        var respCenters = p.RespCenters?.ToList() ?? new List<string>();
        var nos = p.Nos?.ToList() ?? new List<string>();

        var where = new List<string>
        {
            "sr.[Date] >= @from",
            "sr.[Date] <= @to"
        };
        if (respCenters.Count > 0) where.Add("sr.[Responsibility Center] IN @respCenters");
        if (nos.Count > 0) where.Add("sr.[Employee No_] IN @nos");

        var sql = $@"
WITH ClBal AS (
  SELECT [Employee No_] AS EmployeeNo, SUM([Qty]) AS Balance
  FROM {leave}
  WHERE [Cause of Absence Code] = 'CL' AND [Date] <= @to
  GROUP BY [Employee No_]
)
SELECT
  resp.[Company Name] AS CompanyName,
  sr.[Employee No_] AS EmpNo,
  e.[Initials] AS EmpName,
  cat.[Name] AS Department,
  det.[Name] AS Section,
  e.[Bank Account No_] AS BankAcNo,
  (sr.[Basic (S)] + sr.[Dearness Allowance (S)]) AS BasicPay,
  ISNULL(clb.Balance, 0) AS CL,
  sr.[Total Earnings] AS GrossPay,
  sr.[Professional Tax] AS PTPaid,
  CAST(ISNULL(jt.[Executive], 0) AS BIT) AS Executive
FROM {sr} sr
INNER JOIN {emp} e ON e.[No_] = sr.[Employee No_]
LEFT JOIN {rc} resp ON resp.[Code] = sr.[Responsibility Center]
LEFT JOIN {cat} cat ON cat.[Code] = e.[Department]
LEFT JOIN {det} det ON det.[Category] = e.[Department] AND det.[Code] = e.[Section]
LEFT JOIN {sal} jt ON jt.[Code] = e.[Job Title Code]
LEFT JOIN ClBal clb ON clb.EmployeeNo = sr.[Employee No_]
WHERE " + string.Join(" AND ", where) + @"
ORDER BY sr.[Employee No_]";

        object prmObj = respCenters.Count > 0 || nos.Count > 0
            ? new { from, to, respCenters = respCenters.Count > 0 ? respCenters : null, nos = nos.Count > 0 ? nos : null }
            : new { from, to };

        return (sql, prmObj);
    }

    /// <summary>Legacy <c>Database.MediclaimEncash</c>.</summary>
    private (string? sql, object? prm) BuildMediclaimEncashSqlAndParams(ITenantScope scope, SalesReportParams p)
    {
        if (!DateTime.TryParse(p.From, out var from) || !DateTime.TryParse(p.To, out var to))
            return (null, null);

        var sr = scope.GetQualifiedTableName("Emp_ Salary Register", false);
        var emp = scope.GetQualifiedTableName("Employee", false);
        var rc = scope.GetQualifiedTableName("Responsibility Center", false);

        var respCenters = p.RespCenters?.ToList() ?? new List<string>();

        var where = new List<string>
        {
            "sr.[ESIC] = 0",
            "sr.[Date] >= @from",
            "sr.[Date] <= @to"
        };
        if (respCenters.Count > 0) where.Add("sr.[Responsibility Center] IN @respCenters");

        var sql = $@"
SELECT
  resp.[Company Name] AS CompanyName,
  'Mediclaim Encashment' AS ReportName,
  'Location : ' + resp.[Name] AS LocationName,
  sr.[Employee No_] AS EmpNo,
  e.[Initials] AS EmpName,
  sr.[Basic (S)] AS Basic,
  sr.[Dearness Allowance (S)] AS DA,
  (sr.[Basic (S)] + sr.[Dearness Allowance (S)]) AS Total,
  CASE WHEN e.[Last Work Date (ESIC)] IS NULL OR e.[Last Work Date (ESIC)] <= '1753-01-02' THEN '' ELSE FORMAT(e.[Last Work Date (ESIC)], 'dd-MMM-yyyy') END AS ExitDate
FROM {sr} sr
INNER JOIN {emp} e ON e.[No_] = sr.[Employee No_]
LEFT JOIN {rc} resp ON resp.[Code] = sr.[Responsibility Center]
WHERE " + string.Join(" AND ", where) + @"
ORDER BY sr.[Employee No_]";

        object prm = respCenters.Count > 0
            ? new { from, to, respCenters = respCenters.Count > 0 ? respCenters : null }
            : new { from, to };

        return (sql, prm);
    }

    /// <summary>Employee master rows for Join &amp; Leave (legacy <c>EmployeeInfos</c> + filters in service).</summary>
    private (string? sql, object? prm) BuildEmployeeJoinLeaveSqlAndParams(ITenantScope scope, SalesReportParams p)
    {
        var emp = scope.GetQualifiedTableName("Employee", false);
        var cat = scope.GetQualifiedTableName("Group Category", false);
        var det = scope.GetQualifiedTableName("Group Details", false);
        var rc = scope.GetQualifiedTableName("Responsibility Center", false);
        var respCenters = p.RespCenters?.ToList() ?? new List<string>();
        var nos = p.Nos?.ToList() ?? new List<string>();

        var where = new List<string> { "1=1" };
        if (respCenters.Count > 0) where.Add("e.[Responsibility Center] IN @respCenters");
        if (nos.Count > 0) where.Add("e.[No_] IN @nos");

        var sql = $@"
SELECT
  resp.[Company Name] AS CompanyName,
  resp.[Name] AS LocationName,
  e.[No_] AS EmpNo,
  e.[Initials] AS EmpName,
  cat.[Name] AS Department,
  det.[Name] AS Section,
  e.[PF No_] AS PFNo,
  e.[UAN No_] AS UANNo,
  e.[ESIC No_] AS ESICNo,
  e.[Birth Date] AS BirthDate,
  e.[Employment Date] AS JoiningDate,
  e.[Termination Date] AS LeavingDate,
  CASE e.[Status] WHEN 0 THEN 'Active' WHEN 2 THEN 'Inactive' WHEN 3 THEN 'Terminated' ELSE '' END AS Status
FROM {emp} e
LEFT JOIN {rc} resp ON resp.[Code] = e.[Responsibility Center]
LEFT JOIN {cat} cat ON cat.[Code] = e.[Department]
LEFT JOIN {det} det ON det.[Category] = e.[Department] AND det.[Code] = e.[Section]
WHERE " + string.Join(" AND ", where);

        object? prm = respCenters.Count > 0 || nos.Count > 0
            ? new { respCenters = respCenters.Count > 0 ? respCenters : null, nos = nos.Count > 0 ? nos : null }
            : null;

        return (sql, prm);
    }

    private sealed class CLEncashSqlRow
    {
        public string CompanyName { get; set; } = "";
        public string EmpNo { get; set; } = "";
        public string EmpName { get; set; } = "";
        public string Department { get; set; } = "";
        public string Section { get; set; } = "";
        public string BankAcNo { get; set; } = "";
        public decimal BasicPay { get; set; }
        public decimal CL { get; set; }
        public decimal GrossPay { get; set; }
        public decimal PTPaid { get; set; }
        public bool Executive { get; set; }
    }

    private sealed class EmployeeJoinLeaveSqlRow
    {
        public string CompanyName { get; set; } = "";
        public string LocationName { get; set; } = "";
        public string EmpNo { get; set; } = "";
        public string EmpName { get; set; } = "";
        public string Department { get; set; } = "";
        public string Section { get; set; } = "";
        public string PFNo { get; set; } = "";
        public string UANNo { get; set; } = "";
        public string ESICNo { get; set; } = "";
        public DateTime? BirthDate { get; set; }
        public DateTime? JoiningDate { get; set; }
        public DateTime? LeavingDate { get; set; }
        public string? Status { get; set; }
    }
}
