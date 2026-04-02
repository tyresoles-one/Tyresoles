using System.Data.Common;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Tyresoles.Sql.Abstractions;
using Tyresoles.Sql.Core;

namespace Tyresoles.Sql.SqlServer;

public class SqlServerConnectionFactory : IDbConnectionFactory
{
    public DbConnection CreateConnection(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new ArgumentException("Connection string must not be null or empty.", nameof(connectionString));
        return new SqlConnection(connectionString);
    }
}

public static class Extensions
{
    /// <summary>
    /// Registers Tyresoles.Sql services with SQL Server. Call Configure&lt;DataverseOptions&gt; or bind from config first.
    /// If the host registers an ILoggerFactory (e.g. AddTyresolesLogger), query logging will be available at Debug level.
    /// </summary>
    public static IServiceCollection AddTyresolesSql(this IServiceCollection services)
    {
        services.AddSingleton<IDbConnectionFactory, SqlServerConnectionFactory>();
        services.AddSingleton<IDataverse>(sp => new Dataverse(
            sp.GetRequiredService<IDbConnectionFactory>(),
            sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<Core.Configuration.DataverseOptions>>(),
            sp.GetRequiredService<ILoggerFactory>(),
            sp.GetServices<IDbCommandInterceptor>()));
        return services;
    }

    /// <summary>
    /// Registers Tyresoles.Sql with configuration bound from the "Tyresoles" or "tenants" section.
    /// </summary>
    public static IServiceCollection AddTyresolesSql(this IServiceCollection services, Microsoft.Extensions.Configuration.IConfiguration configuration)
    {
        var tyresolesSection = configuration.GetSection("Tyresoles");
        var tenantsSection = configuration.GetSection("tenants");
        services.Configure<Core.Configuration.DataverseOptions>(o =>
        {
            if (tyresolesSection.Exists())
            {
                tyresolesSection.Bind(o);
            }
            if ((o.Tenants == null || o.Tenants.Count == 0) && tenantsSection.Exists())
            {
                var tenants = tenantsSection.Get<Dictionary<string, Core.Configuration.TenantConfiguration>>();
                if (tenants != null)
                    o.Tenants = tenants;
            }
        });
        return services.AddTyresolesSql();
    }
}
