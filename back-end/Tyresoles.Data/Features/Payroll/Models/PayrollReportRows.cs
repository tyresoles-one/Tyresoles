// DTOs for payroll reports. Property names align with RDLC DataSet_Result and legacy Models.Report.

namespace Tyresoles.Data.Features.Payroll.Models;

/// <summary>Internal row from salary register + employee + dept + resp center + bank (one row per pay record).</summary>
public sealed class PayRecordRow
{
    public string EmpNo { get; set; } = "";
    public string EmpInitials { get; set; } = "";
    public string NameOnESIC { get; set; } = "";
    public string NameOnPF { get; set; } = "";
    public string JobTitle { get; set; } = "";
    public string CompanyName { get; set; } = "";
    public string LocationName { get; set; } = "";
    public string RespCenterCode { get; set; } = "";
    public string Department { get; set; } = "";
    public string Section { get; set; } = "";
    public DateTime Date { get; set; }
    public DateTime? BirthDate { get; set; }
    public DateTime? JoinDate { get; set; }
    public DateTime? ConfirmDate { get; set; }
    public DateTime? LeftDate { get; set; }
    public DateTime? ESICLastWorkDate { get; set; }
    public decimal Basic { get; set; }
    public decimal DA { get; set; }
    public decimal HRA { get; set; }
    public decimal CA { get; set; }
    public decimal ProdInc { get; set; }
    public decimal Incentive { get; set; }
    public decimal TotalEarn { get; set; }
    public decimal SalaryAdv { get; set; }
    public decimal SocShare { get; set; }
    public decimal SocLoan { get; set; }
    public decimal ESIC { get; set; }
    public decimal StaffLoan { get; set; }
    public decimal PF { get; set; }
    public decimal LIC { get; set; }
    public decimal ProfTax { get; set; }
    public decimal TDS { get; set; }
    public decimal LWF { get; set; }
    public decimal TotalDeduct { get; set; }
    public decimal NetPay { get; set; }
    public bool IsHold { get; set; }
    public string BankCode { get; set; } = "";
    public string BankAccNo { get; set; } = "";
    public string UANNo { get; set; } = "";
    public string ESICNo { get; set; } = "";
    public int EsicExitReason { get; set; }
    public string PANNo { get; set; } = "";
    public string PFNo { get; set; } = "";
    public string AdhaarNo { get; set; } = "";
    public string SocAccNo { get; set; } = "";
    public string BankName { get; set; } = "";
    public string BankAddress { get; set; } = "";
    public string BankAddress2 { get; set; } = "";
    public string BankCity { get; set; } = "";
    public decimal WorkDays { get; set; }
    public decimal WeeklyOff { get; set; }
    public decimal PresentDays { get; set; }
    public decimal ProductionDays { get; set; }
    public decimal AbsentDays { get; set; }
    public decimal PaidHolidays { get; set; }
    public decimal PLDays { get; set; }
    public decimal SLDays { get; set; }
    public decimal CLDays { get; set; }
    public decimal CLBalance { get; set; }
    public decimal SLBalance { get; set; }
    public decimal PLBalance { get; set; }
    /// <summary>Employee flag: non-zero = do not calculate PF (legacy PayRecord.StopPF).</summary>
    public byte StopPFDeduction { get; set; }
    /// <summary>Employee flag: non-zero = no EPS wage (legacy PayRecord.StopEPS).</summary>
    public byte StopEPS { get; set; }
    /// <summary>PF establishment code for PF Details / Summary.</summary>
    public string PFEstCode { get; set; } = "";
    public double Age()
    {
        double val = 0;
        val = Convert.ToDouble((DateTime.Now - BirthDate)?.TotalDays / 365);
        return val;
    }
}

/// <summary>Pay Slip / PaySheet report row (RDLC ReportPay).</summary>
public sealed class ReportPayRow
{
    public string ReportName { get; set; } = "";
    public string RespCenter { get; set; } = "";
    public string CompanyName { get; set; } = "";
    public string CompanyAddLine1 { get; set; } = "";
    public string CompanyAddLine2 { get; set; } = "";
    public string FilterText { get; set; } = "";
    public string DepartmentTxt { get; set; } = "";
    public string SectionTxt { get; set; } = "";
    public string No_Employee { get; set; } = "";
    public string Initials_Employee { get; set; } = "";
    public string SocAccountNo_Employee { get; set; } = "";
    public string PFNo_Employee { get; set; } = "";
    public string UANNo_Employee { get; set; } = "";
    public string ESICNo_Employee { get; set; } = "";
    public string PANNo_Employee { get; set; } = "";
    public string JobTitle_Employee { get; set; } = "";
    public string AdhaarNo_Employee { get; set; } = "";
    public decimal PLDays_EmpSalaryRegister { get; set; }
    public decimal SLDays_EmpSalaryRegister { get; set; }
    public decimal CLDays_EmpSalaryRegister { get; set; }
    public decimal PresentDays_EmpSalaryRegister { get; set; }
    public decimal WorkDays_EmpSalaryRegister { get; set; }
    public decimal WeaklyOff_EmpSalaryRegister { get; set; }
    public decimal PaidHolidays_EmpSalaryRegister { get; set; }
    public decimal AbsentDays_EmpSalaryRegister { get; set; }
    public decimal CLBalance { get; set; }
    public decimal PLBalance { get; set; }
    public decimal SLBalance { get; set; }
    public decimal NetPay_EmpSalaryRegister { get; set; }
    public decimal TotalEarnings_EmpSalaryRegister { get; set; }
    public decimal TotalDeductions_EmpSalaryRegister { get; set; }
    public string NetPayTxt { get; set; } = "";
    public string PaymentText { get; set; } = "";
    public string EarnText1 { get; set; } = ""; public string EarnText2 { get; set; } = ""; public string EarnText3 { get; set; } = ""; public string EarnText4 { get; set; } = ""; public string EarnText5 { get; set; } = ""; public string EarnText6 { get; set; } = ""; public string EarnText7 { get; set; } = ""; public string EarnText8 { get; set; } = ""; public string EarnText9 { get; set; } = ""; public string EarnText10 { get; set; } = ""; public string EarnText11 { get; set; } = "";
    public decimal EarnAmount1 { get; set; } public decimal EarnAmount2 { get; set; } public decimal EarnAmount3 { get; set; } public decimal EarnAmount4 { get; set; } public decimal EarnAmount5 { get; set; } public decimal EarnAmount6 { get; set; } public decimal EarnAmount7 { get; set; } public decimal EarnAmount8 { get; set; } public decimal EarnAmount9 { get; set; } public decimal EarnAmount10 { get; set; } public decimal EarnAmount11 { get; set; }
    public string DeductText1 { get; set; } = ""; public string DeductText2 { get; set; } = ""; public string DeductText3 { get; set; } = ""; public string DeductText4 { get; set; } = ""; public string DeductText5 { get; set; } = ""; public string DeductText6 { get; set; } = ""; public string DeductText7 { get; set; } = ""; public string DeductText8 { get; set; } = ""; public string DeductText9 { get; set; } = ""; public string DeductText10 { get; set; } = ""; public string DeductText11 { get; set; } = "";
    public decimal DeductAmount1 { get; set; } public decimal DeductAmount2 { get; set; } public decimal DeductAmount3 { get; set; } public decimal DeductAmount4 { get; set; } public decimal DeductAmount5 { get; set; } public decimal DeductAmount6 { get; set; } public decimal DeductAmount7 { get; set; } public decimal DeductAmount8 { get; set; } public decimal DeductAmount9 { get; set; } public decimal DeductAmount10 { get; set; } public decimal DeductAmount11 { get; set; }
}

/// <summary>Department Summary row (DeptPaySummary.rdlc).</summary>
public sealed class PaySummaryDeptRow
{
    public string ReportName { get; set; } = "";
    public string PeriodText { get; set; } = "";
    public string CompanyName { get; set; } = "";
    public string LocationName { get; set; } = "";
    public string Type { get; set; } = "";
    public string Particular { get; set; } = "";
    public string UserName { get; set; } = "";
    public string ClmName1 { get; set; } = ""; public string ClmName2 { get; set; } = ""; public string ClmName3 { get; set; } = ""; public string ClmName4 { get; set; } = ""; public string ClmName5 { get; set; } = ""; public string ClmName6 { get; set; } = ""; public string ClmName7 { get; set; } = ""; public string ClmName8 { get; set; } = ""; public string ClmName9 { get; set; } = ""; public string ClmName10 { get; set; } = "";
    public decimal ClmValue1 { get; set; } public decimal ClmValue2 { get; set; } public decimal ClmValue3 { get; set; } public decimal ClmValue4 { get; set; } public decimal ClmValue5 { get; set; } public decimal ClmValue6 { get; set; } public decimal ClmValue7 { get; set; } public decimal ClmValue8 { get; set; } public decimal ClmValue9 { get; set; } public decimal ClmValue10 { get; set; }
    public string ClmColor1 { get; set; } = ""; public string ClmColor2 { get; set; } = ""; public string ClmColor3 { get; set; } = ""; public string ClmColor4 { get; set; } = ""; public string ClmColor5 { get; set; } = ""; public string ClmColor6 { get; set; } = ""; public string ClmColor7 { get; set; } = ""; public string ClmColor8 { get; set; } = ""; public string ClmColor9 { get; set; } = ""; public string ClmColor10 { get; set; } = "";
}

/// <summary>Bank Payment Details row (BankPaySheet.rdlc).</summary>
public sealed class BankPayDetailsRow
{
    public string CompanyName { get; set; } = "";
    public string LocationName { get; set; } = "";
    public string PeriodText { get; set; } = "";
    public string ReportName { get; set; } = "";
    public string UserName { get; set; } = "";
    public string BankName { get; set; } = "";
    public string Description { get; set; } = "";
    public string EmpBankAccNo { get; set; } = "";
    public string EmployeeName { get; set; } = "";
    public string EmployeeNo { get; set; } = "";
    public decimal Salary { get; set; }
    public bool Hold { get; set; }
}

/// <summary>Pay Mode Summary row.</summary>
public sealed class PaymentModeSummaryRow
{
    public string CompanyName { get; set; } = "";
    public string Location { get; set; } = "";
    public string ReportName { get; set; } = "";
    public decimal Amount { get; set; }
    public decimal HoldAmount { get; set; }
    public string Name { get; set; } = "";
}

/// <summary>ESIC Summary (ESICSummary.rdlc) — legacy <c>Models.Report.PayESICSummary</c> / <c>Db.Payroll.ESICSummary</c>.</summary>
public sealed class PayESICSummaryRow
{
    public string CompanyName { get; set; } = "";
    public string LocationName { get; set; } = "";
    public string ReportName { get; set; } = "";
    public string MonthName { get; set; } = "";
    public string Particular { get; set; } = "";
    public decimal Amount { get; set; }
}

/// <summary>Leave Register row (LeaveReg.rdlc).</summary>
public sealed class LeaveRegRow
{
    public string EmpNo { get; set; } = "";
    public string EmpName { get; set; } = "";
    public string Department { get; set; } = "";
    public string Section { get; set; } = "";
    public string JobTitle { get; set; } = "";
    public string RespCenter { get; set; } = "";
    public string JoiningDate { get; set; } = "";
    public string LeaveDate { get; set; } = "";
    public string ConfirmDate { get; set; } = "";
    public decimal PLOBal { get; set; } public decimal PLEarn { get; set; } public decimal PLDeduct { get; set; } public decimal PL { get; set; }
    public decimal SLOBal { get; set; } public decimal SLEarn { get; set; } public decimal SLDeduct { get; set; } public decimal SL { get; set; }
    public decimal CLOBal { get; set; } public decimal CLEarn { get; set; } public decimal CLDeduct { get; set; } public decimal CL { get; set; }
}

/// <summary>Casual Leave Encashment (CLEncash.rdlc) — aligns with legacy Navision EncashCL.</summary>
public sealed class CLEncashReportRow
{
    public string CompanyName { get; set; } = "";
    public string ReportName { get; set; } = "Casual Leave Encashment";
    public string EmpNo { get; set; } = "";
    public string EmpName { get; set; } = "";
    public string Department { get; set; } = "";
    public string Section { get; set; } = "";
    public string BankAcNo { get; set; } = "";
    public decimal BasicPay { get; set; }
    /// <summary>CL balance days (legacy field name <c>CL</c>).</summary>
    public decimal CL { get; set; }
    public decimal GrossPay { get; set; }
    public decimal LeaveEncash { get; set; }
    public decimal TotalPay { get; set; }
    public decimal PTPayable { get; set; }
    public decimal PTPaid { get; set; }
    public decimal PTDiff { get; set; }
    public decimal FinalEncash { get; set; }
    public bool Executive { get; set; }
}

/// <summary>Mediclaim Encashment (Mediclaim.rdlc).</summary>
public sealed class MediclaimReportRow
{
    public string CompanyName { get; set; } = "";
    public string ReportName { get; set; } = "Mediclaim Encashment";
    public string LocationName { get; set; } = "";
    public string EmpNo { get; set; } = "";
    public string EmpName { get; set; } = "";
    public decimal Basic { get; set; }
    public decimal DA { get; set; }
    public decimal Total { get; set; }
    public string ExitDate { get; set; } = "";
}

/// <summary>Declared Holidays row.</summary>
public sealed class DeclaredHolidaysRow
{
    public string Location { get; set; } = "";
    public string Occasion { get; set; } = "";
    public string Date { get; set; } = "";
    public string ReportName { get; set; } = "";
    public string CompanyName { get; set; } = "";
    public string UserName { get; set; } = "";
    public string PeriodTxt { get; set; } = "";
}

/// <summary>PF Details (PFDetails.rdlc) — aligns with legacy PFDetails calculation.</summary>
public sealed class PFDetailsReportRow
{
    public string CompanyName { get; set; } = "";
    public string LocationName { get; set; } = "";
    public string ReportName { get; set; } = "PF Details";
    public string MonthName { get; set; } = "";
    public string EmpId { get; set; } = "";
    public string Name { get; set; } = "";
    public decimal GrossWage { get; set; }
    public decimal EPFWage { get; set; }
    public decimal EPSWage { get; set; }
    public decimal EDLIWage { get; set; }
    public decimal EPFAmount { get; set; }
    public decimal EPSAmount { get; set; }
    public decimal DiffAmount { get; set; }
    public decimal NCPDays { get; set; }
    public decimal Refund { get; set; }
    public DateTime Date { get; set; }
    public string PFEstCode { get; set; } = "";
    
}

/// <summary>PF Summary (PFSummary.rdlc) — legacy <c>Models.Report.PFSummary</c> / <c>Db.Payroll.PFSummary</c>.</summary>
public sealed class PFSummaryReportRow
{
    public string CompanyName { get; set; } = "";
    public string LocationName { get; set; } = "";
    public string ReportName { get; set; } = "";
    public string MonthName { get; set; } = "";
    public string EstCode { get; set; } = "";
    public string Particular { get; set; } = "";
    public decimal Amount1 { get; set; }
    public decimal Amount2 { get; set; }
    public decimal Total { get; set; }
    public string Text { get; set; } = "";
    public string Group { get; set; } = "";
}

/// <summary>TDS / PT report row (TDSMonthly, TDSQuarterly, etc.).</summary>
public sealed class PayTDSRow
{
    public string CompanyName { get; set; } = "";
    public string MonthName { get; set; } = "";
    public string LocationName { get; set; } = "";
    public string ReportName { get; set; } = "";
    public string MonthTxt1 { get; set; } = ""; public string MonthTxt2 { get; set; } = ""; public string MonthTxt3 { get; set; } = ""; public string MonthTxt4 { get; set; } = ""; public string MonthTxt5 { get; set; } = ""; public string MonthTxt6 { get; set; } = ""; public string MonthTxt7 { get; set; } = ""; public string MonthTxt8 { get; set; } = ""; public string MonthTxt9 { get; set; } = ""; public string MonthTxt10 { get; set; } = ""; public string MonthTxt11 { get; set; } = ""; public string MonthTxt12 { get; set; } = "";
    public decimal Month1 { get; set; } public decimal Month2 { get; set; } public decimal Month3 { get; set; } public decimal Month4 { get; set; } public decimal Month5 { get; set; } public decimal Month6 { get; set; } public decimal Month7 { get; set; } public decimal Month8 { get; set; } public decimal Month9 { get; set; } public decimal Month10 { get; set; } public decimal Month11 { get; set; } public decimal Month12 { get; set; }
    public string EmpId { get; set; } = "";
    public string Name { get; set; } = "";
    public decimal Gross { get; set; }
}
