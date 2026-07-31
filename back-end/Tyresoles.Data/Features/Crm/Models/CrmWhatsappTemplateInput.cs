using System;

namespace Tyresoles.Data.Features.Crm.Models;

public class CrmWhatsappTemplateInput
{
    public Guid? Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Language { get; set; } = string.Empty;
    public string MessageText { get; set; } = string.Empty;
}
