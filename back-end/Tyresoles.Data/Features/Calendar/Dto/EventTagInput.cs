using Tyresoles.Data.Features.Calendar.Entities;

namespace Tyresoles.Data.Features.Calendar.Dto;

public class EventTagInput
{
    public EventTagType TagType { get; set; }
    public string TagKey { get; set; } = string.Empty;
}
