using System;
using Tyresoles.Sql.Abstractions;

namespace Tyresoles.Data.Features.Procurement;

/// <summary>
/// GraphQL filtering/sorting translate predicates to SQL via <see cref="EntityMetadataResolvers.GetColumnName"/>.
/// <see cref="NavColumnAttribute"/> maps CLR names to NAV column names.
/// <see cref="JoinSqlAliasAttribute"/> prefixes <c>t0.</c>/<c>t1.</c> in JOINs so columns like <c>No_</c> are not ambiguous
/// between Purchase Line and Vendor.
/// </summary>
public class ProcurementNewNumberingDto
{
    // Mapped Fields (Purchase Line — t0)
    [NavColumn("Document No_")] [JoinSqlAlias("t0")] public string OrderNo { get; set; } = string.Empty;
    [NavColumn("Line No_")] [JoinSqlAlias("t0")] public int LineNo { get; set; }
    [NavColumn("No_")] [JoinSqlAlias("t0")] public string No { get; set; } = string.Empty;
    [NavColumn("Make")] [JoinSqlAlias("t0")] public string Make { get; set; } = string.Empty;
    [NavColumn("Serial No_")] [JoinSqlAlias("t0")] public string SerialNo { get; set; } = string.Empty;
    [NavColumn("Dispatch Order No_")] [JoinSqlAlias("t0")] public string DispatchOrderNo { get; set; } = string.Empty;
    [NavColumn("Dispatch Date")] [JoinSqlAlias("t0")] public DateTime? DispatchDate { get; set; }
    [NavColumn("Dispatch Destination")] [JoinSqlAlias("t0")] public string DispatchDestination { get; set; } = string.Empty;
    [NavColumn("Dispatch Vehicle No_")] [JoinSqlAlias("t0")] public string DispatchVehicleNo { get; set; } = string.Empty;
    [NavColumn("Dispatch Mobile No")] [JoinSqlAlias("t0")] public string DispatchMobileNo { get; set; } = string.Empty;
    [NavColumn("Dispatch Transporter")] [JoinSqlAlias("t0")] public string DispatchTransporter { get; set; } = string.Empty;
    [NavColumn("Button")] [JoinSqlAlias("t0")] public string Button { get; set; } = string.Empty;
    [NavColumn("Model")] [JoinSqlAlias("t0")] public string Model { get; set; } = string.Empty;
    [NavColumn("New Serial No_")] [JoinSqlAlias("t0")] public string NewSerialNo { get; set; } = string.Empty;
    [NavColumn("Inspection")] [JoinSqlAlias("t0")] public string FactInspection { get; set; } = string.Empty;
    [NavColumn("Rejection Reason")] [JoinSqlAlias("t0")] public string RejectionReason { get; set; } = string.Empty;
    [NavColumn("Order Date")] [JoinSqlAlias("t0")] public DateTime? Date { get; set; }

    // Join Support Fields (Internal)
    [SqlNotMapped] public string BuyFromVendorNo { get; set; } = string.Empty;
    [SqlNotMapped] public string InspectorCode { get; set; } = string.Empty;
    [SqlNotMapped] public string FactInspectorCode { get; set; } = string.Empty;
    [SqlNotMapped] public string FactInspectorFinalCode { get; set; } = string.Empty;

    // Joined Fields (Vendor — t1)
    [NavColumn("Name")] [JoinSqlAlias("t1")] public string Supplier { get; set; } = string.Empty;
    [NavColumn("Group Details")] [JoinSqlAlias("t1")] public string Location { get; set; } = string.Empty;

    // SelectRaw initials — not physical columns; ORDER BY can still use SELECT aliases
    public string Inspector { get; set; } = string.Empty;
    public string FactInspector { get; set; } = string.Empty;
    public string FactInspectorFinal { get; set; } = string.Empty;

    // Calculated Fields (SelectRaw) — do not use in GraphQL WHERE contains; ORDER BY alias is OK
    public string SortNo { get; set; } = string.Empty;
    public string Inspection { get; set; } = string.Empty;
    public string OrderStatus { get; set; } = string.Empty;
    public string Remark { get; set; } = string.Empty;
}
