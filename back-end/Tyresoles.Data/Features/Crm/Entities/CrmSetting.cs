using System;

namespace Tyresoles.Data.Features.Crm.Entities;

public class CrmSetting
{
    public string Key { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public string? Description { get; set; }
}
