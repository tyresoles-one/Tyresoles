using Microsoft.Identity.Client;
using System;
using System.Collections.Generic;
using System.Text.Json;

namespace Tyresoles.Data.Features.Production.Models;

/// <summary>Row from NAV table <c>Procurement Configs</c> (<see cref="Dataverse.NavLive.ProcurementConfigs"/>).</summary>
public class ProcurementConfigDto
{
    public int Type { get; set; }
    public string ItemNo { get; set; } = "";
    public string Market { get; set; } = "";
    public int Qty { get; set; }
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
}

public class FetchParams
{
    public List<string> RespCenters { get; set; } = new();
    public List<string> Regions { get; set; } = new();
    public List<string> Areas { get; set; } = new();
    public List<string> Nos { get; set; } = new();
    public string Type { get; set; } = "";
    public string View { get; set; } = "";
    public string UserCode { get; set; } = "";
    public string UserDepartment { get; set; } = "";
    public string UserType { get; set; } = "";
    public string UserSpecialToken { get; set; } = "";
    public string From { get; set; } = "";
    public string To { get; set; } = "";
    public string ReportName { get; set; } = "";
}

public class CasingItem
{
    public string Code { get; set; } = "";
    public string MinRate { get; set; } = "";
    public string MaxRate { get; set; } = "";
    public string Category { get; set; } = "";
    public string Name { get; set; } = "";
}

public class CodeName
{
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
}

public class VendorModel
{
    public string No { get; set; } = "";
    public string Name { get; set; } = "";
    public string Address { get; set; } = "";
    public string Address2 { get; set; } = "";
    public string City { get; set; } = "";
    public string Category { get; set; } = "";
    public string Detail { get; set; } = "";
    public string RespCenter { get; set; } = "";
    public string MobileNo { get; set; } = "";
    public string EcoMgrCode { get; set; } = "";
    public string NameOnInvoice { get; set; } = "";
    public string BankName { get; set; } = "";
    public string BankIFSC { get; set; } = "";
    public string BankAccNo { get; set; } = "";
    public string PostCode { get; set; } = "";
    public string BankBranch { get; set; } = "";
    public bool SelfInvoice { get; set; }
    public string PanNo { get; set; } = "";
    public string AdhaarNo { get; set; } = "";
    public string GSTRegistrationNo { get; set; } = "";
    public decimal Balance { get; set; }
    public string StateCode { get; set; } = "";
}

public class OrderInfo
{
    public string OrderNo { get; set; } = "";
    public string Supplier { get; set; } = "";
    public string SupplierCode { get; set; } = "";
    public string RespCenter { get; set; } = "";
    public int Status { get; set; }
    public string Location { get; set; } = "";
    public string ManagerCode { get; set; } = "";
    public string Date { get; set; } = "";
    public string Manager { get; set; } = "";
    public int Qty { get; set; }
    public decimal Amount { get; set; }
}

public class OrderLine
{
    public string No { get; set; } = "";
    public int LineNo { get; set; }
    public string VendorNo { get; set; } = "";
    public string ItemNo { get; set; } = "";
    public string Make { get; set; } = "";
    public string SerialNo { get; set; } = "";
    public decimal Amount { get; set; }
    public string SubMake { get; set; } = "";
    public string InspectorCode { get; set; } = "";
    public string SortNo { get; set; } = "";
    public string Inspection { get; set; } = "";
    public string Inspector { get; set; } = "";
}

public class OrderLineDispatch
{
    public string OrderNo { get; set; } = "";
    public int LineNo { get; set; }
    public string No { get; set; } = "";
    public string Make { get; set; } = "";
    public string SerialNo { get; set; } = "";
    public string DispatchOrderNo { get; set; } = "";
    public string DispatchDate { get; set; } = "";
    public string DispatchDestination { get; set; } = "";
    public string DispatchVehicleNo { get; set; } = "";
    public string DispatchMobileNo { get; set; } = "";
    public string DispatchTransporter { get; set; } = "";
    public string Button { get; set; } = "";
    public string Model { get; set; } = "";
    public string FactInspection { get; set; } = "";
    public string NewSerialNo { get; set; } = "";
    public string RejectionReason { get; set; } = "";
    public string Supplier { get; set; } = "";
    public string Location { get; set; } = "";
    public string Date { get; set; } = "";
    public string SortNo { get; set; } = "";
    public string Inspection { get; set; } = "";
    public string OrderStatus { get; set; } = "";
    public string Inspector { get; set; } = "";
    public string FactInspector { get; set; } = "";
    public string FactInspectorFinal { get; set; } = "";
    public string Remark { get; set; } = "";
}

public class DispatchOrder
{
    public string No { get; set; } = "";
    public string MobileNo { get; set; } = "";
    public string VehicleNo { get; set; } = "";
    public string Destination { get; set; } = "";
    public string Date { get; set; } = "";
    public int Tyres { get; set; }
    public string Status { get; set; } = "";
}

public class Tile
{
    public string Description { get; set; } = "";
    public string Label { get; set; } = "";
    public decimal Value { get; set; }
}

public class ShipmentInfo
{
    public string No { get; set; } = "";
    public string Destination { get; set; } = "";
    public string MobileNo { get; set; } = "";
    public string Transport { get; set; } = "";
    public string VehicleNo { get; set; } = "";
    public string Date { get; set; } = "";
}

public class ClaimRatio
{
    public string CompanyName { get; set; } = "";
    public string ReportName { get; set; } = "";
    public string Locations { get; set; } = "";
    public string Period { get; set; } = "";
    public string View { get; set; } = "";
    public string Particular { get; set; } = "";
    public string Group { get; set; } = "";
    public string ParticularLbl { get; set; } = "";
    public bool bValue { get; set; } = false;
    public int Sold { get; set; } = 0;
    public int Claims { get; set; } = 0;
    public int Pass { get; set; } = 0;
    public int Reject { get; set; } = 0;
    public int Unsettled { get; set; } = 0;
    public int SpecialCase { get; set; } = 0;
    public decimal ClaimPercent { get; set; } = 0;
    public decimal PassPercent { get; set; } = 0;
    public decimal SaleValue { get; set;  } = 0;
    public decimal CreditNoteValue { get; set; } = 0;
    public decimal CreditNotePercent {  get; set; } = 0;
}
public class ClaimRatioDashboard
{
    public string View { get; set; } = "";
    public string RespCenter { get; set; } = "";
    public string Level01 { get; set; } = "";
    public string Level02 { get; set; } = "";
    public string Level03 { get; set; } = "";
    public string Level04 { get; set; } = ""; 
    public int Purchase {  get; set; } = 0;
    public int Sold { get; set; } = 0;
    public int Claims { get; set; } = 0;
    public int Pass { get; set; } = 0;
    public int Reject { get; set; } = 0;
    public int Unsettled { get; set; } = 0;
    public int SpecialCase { get; set; } = 0;
    public decimal ClaimPercent { get; set; } = 0;
    public decimal PassPercent { get; set; } = 0;
    public decimal SaleValue { get; set; } = 0;
    public decimal CreditNoteValue { get; set; } = 0;
    public decimal SaleClaimRatio { get; set; } = 0;

    public override string ToString()
    {
        return JsonSerializer.Serialize(this);
    }
}

public class ClaimRatioDashboardValue
{
    public string RespCenter { get; set; }
    public decimal Value {  get; set; } = 0;
}

public class ProcurementDashboard
{
    public string Size { get; set; } = string.Empty;
    public string Market { get; set; } = string.Empty;    
    public int Target { get; set; } = 0;
    public int Purchased { get; set; } = 0;
    public int InTransit { get; set; } = 0;
    public int InTransitSql { get; set; } = 0;
    public int PurchasedLastMonth { get; set; } = 0;
    public decimal AvgCost { get; set;  } = 0;
    public decimal AvgCostLastMonth { get; set; } = 0;
    public decimal Freight { get; set; } = 0;
}