using System.Collections.Generic;

namespace Tyresoles.SqlServer.Cli.Models;

public class MaintenanceTemplate
{
    public string Name { get; set; } = "Maintenance Plan";
    public bool StopOnError { get; set; } = true;
    public List<MaintenanceTaskStep> Tasks { get; set; } = new();
}

public class MaintenanceTaskStep
{
    public string Type { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public Dictionary<string, object> Parameters { get; set; } = new();
}
