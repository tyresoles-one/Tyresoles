using System.Data.Common;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Tyresoles.Sql.Abstractions;
using Tyresoles.Sql.Core.Configuration;

namespace Tyresoles.Sql.Core;

public interface IDbConnectionFactory
{
    DbConnection CreateConnection(string connectionString);
}

/// <summary>Category used for query logging. Set LogLevel to Debug (or Information) to log SQL and parameters.</summary>
public static class SqlLogCategories
{
    public const string Queries = "Tyresoles.Sql";
}

public class Dataverse : IDataverse
{
    private readonly IDbConnectionFactory _connectionFactory;
    private readonly DataverseOptions _options;
    private readonly IEnumerable<IDbCommandInterceptor> _interceptors;
    private readonly ILogger? _logger;

    public Dataverse(IDbConnectionFactory connectionFactory, IOptions<DataverseOptions> options, ILoggerFactory? loggerFactory = null, IEnumerable<IDbCommandInterceptor>? interceptors = null)
    {
        _connectionFactory = connectionFactory;
        _options = options.Value;
        _logger = loggerFactory?.CreateLogger(SqlLogCategories.Queries);
        _interceptors = interceptors ?? Array.Empty<IDbCommandInterceptor>();
    }

    public ITenantScope ForTenant(string tenantKey)
    {
        if (!_options.Tenants.TryGetValue(tenantKey, out var config))
            throw new ArgumentException($"Tenant '{tenantKey}' not found in configuration.");
            
        config.Name = tenantKey;
        return new TenantScope(config, _connectionFactory, _logger, _interceptors);
    }

    public ITenantScope DefaultTenant => ForTenant(_options.DefaultTenantKey); 
}
