using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Tyresoles.Data;
using Tyresoles.Data.Infrastructure;
using Tyresoles.Data.Features.Admin.Auth;
using Tyresoles.Data.Features.DriveSync;
using Tyresoles.Data.Features.Admin.Session;
using Tyresoles.Data.Features.Admin.User;
using Tyresoles.Data.Features.Protean;
using Tyresoles.Data.Features.Sales;
using Tyresoles.Data.Features.Purchase;
using Tyresoles.Data.Features.Procurement;
using Tyresoles.Data.Features.Common;
using Tyresoles.Logger.Extensions;
using Tyresoles.Reporting.Abstractions;
using Tyresoles.Reporting.Extensions;
using Tyresoles.Sql.SqlServer;
using Tyresoles.Web;
using Tyresoles.Web.Auth;
using Tyresoles.Protean;
using Tyresoles.Easebuzz;
using Tyresoles.Data.Features.Sales.Reports;
using Tyresoles.Data.Features.Sales.Dashboard;
using Tyresoles.Data.Features.Payroll;
using Tyresoles.Data.Features.Production;
using Tyresoles.Data.Features.Calendar;
using Tyresoles.Data.Features.Accounts;
using Tyresoles.Data.Features.Accounts.Models;
using Tyresoles.Data.Features.RemoteAssist;
using Tyresoles.Sql.Abstractions;
using Tyresoles.Web.Features.RemoteAssist;
using Tyresoles.Web.Features.VpnInstaller;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

// File logger: writes to ./logs/ (configurable). Query logs from Tyresoles.Sql at Information.
builder.Logging.AddTyresolesLogger(builder.Configuration.GetSection("TyresolesLogger"));
builder.Logging.AddFilter("Tyresoles.Sql", LogLevel.Information);

builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.SectionName));
builder.Services.Configure<PasswordPolicyOptions>(builder.Configuration.GetSection(PasswordPolicyOptions.SectionName));
builder.Services.Configure<Tyresoles.Data.Features.Admin.User.UserPasswordBinaryOptions>(
    builder.Configuration.GetSection(Tyresoles.Data.Features.Admin.User.UserPasswordBinaryOptions.SectionName));

builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton<IDbCommandInterceptor, Tyresoles.Data.Features.Admin.User.UserPasswordBinaryInterceptor>();

// Redis Setup
var useRedis = builder.Configuration.GetValue<bool>("UseRedis", true);
IConnectionMultiplexer? multiplexer = null;
Exception? redisException = null;

if (useRedis)
{
    try 
    {
        var redisConn = builder.Configuration.GetConnectionString("Redis") ?? "localhost:6379";
        var redisOptions = ConfigurationOptions.Parse(redisConn);
        redisOptions.AbortOnConnectFail = false;
        redisOptions.ConnectTimeout = 5000; // 5s timeout
        multiplexer = ConnectionMultiplexer.Connect(redisOptions);
        builder.Services.AddSingleton<IConnectionMultiplexer>(multiplexer);
        builder.Services.AddStackExchangeRedisCache(options =>
        {
            options.Configuration = redisConn;
            options.InstanceName = "Tyresoles:";
        });
    }
    catch (Exception ex)
    {
        // We'll log this after the app is built so we have access to the logger
        redisException = ex;
        useRedis = false;
    }
}

if (redisException != null)
{
    // If Redis failed, ensure we don't try to use the multiplexer
    multiplexer = null;
}

if (!useRedis)
{
    builder.Services.AddDistributedMemoryCache();
    // Register a dummy multiplexer if needed by other services, or handle nulls
}
builder.Services.AddSingleton<GlobalQueryCache>();

builder.Services.AddTyresolesSql(builder.Configuration);
builder.Services.AddSingleton<IPasswordEncryptionService, PasswordEncryptionService>();
if (useRedis)
{
    builder.Services.AddScoped<ISessionStore, RedisSessionStore>();
}
else
{
    // InMemorySessionStore holds a per-instance dictionary; must be singleton so login and GetSessions share the same store.
    builder.Services.AddSingleton<ISessionStore, InMemorySessionStore>();
}
builder.Services.AddScoped<IDataverseDataService, DataverseDataService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<ISalesService, SalesService>();
builder.Services.AddScoped<ISalesReportService, SalesReportService>();
builder.Services.AddScoped<IPayrollReportService, PayrollReportService>();
builder.Services.AddScoped<Tyresoles.Data.Features.Payroll.IPayrollService, Tyresoles.Data.Features.Payroll.PayrollService>();
builder.Services.AddScoped<IProductionReportService, ProductionReportService>();
builder.Services.AddScoped<IProductionService, ProductionService>();
builder.Services.AddScoped<Tyresoles.Data.Features.Accounts.Reports.IAccountsReportService, Tyresoles.Data.Features.Accounts.Reports.AccountsReportService>();
builder.Services.AddScoped<ISalesDashboardService, SalesDashboardService>();
builder.Services.AddScoped<ScopedQueryCache>();
builder.Services.AddScoped<IProteanDataService, ProteanDataService>();
builder.Services.AddScoped<IProteanService, ProteanService>();
builder.Services.AddScoped<IPurchaseService, PurchaseService>();
builder.Services.AddScoped<IFixedAssetService, FixedAssetService>();
builder.Services.AddScoped<IProcurementService, ProcurementService>();
builder.Services.AddScoped<ICommonDataService, CommonDataService>();
builder.Services.AddScoped<Tyresoles.Data.Features.Accounts.IAccountService, Tyresoles.Data.Features.Accounts.AccountService>();
builder.Services.Configure<NavWebServiceSettings>(builder.Configuration.GetSection(NavWebServiceSettings.SectionName));
builder.Services.AddScoped<Connector>();
builder.Services.AddProtean();
builder.Services.AddEasebuzz();

// Notification System
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddSingleton<INotificationPublisher, Tyresoles.Web.GraphQL.GraphQLNotificationPublisher>();
builder.Services.AddHostedService<Tyresoles.Web.Services.ReminderBackgroundService>();

// Calendar module: separate database (EF Core)
builder.Services.AddDbContext<CalendarDbContext>(options =>
{
    var conn = builder.Configuration.GetConnectionString("Calendar")
        ?? "Server=(localdb)\\mssqllocaldb;Database=TyresolesCalendar;Trusted_Connection=True;TrustServerCertificate=True";
    options.UseSqlServer(conn, sqlOptions =>
    {
        sqlOptions.EnableRetryOnFailure(
            maxRetryCount: 5,
            maxRetryDelay: TimeSpan.FromSeconds(30),
            errorNumbersToAdd: null);
    });
});
builder.Services.AddScoped<ICalendarService, CalendarService>();
builder.Services.Configure<RemoteAssistOptions>(builder.Configuration.GetSection(RemoteAssistOptions.SectionName));
builder.Services.Configure<VpnInstallerOptions>(builder.Configuration.GetSection(VpnInstallerOptions.SectionName));
builder.Services.Configure<RemoteAssistIceOptions>(builder.Configuration.GetSection(RemoteAssistOptions.SectionName));
builder.Services.AddSingleton<RemoteAssistControlGate>();
builder.Services.AddSingleton<IRemoteAssistControlNotifier>(sp => sp.GetRequiredService<RemoteAssistControlGate>());
builder.Services.AddScoped<IRemoteAssistService, RemoteAssistService>();
builder.Services.AddSingleton<RemoteAssistSignalingHub>();
builder.Services.AddSingleton<RemoteAssistJwtValidator>();

// NavisionEdits module: separate DbContext on same Db_Extra database
builder.Services.AddDbContext<Tyresoles.Data.Features.NavisionEdits.NavEditDbContext>(options =>
{
    var conn = builder.Configuration.GetConnectionString("Calendar")
        ?? "Server=(localdb)\\mssqllocaldb;Database=TyresolesCalendar;Trusted_Connection=True;TrustServerCertificate=True";
    options.UseSqlServer(conn, sqlOptions =>
    {
        sqlOptions.EnableRetryOnFailure(
            maxRetryCount: 5,
            maxRetryDelay: TimeSpan.FromSeconds(30),
            errorNumbersToAdd: null);
    });
});
builder.Services.AddScoped<Tyresoles.Data.Features.NavisionEdits.INavEditService, Tyresoles.Data.Features.NavisionEdits.NavEditService>();

// DriveSync module: separate DbContext on Db_Extra database (conceptually)
builder.Services.AddDbContext<DriveSyncDbContext>(options =>
{
    var conn = builder.Configuration.GetConnectionString("Calendar")
        ?? "Server=(localdb)\\mssqllocaldb;Database=TyresolesCalendar;Trusted_Connection=True;TrustServerCertificate=True";
    options.UseSqlServer(conn, sqlOptions =>
    {
        sqlOptions.EnableRetryOnFailure(maxRetryCount: 5, maxRetryDelay: TimeSpan.FromSeconds(30), errorNumbersToAdd: null);
    });
});
builder.Services.AddScoped<IDriveSyncService, DriveSyncService>();

// JWT: expiry options for UserService (Data layer); token generation in Web.
builder.Services.AddSingleton<IJwtTokenService, JwtTokenService>();
builder.Services.AddSingleton<JwtExpiryOptions>(sp =>
{
    var opts = sp.GetRequiredService<IOptions<JwtOptions>>().Value;
    return new JwtExpiryOptions
    {
        DealerSalesExpiryHours = opts.DealerSalesExpiryHours,
        DefaultExpiryHours = opts.DefaultExpiryHours
    };
});
builder.Services.AddSingleton<PasswordPolicyOptions>(sp =>
    sp.GetRequiredService<IOptions<PasswordPolicyOptions>>().Value);

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        var jwtOpts = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();
        var secret = jwtOpts.Secret ?? "";
        var keyBytes = IsBase64(secret) ? Convert.FromBase64String(secret) : Encoding.UTF8.GetBytes(secret);
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(keyBytes),
            ValidIssuer = jwtOpts.Issuer,
            ValidAudience = jwtOpts.Audience,
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(1)
        };
        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = async context =>
            {
                var sessionId = context.Principal?.FindFirst("sessionId")?.Value;
                if (string.IsNullOrWhiteSpace(sessionId))
                {
                    context.Fail($"{SessionAuthConstants.RevokedMarker}: Session is missing from token.");
                    return;
                }

                var store = context.HttpContext.RequestServices.GetRequiredService<ISessionStore>();
                var session = await store.GetAsync(sessionId, context.HttpContext.RequestAborted).ConfigureAwait(false);
                if (session is null)
                {
                    context.Fail($"{SessionAuthConstants.RevokedMarker}: Session ended or expired.");
                }
            }
        };
    });
builder.Services.AddAuthorization();

builder.Services.AddRateLimiter(options =>
{
    options.AddFixedWindowLimiter("RemoteAssist", o =>
    {
        o.Window = TimeSpan.FromMinutes(1);
        o.PermitLimit = 60;
        o.QueueLimit = 0;
    });
});

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
            .AllowAnyMethod()
            .AllowAnyHeader();
    });
});

builder.Services.AddTyresolesReporting(builder.Configuration);
builder.Services.PostConfigure<Tyresoles.Reporting.Configuration.ReportingOptions>(o =>
{
    if (!string.IsNullOrEmpty(o.ReportsPath) && !System.IO.Path.IsPathRooted(o.ReportsPath))
        o.ReportsPath = System.IO.Path.Combine(builder.Environment.ContentRootPath, o.ReportsPath);
});

builder.Services.AddControllers();

// Single GraphQL server: subscriptions + schema (avoid registering two default executors).
var gqlExecutor = builder.Services.AddGraphQLServer();
if (useRedis && multiplexer != null)
{
    gqlExecutor = gqlExecutor.AddRedisSubscriptions(_ => multiplexer);
}
else
{
    gqlExecutor = gqlExecutor.AddInMemorySubscriptions();
}

gqlExecutor
    .AddFiltering()
    .AddSorting()
    .AddProjections()
    .ModifyRequestOptions(o => {
        o.IncludeExceptionDetails = builder.Environment.IsDevelopment();
        // NAV SOAP mutations may loop many lines (e.g. receipt/dispatch); 60s is too low for large batches.
        o.ExecutionTimeout = TimeSpan.FromMinutes(15);
    })
    .AddErrorFilter<Tyresoles.Web.GraphQL.NavConnectorErrorFilter>()
    .AddQueryType<Query>()
    .AddMutationType<Mutation>()
    .AddSubscriptionType<Tyresoles.Web.GraphQL.Subscription>()
    .AddTypeExtension<VendorTypeExtension>();

var app = builder.Build();

var navWs = app.Configuration.GetSection(NavWebServiceSettings.SectionName).Get<NavWebServiceSettings>();
if (navWs is { Url: not null } && !string.IsNullOrWhiteSpace(navWs.Url) && string.IsNullOrWhiteSpace(navWs.UserID))
{
    app.Logger.LogWarning(
        "NavWebService.Url is configured but UserID is empty. NAV SOAP calls use the process Windows identity only. If you see 401/MessageSecurityException on Negotiate, set UserID, Password, and Domain under NavWebService in appsettings (same values as an account allowed by IIS for this endpoint).");
}

if (redisException != null)
{
    app.Logger.LogError(redisException, "Redis connection failed. Falling back to In-Memory mode.");
}

// Ensure Calendar DB exists and is up to date (no migrations; schema from model + seed)
try
{
    using (var scope = app.Services.CreateScope())
    {
        var calendarDb = scope.ServiceProvider.GetRequiredService<CalendarDbContext>();
        await calendarDb.Database.EnsureCreatedAsync();

        // Ensure tables exist (EnsureCreated only works if DB is missing)
        await calendarDb.Database.ExecuteSqlRawAsync(@"
            IF OBJECT_ID('dbo.Notifications', 'U') IS NULL
            BEGIN
                CREATE TABLE dbo.[Notifications] (
                    [Id] uniqueidentifier NOT NULL,
                    [UserId] nvarchar(128) NOT NULL,
                    [Title] nvarchar(500) NOT NULL,
                    [Message] nvarchar(4000) NOT NULL,
                    [Type] int NOT NULL,
                    [Link] nvarchar(1000) NULL,
                    [IsRead] bit NOT NULL,
                    [CreatedAt] datetime2 NOT NULL,
                    CONSTRAINT [PK_Notifications] PRIMARY KEY ([Id])
                );
                CREATE INDEX [IX_Notifications_IsRead] ON dbo.[Notifications] ([IsRead]);
                CREATE INDEX [IX_Notifications_UserId_CreatedAt] ON dbo.[Notifications] ([UserId], [CreatedAt]);
            END

            IF OBJECT_ID('dbo.NotificationPreferences', 'U') IS NULL
            BEGIN
                CREATE TABLE dbo.[NotificationPreferences] (
                    [UserId] nvarchar(128) NOT NULL,
                    [Channel] int NOT NULL,
                    [DefaultMinutesBefore] int NOT NULL,
                    [EmailEnabled] bit NOT NULL,
                    [PushEnabled] bit NOT NULL,
                    [UpdatedAt] datetime2 NOT NULL,
                    CONSTRAINT [PK_NotificationPreferences] PRIMARY KEY ([UserId], [Channel])
                );
            END

            IF OBJECT_ID('dbo.CalendarAuditLogs', 'U') IS NULL
            BEGIN
                CREATE TABLE dbo.[CalendarAuditLogs] (
                    [Id] bigint NOT NULL IDENTITY,
                    [EventId] uniqueidentifier NOT NULL,
                    [Action] int NOT NULL,
                    [UserId] nvarchar(128) NOT NULL,
                    [Payload] nvarchar(4000) NULL,
                    [CreatedAtUtc] datetime2 NOT NULL,
                    CONSTRAINT [PK_CalendarAuditLogs] PRIMARY KEY ([Id])
                );
            END

            IF OBJECT_ID('dbo.CalendarTasks', 'U') IS NULL
            BEGIN
                CREATE TABLE dbo.[CalendarTasks] (
                    [Id] uniqueidentifier NOT NULL,
                    [EventId] uniqueidentifier NOT NULL,
                    [ParentTaskId] uniqueidentifier NULL,
                    [Title] nvarchar(500) NOT NULL,
                    [IsCompleted] bit NOT NULL,
                    [SortOrder] int NOT NULL,
                    [CreatedAt] datetime2 NOT NULL,
                    [UpdatedAt] datetime2 NOT NULL,
                    CONSTRAINT [PK_CalendarTasks] PRIMARY KEY ([Id]),
                    CONSTRAINT [FK_CalendarTasks_CalendarEvents_EventId] FOREIGN KEY ([EventId]) REFERENCES dbo.[CalendarEvents] ([Id]) ON DELETE CASCADE,
                    CONSTRAINT [FK_CalendarTasks_CalendarTasks_ParentTaskId] FOREIGN KEY ([ParentTaskId]) REFERENCES dbo.[CalendarTasks] ([Id]) ON DELETE NO ACTION
                );
                CREATE INDEX [IX_CalendarTasks_EventId] ON dbo.[CalendarTasks] ([EventId]);
                CREATE INDEX [IX_CalendarTasks_ParentTaskId] ON dbo.[CalendarTasks] ([ParentTaskId]);
            END
        ");

        if (!await calendarDb.EventTypes.AnyAsync())
        {
            calendarDb.EventTypes.AddRange(
                new Tyresoles.Data.Features.Calendar.Entities.EventType { Name = "Meeting", Color = "#3b82f6", IsSystem = true, SortOrder = 1 },
                new Tyresoles.Data.Features.Calendar.Entities.EventType { Name = "Call", Color = "#10b981", IsSystem = true, SortOrder = 2 },
                new Tyresoles.Data.Features.Calendar.Entities.EventType { Name = "Visit", Color = "#f59e0b", IsSystem = true, SortOrder = 3 },
                new Tyresoles.Data.Features.Calendar.Entities.EventType { Name = "Task", Color = "#8b5cf6", IsSystem = true, SortOrder = 4 },
                new Tyresoles.Data.Features.Calendar.Entities.EventType { Name = "Leave", Color = "#ef4444", IsSystem = true, SortOrder = 5 });
            await calendarDb.SaveChangesAsync();
        }
    }
}
catch (Exception ex)
{
    var logger = app.Services.GetRequiredService<ILogger<Program>>();
    logger.LogWarning(ex, "Could not initialize Calendar Database. Calendar features may be unavailable.");
}

app.UseWebSockets();

// Ensure NavisionEdits tables exist in Db_Extra
try
{
    using (var scope = app.Services.CreateScope())
    {
        var navEditDb = scope.ServiceProvider.GetRequiredService<Tyresoles.Data.Features.NavisionEdits.NavEditDbContext>();
        await navEditDb.Database.ExecuteSqlRawAsync(@"
            IF OBJECT_ID('dbo.NavEditRequestTypes', 'U') IS NULL
            BEGIN
                CREATE TABLE dbo.[NavEditRequestTypes] (
                    [Id] int NOT NULL IDENTITY,
                    [Name] nvarchar(200) NOT NULL,
                    [Code] nvarchar(50) NOT NULL,
                    [Description] nvarchar(500) NULL,
                    [Icon] nvarchar(50) NULL,
                    [NavTable] nvarchar(200) NOT NULL,
                    [NavPrimaryKeyColumn] nvarchar(200) NOT NULL,
                    [FieldsJson] nvarchar(max) NOT NULL,
                    [IsActive] bit NOT NULL DEFAULT 1,
                    [SortOrder] int NOT NULL DEFAULT 0,
                    [CreatedAt] datetime2 NOT NULL,
                    [CreatedBy] nvarchar(128) NOT NULL DEFAULT '',
                    [UpdatedAt] datetime2 NULL,
                    [UpdatedBy] nvarchar(128) NULL,
                    CONSTRAINT [PK_NavEditRequestTypes] PRIMARY KEY ([Id])
                );
                CREATE UNIQUE INDEX [IX_NavEditRequestTypes_Code] ON dbo.[NavEditRequestTypes] ([Code]);
                CREATE INDEX [IX_NavEditRequestTypes_IsActive] ON dbo.[NavEditRequestTypes] ([IsActive]);
            END

            IF OBJECT_ID('dbo.NavEditRequests', 'U') IS NULL
            BEGIN
                CREATE TABLE dbo.[NavEditRequests] (
                    [Id] uniqueidentifier NOT NULL,
                    [RequestTypeId] int NOT NULL,
                    [RecordKey] nvarchar(200) NOT NULL,
                    [RequestBody] nvarchar(max) NOT NULL,
                    [UserId] nvarchar(128) NOT NULL,
                    [UserFullName] nvarchar(200) NULL,
                    [Status] int NOT NULL DEFAULT 1,
                    [Remark] nvarchar(1000) NULL,
                    [AdminRemark] nvarchar(1000) NULL,
                    [ProcessedBy] nvarchar(128) NULL,
                    [ProcessedAt] datetime2 NULL,
                    [CreatedAt] datetime2 NOT NULL,
                    [UpdatedAt] datetime2 NULL,
                    CONSTRAINT [PK_NavEditRequests] PRIMARY KEY ([Id]),
                    CONSTRAINT [FK_NavEditRequests_Types] FOREIGN KEY ([RequestTypeId]) REFERENCES dbo.[NavEditRequestTypes] ([Id])
                );
                CREATE INDEX [IX_NavEditRequests_UserId] ON dbo.[NavEditRequests] ([UserId]);
                CREATE INDEX [IX_NavEditRequests_Status] ON dbo.[NavEditRequests] ([Status]);
                CREATE INDEX [IX_NavEditRequests_CreatedAt] ON dbo.[NavEditRequests] ([CreatedAt]);
            END

            IF OBJECT_ID('dbo.NavEditApprovals', 'U') IS NULL
            BEGIN
                CREATE TABLE dbo.[NavEditApprovals] (
                    [Id] uniqueidentifier NOT NULL,
                    [RequestId] uniqueidentifier NOT NULL,
                    [Level] int NOT NULL,
                    [Role] nvarchar(100) NOT NULL,
                    [RoleLabel] nvarchar(200) NULL,
                    [ApproverUserIdsJson] nvarchar(max) NULL,
                    [ApprovedBy] nvarchar(128) NULL,
                    [Status] int NOT NULL DEFAULT 0,
                    [Comment] nvarchar(1000) NULL,
                    [ActionDate] datetime2 NULL,
                    [CreatedAt] datetime2 NOT NULL,
                    CONSTRAINT [PK_NavEditApprovals] PRIMARY KEY ([Id]),
                    CONSTRAINT [FK_NavEditApprovals_Request] FOREIGN KEY ([RequestId]) REFERENCES dbo.[NavEditRequests] ([Id]) ON DELETE CASCADE
                );
                CREATE INDEX [IX_NavEditApprovals_RequestId] ON dbo.[NavEditApprovals] ([RequestId]);
                CREATE INDEX [IX_NavEditApprovals_RequestId_Level] ON dbo.[NavEditApprovals] ([RequestId], [Level]);
            END

            IF OBJECT_ID('dbo.NavEditApprovals', 'U') IS NOT NULL
            AND NOT EXISTS (
                SELECT 1 FROM sys.columns c
                INNER JOIN sys.tables t ON c.object_id = t.object_id
                WHERE t.name = 'NavEditApprovals' AND SCHEMA_NAME(t.schema_id) = 'dbo' AND c.name = 'ApproverUserIdsJson')
            BEGIN
                ALTER TABLE dbo.[NavEditApprovals] ADD [ApproverUserIdsJson] nvarchar(max) NULL;
            END
        ");
    }
}
catch (Exception ex)
{
    var logger = app.Services.GetRequiredService<ILogger<Program>>();
    logger.LogWarning(ex, "Could not initialize NavisionEdits tables. Edit request features may be unavailable.");
}

try
{
    using (var scope = app.Services.CreateScope())
    {
        var calendarDb = scope.ServiceProvider.GetRequiredService<CalendarDbContext>();
        await calendarDb.Database.ExecuteSqlRawAsync(@"
            IF OBJECT_ID('dbo.RemoteAssistSessions', 'U') IS NULL
            BEGIN
                CREATE TABLE dbo.[RemoteAssistSessions] (
                    [Id] uniqueidentifier NOT NULL,
                    [JoinCode] nvarchar(16) NOT NULL,
                    [HostUserId] nvarchar(128) NOT NULL,
                    [HostDisplayName] nvarchar(200) NULL,
                    [ViewerUserId] nvarchar(128) NULL,
                    [ViewerDisplayName] nvarchar(200) NULL,
                    [Status] int NOT NULL DEFAULT 0,
                    [CreatedAtUtc] datetime2 NOT NULL,
                    [ExpiresAtUtc] datetime2 NOT NULL,
                    [EndedAtUtc] datetime2 NULL,
                    [EndedByUserId] nvarchar(128) NULL,
                    [ControlApprovedAtUtc] datetime2 NULL,
                    CONSTRAINT [PK_RemoteAssistSessions] PRIMARY KEY ([Id])
                );
                CREATE UNIQUE INDEX [IX_RemoteAssistSessions_JoinCode] ON dbo.[RemoteAssistSessions] ([JoinCode]);
                CREATE INDEX [IX_RemoteAssistSessions_HostUserId] ON dbo.[RemoteAssistSessions] ([HostUserId]);
                CREATE INDEX [IX_RemoteAssistSessions_ExpiresAtUtc] ON dbo.[RemoteAssistSessions] ([ExpiresAtUtc]);
            END

            IF OBJECT_ID('dbo.RemoteAssistSessions', 'U') IS NOT NULL
            AND NOT EXISTS (
                SELECT 1 FROM sys.columns c
                INNER JOIN sys.tables t ON c.object_id = t.object_id
                WHERE SCHEMA_NAME(t.schema_id) = 'dbo' AND t.name = 'RemoteAssistSessions' AND c.name = 'ControlApprovedAtUtc')
            BEGIN
                ALTER TABLE dbo.[RemoteAssistSessions] ADD [ControlApprovedAtUtc] datetime2 NULL;
            END
        ");
    }
}
catch (Exception ex)
{
    var logger = app.Services.GetRequiredService<ILogger<Program>>();
    logger.LogWarning(ex, "Could not initialize RemoteAssist tables. Remote assist may be unavailable.");
}

// Ensure DriveSync tables exist
try
{
    using (var scope = app.Services.CreateScope())
    {
        var driveSyncDb = scope.ServiceProvider.GetRequiredService<DriveSyncDbContext>();
        await driveSyncDb.Database.ExecuteSqlRawAsync(@"
            IF OBJECT_ID('dbo.UserConfigs', 'U') IS NULL
            BEGIN
                CREATE TABLE dbo.[UserConfigs] (
                    [Id] uniqueidentifier NOT NULL,
                    [UserId] nvarchar(128) NOT NULL,
                    [TargetFolderId] nvarchar(256) NOT NULL,
                    [QuotaBytes] bigint NOT NULL,
                    [UsedBytes] bigint NOT NULL,
                    [AllowedExtensionsJson] nvarchar(max) NULL,
                    [DeniedExtensionsJson] nvarchar(max) NULL,
                    [IsActive] bit NOT NULL,
                    [CreatedAt] datetime2 NOT NULL,
                    [UpdatedAt] datetime2 NOT NULL,
                    CONSTRAINT [PK_UserConfigs] PRIMARY KEY ([Id])
                );
                CREATE UNIQUE INDEX [IX_UserConfigs_UserId] ON dbo.[UserConfigs] ([UserId]);
            END
        ");
    }
}
catch (Exception ex)
{
    var logger = app.Services.GetRequiredService<ILogger<Program>>();
    logger.LogWarning(ex, "Could not initialize DriveSync tables. Sync module may be unavailable.");
}



app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

// Emit one Tyresoles.Sql log at startup so the category appears in the log file; SQL queries log when you run them (e.g. login mutation).
app.Lifetime.ApplicationStarted.Register(() =>
{
    var logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Tyresoles.Sql");
    logger.LogInformation("Query logging active. Run a GraphQL mutation (e.g. login) to see SQL entries.");
});

app.MapGet("/", () => Results.Redirect("/graphql", permanent: false));

app.MapGet("/api/reports/{reportName}/pdf", async (
    string reportName,
    IReportRenderer renderer,
    HttpContext httpContext,
    string? disposition,
    CancellationToken cancellationToken) =>
{
    disposition = (disposition ?? "inline").Equals("attachment", StringComparison.OrdinalIgnoreCase) ? "attachment" : "inline";
    var query = httpContext.Request.Query;
    var parameters = query
        .Where(q => !string.Equals(q.Key, "disposition", StringComparison.OrdinalIgnoreCase))
        .ToDictionary(q => q.Key, q => (object?)q.Value.ToString());
    var input = parameters.Count > 0 ? new Tyresoles.Reporting.Abstractions.ReportInput { Parameters = parameters } : null;
    try
    {
        var stream = await renderer.RenderPdfAsync(reportName, input ?? new Tyresoles.Reporting.Abstractions.ReportInput(), cancellationToken);
        var fileName = $"{reportName}.pdf";
        if (disposition == "attachment")
            return Results.File(stream, "application/pdf", fileName);
        return Results.Stream(stream, "application/pdf");
    }
    catch (FileNotFoundException ex)
    {
        return Results.NotFound(new { error = "Report not found.", message = ex.Message });
    }
    catch (Exception)
    {
        return Results.Problem(detail: "Report rendering failed.", statusCode: 500);
    }
})
.WithName("GetReportPdf")
.WithTags("Reports")
.RequireAuthorization();

app.MapGet("/api/easebuzz/status", (IEasebuzzPaymentService paymentService) =>
{
    _ = paymentService;
    return Results.Ok(new { status = "ok", message = "Easebuzz payment service is registered." });
})
.WithName("GetEasebuzzStatus")
.WithTags("Easebuzz");

app.MapGraphQL();
app.MapControllers();
app.MapRemoteAssistWebSocket();

app.Run();

static bool IsBase64(string value)
{
    if (string.IsNullOrWhiteSpace(value) || value.Length % 4 != 0) return false;
    try
    {
        Convert.FromBase64String(value.Trim());
        return true;
    }
    catch
    {
        return false;
    }
}
