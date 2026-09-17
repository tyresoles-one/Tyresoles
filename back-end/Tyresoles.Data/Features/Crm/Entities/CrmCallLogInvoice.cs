using System;

namespace Tyresoles.Data.Features.Crm.Entities;

public class CrmCallLogInvoice
{
    public Guid Id { get; set; }
    public Guid CallLogId { get; set; }
    public CrmCallLog? CallLog { get; set; }
    public string InvoiceNo { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public decimal TyreQuantity { get; set; }
    public DateTime CreatedAt { get; set; }
}
