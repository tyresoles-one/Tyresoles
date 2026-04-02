namespace Tyresoles.Data.Features.Calendar.Dto;

public class FreeBusyDto
{
    public string UserId { get; set; } = string.Empty;
    public List<FreeBusySlotDto> Busy { get; set; } = new();
}

public class FreeBusySlotDto
{
    public DateTime StartUtc { get; set; }
    public DateTime EndUtc { get; set; }
    public Guid? EventId { get; set; }
    public string? Title { get; set; }
}
