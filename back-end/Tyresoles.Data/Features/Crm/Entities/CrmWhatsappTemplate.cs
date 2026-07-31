using System;

namespace Tyresoles.Data.Features.Crm.Entities;

public class CrmWhatsappTemplate
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Language { get; set; } = string.Empty; // e.g. "English", "Hindi", "Marathi"
    public string MessageText { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}
