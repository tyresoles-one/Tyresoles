using System;

namespace Tyresoles.Data.Features.Crm.Models;

public class CrmWhatsappImageInput
{
    public Guid? Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }
    public string? Base64Data { get; set; }
    public string? Products { get; set; }
}
