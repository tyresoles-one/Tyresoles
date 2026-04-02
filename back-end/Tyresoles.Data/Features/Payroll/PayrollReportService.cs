using Dataverse.NavLive;
using System.Collections.Concurrent;
using System.Data;
using System.Reflection;
using Tyresoles.Data.Features.Payroll.Models;
using Tyresoles.Data.Features.Sales.Reports;
using Tyresoles.Reporting.Abstractions;
using Tyresoles.Sql.Abstractions;

namespace Tyresoles.Data.Features.Payroll;

/// <summary>
/// Payroll report service using Tyresoles.Sql for data and Tyresoles.Reporting for RDLC.
/// Uses existing <see cref="SalesReportParams"/> (ReportName, From, To, Nos, RespCenters, Regions, Type, View, ReportOutput).
/// Leave register uses a single CTE query (legacy <c>Database.LeaveRegister</c> parity); CL encash / mediclaim use dedicated parameterized SQL.
/// Heavy SQL builders live in <c>PayrollReportService.Queries.cs</c> (partial class).
/// </summary>
public sealed partial class PayrollReportService : IPayrollReportService
{
    private readonly IReportRenderer _reportRenderer;

    private static readonly IReadOnlyList<string> ReportNamesList = new[]
    {
        "Pay Slip", "Pay Sheet", "Department Summary", "Bank Payment Details", "Pay Mode Summary",
        "ESIC Details", "Non ESIC Details", "ESIC Summary", "Employee Join & Leave", "PF Details", "PF Summary", "PF Exit Report",
        "TDS Report", "Prof. Tax Report", "Leave Register", "Leave Balance", "CL Encashment", "Leave Encashment", "Mediclaim Encashment",
        "Mandays", "Mandays (WO/Ab)", "Employee Grade", "Labour Welfare Fund", "Employee Gratuity", "Employee Bonus",
        "Full & Final Settlement", "Employee Head Count", "Attendace Template", "Declared Holidays"
    };

    private static readonly List<ReportMeta> ReportMetaList = new()
    {
        new() { Id = 1, Name = "Pay Slip", ShowNos = true, ShowRegions = true },
        new() { Id = 2, Name = "Pay Sheet", ShowNos = true, ShowRegions = true },
        new() { Id = 9, Name = "Employee Join & Leave", ShowType = true, ShowView = true, TypeOptions = new List<string> { "Active", "Inactive", "Terminated" }, ViewOptions = new List<string> { "All" } },
        new() { Id = 13, Name = "TDS Report", ShowView = true, ViewOptions = new List<string> { "Monthly", "Quarterly", "Half Yearly", "Yearly" } },
        new() { Id = 14, Name = "Prof. Tax Report", ShowView = true, ViewOptions = new List<string> { "Monthly", "Quarterly", "Half Yearly", "Yearly" } },
        new() { Id = 15, Name = "Leave Register", ShowNos = true, ShowType = true, TypeOptions = new List<string> { "Active", "Inactive", "Terminated" } },
        new() { Id = 16, Name = "Leave Balance", ShowNos = true, ShowType = true, TypeOptions = new List<string> { "Active", "Inactive", "Terminated" } },
        new() { Id = 22, Name = "Employee Grade", ShowNos = true, ShowType = true, TypeOptions = new List<string> { "Active", "Inactive", "Terminated" } },
        new() { Id = 26, Name = "Full & Final Settlement", ShowNos = true, ShowType = true, TypeOptions = new List<string> { "Active", "Inactive", "Terminated" } },
    };

    public PayrollReportService(IReportRenderer reportRenderer)
    {
        _reportRenderer = reportRenderer ?? throw new ArgumentNullException(nameof(reportRenderer));
    }

    public IReadOnlyList<string> GetReportNames() => ReportNamesList;

    public async Task<List<ReportMeta>> GetReportMetaAsync(
        ITenantScope scope,
        string? reports = null,
        CancellationToken cancellationToken = default)
    {
        var query = scope.Query<GroupDetails>()
            .Where(x => x.Category == "RPT-PAYRL");

        var detailsArr = await scope.ToArrayAsync(query.OrderBy(x => x.Name), cancellationToken).ConfigureAwait(false);

        // Filter by report codes in memory (query layer does not support names.Contains(x.Code) in all contexts)
        if (!string.IsNullOrWhiteSpace(reports))
        {
            var names = new HashSet<string>(reports.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries), StringComparer.OrdinalIgnoreCase);
            if (names.Count > 0 && detailsArr != null)
                detailsArr = detailsArr.Where(d => names.Contains(d.Code)).ToArray();
        }
        var meta = ReportMetaList;
        var result = new List<ReportMeta>();

        foreach (var detail in detailsArr)
        {
            var m = meta.FirstOrDefault(x => string.Equals(x.Name, detail.Name, StringComparison.OrdinalIgnoreCase));
            if (m != null)
            {
                result.Add(new ReportMeta
                {
                    Id = m.Id,
                    Code = detail.Code,
                    Name = m.Name,
                    DatePreset = m.DatePreset,
                    OutputFormats = m.OutputFormats,
                    TypeOptions = m.TypeOptions,
                    ViewOptions = m.ViewOptions,
                    ShowType = m.ShowType,
                    ShowView = m.ShowView,
                    ShowCustomers = m.ShowCustomers,
                    ShowDealers = m.ShowDealers,
                    ShowAreas = m.ShowAreas,
                    ShowRegions = m.ShowRegions,
                    ShowRespCenters = m.ShowRespCenters,
                    ShowNos = m.ShowNos,
                    RequiredFields = m.RequiredFields
                });
            }
            else
            {
                // Fallback for reports not in hardcoded list
                result.Add(new ReportMeta { Id = 0, Code = detail.Code, Name = detail.Name, DatePreset = "Today" });
            }
        }

        return result;
    }


    public async Task<byte[]> RenderReportAsync(
        ITenantScope scope,
        string reportName,
        SalesReportParams parameters,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(reportName))
            throw new ArgumentException("Report name is required.", nameof(reportName));
        ArgumentNullException.ThrowIfNull(parameters);

        var (rdlcName, dataSource, reportParameters) = await GetReportDataAsync(scope, reportName, parameters, cancellationToken).ConfigureAwait(false);
        if (dataSource == null)
            return Array.Empty<byte>();

        var input = new ReportInput
        {
            Parameters = reportParameters ?? BuildParameters(parameters),
            DataSources = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase) { ["DataSet_Result"] = dataSource }
        };

        var isExcel = string.Equals(parameters.ReportOutput, "EXCEL", StringComparison.OrdinalIgnoreCase);
        await using var stream = isExcel
            ? await _reportRenderer.RenderExcelAsync(rdlcName, input, cancellationToken).ConfigureAwait(false)
            : await _reportRenderer.RenderPdfAsync(rdlcName, input, cancellationToken).ConfigureAwait(false);

        using var ms = new MemoryStream();
        await stream.CopyToAsync(ms, cancellationToken).ConfigureAwait(false);
        return ms.ToArray();
    }

    private static Dictionary<string, object?>? BuildParameters(SalesReportParams p)
    {
        var dict = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        if (!string.IsNullOrEmpty(p.From)) dict["From"] = p.From;
        if (!string.IsNullOrEmpty(p.To)) dict["To"] = p.To;
        return dict.Count > 0 ? dict : null;
    }

    private async Task<(string rdlcName, object? data, Dictionary<string, object?>? reportParameters)> GetReportDataAsync(
        ITenantScope scope,
        string reportName,
        SalesReportParams p,
        CancellationToken ct)
    {
        var name = reportName.Trim();
        (string rdlc, object? data) result = name switch
        {
            "Pay Slip" => await GetPaySlipAsync(scope, p, ct).ConfigureAwait(false),
            "Pay Sheet" => await GetPaySheetAsync(scope, p, ct).ConfigureAwait(false),
            "Department Summary" => await GetDepartmentSummaryAsync(scope, p, ct).ConfigureAwait(false),
            "Bank Payment Details" => await GetBankPayDetailsAsync(scope, p, ct).ConfigureAwait(false),
            "Pay Mode Summary" => await GetPayModeSummaryAsync(scope, p, ct).ConfigureAwait(false),
            "ESIC Details" => await GetESICDetailsAsync(scope, p, ct).ConfigureAwait(false),
            "Non ESIC Details" => await GetNonESICDetailsAsync(scope, p, ct).ConfigureAwait(false),
            "ESIC Summary" => await GetESICSummaryAsync(scope, p, ct).ConfigureAwait(false),
            "Employee Join & Leave" => await GetEmployeeJoinLeaveAsync(scope, p, ct).ConfigureAwait(false),
            "PF Details" => await GetPFDetailsAsync(scope, p, ct).ConfigureAwait(false),
            "PF Summary" => await GetPFSummaryAsync(scope, p, ct).ConfigureAwait(false),
            "PF Exit Report" => await GetPFExitAsync(scope, p, ct).ConfigureAwait(false),
            "TDS Report" => await GetTDSReportAsync(scope, p, ct).ConfigureAwait(false),
            "Prof. Tax Report" => await GetPTReportAsync(scope, p, ct).ConfigureAwait(false),
            "Leave Register" => await GetLeaveRegisterAsync(scope, p, ct).ConfigureAwait(false),
            "Leave Balance" => await GetLeaveBalanceAsync(scope, p, ct).ConfigureAwait(false),
            "CL Encashment" => await GetCLEncashmentAsync(scope, p, ct).ConfigureAwait(false),
            "Leave Encashment" => await GetLeaveEncashmentAsync(scope, p, ct).ConfigureAwait(false),
            "Mediclaim Encashment" => await GetMediclaimEncashmentAsync(scope, p, ct).ConfigureAwait(false),
            "Mandays" => await GetMandaysAsync(scope, p, "Mandays", ct).ConfigureAwait(false),
            "Mandays (WO/Ab)" => await GetMandaysAsync(scope, p, "ManDaysWOAb", ct).ConfigureAwait(false),
            "Employee Grade" => await GetEmployeeGradeAsync(scope, p, ct).ConfigureAwait(false),
            "Labour Welfare Fund" => await GetLabourWelfareFundAsync(scope, p, ct).ConfigureAwait(false),
            "Employee Gratuity" => await GetEmployeeGratuityAsync(scope, p, ct).ConfigureAwait(false),
            "Employee Bonus" => await GetEmployeeBonusAsync(scope, p, ct).ConfigureAwait(false),
            "Full & Final Settlement" => await GetFullAndFinalAsync(scope, p, ct).ConfigureAwait(false),
            "Employee Head Count" => await GetEmployeeHeadCountAsync(scope, p, ct).ConfigureAwait(false),
            "Attendace Template" => await GetLeaveRegisterTemplateAsync(scope, p, ct).ConfigureAwait(false),
            "Declared Holidays" => await GetDeclaredHolidaysAsync(scope, p, ct).ConfigureAwait(false),
            _ => ("", null)
        };
        return (result.rdlc, result.data, null);
    }

    private async Task<(string, object?)> GetPaySlipAsync(ITenantScope scope, SalesReportParams p, CancellationToken ct)
    {
        var records = await GetPayRecordsAsync(scope, p, ct).ConfigureAwait(false);
        if (records.Count == 0) return ("PaySlip", null);
        var respCenters = await GetResponsibilityCentersAsync(scope, p, ct).ConfigureAwait(false);
        var rows = new List<ReportPayRow>();
        foreach (var rec in records.OrderBy(r => r.Department).ThenBy(r => r.Section).ThenBy(r => r.EmpNo))
        {
            var (earn, deduct) = SortedPayLines(rec);
            var rc = respCenters.FirstOrDefault(c => string.Equals(c.Code, rec.RespCenterCode, StringComparison.OrdinalIgnoreCase));
            var addr = rc != null ? (rc.Address ?? "") + " " + (rc.Address2 ?? "") + " " + (rc.City ?? "") : "";
            var r = new ReportPayRow
            {
                ReportName = "Pay Slip",
                FilterText = $"For {rec.Date:MMMM-yyyy}",
                CompanyName = rc?.CompanyName ?? rec.CompanyName,
                RespCenter = rec.RespCenterCode,
                CompanyAddLine2 = addr.Trim(),
                No_Employee = rec.EmpNo,
                Initials_Employee = rec.EmpInitials,
                DepartmentTxt = rec.Department,
                SectionTxt = rec.Section,
                JobTitle_Employee = rec.JobTitle,
                CompanyAddLine1 = rec.LocationName,
                PFNo_Employee = rec.PFNo,
                PANNo_Employee = rec.PANNo,
                SocAccountNo_Employee = rec.SocAccNo,
                ESICNo_Employee = rec.ESICNo,
                AdhaarNo_Employee = rec.AdhaarNo,
                UANNo_Employee = rec.UANNo,
                PLDays_EmpSalaryRegister = rec.PLDays,
                CLDays_EmpSalaryRegister = rec.CLDays,
                SLDays_EmpSalaryRegister = rec.SLDays,
                PresentDays_EmpSalaryRegister = rec.ProductionDays,
                WorkDays_EmpSalaryRegister = rec.WorkDays,
                WeaklyOff_EmpSalaryRegister = rec.WeeklyOff,
                PaidHolidays_EmpSalaryRegister = rec.PaidHolidays,
                AbsentDays_EmpSalaryRegister = rec.AbsentDays,
                CLBalance = rec.CLBalance,
                SLBalance = rec.SLBalance,
                PLBalance = rec.PLBalance,
                NetPay_EmpSalaryRegister = rec.NetPay,
                TotalEarnings_EmpSalaryRegister = rec.TotalEarn,
                TotalDeductions_EmpSalaryRegister = rec.TotalDeduct,
                NetPayTxt = NetPayInWords(rec.NetPay),
                PaymentText = $"Note : Salary has been credited to {rec.BankAccNo}, {rec.BankName}"
            };
            ApplyEarnDeduct(r, earn, deduct);
            rows.Add(r);
        }
        return ("PaySlip", ToDataTable(rows));
    }

    private async Task<(string, object?)> GetPaySheetAsync(ITenantScope scope, SalesReportParams p, CancellationToken ct)
    {
        var records = await GetPayRecordsAsync(scope, p, ct).ConfigureAwait(false);
        if (records.Count == 0) return ("PaySheet", null);
        var respCenters = await GetResponsibilityCentersAsync(scope, p, ct).ConfigureAwait(false);
        var rows = new List<ReportPayRow>();
        foreach (var rec in records.OrderBy(r => r.Department).ThenBy(r => r.Section).ThenBy(r => r.EmpNo))
        {
            var (earn, deduct) = SortedPayLines(rec);
            var rc = respCenters.FirstOrDefault(c => string.Equals(c.Code, rec.RespCenterCode, StringComparison.OrdinalIgnoreCase));
            var addr = rc != null ? (rc.Address ?? "") + " " + (rc.Address2 ?? "") + " " + (rc.City ?? "") : "";
            var r = new ReportPayRow
            {
                ReportName = $"Pay Sheet for {rec.Date:MMMM-yyyy}",
                FilterText = $"For {rec.Date:MMMM-yyyy}",
                CompanyName = rc?.CompanyName ?? rec.CompanyName,
                RespCenter = rec.RespCenterCode,
                CompanyAddLine2 = addr.Trim(),
                No_Employee = rec.EmpNo,
                Initials_Employee = rec.EmpInitials,
                DepartmentTxt = rec.Department,
                SectionTxt = rec.Section,
                JobTitle_Employee = rec.JobTitle,
                CompanyAddLine1 = rec.LocationName,
                PFNo_Employee = rec.PFNo,
                PANNo_Employee = rec.PANNo,
                SocAccountNo_Employee = rec.SocAccNo,
                ESICNo_Employee = rec.ESICNo,
                AdhaarNo_Employee = rec.AdhaarNo,
                UANNo_Employee = rec.UANNo,
                PLDays_EmpSalaryRegister = rec.PLDays,
                CLDays_EmpSalaryRegister = rec.CLDays,
                SLDays_EmpSalaryRegister = rec.SLDays,
                PresentDays_EmpSalaryRegister = rec.ProductionDays,
                WorkDays_EmpSalaryRegister = rec.WorkDays,
                WeaklyOff_EmpSalaryRegister = rec.WeeklyOff,
                PaidHolidays_EmpSalaryRegister = rec.PaidHolidays,
                AbsentDays_EmpSalaryRegister = rec.AbsentDays,
                CLBalance = rec.CLBalance,
                SLBalance = rec.SLBalance,
                PLBalance = rec.PLBalance,
                NetPay_EmpSalaryRegister = rec.NetPay,
                TotalEarnings_EmpSalaryRegister = rec.TotalEarn,
                TotalDeductions_EmpSalaryRegister = rec.TotalDeduct,
                NetPayTxt = NetPayInWords(rec.NetPay),
                PaymentText = $"Note : Salary has been credited to {rec.BankAccNo}, {rec.BankName}"
            };
            ApplyEarnDeduct(r, earn, deduct);
            rows.Add(r);
        }
        return ("PaySheet", ToDataTable(rows));
    }

    private static void ApplyEarnDeduct(ReportPayRow r, List<(string Name, decimal Amount)> earn, List<(string Name, decimal Amount)> deduct)
    {
        for (var i = 0; i < earn.Count && i < 11; i++)
        {
            var (name, amt) = earn[i];
            switch (i)
            {
                case 0: r.EarnText1 = name; r.EarnAmount1 = amt; break;
                case 1: r.EarnText2 = name; r.EarnAmount2 = amt; break;
                case 2: r.EarnText3 = name; r.EarnAmount3 = amt; break;
                case 3: r.EarnText4 = name; r.EarnAmount4 = amt; break;
                case 4: r.EarnText5 = name; r.EarnAmount5 = amt; break;
                case 5: r.EarnText6 = name; r.EarnAmount6 = amt; break;
                case 6: r.EarnText7 = name; r.EarnAmount7 = amt; break;
                case 7: r.EarnText8 = name; r.EarnAmount8 = amt; break;
                case 8: r.EarnText9 = name; r.EarnAmount9 = amt; break;
                case 9: r.EarnText10 = name; r.EarnAmount10 = amt; break;
                case 10: r.EarnText11 = name; r.EarnAmount11 = amt; break;
            }
        }
        for (var i = 0; i < deduct.Count && i < 11; i++)
        {
            var (name, amt) = deduct[i];
            switch (i)
            {
                case 0: r.DeductText1 = name; r.DeductAmount1 = amt; break;
                case 1: r.DeductText2 = name; r.DeductAmount2 = amt; break;
                case 2: r.DeductText3 = name; r.DeductAmount3 = amt; break;
                case 3: r.DeductText4 = name; r.DeductAmount4 = amt; break;
                case 4: r.DeductText5 = name; r.DeductAmount5 = amt; break;
                case 5: r.DeductText6 = name; r.DeductAmount6 = amt; break;
                case 6: r.DeductText7 = name; r.DeductAmount7 = amt; break;
                case 7: r.DeductText8 = name; r.DeductAmount8 = amt; break;
                case 8: r.DeductText9 = name; r.DeductAmount9 = amt; break;
                case 9: r.DeductText10 = name; r.DeductAmount10 = amt; break;
                case 10: r.DeductText11 = name; r.DeductAmount11 = amt; break;
            }
        }
    }

    private static List<(string Name, decimal Amount)> EarnLines(PayRecordRow rec)
    {
        var list = new List<(string, decimal)>();
        if (rec.Basic != 0) list.Add(("Basic", rec.Basic));
        if (rec.DA != 0) list.Add(("D.A.", rec.DA));
        if (rec.HRA != 0) list.Add(("H.R.A.", rec.HRA));
        if (rec.CA != 0) list.Add(("C.A.", rec.CA));
        if (rec.Incentive != 0) list.Add(("Incentive", rec.Incentive));
        if (rec.ProdInc != 0) list.Add(("Prod. Incentive", rec.ProdInc));
        if (rec.SalaryAdv != 0) list.Add(("Salary Advance", rec.SalaryAdv));
        return list;
    }

    private static List<(string Name, decimal Amount)> DeductLines(PayRecordRow rec)
    {
        var list = new List<(string, decimal)>();
        if (rec.PF != 0) list.Add(("P.F.", rec.PF));
        if (rec.ESIC != 0) list.Add(("E.S.I.C.", rec.ESIC));
        if (rec.ProfTax != 0) list.Add(("Prof. Tax", rec.ProfTax));
        if (rec.LIC != 0) list.Add(("L.I.C.", rec.LIC));
        if (rec.SocShare != 0) list.Add(("Soc. Share", rec.SocShare));
        if (rec.SocLoan != 0) list.Add(("Soc. Loan", rec.SocLoan));
        if (rec.StaffLoan != 0) list.Add(("Staff Loan", rec.StaffLoan));
        if (rec.TDS != 0) list.Add(("T.D.S.", rec.TDS));
        if (rec.LWF != 0) list.Add(("Labour W. Fund", rec.LWF));
        return list;
    }

    private static (List<(string, decimal)> earn, List<(string, decimal)> deduct) SortedPayLines(PayRecordRow rec)
    {
        return (EarnLines(rec), DeductLines(rec));
    }

    private async Task<(string, object?)> GetDepartmentSummaryAsync(ITenantScope scope, SalesReportParams p, CancellationToken ct)
    {
        var records = await GetPayRecordsAsync(scope, p, ct).ConfigureAwait(false);
        if (records.Count == 0) return ("DeptPaySummary", null);
        var fromStr = DateTime.TryParse(p.From, out var from) ? from.ToString("dd-MMM-yyyy") : "";
        var toStr = DateTime.TryParse(p.To, out var to) ? to.ToString("dd-MMM-yyyy") : "";
        var periodText = $"Period from {fromStr} to {toStr}.";
        var companyName = records[0].CompanyName;
        var locationName = $"For Location {records[0].LocationName}";
        var payHeads = new[] { "No' Of Employees", "Zero Processed", "Basic", "D. A.", "H. R. A.", "C. A.", "Incentive", "Prod. Incentive", "Trav. Allowance", "Tel. Allowance", "Depu. Allowance", "Ch. Ed. Allowance", "Out station All.", "Total Earning", "P. F.", "E. S. I. C.", "Prof. Tax", "L. I. C.", "Soc. Share", "Soc. Loan", "Staff Loan", "T. D. S.", "Salary Advance", "Labour W. Fund", "Total Deduction", "Total Net Pay" };
        var headTypes = new[] { "Info", "Info", "Earn", "Earn", "Earn", "Earn", "Earn", "Earn", "Earn", "Earn", "Earn", "Earn", "Earn", "Earn", "Deduct", "Deduct", "Deduct", "Deduct", "Deduct", "Deduct", "Deduct", "Deduct", "Deduct", "Deduct", "Deduct", "Net" };
        var colors = new[] { "#ccccff", "#b3ffb3", "#ffcce6", "#cc6699" };
        var depts = records.GroupBy(x => x.Department).Select(g => g.First()).ToList();
        var columnRecords = new List<(string ColName, string RowName, decimal Value, string BgColor)>();
        int colorIndex = -1;
        var clmNames = new List<string>();
        foreach (var d in depts)
        {
            colorIndex = (colorIndex + 1) % colors.Length;
            var sections = records.Where(r => r.Department == d.Department).GroupBy(r => r.Section).Select(g => g.First()).OrderBy(r => r.Section).ToList();
            foreach (var s in sections)
            {
                var clmName = !string.IsNullOrEmpty(s.Section) ? s.Section : s.Department;
                clmNames.Add(clmName);
                var recos = records.Where(r => r.Department == s.Department && r.Section == s.Section).ToList();
                columnRecords.Add((clmName, "No' Of Employees", recos.Count, colors[colorIndex]));
                columnRecords.Add((clmName, "Zero Processed", 0, colors[colorIndex]));
                columnRecords.Add((clmName, "Basic", recos.Sum(r => r.Basic), colors[colorIndex]));
                columnRecords.Add((clmName, "D. A.", recos.Sum(r => r.DA), colors[colorIndex]));
                columnRecords.Add((clmName, "H. R. A.", recos.Sum(r => r.HRA), colors[colorIndex]));
                columnRecords.Add((clmName, "C. A.", recos.Sum(r => r.CA), colors[colorIndex]));
                columnRecords.Add((clmName, "Incentive", recos.Sum(r => r.Incentive), colors[colorIndex]));
                columnRecords.Add((clmName, "Prod. Incentive", recos.Sum(r => r.ProdInc), colors[colorIndex]));
                columnRecords.Add((clmName, "Total Earning", recos.Sum(r => r.TotalEarn), colors[colorIndex]));
                columnRecords.Add((clmName, "P. F.", recos.Sum(r => r.PF), colors[colorIndex]));
                columnRecords.Add((clmName, "E. S. I. C.", recos.Sum(r => r.ESIC), colors[colorIndex]));
                columnRecords.Add((clmName, "Prof. Tax", recos.Sum(r => r.ProfTax), colors[colorIndex]));
                columnRecords.Add((clmName, "Total Deduction", recos.Sum(r => r.TotalDeduct), colors[colorIndex]));
                columnRecords.Add((clmName, "Total Net Pay", recos.Sum(r => r.NetPay), colors[colorIndex]));
            }
        }
        var rows = new List<PaySummaryDeptRow>();
        for (int i = 0; i < payHeads.Length; i++)
        {
            var row = new PaySummaryDeptRow
            {
                ReportName = "Departmental Pay Summary",
                PeriodText = periodText,
                CompanyName = companyName,
                LocationName = locationName,
                Type = headTypes[i],
                Particular = payHeads[i],
                UserName = p.EntityCode ?? ""
            };
            for (int j = 0; j < clmNames.Count && j < 10; j++)
            {
                var cr = columnRecords.FirstOrDefault(c => c.ColName == clmNames[j] && c.RowName == payHeads[i]);
                if (cr.ColName != null)
                {
                    switch (j)
                    {
                        case 0: row.ClmName1 = cr.ColName; row.ClmValue1 = cr.Value; row.ClmColor1 = cr.BgColor; break;
                        case 1: row.ClmName2 = cr.ColName; row.ClmValue2 = cr.Value; row.ClmColor2 = cr.BgColor; break;
                        case 2: row.ClmName3 = cr.ColName; row.ClmValue3 = cr.Value; row.ClmColor3 = cr.BgColor; break;
                        case 3: row.ClmName4 = cr.ColName; row.ClmValue4 = cr.Value; row.ClmColor4 = cr.BgColor; break;
                        case 4: row.ClmName5 = cr.ColName; row.ClmValue5 = cr.Value; row.ClmColor5 = cr.BgColor; break;
                        case 5: row.ClmName6 = cr.ColName; row.ClmValue6 = cr.Value; row.ClmColor6 = cr.BgColor; break;
                        case 6: row.ClmName7 = cr.ColName; row.ClmValue7 = cr.Value; row.ClmColor7 = cr.BgColor; break;
                        case 7: row.ClmName8 = cr.ColName; row.ClmValue8 = cr.Value; row.ClmColor8 = cr.BgColor; break;
                        case 8: row.ClmName9 = cr.ColName; row.ClmValue9 = cr.Value; row.ClmColor9 = cr.BgColor; break;
                        case 9: row.ClmName10 = cr.ColName; row.ClmValue10 = cr.Value; row.ClmColor10 = cr.BgColor; break;
                    }
                }
            }
            rows.Add(row);
        }
        return ("DeptPaySummary", ToDataTable(rows));
    }

    private async Task<(string, object?)> GetBankPayDetailsAsync(ITenantScope scope, SalesReportParams p, CancellationToken ct)
    {
        var records = await GetPayRecordsAsync(scope, p, ct).ConfigureAwait(false);
        if (records.Count == 0) return ("BankPaySheet", null);
        var fromStr = DateTime.TryParse(p.From, out var from) ? from.ToString("dd-MMM-yyyy") : "";
        var toStr = DateTime.TryParse(p.To, out var to) ? to.ToString("dd-MMM-yyyy") : "";
        var periodText = $"For Period From {fromStr} to {toStr}";
        var rows = records.Select(rec => new BankPayDetailsRow
        {
            CompanyName = rec.CompanyName,
            LocationName = $"For Location: {rec.LocationName}",
            PeriodText = periodText,
            ReportName = "Bank wise Salary Details",
            UserName = $"Printed By. {p.EntityCode ?? ""}",
            BankName = rec.BankName ?? "Cash",
            Description = $"Salary for {from:MMM yyyy}",
            EmpBankAccNo = rec.BankAccNo,
            EmployeeName = rec.EmpInitials,
            EmployeeNo = rec.EmpNo,
            Salary = rec.NetPay,
            Hold = rec.IsHold
        }).ToList();
        return ("BankPaySheet", ToDataTable(rows));
    }

    private async Task<(string, object?)> GetPayModeSummaryAsync(ITenantScope scope, SalesReportParams p, CancellationToken ct)
    {
        var records = await GetPayRecordsAsync(scope, p, ct).ConfigureAwait(false);
        if (records.Count == 0) return ("PayModeSummary", null);
        var payDates = records.GroupBy(r => r.Date).Select(g => g.First()).ToList();
        var rows = new List<PaymentModeSummaryRow>();
        foreach (var rDate in payDates)
        {
            var byBank = records.Where(r => r.Date == rDate.Date).GroupBy(r => r.BankName).Select(g => g.First()).ToList();
            Console.WriteLine(byBank.Count);
            foreach (var rMode in byBank)
            {
                var name = !string.IsNullOrEmpty(rMode.BankName) ? rMode.BankName : "Cash";
                rows.Add(new PaymentModeSummaryRow
                {
                    CompanyName = rMode.CompanyName,
                    Location = $"For Location: {rMode.LocationName}",
                    ReportName = $"Payment Mode Summary for {rMode.Date:MMMM-yyyy}",
                    Amount = records.Where(r => r.Date == rMode.Date && r.BankName == rMode.BankName && !r.IsHold).Sum(r => r.NetPay),
                    HoldAmount = records.Where(r => r.Date == rMode.Date && (r.BankName ?? "Cash") == name && r.IsHold).Sum(r => r.NetPay),
                    Name = name
                });
            }
        }
        return ("PayModeSummary", ToDataTable(rows));
    }

    private async Task<(string, object?)> GetESICDetailsAsync(ITenantScope scope, SalesReportParams p, CancellationToken ct)
    {
        var records = await GetPayRecordsAsync(scope, p, ct).ConfigureAwait(false);
        var withEsic = records.Where(r => r.ESIC > 0).ToList();
        if (withEsic.Count == 0) return ("ESICDetails", null);
        decimal empShare = 0;
        decimal emrShare = 0;
        if (DateTime.Parse(p.From) < new DateTime(2019, 7, 1))
        {
            empShare = Convert.ToDecimal(1.75);
            emrShare = Convert.ToDecimal(4.75);
        }
        else
        {
            empShare = Convert.ToDecimal(0.75);
            emrShare = Convert.ToDecimal(3.25);
        }

        var rows = withEsic.Select(rec => new
        {
            CompanyName = rec.CompanyName,
            LocationName = $"Location : {rec.LocationName}",
            ReportName = "ESIC Details",
            MonthName = rec.Date.ToString("MMM-yyyy"),
            IPNo = rec.ESICNo,
            IPName = rec.EmpInitials,
            NoOfDays = rec.WorkDays,
            TotalWages = rec.TotalEarn,
            ESICAmount = rec.ESIC,
            LastWorkDate = new DateTime(1753, 1, 1) != rec.ESICLastWorkDate ? rec.ESICLastWorkDate?.ToString("dd-MMM-yy") : "" ?? "",
            Reason = rec.EsicExitReason == 0? "" : rec.EsicExitReason.ToString(),
            FooterEmpShareTxt = "Employee Share (0.75%)",
            FooterEmrShareTxt = "Employer Share (3.25%)",
            EmpShare = Convert.ToDecimal(empShare / 100),
            EmrShare = Convert.ToDecimal(emrShare / 100)
        }).ToList();
        return ("ESICDetails", ToDataTable(rows));
    }

    private async Task<(string, object?)> GetNonESICDetailsAsync(ITenantScope scope, SalesReportParams p, CancellationToken ct)
    {
        var records = await GetPayRecordsAsync(scope, p, ct).ConfigureAwait(false);
        var nonEsic = records.Where(r => r.ESIC == 0).ToList();
        if (nonEsic.Count == 0) return ("NonESICDetail", null);
        var rows = nonEsic.Select(rec => new
        {
            CompanyName = rec.CompanyName,
            LocationName = $"Location : {rec.LocationName}",
            ReportName = "Non ESIC Details",
            MonthName = rec.Date.ToString("MMM-yyyy"),
            EmpId = rec.EmpNo,
            Name = rec.EmpInitials,
            JoinDate = rec.JoinDate?.ToString("dd-MMM-yy") ?? "",
            LeaveDate = new DateTime(1753,1,1) != rec.LeftDate ? rec.LeftDate?.ToString("dd-MMM-yy") : "" ?? "",
            EsicInDate = rec.JoinDate?.ToString("dd-MMM-yy") ?? "",
            EsicOutDate = "",
            BasicDA = rec.Basic + rec.DA,
            GrossSalary = rec.TotalEarn
        }).ToList();
        return ("NonESICDetail", ToDataTable(rows));
    }

    private async Task<(string, object?)> GetESICSummaryAsync(ITenantScope scope, SalesReportParams p, CancellationToken ct)
    {
        var records = await GetPayRecordsAsync(scope, p, ct).ConfigureAwait(false);
        if (records.Count == 0) return ("ESICSummary", null);
        // Must use a concrete row type: ToDataTable(List<object>) uses T=object, which has no properties — RDLC gets an empty DataSet.
        var rows = new List<PayESICSummaryRow>();
        var payDates = records.GroupBy(p => p.Date).Select(p => p.First()).ToList();
        foreach (var payD in payDates)
        {
            decimal employeeShare = records.Where(p => p.ESIC != 0 && p.Date == payD.Date).Sum(p => p.ESIC);
            decimal grossPay = records.Where(p => p.ESIC != 0 && p.Date == payD.Date).Sum(p => p.TotalEarn);
            decimal employerShare = 0;
            if (payD.Date < new DateTime(2019, 7, 1))
                employerShare = grossPay == 0 ? 0 : Math.Round((grossPay * Convert.ToDecimal(4.75 / 100)), 0);
            else
                employerShare = grossPay == 0 ? 0 : Math.Round((grossPay * Convert.ToDecimal(3.25 / 100)), 0);

            var loc = string.Format("Location : {0}", payD.LocationName);
            var month = payD.Date.ToString("MMMM-yyyy");

            rows.Add(new PayESICSummaryRow
            {
                CompanyName = payD.CompanyName,
                ReportName = "ESIC Summary",
                LocationName = loc,
                MonthName = month,
                Particular = "Amount on which ESIC deducted.",
                Amount = grossPay,
            });

            rows.Add(new PayESICSummaryRow
            {
                CompanyName = payD.CompanyName,
                ReportName = "ESIC Summary",
                LocationName = loc,
                MonthName = month,
                Particular = "No of Employees.",
                Amount = records.Count(p => p.ESIC != 0 && p.Date == payD.Date),
            });

            rows.Add(new PayESICSummaryRow
            {
                CompanyName = payD.CompanyName,
                ReportName = "ESIC Summary",
                LocationName = loc,
                MonthName = month,
                Particular = "Share from Employees.",
                Amount = employeeShare,
            });

            rows.Add(new PayESICSummaryRow
            {
                CompanyName = payD.CompanyName,
                ReportName = "ESIC Summary",
                LocationName = loc,
                MonthName = month,
                Particular = "Share from Employer.",
                Amount = employerShare,
            });

            rows.Add(new PayESICSummaryRow
            {
                CompanyName = payD.CompanyName,
                ReportName = "ESIC Summary",
                LocationName = loc,
                MonthName = month,
                Particular = "Total ESIC Amount.",
                Amount = employeeShare + employerShare,
            });
        }
        return ("ESICSummary", ToDataTable(rows));
    }

    private async Task<(string, object?)> GetEmployeeJoinLeaveAsync(ITenantScope scope, SalesReportParams p, CancellationToken ct)
    {
        var (sql, prm) = BuildEmployeeJoinLeaveSqlAndParams(scope, p);
        if (string.IsNullOrEmpty(sql)) return ("EmpJoinLeave", null);
        var raw = await scope.RawQueryToArrayAsync<EmployeeJoinLeaveSqlRow>(sql, prm, ct).ConfigureAwait(false);
        var list = raw?.ToList() ?? new List<EmployeeJoinLeaveSqlRow>();
        if (list.Count == 0) return ("EmpJoinLeave", null);

        DateTime? fromDt = DateTime.TryParse(p.From, out var f) ? f : null;
        DateTime? toDt = DateTime.TryParse(p.To, out var t) ? t : null;
        var typeFilter = (p.Type ?? "").Trim();
        var viewAll = !string.IsNullOrWhiteSpace(p.View);

        IEnumerable<EmployeeJoinLeaveSqlRow> q = list;
        if (typeFilter.Length > 0 && !string.Equals(typeFilter, "All", StringComparison.OrdinalIgnoreCase))
            q = q.Where(r => string.Equals(r.Status ?? "", typeFilter, StringComparison.OrdinalIgnoreCase));

        if (!viewAll && fromDt.HasValue && toDt.HasValue)
        {
            q = q.Where(r =>
            {
                var joinIn = r.JoiningDate.HasValue && r.JoiningDate.Value >= fromDt.Value && r.JoiningDate.Value <= toDt.Value;
                var leaveIn = r.LeavingDate.HasValue && r.LeavingDate.Value.Year > 1753 && r.LeavingDate.Value >= fromDt.Value && r.LeavingDate.Value <= toDt.Value;
                return joinIn || leaveIn;
            });
        }

        var rows = q.Select(rec => new
        {
            CompanyName = rec.CompanyName,
            LocationName = $"Location : {rec.LocationName}",
            ReportName = "Employees List",
            EmpId = rec.EmpNo,
            Name = rec.EmpName,
            BirthDate = rec.BirthDate.HasValue ? rec.BirthDate.Value.ToString("dd-MMM-yy") : "",
            JoiningDate = rec.JoiningDate.HasValue ? rec.JoiningDate.Value.ToString("dd-MMM-yy") : "",
            LeavingDate = rec.LeavingDate.HasValue && rec.LeavingDate.Value.Year > 1753 ? rec.LeavingDate.Value.ToString("dd-MMM-yy") : "",
            PFNo = rec.PFNo,
            UANNo = rec.UANNo,
            ESICNo = rec.ESICNo,
            Department = rec.Department,
            Section = rec.Section
        }).ToList();
        return ("EmpJoinLeave", ToDataTable(rows));
    }

    /// <summary>Parity with legacy <c>Db.Payroll.PFDetails</c>; also the source for <see cref="GetPFSummaryAsync"/> (same as Live <c>PFSummary</c> calling <c>PFDetails</c>).</summary>
    private static List<PFDetailsReportRow> BuildPFDetailsRows(IReadOnlyList<PayRecordRow> records)
    {
        const double ageLimit = 58;
        var rows = new List<PFDetailsReportRow>();
        foreach (var rec in records)
        {
            if (rec.StopPFDeduction != 0) continue;
            string name = !string.IsNullOrEmpty( rec.NameOnPF) ? rec.NameOnPF : rec.EmpInitials;
            name = name.ToUpper();
            decimal wages = rec.Basic + rec.DA;
            decimal wagesX = wages > 14999 ? 15000 : wages;
            PFDetailsReportRow report = new PFDetailsReportRow();
            report.ReportName = "PF Details";
            report.CompanyName = rec.CompanyName;
            report.LocationName = string.Format("Location : {0}", rec.LocationName);
            report.MonthName = rec.Date.ToString("MMMM-yyyy");
            report.EmpId = rec.UANNo;
            report.Name = name;
            report.GrossWage = rec.TotalEarn;
            report.EPSWage = rec.StopEPS == 0 ? 0 : wagesX;
            report.EPFWage = wages;
            report.EDLIWage = wagesX;
            report.EPFAmount = Math.Round(report.EPFWage * Convert.ToDecimal(0.12));
            report.EPSAmount = Math.Round(report.EPSWage * Convert.ToDecimal(8.33 / 100));            
            if ( rec.Age()> ageLimit)
            {
                report.EPSWage = 0;
                report.EPSAmount = 0;
            }
            report.DiffAmount = report.EPFAmount - report.EPSAmount;
            report.Date = rec.Date;
            report.PFEstCode = rec.PFEstCode;
            rows.Add(report);
        }
        return rows;
    }

    private async Task<(string, object?)> GetPFDetailsAsync(ITenantScope scope, SalesReportParams p, CancellationToken ct)
    {
        var records = await GetPayRecordsAsync(scope, p, ct).ConfigureAwait(false);
        if (records.Count == 0) return ("PFDetails", null);
        var rows = BuildPFDetailsRows(records);
        return rows.Count > 0 ? ("PFDetails", ToDataTable(rows)) : ("PFDetails", null);
    }

    private static decimal EmployeeAgeYears(DateTime? birthDate, DateTime atDate)
    {
        if (!birthDate.HasValue) return 0;
        var birth = birthDate.Value.Date;
        var age = atDate.Year - birth.Year;
        if (atDate.DayOfYear < birth.DayOfYear) age--;
        return age;
    }

    /// <summary>Parity with legacy <c>Db.Payroll.PFSummary</c> (builds from same rows as <c>PFDetails</c>).</summary>
    private async Task<(string, object?)> GetPFSummaryAsync(ITenantScope scope, SalesReportParams p, CancellationToken ct)
    {
        var records = await GetPayRecordsAsync(scope, p, ct).ConfigureAwait(false);
        if (records.Count == 0) return ("PFSummary", null);
        var pfDetails = BuildPFDetailsRows(records);
        if (pfDetails.Count == 0) return ("PFSummary", null);

        var fromOk = DateTime.TryParse(p.From, out var fromParam);

        var payDates = pfDetails.GroupBy(c => c.Date).Select(c => c.First()).OrderBy(c => c.Date).ToList();
        var reports = new List<PFSummaryReportRow>();
        var index = 0;
        foreach (var pDate in payDates)
        {
            index++;
            var company = pDate.CompanyName;
            var location = string.Format("Location : {0}", pDate.LocationName);
            const string report = "PF Summary";
            var monthName = index.ToString() + pDate.Date.ToString("MMMM - yyyy");

            reports.Add(new PFSummaryReportRow
            {
                CompanyName = company,
                LocationName = location,
                ReportName = report,
                MonthName = monthName,
                Particular = "Establishment Code.",
                Text = pDate.PFEstCode,
                Group = "one",
            });

            reports.Add(new PFSummaryReportRow
            {
                CompanyName = company,
                LocationName = location,
                ReportName = report,
                MonthName = monthName,
                Particular = "Wage Month",
                Text = pDate.Date.ToString("MMMM - yyyy"),
                Group = "one",
            });

            reports.Add(new PFSummaryReportRow
            {
                CompanyName = company,
                LocationName = location,
                ReportName = report,
                MonthName = monthName,
                Particular = "Gross Wages.",
                Total = pfDetails.Where(c => c.Date == pDate.Date).Sum(c => c.GrossWage),
                Group = "two",
            });

            reports.Add(new PFSummaryReportRow
            {
                CompanyName = company,
                LocationName = location,
                ReportName = report,
                MonthName = monthName,
                Particular = "EPF Wages.",
                Total = pfDetails.Where(c => c.Date == pDate.Date).Sum(c => c.EPFWage),
                Group = "two",
            });

            reports.Add(new PFSummaryReportRow
            {
                CompanyName = company,
                LocationName = location,
                ReportName = report,
                MonthName = monthName,
                Particular = "EPS Wages.",
                Total = pfDetails.Where(c => c.Date == pDate.Date).Sum(c => c.EPSWage),
                Group = "two",
            });

            reports.Add(new PFSummaryReportRow
            {
                CompanyName = company,
                LocationName = location,
                ReportName = report,
                MonthName = monthName,
                Particular = "EDLI Wages.",
                Total = pfDetails.Where(c => c.Date == pDate.Date).Sum(c => c.EDLIWage),
                Group = "two",
            });

            var sumEpf = pfDetails.Where(c => c.Date == pDate.Date).Sum(c => c.EPFAmount);
            var sumDiff = pfDetails.Where(c => c.Date == pDate.Date).Sum(c => c.DiffAmount);
            var sumEps = pfDetails.Where(c => c.Date == pDate.Date).Sum(c => c.EPSAmount);
            var sumEpfWage = pfDetails.Where(c => c.Date == pDate.Date).Sum(c => c.EPFWage);
            var sumEdli = pfDetails.Where(c => c.Date == pDate.Date).Sum(c => c.EDLIWage);

            reports.Add(new PFSummaryReportRow
            {
                CompanyName = company,
                LocationName = location,
                ReportName = report,
                MonthName = monthName,
                Particular = "EPF Contribution A/C No. 1",
                Amount1 = sumEpf,
                Amount2 = sumDiff,
                Total = sumEpf + sumDiff,
                Group = "three",
            });

            reports.Add(new PFSummaryReportRow
            {
                CompanyName = company,
                LocationName = location,
                ReportName = report,
                MonthName = monthName,
                Particular = "EPS Contribution A/C No. 10",
                Amount1 = 0,
                Amount2 = sumEps,
                Total = sumEps,
                Group = "three",
            });

            decimal line9Total;
            if (fromOk && fromParam < new DateTime(2018, 6, 1))
                line9Total = sumEpfWage * 0.65m / 100m;
            else
                line9Total = sumEpfWage * 0.50m / 100m;
            line9Total = Math.Round(line9Total);

            reports.Add(new PFSummaryReportRow
            {
                CompanyName = company,
                LocationName = location,
                ReportName = report,
                MonthName = monthName,
                Particular = "EPF ADM Charges A/C No. 2",
                Amount1 = 0,
                Amount2 = 0,
                Total = line9Total,
                Group = "three",
            });

            var line10Total = Math.Round(sumEdli * 0.50m / 100m);
            reports.Add(new PFSummaryReportRow
            {
                CompanyName = company,
                LocationName = location,
                ReportName = report,
                MonthName = monthName,
                Particular = "EDLI Contribution A/C No. 21",
                Amount1 = 0,
                Amount2 = 0,
                Total = line10Total,
                Group = "three",
            });

            decimal line11Total;
            if (fromOk && fromParam < new DateTime(2018, 6, 1))
                line11Total = Math.Round(sumEdli * 0.01m / 100m);
            else
                line11Total = 0;

            reports.Add(new PFSummaryReportRow
            {
                CompanyName = company,
                LocationName = location,
                ReportName = report,
                MonthName = monthName,
                Particular = "EDLI ADM Charges A/C No. 22",
                Amount1 = 0,
                Amount2 = 0,
                Total = line11Total,
                Group = "three",
            });

            var line7Total = sumEpf + sumDiff;
            var line8Total = sumEps;
            reports.Add(new PFSummaryReportRow
            {
                CompanyName = company,
                LocationName = location,
                ReportName = report,
                MonthName = monthName,
                Particular = "Grand Total",
                Amount1 = 0,
                Amount2 = 0,
                Total = line11Total + line10Total + line9Total + line8Total + line7Total,
                Group = "three",
            });
        }

        return ("PFSummary", ToDataTable(reports));
    }

    private async Task<(string, object?)> GetPFExitAsync(ITenantScope scope, SalesReportParams p, CancellationToken ct)
    {
        var records = await GetPayRecordsAsync(scope, p, ct).ConfigureAwait(false);
        if (records.Count == 0) return ("PFExit", null);
        var rows = records.Select(rec => new { CompanyName = rec.CompanyName, LocationName = rec.LocationName, ReportName = "PF Exit", MonthName = rec.Date.ToString("MMM-yyyy"), UANNo = rec.UANNo, Name = rec.EmpInitials, ExitDate = rec.LeftDate?.ToString("dd-MMM-yy") ?? "", ExitCode = "" }).ToList();
        return ("PFExit", ToDataTable(rows));
    }

    private async Task<(string, object?)> GetTDSReportAsync(ITenantScope scope, SalesReportParams p, CancellationToken ct)
    {
        var records = await GetPayRecordsAsync(scope, p, ct).ConfigureAwait(false);
        var withTds = records.Where(r => r.TDS != 0).ToList();
        if (withTds.Count == 0) return ("TDSMonthly", null);
        var view = p.View ?? "Monthly";
        var rdlc = view switch { "Quarterly" => "TDSQuarterly", "Half Yearly" => "TDSHalfYearly", "Yearly" => "TDSYearly", _ => "TDSMonthly" };
        var rows = withTds.Select(rec => new PayTDSRow { CompanyName = rec.CompanyName, MonthName = rec.Date.ToString("MMM-yyyy"), LocationName = $"Location : {rec.LocationName}", ReportName = "Monthly TDS Report", MonthTxt1 = rec.Date.ToString("MMM-yy"), EmpId = rec.EmpNo, Name = rec.EmpInitials, Month1 = rec.TDS, Gross = rec.TotalEarn }).ToList();
        return (rdlc, ToDataTable(rows));
    }

    private async Task<(string, object?)> GetPTReportAsync(ITenantScope scope, SalesReportParams p, CancellationToken ct)
    {
        var records = await GetPayRecordsAsync(scope, p, ct).ConfigureAwait(false);
        var withPt = records.Where(r => r.ProfTax != 0).ToList();
        if (withPt.Count == 0) return ("TDSMonthly", null);
        var view = p.View ?? "Monthly";
        var rdlc = view switch { "Quarterly" => "TDSQuarterly", "Half Yearly" => "TDSHalfYearly", "Yearly" => "TDSYearly", _ => "TDSMonthly" };
        var rows = withPt.Select(rec => new PayTDSRow { CompanyName = rec.CompanyName, MonthName = rec.Date.ToString("MMM-yyyy"), LocationName = $"Location : {rec.LocationName}", ReportName = "Monthly Prof. Tax Report", MonthTxt1 = rec.Date.ToString("MMM-yy"), EmpId = rec.EmpNo, Name = rec.EmpInitials, Month1 = rec.ProfTax, Gross = rec.TotalEarn }).ToList();
        return (rdlc, ToDataTable(rows));
    }

    private async Task<(string, object?)> GetLeaveRegisterAsync(ITenantScope scope, SalesReportParams p, CancellationToken ct)
    {
        var list = await GetLeaveRegisterDataAsync(scope, p, ct).ConfigureAwait(false);
        return ("LeaveReg", list.Count > 0 ? ToDataTable(list) : null);
    }

    private async Task<(string, object?)> GetLeaveBalanceAsync(ITenantScope scope, SalesReportParams p, CancellationToken ct)
    {
        var list = await GetLeaveRegisterDataAsync(scope, p, ct).ConfigureAwait(false);
        return ("LeaveBalance", list.Count > 0 ? ToDataTable(list) : null);
    }

    private async Task<List<LeaveRegRow>> GetLeaveRegisterDataAsync(ITenantScope scope, SalesReportParams p, CancellationToken ct)
    {
        var (sql, prm) = BuildLeaveRegisterSqlAndParams(scope, p);
        if (string.IsNullOrEmpty(sql)) return new List<LeaveRegRow>();
        var list = await scope.RawQueryToArrayAsync<LeaveRegRow>(sql, prm, ct).ConfigureAwait(false);
        return list?.ToList() ?? new List<LeaveRegRow>();
    }

    private async Task<(string, object?)> GetCLEncashmentAsync(ITenantScope scope, SalesReportParams p, CancellationToken ct)
    {
        var (sql, prm) = BuildCLEncashSqlAndParams(scope, p);
        if (string.IsNullOrEmpty(sql)) return ("CLEncash", null);
        var raw = await scope.RawQueryToArrayAsync<CLEncashSqlRow>(sql, prm, ct).ConfigureAwait(false);
        var list = raw?.Where(r => !r.Executive).ToList() ?? new List<CLEncashSqlRow>();
        if (list.Count == 0) return ("CLEncash", null);

        var rows = new List<CLEncashReportRow>();
        foreach (var r in list)
        {
            if (r.BasicPay <= 0) continue;
            var leaveEncash = r.CL * (r.BasicPay / 30m);
            var totalPay = r.GrossPay + leaveEncash;
            var ptPayable = totalPay > 15000 ? 200m : 0m;
            var ptDiff = ptPayable - r.PTPaid;
            var finalEncash = leaveEncash - ptDiff;
            rows.Add(new CLEncashReportRow
            {
                CompanyName = r.CompanyName,
                ReportName = "Casual Leave Encashment",
                EmpNo = r.EmpNo,
                EmpName = r.EmpName,
                Department = r.Department,
                Section = r.Section,
                BankAcNo = r.BankAcNo,
                BasicPay = r.BasicPay,
                CL = r.CL,
                GrossPay = r.GrossPay,
                LeaveEncash = leaveEncash,
                TotalPay = totalPay,
                PTPayable = ptPayable,
                PTPaid = r.PTPaid,
                PTDiff = ptDiff,
                FinalEncash = finalEncash,
                Executive = r.Executive
            });
        }
        return ("CLEncash", rows.Count > 0 ? ToDataTable(rows) : null);
    }

    private async Task<(string, object?)> GetLeaveEncashmentAsync(ITenantScope scope, SalesReportParams p, CancellationToken ct)
    {
        var records = await GetPayRecordsAsync(scope, p, ct).ConfigureAwait(false);
        if (records.Count == 0) return ("LeaveEncash", null);
        var rows = records.Select(rec => new { CompanyName = rec.CompanyName, ReportName = "LEAVE ENCASHMENT", LocationName = rec.LocationName, EmpName = rec.EmpInitials, EmpNo = rec.EmpNo, PLOBal = rec.PLBalance, CLOBal = rec.CLBalance, SLOBal = rec.SLBalance, PLEarn = 0m, CLEarn = 0m, SLEarn = 0m, PLDeduct = 0m, CLDeduct = 0m, SLDeduct = 0m, LeaveBal = rec.PLBalance + rec.CLBalance + rec.SLBalance, BasicPay = rec.Basic, DAPay = rec.DA }).ToList();
        return ("LeaveEncash", ToDataTable(rows));
    }

    private async Task<(string, object?)> GetMediclaimEncashmentAsync(ITenantScope scope, SalesReportParams p, CancellationToken ct)
    {
        var (sql, prm) = BuildMediclaimEncashSqlAndParams(scope, p);
        if (string.IsNullOrEmpty(sql)) return ("Mediclaim", null);
        var raw = await scope.RawQueryToArrayAsync<MediclaimReportRow>(sql, prm, ct).ConfigureAwait(false);
        return ("Mediclaim", raw != null && raw.Length > 0 ? ToDataTable(raw) : null);
    }

    private async Task<(string, object?)> GetMandaysAsync(ITenantScope scope, SalesReportParams p, string rdlcName, CancellationToken ct)
    {
        var records = await GetPayRecordsAsync(scope, p, ct).ConfigureAwait(false);
        if (records.Count == 0) return (rdlcName, null);
        var rows = records.Select(rec => new { CompanyName = rec.CompanyName, LocationName = rec.LocationName, ReportName = "Mandays", EmpNo = rec.EmpNo, EmpName = rec.EmpInitials, Department = rec.Department, Section = rec.Section, WorkDays = rec.WorkDays, PresentDays = rec.PresentDays, AbsentDays = rec.AbsentDays }).ToList();
        return (rdlcName, ToDataTable(rows));
    }

    private async Task<(string, object?)> GetEmployeeGradeAsync(ITenantScope scope, SalesReportParams p, CancellationToken ct)
    {
        var records = await GetPayRecordsAsync(scope, p, ct).ConfigureAwait(false);
        if (records.Count == 0) return ("EmpGrade", null);
        var rows = records.GroupBy(r => r.EmpNo).Select(g => g.First()).Select(rec => new { CompanyName = rec.CompanyName, ReportName = "Employee Grade", LocationName = rec.LocationName, EmpNo = rec.EmpNo, EmpName = rec.EmpInitials, Department = rec.Department, Section = rec.Section }).ToList();
        return ("EmpGrade", ToDataTable(rows));
    }

    private async Task<(string, object?)> GetLabourWelfareFundAsync(ITenantScope scope, SalesReportParams p, CancellationToken ct)
    {
        var records = await GetPayRecordsAsync(scope, p, ct).ConfigureAwait(false);
        var withLwf = records.Where(r => r.LWF != 0).ToList();
        if (withLwf.Count == 0) return ("EmpLWF", null);
        var rows = withLwf.Select(rec => new { CompanyName = rec.CompanyName, ReportName = "Labour Welfare Fund", LocationName = rec.LocationName, EmpNo = rec.EmpNo, Name = rec.EmpInitials, LWF = rec.LWF }).ToList();
        return ("EmpLWF", ToDataTable(rows));
    }

    private async Task<(string, object?)> GetEmployeeGratuityAsync(ITenantScope scope, SalesReportParams p, CancellationToken ct)
    {
        var records = await GetPayRecordsAsync(scope, p, ct).ConfigureAwait(false);
        if (records.Count == 0) return ("EmpGratuity", null);
        var rows = records.GroupBy(r => r.EmpNo).Select(g => g.First()).Select(rec => new { CompanyName = rec.CompanyName, ReportName = "Employee Gratuity", LocationName = rec.LocationName, EmpNo = rec.EmpNo, Name = rec.EmpInitials }).ToList();
        return ("EmpGratuity", ToDataTable(rows));
    }

    private async Task<(string, object?)> GetEmployeeBonusAsync(ITenantScope scope, SalesReportParams p, CancellationToken ct)
    {
        var records = await GetPayRecordsAsync(scope, p, ct).ConfigureAwait(false);
        if (records.Count == 0) return ("EmpBonusCalc", null);
        var rows = records.Select(rec => new { CompanyName = rec.CompanyName, ReportName = "Employee Bonus", LocationName = rec.LocationName, EmpNo = rec.EmpNo, Name = rec.EmpInitials, Basic = rec.Basic, Bonus = rec.Basic * 0.08m }).ToList();
        return ("EmpBonusCalc", ToDataTable(rows));
    }

    private async Task<(string, object?)> GetFullAndFinalAsync(ITenantScope scope, SalesReportParams p, CancellationToken ct)
    {
        var records = await GetPayRecordsAsync(scope, p, ct).ConfigureAwait(false);
        if (records.Count == 0) return ("EmpFullFinal", null);
        var rows = records.GroupBy(r => r.EmpNo).Select(g => g.First()).Select(rec => new { CompanyName = rec.CompanyName, ReportName = "Full & Final Settlement", LocationName = rec.LocationName, EmpNo = rec.EmpNo, Name = rec.EmpInitials, NetPay = rec.NetPay }).ToList();
        return ("EmpFullFinal", ToDataTable(rows));
    }

    private async Task<(string, object?)> GetEmployeeHeadCountAsync(ITenantScope scope, SalesReportParams p, CancellationToken ct)
    {
        var records = await GetPayRecordsAsync(scope, p, ct).ConfigureAwait(false);
        if (records.Count == 0) return ("EmpHeadCount", null);
        var byDept = records.GroupBy(r => r.Department).Select(g => new { Department = g.Key, Count = g.Select(x => x.EmpNo).Distinct().Count() }).ToList();
        return ("EmpHeadCount", ToDataTable(byDept));
    }

    private async Task<(string, object?)> GetLeaveRegisterTemplateAsync(ITenantScope scope, SalesReportParams p, CancellationToken ct)
    {
        var list = await GetLeaveRegisterDataAsync(scope, p, ct).ConfigureAwait(false);
        return ("LeaveBalanceTemplate", list.Count > 0 ? ToDataTable(list) : null);
    }

    private async Task<(string, object?)> GetDeclaredHolidaysAsync(ITenantScope scope, SalesReportParams p, CancellationToken ct)
    {
        var (sql, prm) = BuildDeclaredHolidaysSqlAndParams(scope, p);
        if (string.IsNullOrEmpty(sql)) return ("DeclaredHolidays", null);
        var rows = await scope.RawQueryToArrayAsync<DeclaredHolidaysRow>(sql, prm, ct).ConfigureAwait(false);
        return ("DeclaredHolidays", rows != null && rows.Length > 0 ? ToDataTable(rows) : null);
    }

    private async Task<List<PayRecordRow>> GetPayRecordsAsync(ITenantScope scope, SalesReportParams p, CancellationToken ct)
    {
        var (sql, prm) = BuildPayRecordsSqlAndParams(scope, p);
        if (string.IsNullOrEmpty(sql)) return new List<PayRecordRow>();
        var list = await scope.RawQueryToArrayAsync<PayRecordRow>(sql, prm, ct).ConfigureAwait(false);
        return list?.ToList() ?? new List<PayRecordRow>();
    }

    private (string? sql, object? prm) BuildPayRecordsSqlAndParams(ITenantScope scope, SalesReportParams p)
    {
        var sr = scope.GetQualifiedTableName("Emp_ Salary Register", false);
        var emp = scope.GetQualifiedTableName("Employee", false);
        var resp = scope.GetQualifiedTableName("Responsibility Center", false);
        var cat = scope.GetQualifiedTableName("Group Category", false);
        var det = scope.GetQualifiedTableName("Group Details", false);
        var bank = scope.GetQualifiedTableName("Bank Account", false);
        var leave = scope.GetQualifiedTableName("Emp_ Leave Register", false);

        DateTime? from = DateTime.TryParse(p.From, out var f) ? f : null;
        DateTime? to = DateTime.TryParse(p.To, out var t) ? t : null;
        var respCenters = p.RespCenters?.ToList() ?? new List<string>();
        var nos = p.Nos?.ToList() ?? new List<string>();
        var regions = p.Regions?.ToList() ?? new List<string>();

        var where = new List<string> { "1=1" };
        where.Add("(@from IS NULL OR sr.[Date] >= @from)");
        where.Add("(@to IS NULL OR sr.[Date] <= @to)");
        if (respCenters.Count > 0) where.Add("sr.[Responsibility Center] IN @respCenters");
        if (nos.Count > 0) where.Add("e.[No_] IN @nos");
        if (regions.Count > 0) where.Add("cat.[Code] IN @regions");

        var sql = $@"
SELECT
  e.[No_] AS EmpNo, e.[Initials] AS EmpInitials, e.[Name (ESIC)] AS NameOnESIC, e.[Name (PF)] AS NameOnPF, e.[Job Title] AS JobTitle,
  rc.[Company Name] AS CompanyName, rc.[Name] AS LocationName, rc.[Code] AS RespCenterCode,
  cat.[Name] AS Department, det.[Name] AS Section,
  sr.[Date], e.[Birth Date] AS BirthDate, e.[Employment Date] AS JoinDate, e.[Emp_ Confirmation Date] AS ConfirmDate, e.[Termination Date] AS LeftDate,
  e.[Last Work Date (ESIC)] AS ESICLastWorkDate,
  sr.[Basic], sr.[Dearness Allowance] AS DA, sr.[House Rent Allowance] AS HRA, sr.[Conv Alllowance] AS CA,
  sr.[Prod_ Incentive] AS ProdInc, sr.[Incentive], sr.[Total Earnings] AS TotalEarn, sr.[Salary Advance] AS SalaryAdv,
  sr.[Soc_ Share Amount] AS SocShare, sr.[Soc_ Loan Repay Amount] AS SocLoan, sr.[ESIC], sr.[Staff Loan Repay Amount] AS StaffLoan,
  sr.[Provident Fund] AS PF, sr.[Life Insurance Premium] AS LIC, sr.[Professional Tax] AS ProfTax, sr.[TDS], sr.[Labour Welfare Fund] AS LWF,
  sr.[Total Deductions] AS TotalDeduct, sr.[Net Pay] AS NetPay, CAST(sr.[Hold Salary] AS BIT) AS IsHold,
  e.[Bank Code] AS BankCode, e.[Bank Account No_] AS BankAccNo, e.[UAN No_] AS UANNo, e.[ESIC No_] AS ESICNo,
  e.[ESIC Exit Reason] AS EsicExitReason, e.[PAN No_] AS PANNo, e.[PF No_] AS PFNo, e.[Adhaar No_] AS AdhaarNo, e.[Soc_ Account No] AS SocAccNo,
  e.[Stop PF Deduction] AS StopPFDeduction, e.[Stop EPS] AS StopEPS, rc.[PF Est_ Code] AS PFEstCode,
  b.[Bank Name] AS BankName, b.[Address] AS BankAddress, b.[Address 2] AS BankAddress2, b.[City] AS BankCity,
  sr.[Work Days] AS WorkDays, sr.[Weakly Off] AS WeeklyOff, sr.[Present Days] AS PresentDays, sr.[Production Days] AS ProductionDays,
  sr.[Absent Days] AS AbsentDays, sr.[Paid Holidays] AS PaidHolidays, sr.[PL Days] AS PLDays, sr.[SL Days] AS SLDays, sr.[CL Days] AS CLDays,
  (SELECT ISNULL(SUM(lr.[Qty]),0) FROM {leave} lr WHERE lr.[Date] <= sr.[Date] AND lr.[Employee No_] = sr.[Employee No_] AND lr.[Cause of Absence Code] = 'CL' AND lr.[Responsibility Center] = sr.[Responsibility Center]) AS CLBalance,
  (SELECT ISNULL(SUM(lr2.[Qty]),0) FROM {leave} lr2 WHERE lr2.[Date] <= sr.[Date] AND lr2.[Employee No_] = sr.[Employee No_] AND lr2.[Cause of Absence Code] = 'SL' AND lr2.[Responsibility Center] = sr.[Responsibility Center]) AS SLBalance,
  (SELECT ISNULL(SUM(lr3.[Qty]),0) FROM {leave} lr3 WHERE lr3.[Date] <= sr.[Date] AND lr3.[Employee No_] = sr.[Employee No_] AND lr3.[Cause of Absence Code] = 'PL' AND lr3.[Responsibility Center] = sr.[Responsibility Center]) AS PLBalance
FROM {sr} sr
LEFT JOIN {emp} e ON e.[No_] = sr.[Employee No_]
LEFT JOIN {resp} rc ON rc.[Code] = sr.[Responsibility Center]
LEFT JOIN {cat} cat ON cat.[Code] = e.[Department]
LEFT JOIN {det} det ON det.[Category] = e.[Department] AND det.[Code] = e.[Section]
LEFT JOIN {bank} b ON b.[No_] = e.[Bank Code]
WHERE " + string.Join(" AND ", where) + @"
ORDER BY sr.[Date], sr.[Employee No_]";

        object prm = respCenters.Count > 0 || nos.Count > 0 || regions.Count > 0
            ? new { from, to, respCenters = respCenters.Count > 0 ? respCenters : null, nos = nos.Count > 0 ? nos : null, regions = regions.Count > 0 ? regions : null }
            : new { from, to };
        return (sql, prm);
    }

    private (string? sql, object? prm) BuildDeclaredHolidaysSqlAndParams(ITenantScope scope, SalesReportParams p)
    {
        var resp = scope.GetQualifiedTableName("Responsibility Center", false);
        var cal = scope.GetQualifiedTableName("Shop Calendar Holiday", false);
        var fromStr = p.From ?? "";
        var toStr = p.To ?? "";
        var respCenters = p.RespCenters?.ToList() ?? new List<string>();
        var where = new List<string>();
        if (respCenters.Count > 0) where.Add("rc.[Code] IN @respCenters");
        var whereClause = where.Count > 0 ? "WHERE " + string.Join(" AND ", where) : "";
        var sql = $@"
SELECT rc.[Name] AS Location, h.[Description] AS Occasion, FORMAT(h.[Date], 'dd-MMM-yyyy') AS [Date], 'Declared Holiday List' AS ReportName, rc.[Company Name] AS CompanyName, @userName AS UserName, 'For Period ' + @from + ' to ' + @to AS PeriodTxt
FROM {resp} rc
LEFT JOIN {cal} h ON h.[Shop Calendar Code] = rc.[Shop Calendar Code] AND h.[Date] >= @from AND h.[Date] <= @to
{whereClause}";
        object prm = respCenters.Count > 0 ? new { from = fromStr, to = toStr, userName = p.EntityCode ?? "", respCenters } : new { from = fromStr, to = toStr, userName = p.EntityCode ?? "" };
        return (sql, prm);
    }

    private async Task<List<RespCenterRow>> GetResponsibilityCentersAsync(ITenantScope scope, SalesReportParams p, CancellationToken ct)
    {
        var resp = scope.GetQualifiedTableName("Responsibility Center", false);
        var respCenters = p.RespCenters?.ToList() ?? new List<string>();
        var sql = $"SELECT [Code] AS Code, [Name], [Company Name] AS CompanyName, [Address], [Address 2] AS Address2, [City] FROM {resp}";
        object? prm = null;
        if (respCenters.Count > 0) { sql += " WHERE [Code] IN @respCenters"; prm = new { respCenters }; }
        var list = await scope.RawQueryToArrayAsync<RespCenterRow>(sql, prm, ct).ConfigureAwait(false);
        return list?.ToList() ?? new List<RespCenterRow>();
    }

    private sealed class RespCenterRow
    {
        public string Code { get; set; } = "";
        public string? Name { get; set; }
        public string? CompanyName { get; set; }
        public string? Address { get; set; }
        public string? Address2 { get; set; }
        public string? City { get; set; }
    }

    private static readonly ConcurrentDictionary<Type, (PropertyInfo[] Props, Type[] ColTypes)> ToDataTableSchemaCache = new();

    private static DataTable? ToDataTable<T>(IEnumerable<T> rows)
    {
        var type = typeof(T);
        var (props, colTypes) = ToDataTableSchemaCache.GetOrAdd(type, t =>
        {
            var p = t.GetProperties();
            var types = new Type[p.Length];
            for (int i = 0; i < p.Length; i++)
            {
                var propType = p[i].PropertyType;
                if (propType.IsGenericType && propType.GetGenericTypeDefinition() == typeof(Nullable<>))
                    propType = Nullable.GetUnderlyingType(propType)!;
                types[i] = propType == typeof(DateTime) ? typeof(DateTime) : propType == typeof(decimal) ? typeof(decimal) : propType == typeof(int) ? typeof(int) : propType == typeof(bool) ? typeof(bool) : typeof(string);
            }
            return (p, types);
        });

        var dt = new DataTable();
        for (int i = 0; i < props.Length; i++)
            dt.Columns.Add(props[i].Name, colTypes[i]);

        foreach (var r in rows)
        {
            var row = dt.NewRow();
            for (int i = 0; i < props.Length; i++)
            {
                var val = props[i].GetValue(r);
                row[props[i].Name] = val ?? DBNull.Value;
            }
            dt.Rows.Add(row);
        }
        return dt;
    }

    /// <summary>Parity with legacy Navision <c>Tyresoles.One.Data.Navision.Db.Payroll.SalarySlip</c>: <c>Math.Round(record.NetPay).ToWords("(INR)", "paisa Only.")</c>.</summary>
    private static string NetPayInWords(decimal netPay) =>
        ToWords(Math.Round(netPay), "(INR)", "paisa Only.");

    private static string ToWords(decimal number, string prefix, string suffix) =>
        prefix + NumberToWords(number) + " " + suffix;

    private static string NumberToWords(decimal number)
    {
        if (number == 0) return "Zero";
        if (number < 0) return "Minus " + NumberToWords(Math.Abs(number));

        string words = "";
        long intPart = (long)Math.Truncate(number);
        int decPart = (int)((number - intPart) * 100);

        if (intPart > 0) words += NumberToWords(intPart);
        if (decPart > 0)
        {
            if (words != "") words += " and ";
            words += NumberToWords(decPart);
        }
        return words;
    }

    private static string NumberToWords(long number)
    {
        if (number == 0) return "Zero";
        if (number < 0) return "Minus " + NumberToWords(Math.Abs(number));

        string words = "";

        if ((number / 10000000) > 0)
        {
            words += NumberToWords(number / 10000000) + " Crore ";
            number %= 10000000;
        }

        if ((number / 100000) > 0)
        {
            words += NumberToWords(number / 100000) + " Lakh ";
            number %= 100000;
        }

        if ((number / 1000) > 0)
        {
            words += NumberToWords(number / 1000) + " Thousand ";
            number %= 1000;
        }

        if ((number / 100) > 0)
        {
            words += NumberToWords(number / 100) + " Hundred ";
            number %= 100;
        }

        if (number > 0)
        {
            if (words != "") words += "and ";
            var unitsMap = new[] { "Zero", "One", "Two", "Three", "Four", "Five", "Six", "Seven", "Eight", "Nine", "Ten", "Eleven", "Twelve", "Thirteen", "Fourteen", "Fifteen", "Sixteen", "Seventeen", "Eighteen", "Nineteen" };
            var tensMap = new[] { "Zero", "Ten", "Twenty", "Thirty", "Forty", "Fifty", "Sixty", "Seventy", "Eighty", "Ninety" };

            if (number < 20)
                words += unitsMap[number];
            else
            {
                words += tensMap[number / 10];
                if ((number % 10) > 0)
                    words += "-" + unitsMap[number % 10];
            }
        }

        return words;
    }
}
