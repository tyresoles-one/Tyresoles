namespace Tyresoles.SqlServer.Cli.Configuration;

public class AppSettings
{
    public ConnectionStringsSettings ConnectionStrings { get; set; } = new();
    public BackupSettings BackupSettings { get; set; } = new();
    public MaintenanceSettings MaintenanceSettings { get; set; } = new();
    public SyncSettings SyncSettings { get; set; } = new();
    public SmtpSettings SmtpSettings { get; set; } = new();
}

public class ConnectionStringsSettings
{
    public string DefaultConnection { get; set; } = string.Empty;
}

public class BackupSettings
{
    public string BackupDirectory { get; set; } = string.Empty;
    public List<string> Databases { get; set; } = new();
}

public class MaintenanceSettings
{
    public string ScriptsDirectory { get; set; } = string.Empty;
}

public class SyncSettings
{
    public string RcloneExecutablePath { get; set; } = "rclone";
    public string DestinationPath { get; set; } = string.Empty;
    public string RcloneAdditionalArgs { get; set; } = string.Empty;
}

public class SmtpSettings
{
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 587;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string FromEmail { get; set; } = string.Empty;
    public string ToEmail { get; set; } = string.Empty;
}
