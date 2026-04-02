namespace Tyresoles.Data.Features.Sales.Reports.Models;

/// <summary>
/// One row for Posted Sales Invoice RDLC (DataSet_Result). Map from Sales Invoice Line + Header.
/// Add fields as needed to match your PostedSalesInvoice.rdlc.
/// </summary>
public sealed class PostedSalesInvoiceRow
{
    public string? InvoiceNo { get; set; }
    public string? InvoiceDate { get; set; }
    public string? CustomerNo { get; set; }
    public string? SellToCustomerName { get; set; }
    public int LineNo { get; set; }
    public string? Description { get; set; }
    public string? No { get; set; }
    public decimal Quantity { get; set; }
    public string? UnitOfMeasure { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal Amount { get; set; }
    public decimal LineDiscountAmount { get; set; }
    public string? ExternalDocumentNo { get; set; }
    public string? ResponsibilityCenter { get; set; }
    public string? UserID { get; set; }
    public string? Make { get; set; }
    public string? SerialNo { get; set; }
    public string? Pattern { get; set; }
    public decimal TotalGSTAmount { get; set; }
    public DateTime? PostingDate { get; set; }

    // Legacy fields for cloning
    public string? Company { get; set; }
    public byte[]? Logo { get; set; }
    public string? Company_Address { get; set; }
    public string? Company_Address2 { get; set; }
    public string? RegOffAddress { get; set; }
    public string? PANText { get; set; }
    public string? GSTText { get; set; }
    public string? CINText { get; set; }
    public string? JurisdictionText { get; set; }
    public string? SGSTText { get; set; }
    public string? CGSTText { get; set; }
    public string? IGSTText { get; set; }
    public string? BillToAddress1 { get; set; }
    public string? BillToAddress2 { get; set; }
    public string? BillToAddress3 { get; set; }
    public string? BillToAddress4 { get; set; }
    public string? BillToAddress5 { get; set; }
    public string? BillToAddress6 { get; set; }
    public string? BillToAddress7 { get; set; }
    public string? ShipToAddress1 { get; set; }
    public string? ShipToAddress2 { get; set; }
    public string? ShipToAddress3 { get; set; }
    public string? ShipToAddress4 { get; set; }
    public string? ShipToAddress5 { get; set; }
    public string? ShipToAddress6 { get; set; }
    public string? ShipToAddress7 { get; set; }
    public string? InvoiceText { get; set; }
    public string? ExternalDocNo { get; set; }
    public string? ExternalDocNoCaption { get; set; }
    public string? DocumentCaption { get; set; }
    public string? UserName { get; set; }
    public string? EInvAckNo { get; set; }
    public string? EInvAckDate { get; set; }
    public string? EInvIRNNo { get; set; }
    public byte[]? EinvQRCode { get; set; }
    public string? BankName { get; set; }
    public string? BankAccNo { get; set; }
    public string? BankIFCS { get; set; }
    public string? BankAccNo2 { get; set; }
    public string? BankIFSC2 { get; set; }
    public string? BankBranch { get; set; }
    public string? BankText01 { get; set; }
    public string? BankText02 { get; set; }
    public string? BankText03 { get; set; }
    public string? BankText04 { get; set; }
    public string? BankText05 { get; set; }
    public decimal GstAmount { get; set; }
    public decimal BaseAmount { get; set; }
    public decimal GrandTotal { get; set; }
    public string? GrandTotalText { get; set; }
    public decimal Freight { get; set; }
    public decimal RoundUp { get; set; }
    public decimal TDS { get; set; }
    public bool ShowDetail { get; set; }
    public string? ItemDiscription { get; set; }
    public string? Remark { get; set; }
    public string? UOM { get; set; }
    public string? HSNSAC { get; set; }
    public decimal TotalQty { get; set; }
    public string? QuantityFormatTxt { get; set; }
    public decimal Rate { get; set; }
    public decimal FinalAmount { get; set; }
    public decimal TotalFinalAmount { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal SGSTPercent { get; set; }
    public decimal SGSTAmount { get; set; }
    public decimal CGSTPercent { get; set; }
    public decimal CGSTAmount { get; set; }
    public decimal IGSTPercent { get; set; }
    public decimal IGSTAmount { get; set; }
    public string? GSTPercentFormat { get; set; }
    public string? SGSTTxt { get; set; }
    public string? CGSTTxt { get; set; }
    public string? IGSTTxt { get; set; }
    public decimal GSTPercentage { get; set; }
    public string? Description_PostedAssemblyLine { get; set; }
    public string? UnitofMeasureCode_PostedAssemblyLine { get; set; }
    public decimal Quantity_PostedAssemblyLine { get; set; }
    public decimal Line_GSTBaseAmount { get; set; }
    public decimal Line_SGSTAmount { get; set; }
    public decimal Line_CGSTAmount { get; set; }
    public decimal Line_IGSTAmount { get; set; }
    public decimal Line_FinalAmount { get; set; }

    // Extra Fields for query mapping
    public string? RespCenter { get; set; }
    public string? ExtDocNo { get; set; }
    public string? PONo { get; set; }
    public decimal LineAmount { get; set; }
    public string? ItemVariant_Pattern { get; set; }
    public string? Item_ProdGroup { get; set; }
    public string? Item_ItemCategory { get; set; }
    public string? Item_AlternateNo { get; set; }
    public string? Item_Description { get; set; }
    public string? Item_No2 { get; set; }
    public string? Line_No_ { get; set; }
    public string? Line_Description { get; set; }
    public decimal Line_TotalGstAmount { get; set; }
    public decimal Line_BaseGstAmount { get; set; }
    public decimal Line_Frieght { get; set; }
    public decimal Line_TDS { get; set; }
    public decimal Line_LineDiscAmount { get; set; }
    public decimal Line_GstPercent { get; set; }
    public string? Line_GenProdPostGroup { get; set; }
    public int Line_GstJurisdictionType { get; set; }
    public DateTime Date { get; set; }
    public string? CustomerGenBusPostingGroup { get; set; }
}
