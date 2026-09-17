using System;

namespace Tyresoles.Data.Features.Crm.Entities;

public class CrmLanguage
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
}
