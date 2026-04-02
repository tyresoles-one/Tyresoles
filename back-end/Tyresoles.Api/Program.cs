using Microsoft.AspNetCore.Authentication.JwtBearer;
using Tyresoles.Logger.Extensions;
using Tyresoles.Sql.Abstractions;
using Tyresoles.Sql.SqlServer;

var builder = WebApplication.CreateBuilder(args);

// 1. Logging
builder.Logging.ClearProviders(); // Use Tyresoles Logger exclusively or additive? Usually additive.
builder.Logging.AddTyresolesLogger();

// 2. Configuration & Services
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi();

// SQL Data Access
builder.Services.AddTyresolesSql(builder.Configuration);
builder.Services.Configure<Tyresoles.Data.Features.Common.NavWebServiceSettings>(builder.Configuration.GetSection(Tyresoles.Data.Features.Common.NavWebServiceSettings.SectionName));
builder.Services.AddScoped<Tyresoles.Data.Features.Common.Connector>();
builder.Services.AddScoped<Tyresoles.Data.Features.ProductionService.IProductionService, Tyresoles.Data.Features.ProductionService.ProductionService>();

// Authentication (Mock for now, ready for implementation)
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = "https://identity.tyresoles.com"; // Placeholder
        options.Audience = "tyresoles-api";
    });
builder.Services.AddAuthorization();

// Health Checks
builder.Services.AddHealthChecks()
    .AddCheck<SqlHealthCheck>("sql_check"); // Custom check needed or create simple lambda

var app = builder.Build();

// 3. Pipeline
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();

// 4. Endpoints
app.MapHealthChecks("/health");

app.MapGet("/api/tenants", (IDataverse dataverse) =>
{
    var tenant = dataverse.DefaultTenant;
    return Results.Ok(new { message = $"Connected to tenant: {tenant.TenantKey}" });
})
.WithName("GetTenants")
.WithTags("Tenants")
.RequireAuthorization(); // Require auth for default endpoint

app.Run();

// Minimal Health Check
internal class SqlHealthCheck : Microsoft.Extensions.Diagnostics.HealthChecks.IHealthCheck
{
    private readonly IDataverse _dataverse;
    public SqlHealthCheck(IDataverse dataverse) => _dataverse = dataverse;
    
    public async Task<Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult> CheckHealthAsync(
        Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckContext context, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var scope = _dataverse.DefaultTenant;
            await scope.ExecuteScalarAsync<int>("SELECT 1", null, cancellationToken);
            return Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy();
        }
        catch (Exception ex)
        {
            return Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Unhealthy("SQL Unreachable", ex);
        }
    }
}
