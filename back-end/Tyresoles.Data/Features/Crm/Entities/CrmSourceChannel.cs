using System;

namespace Tyresoles.Data.Features.Crm.Entities;

public class CrmSourceChannel
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Code { get; set; }
    public int? ParentId { get; set; }
    public bool IsActive { get; set; } = true;
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
