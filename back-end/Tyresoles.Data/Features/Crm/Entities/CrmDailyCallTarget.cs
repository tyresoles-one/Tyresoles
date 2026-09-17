using System;

namespace Tyresoles.Data.Features.Crm.Entities;

public class CrmDailyCallTarget
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string AgentUsername { get; set; } = string.Empty;
    public int DailyTarget { get; set; } = 30;
    public int WeeklyTarget { get; set; } = 150;
    public int MonthlyTarget { get; set; } = 600;
    public bool IsActive { get; set; } = true;
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public string? CreatedBy { get; set; }
}
