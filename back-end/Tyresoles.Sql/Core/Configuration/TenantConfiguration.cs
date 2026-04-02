using Tyresoles.Sql.Abstractions;

namespace Tyresoles.Sql.Core.Configuration;

public class DataverseOptions
{
    public string DefaultTenantKey { get; set; } = "NavLive";
    public Dictionary<string, TenantConfiguration> Tenants { get; set; } = new();
}

public class TenantConfiguration
{
    public string Name { get; set; } = string.Empty;
    public string ConnectionString { get; set; } = string.Empty;
    public string SchemaPrefix { get; set; } = string.Empty;
    public string DefaultCompany { get; set; } = string.Empty;
    public DbProvider Provider { get; set; } = DbProvider.SqlServer;
    public Dictionary<string, Dictionary<string, string>> ColumnMappings { get; set; } = new();

    /// <summary>Command timeout in seconds. Default 30. Use 0 for no timeout.</summary>
    public int CommandTimeout { get; set; } = 30;

    /// <summary>Enable retry for transient failures. Default false.</summary>
    public bool EnableRetry { get; set; }
    /// <summary>Number of retry attempts when EnableRetry is true. Default 3.</summary>
    public int RetryCount { get; set; } = 3;
    /// <summary>Delay in milliseconds between retries. Default 500.</summary>
    public int RetryDelayMilliseconds { get; set; } = 500;
}
