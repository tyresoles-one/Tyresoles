namespace Tyresoles.Data.Features.WindowsServices;

public sealed class WindowsServiceOptions
{
    public const string SectionName = "WindowsServices";

    public bool Enabled { get; set; } = true;

    public List<WindowsServiceEntryOptions> Services { get; set; } = new();
}

public sealed class WindowsServiceEntryOptions
{
    public string Name { get; set; } = "";

    public bool CanStart { get; set; }

    public bool CanStop { get; set; }
}
