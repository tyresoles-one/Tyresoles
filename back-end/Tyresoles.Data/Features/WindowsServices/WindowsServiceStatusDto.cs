namespace Tyresoles.Data.Features.WindowsServices;

public sealed class WindowsServiceStatusDto
{
    public string Name { get; set; } = "";

    public string DisplayName { get; set; } = "";

    public string State { get; set; } = "Unknown";

    public bool IsRunning { get; set; }

    public bool CanStart { get; set; }

    public bool CanStop { get; set; }
}
