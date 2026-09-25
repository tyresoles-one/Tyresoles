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
using Tyresoles.Data.Features.WindowsServices;
using Tyresoles.Sql.Abstractions;
using Tyresoles.Data.Features.Crm;
using Tyresoles.Web.Features.RemoteAssist;
using Tyresoles.Web.Features.VpnInstaller;
using Tyresoles.Web.Features.Downloads;
using Tyresoles.Web.Features.DriveSync;
using Tyresoles.Web.Features.WindowsServices;
using Tyresoles.Data.Features.Merger;
using Tyresoles.Data.Features.Admin.EmailAccounts;
using Tyresoles.Web.Features.EmailAccounts;
using Tyresoles.Web.Services.Email;
using Tyresoles.Web.GraphQL;
using StackExchange.Redis;
using Microsoft.Data.SqlClient;

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
builder.Services.AddScoped<IMergerService, MergerService>();
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
builder.Services.Configure<DownloadsOptions>(builder.Configuration.GetSection(DownloadsOptions.SectionName));
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

// CRM module: separate DbContext on same Db_Extra database
builder.Services.AddDbContext<CrmDbContext>(options =>
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

// DriveSync: Nav Live User fields + Google service account (hybrid: client upload token, server-proxied restore).
builder.Services.Configure<DriveSyncGoogleOptions>(builder.Configuration.GetSection(DriveSyncGoogleOptions.SectionName));
builder.Services.AddScoped<IDriveSyncOAuthService, DriveSyncOAuthService>();
builder.Services.AddScoped<IGoogleDriveBackupGateway, GoogleDriveBackupGateway>();
builder.Services.AddScoped<IDriveSyncService, DriveSyncService>();

builder.Services.Configure<WindowsServiceOptions>(builder.Configuration.GetSection(WindowsServiceOptions.SectionName));
builder.Services.AddSingleton<IWindowsServiceManager, WindowsServiceManager>();

builder.Services.Configure<RediffmailSettings>(builder.Configuration.GetSection(RediffmailSettings.SectionName));
builder.Services.AddHttpClient<IRediffmailService, RediffmailService>();

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
builder.Services.AddHttpClient();
builder.Services.AddScoped<Tyresoles.Data.Features.Crm.Services.ILiveLeadDiscoveryService, Tyresoles.Data.Features.Crm.Services.LiveLeadDiscoveryService>();

// Email Campaign Marketing Services
builder.Services.Configure<AmazonSesSmtpSettings>(builder.Configuration.GetSection(AmazonSesSmtpSettings.SectionName));
builder.Services.AddSingleton<ISpamScoringService, SpamScoringService>();
builder.Services.AddScoped<IEmailCampaignSenderService, EmailCampaignSenderService>();
builder.Services.AddHostedService<EmailCampaignBackgroundService>();

// WhatsApp Campaign Marketing Services
builder.Services.Configure<Tyresoles.Web.Services.Whatsapp.WhatsappSettings>(builder.Configuration.GetSection("WhatsappSettings"));
builder.Services.AddScoped<Tyresoles.Web.Services.Whatsapp.IWhatsappCloudApiService, Tyresoles.Web.Services.Whatsapp.WhatsappCloudApiService>();
builder.Services.AddHostedService<Tyresoles.Web.Services.Whatsapp.WhatsappCampaignBackgroundService>();

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
    .AddTypeExtension<VendorTypeExtension>()
    .AddTypeExtension<EmailAccountQueryExtension>()
    .AddTypeExtension<EmailAccountMutationExtension>()
    .AddTypeExtension<CrmCampaignQueryExtension>()
    .AddTypeExtension<CrmCampaignMutationExtension>()
    .AddTypeExtension<Tyresoles.Web.GraphQL.CrmWhatsappCampaignQueryExtension>()
    .AddTypeExtension<Tyresoles.Web.GraphQL.CrmWhatsappCampaignMutationExtension>();

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

// Ensure CRM tables exist in Db_Extra
try
{
    using (var scope = app.Services.CreateScope())
    {
        var crmDb = scope.ServiceProvider.GetRequiredService<CrmDbContext>();
        await crmDb.Database.ExecuteSqlRawAsync(@"
            IF OBJECT_ID('dbo.CrmContactType', 'U') IS NULL
            BEGIN
                CREATE TABLE dbo.[CrmContactType] (
                    [Id] int NOT NULL IDENTITY(1,1),
                    [Name] nvarchar(max) NOT NULL,
                    CONSTRAINT [PK_CrmContactType] PRIMARY KEY ([Id])
                );
            END

            IF OBJECT_ID('dbo.CrmSource', 'U') IS NULL
            BEGIN
                CREATE TABLE dbo.[CrmSource] (
                    [Id] int NOT NULL IDENTITY(1,1),
                    [Name] nvarchar(max) NOT NULL,
                    CONSTRAINT [PK_CrmSource] PRIMARY KEY ([Id])
                );
            END

            IF OBJECT_ID('dbo.CrmSourceChannel', 'U') IS NULL
            BEGIN
                CREATE TABLE dbo.[CrmSourceChannel] (
                    [Id] int NOT NULL IDENTITY(1,1),
                    [Name] nvarchar(200) NOT NULL,
                    [Code] nvarchar(100) NULL,
                    [ParentId] int NULL,
                    [IsActive] bit NOT NULL CONSTRAINT [DF_CrmSourceChannel_IsActive] DEFAULT 1,
                    [Description] nvarchar(500) NULL,
                    [CreatedAt] datetime2 NOT NULL CONSTRAINT [DF_CrmSourceChannel_CreatedAt] DEFAULT SYSUTCDATETIME(),
                    CONSTRAINT [PK_CrmSourceChannel] PRIMARY KEY ([Id]),
                    CONSTRAINT [FK_CrmSourceChannel_CrmSource_ParentId] FOREIGN KEY ([ParentId]) REFERENCES dbo.[CrmSource] ([Id]) ON DELETE SET NULL
                );

                INSERT INTO dbo.[CrmSourceChannel] ([Name], [Code], [Description]) VALUES
                    ('Web-Harvester', 'WEB-HARVESTER', 'Automated web directory and listing harvester'),
                    ('Google-Maps', 'GOOGLE-MAPS', 'Google Maps and Places business discovery'),
                    ('IndiaMART', 'INDIAMART', 'IndiaMART B2B marketplace leads'),
                    ('TradeIndia', 'TRADEINDIA', 'TradeIndia marketplace inquiries'),
                    ('Justdial', 'JUSTDIAL', 'Justdial local search listings'),
                    ('Direct-Call', 'DIRECT-CALL', 'Inbound and cold phone calling inquiries'),
                    ('WhatsApp', 'WHATSAPP', 'WhatsApp messaging & inbound campaign replies'),
                    ('Field-Visit', 'FIELD-VISIT', 'On-ground yard and fleet field audits'),
                    ('Website-Form', 'WEBSITE-FORM', 'Tyresoles website lead form submissions'),
                    ('Referral', 'REFERRAL', 'Existing customer and dealer word-of-mouth referrals'),
                    ('Directory', 'DIRECTORY', 'Trade and transport association directories'),
                    ('Tender', 'TENDER', 'Government and corporate tender notices'),
                    ('Fleet-Audit', 'FLEET-AUDIT', 'Direct fleet yard survey and tyre inspection');
            END

            IF OBJECT_ID('dbo.CrmStage', 'U') IS NULL
            BEGIN
                CREATE TABLE dbo.[CrmStage] (
                    [Id] int NOT NULL IDENTITY(1,1),
                    [Name] nvarchar(max) NOT NULL,
                    CONSTRAINT [PK_CrmStage] PRIMARY KEY ([Id])
                );
            END

            IF OBJECT_ID('dbo.CrmPriority', 'U') IS NULL
            BEGIN
                CREATE TABLE dbo.[CrmPriority] (
                    [Id] int NOT NULL IDENTITY(1,1),
                    [Name] nvarchar(max) NOT NULL,
                    CONSTRAINT [PK_CrmPriority] PRIMARY KEY ([Id])
                );
            END

            IF OBJECT_ID('dbo.CrmActivityType', 'U') IS NULL
            BEGIN
                CREATE TABLE dbo.[CrmActivityType] (
                    [Id] int NOT NULL IDENTITY(1,1),
                    [Name] nvarchar(max) NOT NULL,
                    CONSTRAINT [PK_CrmActivityType] PRIMARY KEY ([Id])
                );
            END

            IF OBJECT_ID('dbo.CrmActivityOutcome', 'U') IS NULL
            BEGIN
                CREATE TABLE dbo.[CrmActivityOutcome] (
                    [Id] int NOT NULL IDENTITY(1,1),
                    [Name] nvarchar(max) NOT NULL,
                    CONSTRAINT [PK_CrmActivityOutcome] PRIMARY KEY ([Id])
                );
            END

            IF OBJECT_ID('dbo.CrmContact', 'U') IS NULL
            BEGIN
                CREATE TABLE dbo.[CrmContact] (
                    [Id] uniqueidentifier NOT NULL,
                    [ContactType] nvarchar(max) NULL,
                    [FullName] nvarchar(max) NOT NULL,
                    [CompanyName] nvarchar(max) NULL,
                    [MobileNo] nvarchar(max) NULL,
                    [MobileNo2] nvarchar(max) NULL,
                    [EmailIds] nvarchar(max) NULL,
                    [IsDecisionMaker] bit NOT NULL,
                    [Address] nvarchar(max) NULL,
                    [City] nvarchar(max) NULL,
                    [State] nvarchar(max) NULL,
                    [RespCenter] nvarchar(max) NULL,
                    [ERPCustomerNos] nvarchar(max) NULL,
                    [ERPAreaCodes] nvarchar(max) NULL,
                    [Products] nvarchar(max) NULL,
                    [Tags] nvarchar(max) NULL,
                    [IsActive] bit NOT NULL,
                    [CreatedBy] nvarchar(max) NULL,
                    [AssignedTo] nvarchar(max) NULL,
                    [Website] nvarchar(max) NULL,
                    [Snippet] nvarchar(max) NULL,
                    CONSTRAINT [PK_CrmContact] PRIMARY KEY ([Id])
                );
            END

            IF OBJECT_ID('dbo.CrmContact', 'U') IS NOT NULL AND COL_LENGTH('dbo.CrmContact', 'ERPAreaCodes') IS NULL
            BEGIN
                ALTER TABLE dbo.[CrmContact] ADD [ERPAreaCodes] nvarchar(max) NULL;
            END

            IF OBJECT_ID('dbo.CrmContact', 'U') IS NOT NULL AND COL_LENGTH('dbo.CrmContact', 'Products') IS NULL
            BEGIN
                ALTER TABLE dbo.[CrmContact] ADD [Products] nvarchar(max) NULL;
            END

            IF OBJECT_ID('dbo.CrmContact', 'U') IS NOT NULL AND COL_LENGTH('dbo.CrmContact', 'LastCallDate') IS NULL
            BEGIN
                ALTER TABLE dbo.[CrmContact] ADD [LastCallDate] datetime2 NULL;
            END

            IF OBJECT_ID('dbo.CrmContact', 'U') IS NOT NULL AND COL_LENGTH('dbo.CrmContact', 'LastCallOutcome') IS NULL
            BEGIN
                ALTER TABLE dbo.[CrmContact] ADD [LastCallOutcome] nvarchar(100) NULL;
                IF OBJECT_ID('dbo.CrmCallLog', 'U') IS NOT NULL
                BEGIN
                    EXEC('
                        UPDATE c
                        SET c.LastCallOutcome = log.Outcome
                        FROM dbo.CrmContact c
                        CROSS APPLY (
                            SELECT TOP 1 l.Outcome
                            FROM dbo.CrmCallLog l
                            WHERE l.ContactId = c.Id
                            ORDER BY l.CallDate DESC
                        ) log
                    ');
                END
            END

            IF OBJECT_ID('dbo.CrmContact', 'U') IS NOT NULL AND COL_LENGTH('dbo.CrmContact', 'LeadSourceType') IS NULL
            BEGIN
                ALTER TABLE dbo.[CrmContact] ADD [LeadSourceType] nvarchar(100) NOT NULL CONSTRAINT DF_CrmContact_LeadSourceType DEFAULT 'Manual';
            END

            IF OBJECT_ID('dbo.CrmContact', 'U') IS NOT NULL AND COL_LENGTH('dbo.CrmContact', 'LeadSourceChannel') IS NULL
            BEGIN
                ALTER TABLE dbo.[CrmContact] ADD [LeadSourceChannel] nvarchar(100) NULL;
            END

            IF OBJECT_ID('dbo.CrmContact', 'U') IS NOT NULL AND COL_LENGTH('dbo.CrmContact', 'SourceUrl') IS NULL
            BEGIN
                ALTER TABLE dbo.[CrmContact] ADD [SourceUrl] nvarchar(max) NULL;
            END

            IF OBJECT_ID('dbo.CrmContact', 'U') IS NOT NULL AND COL_LENGTH('dbo.CrmContact', 'Division') IS NULL
            BEGIN
                ALTER TABLE dbo.[CrmContact] ADD [Division] nvarchar(100) NULL;
            END

            IF OBJECT_ID('dbo.CrmContact', 'U') IS NOT NULL AND COL_LENGTH('dbo.CrmContact', 'TargetProduct') IS NULL
            BEGIN
                ALTER TABLE dbo.[CrmContact] ADD [TargetProduct] nvarchar(200) NULL;
            END

            IF OBJECT_ID('dbo.CrmContact', 'U') IS NOT NULL AND COL_LENGTH('dbo.CrmContact', 'QualityScore') IS NULL
            BEGIN
                ALTER TABLE dbo.[CrmContact] ADD [QualityScore] decimal(5,2) NULL;
            END

            IF OBJECT_ID('dbo.CrmContact', 'U') IS NOT NULL AND COL_LENGTH('dbo.CrmContact', 'ScrapingQuery') IS NULL
            BEGIN
                ALTER TABLE dbo.[CrmContact] ADD [ScrapingQuery] nvarchar(max) NULL;
            END

            IF OBJECT_ID('dbo.CrmContact', 'U') IS NOT NULL AND COL_LENGTH('dbo.CrmContact', 'HarvestedAt') IS NULL
            BEGIN
                ALTER TABLE dbo.[CrmContact] ADD [HarvestedAt] datetime2 NULL;
            END

            IF OBJECT_ID('dbo.CrmContact', 'U') IS NOT NULL AND COL_LENGTH('dbo.CrmContact', 'Website') IS NULL
            BEGIN
                ALTER TABLE dbo.[CrmContact] ADD [Website] nvarchar(max) NULL;
            END

            IF OBJECT_ID('dbo.CrmContact', 'U') IS NOT NULL AND COL_LENGTH('dbo.CrmContact', 'Snippet') IS NULL
            BEGIN
                ALTER TABLE dbo.[CrmContact] ADD [Snippet] nvarchar(max) NULL;
            END

            IF OBJECT_ID('dbo.CrmContact', 'U') IS NOT NULL AND COL_LENGTH('dbo.CrmContact', 'PrefLanguage') IS NULL
            BEGIN
                ALTER TABLE dbo.[CrmContact] ADD [PrefLanguage] nvarchar(100) NULL;
            END

            IF OBJECT_ID('dbo.CrmContact', 'U') IS NOT NULL AND COL_LENGTH('dbo.CrmContact', 'Location') IS NULL
            BEGIN
                ALTER TABLE dbo.[CrmContact] ADD [Location] nvarchar(200) NULL;
            END

            IF OBJECT_ID('dbo.CrmContact', 'U') IS NOT NULL AND COL_LENGTH('dbo.CrmContact', 'CreatedBy') IS NULL
            BEGIN
                ALTER TABLE dbo.[CrmContact] ADD [CreatedBy] nvarchar(max) NULL;
            END

            IF OBJECT_ID('dbo.CrmContact', 'U') IS NOT NULL AND COL_LENGTH('dbo.CrmContact', 'CreatedAt') IS NULL
            BEGIN
                ALTER TABLE dbo.[CrmContact] ADD [CreatedAt] datetime2 NOT NULL CONSTRAINT DF_CrmContact_CreatedAt DEFAULT SYSUTCDATETIME();
            END

            IF OBJECT_ID('dbo.CrmContact', 'U') IS NOT NULL AND COL_LENGTH('dbo.CrmContact', 'ModifiedAt') IS NULL
            BEGIN
                ALTER TABLE dbo.[CrmContact] ADD [ModifiedAt] datetime2 NULL;
            END

            IF OBJECT_ID('dbo.CrmContact', 'U') IS NOT NULL AND COL_LENGTH('dbo.CrmContact', 'ModifiedBy') IS NULL
            BEGIN
                ALTER TABLE dbo.[CrmContact] ADD [ModifiedBy] nvarchar(max) NULL;
            END

            IF OBJECT_ID('dbo.CrmCallLog', 'U') IS NULL
            BEGIN
                CREATE TABLE dbo.[CrmCallLog] (
                    [Id] uniqueidentifier NOT NULL,
                    [ContactId] uniqueidentifier NOT NULL,
                    [CallDate] datetime2 NOT NULL,
                    [Outcome] nvarchar(100) NOT NULL,
                    [Notes] nvarchar(max) NULL,
                    [CreatedBy] nvarchar(128) NOT NULL,
                    [SalesUserId] nvarchar(128) NULL,
                    CONSTRAINT [PK_CrmCallLog] PRIMARY KEY ([Id]),
                    CONSTRAINT [FK_CrmCallLog_CrmContact_ContactId] FOREIGN KEY ([ContactId]) REFERENCES dbo.[CrmContact] ([Id]) ON DELETE CASCADE
                );
                CREATE INDEX [IX_CrmCallLog_ContactId] ON dbo.[CrmCallLog] ([ContactId]);
            END

            IF OBJECT_ID('dbo.CrmCallLog', 'U') IS NOT NULL AND COL_LENGTH('dbo.CrmCallLog', 'SalesUserId') IS NULL
            BEGIN
                ALTER TABLE dbo.[CrmCallLog] ADD [SalesUserId] nvarchar(128) NULL;
            END

            IF OBJECT_ID('dbo.CrmCallLog', 'U') IS NOT NULL AND COL_LENGTH('dbo.CrmCallLog', 'InvoiceNos') IS NULL
            BEGIN
                ALTER TABLE dbo.[CrmCallLog] ADD [InvoiceNos] nvarchar(max) NULL;
            END

            IF OBJECT_ID('dbo.CrmCallLog', 'U') IS NOT NULL AND COL_LENGTH('dbo.CrmCallLog', 'InvoiceAmount') IS NULL
            BEGIN
                ALTER TABLE dbo.[CrmCallLog] ADD [InvoiceAmount] decimal(18,2) NULL;
            END

            IF OBJECT_ID('dbo.CrmCallLog', 'U') IS NOT NULL AND COL_LENGTH('dbo.CrmCallLog', 'TyreQuantity') IS NULL
            BEGIN
                ALTER TABLE dbo.[CrmCallLog] ADD [TyreQuantity] decimal(18,2) NULL;
            END

            IF OBJECT_ID('dbo.CrmCallLogInvoice', 'U') IS NULL
            BEGIN
                CREATE TABLE dbo.[CrmCallLogInvoice] (
                    [Id] uniqueidentifier NOT NULL,
                    [CallLogId] uniqueidentifier NOT NULL,
                    [InvoiceNo] nvarchar(100) NOT NULL,
                    [Amount] decimal(18,2) NOT NULL CONSTRAINT [DF_CrmCallLogInvoice_Amount] DEFAULT(0),
                    [TyreQuantity] decimal(18,2) NOT NULL CONSTRAINT [DF_CrmCallLogInvoice_TyreQuantity] DEFAULT(0),
                    [CreatedAt] datetime2 NOT NULL,
                    CONSTRAINT [PK_CrmCallLogInvoice] PRIMARY KEY ([Id]),
                    CONSTRAINT [FK_CrmCallLogInvoice_CrmCallLog_CallLogId] FOREIGN KEY ([CallLogId]) REFERENCES dbo.[CrmCallLog] ([Id]) ON DELETE CASCADE
                );
                CREATE INDEX [IX_CrmCallLogInvoice_CallLogId] ON dbo.[CrmCallLogInvoice] ([CallLogId]);
                CREATE INDEX [IX_CrmCallLogInvoice_InvoiceNo] ON dbo.[CrmCallLogInvoice] ([InvoiceNo]);
            END
            ELSE
            BEGIN
                IF COL_LENGTH('dbo.CrmCallLogInvoice', 'Amount') IS NULL
                BEGIN
                    ALTER TABLE dbo.[CrmCallLogInvoice] ADD [Amount] decimal(18,2) NOT NULL CONSTRAINT [DF_CrmCallLogInvoice_Amount] DEFAULT(0);
                END
                IF COL_LENGTH('dbo.CrmCallLogInvoice', 'TyreQuantity') IS NULL
                BEGIN
                    ALTER TABLE dbo.[CrmCallLogInvoice] ADD [TyreQuantity] decimal(18,2) NOT NULL CONSTRAINT [DF_CrmCallLogInvoice_TyreQuantity] DEFAULT(0);
                END
            END

            IF OBJECT_ID('dbo.CrmCallReminder', 'U') IS NULL
            BEGIN
                CREATE TABLE dbo.[CrmCallReminder] (
                    [Id] uniqueidentifier NOT NULL,
                    [ContactId] uniqueidentifier NOT NULL,
                    [ReminderDate] datetime2 NOT NULL,
                    [Notes] nvarchar(max) NULL,
                    [IsCompleted] bit NOT NULL,
                    [CreatedAt] datetime2 NOT NULL,
                    [CreatedBy] nvarchar(128) NOT NULL,
                    CONSTRAINT [PK_CrmCallReminder] PRIMARY KEY ([Id]),
                    CONSTRAINT [FK_CrmCallReminder_CrmContact_ContactId] FOREIGN KEY ([ContactId]) REFERENCES dbo.[CrmContact] ([Id]) ON DELETE CASCADE
                );
                CREATE INDEX [IX_CrmCallReminder_ContactId] ON dbo.[CrmCallReminder] ([ContactId]);
                CREATE INDEX [IX_CrmCallReminder_IsCompleted] ON dbo.[CrmCallReminder] ([IsCompleted]);
            END

            IF OBJECT_ID('dbo.CrmAgentContact', 'U') IS NULL
            BEGIN
                CREATE TABLE dbo.[CrmAgentContact] (
                    [Id] uniqueidentifier NOT NULL,
                    [AgentUsername] nvarchar(128) NOT NULL,
                    [ContactId] uniqueidentifier NOT NULL,
                    [AllocatedAt] datetime2 NOT NULL,
                    [DeallocatedAt] datetime2 NULL,
                    [DeallocatedBy] nvarchar(128) NULL,
                    [LastCallOutcome] nvarchar(100) NULL,
                    [LastCallDate] datetime2 NULL,
                    [LastCallNotes] nvarchar(max) NULL,
                    [CallCount] int NOT NULL DEFAULT 0,
                    CONSTRAINT [PK_CrmAgentContact] PRIMARY KEY ([Id]),
                    CONSTRAINT [FK_CrmAgentContact_CrmContact_ContactId] FOREIGN KEY ([ContactId]) REFERENCES dbo.[CrmContact] ([Id]) ON DELETE CASCADE
                );
                CREATE INDEX [IX_CrmAgentContact_AgentUsername] ON dbo.[CrmAgentContact] ([AgentUsername]);
                CREATE INDEX [IX_CrmAgentContact_ContactId] ON dbo.[CrmAgentContact] ([ContactId]);
            END

            IF OBJECT_ID('dbo.CrmSetting', 'U') IS NULL
            BEGIN
                CREATE TABLE dbo.[CrmSetting] (
                    [Key] nvarchar(100) NOT NULL,
                    [Value] nvarchar(max) NOT NULL,
                    [Description] nvarchar(max) NULL,
                    CONSTRAINT [PK_CrmSetting] PRIMARY KEY ([Key])
                );
                INSERT INTO dbo.[CrmSetting] ([Key], [Value], [Description])
                VALUES ('ContactsPerAgent', '10', 'Maximum active allocated contacts per calling agent');
            END

            -- Auto-migration of CrmContact.AssignedTo into CrmAgentContact removed to strictly enforce manual on-demand contact allocation

            IF OBJECT_ID('dbo.CrmWhatsappImage', 'U') IS NULL
            BEGIN
                CREATE TABLE dbo.[CrmWhatsappImage] (
                    [Id] uniqueidentifier NOT NULL,
                    [Name] nvarchar(max) NOT NULL,
                    [ImageUrl] nvarchar(max) NULL,
                    [Base64Data] nvarchar(max) NULL,
                    [CreatedAt] datetime2 NOT NULL,
                    CONSTRAINT [PK_CrmWhatsappImage] PRIMARY KEY ([Id])
                );
            END

            IF COL_LENGTH('dbo.CrmWhatsappImage', 'Products') IS NULL
            BEGIN
                ALTER TABLE dbo.[CrmWhatsappImage] ADD [Products] nvarchar(max) NULL;
            END

            IF OBJECT_ID('dbo.CrmWhatsappTemplate', 'U') IS NULL
            BEGIN
                CREATE TABLE dbo.[CrmWhatsappTemplate] (
                    [Id] uniqueidentifier NOT NULL,
                    [Name] nvarchar(max) NOT NULL,
                    [Language] nvarchar(100) NOT NULL,
                    [MessageText] nvarchar(max) NOT NULL,
                    [CreatedAt] datetime2 NOT NULL,
                    CONSTRAINT [PK_CrmWhatsappTemplate] PRIMARY KEY ([Id])
                );
            END

            IF OBJECT_ID('dbo.CrmProduct', 'U') IS NULL
            BEGIN
                CREATE TABLE dbo.[CrmProduct] (
                    [Id] uniqueidentifier NOT NULL,
                    [Code] nvarchar(200) NOT NULL,
                    [Category] nvarchar(200) NULL,
                    [ProductGroup] nvarchar(200) NULL,
                    [FinalPrice] decimal(18,2) NOT NULL,
                    [RespCenters] nvarchar(max) NULL,
                    [WhatsappImageCode] nvarchar(200) NULL,
                    [CreatedAt] datetime2 NOT NULL,
                    CONSTRAINT [PK_CrmProduct] PRIMARY KEY ([Id])
                );
            END

            IF COL_LENGTH('dbo.CrmProduct', 'WhatsappImageCode') IS NULL
            BEGIN
                ALTER TABLE dbo.[CrmProduct] ADD [WhatsappImageCode] nvarchar(200) NULL;
            END

            IF OBJECT_ID('dbo.CrmWhatsappTemplate', 'U') IS NOT NULL AND COL_LENGTH('dbo.CrmWhatsappTemplate', 'LanguageCode') IS NULL
            BEGIN
                ALTER TABLE dbo.[CrmWhatsappTemplate] ADD [LanguageCode] nvarchar(50) NULL;
            END

            IF OBJECT_ID('dbo.CrmLanguage', 'U') IS NULL
            BEGIN
                CREATE TABLE dbo.[CrmLanguage] (
                    [Id] int IDENTITY(1,1) NOT NULL,
                    [Code] nvarchar(50) NOT NULL,
                    [Name] nvarchar(200) NOT NULL,
                    CONSTRAINT [PK_CrmLanguage] PRIMARY KEY ([Id])
                );
                CREATE UNIQUE INDEX [IX_CrmLanguage_Code] ON dbo.[CrmLanguage] ([Code]);
                INSERT INTO dbo.[CrmLanguage] ([Code], [Name]) VALUES
                    ('en', 'English'),
                    ('hi', 'Hindi'),
                    ('mr', 'Marathi'),
                    ('gu', 'Gujarati'),
                    ('ta', 'Tamil'),
                    ('te', 'Telugu'),
                    ('kn', 'Kannada'),
                    ('bn', 'Bengali'),
                    ('pa', 'Punjabi');
            END

            IF OBJECT_ID('dbo.CrmEmailCampaign', 'U') IS NULL
            BEGIN
                CREATE TABLE dbo.[CrmEmailCampaign] (
                    [Id] uniqueidentifier NOT NULL,
                    [Name] nvarchar(250) NOT NULL,
                    [Subject] nvarchar(500) NOT NULL,
                    [PreviewText] nvarchar(500) NULL,
                    [FromName] nvarchar(150) NOT NULL,
                    [FromEmail] nvarchar(250) NOT NULL,
                    [ReplyToEmail] nvarchar(250) NULL,
                    [CampaignType] nvarchar(50) NOT NULL,
                    [ContentType] nvarchar(50) NOT NULL,
                    [BodyHtml] nvarchar(max) NULL,
                    [BodyText] nvarchar(max) NULL,
                    [Status] nvarchar(50) NOT NULL,
                    [ScheduledAt] datetime2 NULL,
                    [StartedAt] datetime2 NULL,
                    [CompletedAt] datetime2 NULL,
                    [TargetSegmentFilterJson] nvarchar(max) NULL,
                    [TotalRecipients] int NOT NULL DEFAULT 0,
                    [SentCount] int NOT NULL DEFAULT 0,
                    [DeliveredCount] int NOT NULL DEFAULT 0,
                    [OpenedCount] int NOT NULL DEFAULT 0,
                    [UniqueOpenedCount] int NOT NULL DEFAULT 0,
                    [ClickedCount] int NOT NULL DEFAULT 0,
                    [UniqueClickedCount] int NOT NULL DEFAULT 0,
                    [BouncedCount] int NOT NULL DEFAULT 0,
                    [UnsubscribedCount] int NOT NULL DEFAULT 0,
                    [SpamComplaintCount] int NOT NULL DEFAULT 0,
                    [CreatedBy] nvarchar(128) NULL,
                    [CreatedAt] datetime2 NOT NULL,
                    [UpdatedAt] datetime2 NULL,
                    CONSTRAINT [PK_CrmEmailCampaign] PRIMARY KEY ([Id])
                );
                CREATE INDEX [IX_CrmEmailCampaign_Status_ScheduledAt] ON dbo.[CrmEmailCampaign] ([Status], [ScheduledAt]);
            END

            IF OBJECT_ID('dbo.CrmEmailTemplate', 'U') IS NULL
            BEGIN
                CREATE TABLE dbo.[CrmEmailTemplate] (
                    [Id] uniqueidentifier NOT NULL,
                    [Name] nvarchar(250) NOT NULL,
                    [Category] nvarchar(100) NOT NULL,
                    [Subject] nvarchar(500) NOT NULL,
                    [PreviewText] nvarchar(500) NULL,
                    [BodyHtml] nvarchar(max) NULL,
                    [BodyText] nvarchar(max) NULL,
                    [IsActive] bit NOT NULL DEFAULT 1,
                    [CreatedBy] nvarchar(128) NULL,
                    [CreatedAt] datetime2 NOT NULL,
                    [UpdatedAt] datetime2 NULL,
                    CONSTRAINT [PK_CrmEmailTemplate] PRIMARY KEY ([Id])
                );
            END

            IF OBJECT_ID('dbo.CrmEmailCampaignRecipient', 'U') IS NULL
            BEGIN
                CREATE TABLE dbo.[CrmEmailCampaignRecipient] (
                    [Id] uniqueidentifier NOT NULL,
                    [CampaignId] uniqueidentifier NOT NULL,
                    [ContactId] uniqueidentifier NULL,
                    [EmailAddress] nvarchar(250) NOT NULL,
                    [FullName] nvarchar(250) NOT NULL,
                    [CompanyName] nvarchar(250) NULL,
                    [Status] nvarchar(50) NOT NULL,
                    [TrackingToken] nvarchar(64) NOT NULL,
                    [SentAt] datetime2 NULL,
                    [DeliveredAt] datetime2 NULL,
                    [OpenedAt] datetime2 NULL,
                    [OpenCount] int NOT NULL DEFAULT 0,
                    [ClickedAt] datetime2 NULL,
                    [ClickCount] int NOT NULL DEFAULT 0,
                    [BouncedAt] datetime2 NULL,
                    [BounceType] nvarchar(50) NULL,
                    [BounceReason] nvarchar(max) NULL,
                    [ErrorMessage] nvarchar(max) NULL,
                    [CreatedAt] datetime2 NOT NULL,
                    CONSTRAINT [PK_CrmEmailCampaignRecipient] PRIMARY KEY ([Id]),
                    CONSTRAINT [FK_CrmEmailCampaignRecipient_CrmEmailCampaign] FOREIGN KEY ([CampaignId]) REFERENCES dbo.[CrmEmailCampaign] ([Id]) ON DELETE CASCADE,
                    CONSTRAINT [FK_CrmEmailCampaignRecipient_CrmContact] FOREIGN KEY ([ContactId]) REFERENCES dbo.[CrmContact] ([Id]) ON DELETE SET NULL
                );
                CREATE INDEX [IX_CrmEmailCampaignRecipient_CampaignId_Status] ON dbo.[CrmEmailCampaignRecipient] ([CampaignId], [Status]);
                CREATE UNIQUE INDEX [IX_CrmEmailCampaignRecipient_TrackingToken] ON dbo.[CrmEmailCampaignRecipient] ([TrackingToken]);
                CREATE INDEX [IX_CrmEmailCampaignRecipient_EmailAddress] ON dbo.[CrmEmailCampaignRecipient] ([EmailAddress]);
            END

            IF OBJECT_ID('dbo.CrmEmailSuppressionList', 'U') IS NULL
            BEGIN
                CREATE TABLE dbo.[CrmEmailSuppressionList] (
                    [Id] uniqueidentifier NOT NULL,
                    [EmailAddress] nvarchar(250) NOT NULL,
                    [Reason] nvarchar(50) NOT NULL,
                    [DiagnosticCode] nvarchar(max) NULL,
                    [SourceCampaignId] uniqueidentifier NULL,
                    [CreatedAt] datetime2 NOT NULL,
                    CONSTRAINT [PK_CrmEmailSuppressionList] PRIMARY KEY ([Id])
                );
                CREATE UNIQUE INDEX [IX_CrmEmailSuppressionList_EmailAddress] ON dbo.[CrmEmailSuppressionList] ([EmailAddress]);
            END

            IF OBJECT_ID('dbo.CrmEmailTrackingLink', 'U') IS NULL
            BEGIN
                CREATE TABLE dbo.[CrmEmailTrackingLink] (
                    [Id] uniqueidentifier NOT NULL,
                    [CampaignId] uniqueidentifier NOT NULL,
                    [OriginalUrl] nvarchar(max) NOT NULL,
                    [LinkHash] nvarchar(64) NOT NULL,
                    [ClickCount] int NOT NULL DEFAULT 0,
                    [CreatedAt] datetime2 NOT NULL,
                    CONSTRAINT [PK_CrmEmailTrackingLink] PRIMARY KEY ([Id]),
                    CONSTRAINT [FK_CrmEmailTrackingLink_CrmEmailCampaign] FOREIGN KEY ([CampaignId]) REFERENCES dbo.[CrmEmailCampaign] ([Id]) ON DELETE CASCADE
                );
                CREATE INDEX [IX_CrmEmailTrackingLink_CampaignId_LinkHash] ON dbo.[CrmEmailTrackingLink] ([CampaignId], [LinkHash]);
            END

            IF OBJECT_ID('dbo.CrmEmailEventLog', 'U') IS NULL
            BEGIN
                CREATE TABLE dbo.[CrmEmailEventLog] (
                    [Id] uniqueidentifier NOT NULL,
                    [CampaignId] uniqueidentifier NOT NULL,
                    [RecipientId] uniqueidentifier NULL,
                    [EmailAddress] nvarchar(250) NOT NULL,
                    [EventType] nvarchar(50) NOT NULL,
                    [Details] nvarchar(max) NULL,
                    [UserAgent] nvarchar(max) NULL,
                    [IpAddress] nvarchar(100) NULL,
                    [IsMachineOpen] bit NOT NULL DEFAULT 0,
                    [Timestamp] datetime2 NOT NULL,
                    CONSTRAINT [PK_CrmEmailEventLog] PRIMARY KEY ([Id]),
                    CONSTRAINT [FK_CrmEmailEventLog_CrmEmailCampaign] FOREIGN KEY ([CampaignId]) REFERENCES dbo.[CrmEmailCampaign] ([Id]) ON DELETE CASCADE
                );
                CREATE INDEX [IX_CrmEmailEventLog_CampaignId_EventType] ON dbo.[CrmEmailEventLog] ([CampaignId], [EventType]);
                CREATE INDEX [IX_CrmEmailEventLog_RecipientId] ON dbo.[CrmEmailEventLog] ([RecipientId]);
            END

            IF OBJECT_ID('dbo.CrmWhatsappTemplate', 'U') IS NOT NULL
            BEGIN
                IF COL_LENGTH('dbo.CrmWhatsappTemplate', 'MetaTemplateId') IS NULL
                    ALTER TABLE dbo.[CrmWhatsappTemplate] ADD [MetaTemplateId] nvarchar(150) NULL;
                IF COL_LENGTH('dbo.CrmWhatsappTemplate', 'Category') IS NULL
                    ALTER TABLE dbo.[CrmWhatsappTemplate] ADD [Category] nvarchar(50) NOT NULL DEFAULT 'MARKETING';
                IF COL_LENGTH('dbo.CrmWhatsappTemplate', 'Status') IS NULL
                    ALTER TABLE dbo.[CrmWhatsappTemplate] ADD [Status] nvarchar(50) NOT NULL DEFAULT 'APPROVED';
                IF COL_LENGTH('dbo.CrmWhatsappTemplate', 'HeaderType') IS NULL
                    ALTER TABLE dbo.[CrmWhatsappTemplate] ADD [HeaderType] nvarchar(50) NOT NULL DEFAULT 'NONE';
                IF COL_LENGTH('dbo.CrmWhatsappTemplate', 'HeaderText') IS NULL
                    ALTER TABLE dbo.[CrmWhatsappTemplate] ADD [HeaderText] nvarchar(max) NULL;
                IF COL_LENGTH('dbo.CrmWhatsappTemplate', 'HeaderMediaUrl') IS NULL
                    ALTER TABLE dbo.[CrmWhatsappTemplate] ADD [HeaderMediaUrl] nvarchar(max) NULL;
                IF COL_LENGTH('dbo.CrmWhatsappTemplate', 'BodyText') IS NULL
                    ALTER TABLE dbo.[CrmWhatsappTemplate] ADD [BodyText] nvarchar(max) NULL;
                IF COL_LENGTH('dbo.CrmWhatsappTemplate', 'FooterText') IS NULL
                    ALTER TABLE dbo.[CrmWhatsappTemplate] ADD [FooterText] nvarchar(max) NULL;
                IF COL_LENGTH('dbo.CrmWhatsappTemplate', 'ButtonsJson') IS NULL
                    ALTER TABLE dbo.[CrmWhatsappTemplate] ADD [ButtonsJson] nvarchar(max) NULL;
                IF COL_LENGTH('dbo.CrmWhatsappTemplate', 'ComponentsJson') IS NULL
                    ALTER TABLE dbo.[CrmWhatsappTemplate] ADD [ComponentsJson] nvarchar(max) NULL;
                IF COL_LENGTH('dbo.CrmWhatsappTemplate', 'SampleValuesJson') IS NULL
                    ALTER TABLE dbo.[CrmWhatsappTemplate] ADD [SampleValuesJson] nvarchar(max) NULL;
                IF COL_LENGTH('dbo.CrmWhatsappTemplate', 'VariableMappingsJson') IS NULL
                    ALTER TABLE dbo.[CrmWhatsappTemplate] ADD [VariableMappingsJson] nvarchar(max) NULL;
                IF COL_LENGTH('dbo.CrmWhatsappTemplate', 'QualityScore') IS NULL
                    ALTER TABLE dbo.[CrmWhatsappTemplate] ADD [QualityScore] nvarchar(50) NULL;
                IF COL_LENGTH('dbo.CrmWhatsappTemplate', 'RejectedReason') IS NULL
                    ALTER TABLE dbo.[CrmWhatsappTemplate] ADD [RejectedReason] nvarchar(max) NULL;
                IF COL_LENGTH('dbo.CrmWhatsappTemplate', 'SyncedAt') IS NULL
                    ALTER TABLE dbo.[CrmWhatsappTemplate] ADD [SyncedAt] datetime2 NULL;
                IF COL_LENGTH('dbo.CrmWhatsappTemplate', 'UpdatedAt') IS NULL
                    ALTER TABLE dbo.[CrmWhatsappTemplate] ADD [UpdatedAt] datetime2 NULL;
            END

            IF OBJECT_ID('dbo.CrmWhatsappCampaign', 'U') IS NULL
            BEGIN
                CREATE TABLE dbo.[CrmWhatsappCampaign] (
                    [Id] uniqueidentifier NOT NULL,
                    [Name] nvarchar(250) NOT NULL,
                    [TemplateId] uniqueidentifier NULL,
                    [TemplateName] nvarchar(250) NULL,
                    [LanguageCode] nvarchar(50) NULL DEFAULT 'en',
                    [SenderPhoneNumberId] nvarchar(100) NULL,
                    [DisplayPhoneNumber] nvarchar(50) NULL,
                    [Status] nvarchar(50) NOT NULL DEFAULT 'Draft',
                    [ScheduledAt] datetime2 NULL,
                    [StartedAt] datetime2 NULL,
                    [CompletedAt] datetime2 NULL,
                    [TargetSegmentFilterJson] nvarchar(max) NULL,
                    [VariableMappingsJson] nvarchar(max) NULL,
                    [HeaderMediaUrl] nvarchar(max) NULL,
                    [TotalRecipients] int NOT NULL DEFAULT 0,
                    [SentCount] int NOT NULL DEFAULT 0,
                    [DeliveredCount] int NOT NULL DEFAULT 0,
                    [ReadCount] int NOT NULL DEFAULT 0,
                    [RepliedCount] int NOT NULL DEFAULT 0,
                    [FailedCount] int NOT NULL DEFAULT 0,
                    [CostPerMessage] decimal(18,4) NOT NULL DEFAULT 0.80,
                    [EstimatedCost] decimal(18,2) NULL,
                    [ActualCost] decimal(18,2) NULL,
                    [FailureReason] nvarchar(max) NULL,
                    [CreatedBy] nvarchar(128) NULL,
                    [CreatedAt] datetime2 NOT NULL,
                    [UpdatedAt] datetime2 NULL,
                    CONSTRAINT [PK_CrmWhatsappCampaign] PRIMARY KEY ([Id]),
                    CONSTRAINT [FK_CrmWhatsappCampaign_Template] FOREIGN KEY ([TemplateId]) REFERENCES dbo.[CrmWhatsappTemplate] ([Id]) ON DELETE SET NULL
                );
                CREATE INDEX [IX_CrmWhatsappCampaign_Status_ScheduledAt] ON dbo.[CrmWhatsappCampaign] ([Status], [ScheduledAt]);
            END

            IF OBJECT_ID('dbo.CrmWhatsappCampaignRecipient', 'U') IS NULL
            BEGIN
                CREATE TABLE dbo.[CrmWhatsappCampaignRecipient] (
                    [Id] uniqueidentifier NOT NULL,
                    [CampaignId] uniqueidentifier NOT NULL,
                    [ContactId] uniqueidentifier NULL,
                    [PhoneNumber] nvarchar(50) NOT NULL,
                    [FullName] nvarchar(250) NOT NULL,
                    [CompanyName] nvarchar(250) NULL,
                    [Status] nvarchar(50) NOT NULL DEFAULT 'Queued',
                    [MetaMessageId] nvarchar(150) NULL,
                    [SentAt] datetime2 NULL,
                    [DeliveredAt] datetime2 NULL,
                    [ReadAt] datetime2 NULL,
                    [RepliedAt] datetime2 NULL,
                    [ReplyMessageText] nvarchar(max) NULL,
                    [ErrorCode] int NULL,
                    [ErrorMessage] nvarchar(max) NULL,
                    [CreatedAt] datetime2 NOT NULL,
                    CONSTRAINT [PK_CrmWhatsappCampaignRecipient] PRIMARY KEY ([Id]),
                    CONSTRAINT [FK_CrmWhatsappCampaignRecipient_Campaign] FOREIGN KEY ([CampaignId]) REFERENCES dbo.[CrmWhatsappCampaign] ([Id]) ON DELETE CASCADE,
                    CONSTRAINT [FK_CrmWhatsappCampaignRecipient_Contact] FOREIGN KEY ([ContactId]) REFERENCES dbo.[CrmContact] ([Id]) ON DELETE SET NULL
                );
                CREATE INDEX [IX_CrmWhatsappCampaignRecipient_CampaignId_Status] ON dbo.[CrmWhatsappCampaignRecipient] ([CampaignId], [Status]);
                CREATE INDEX [IX_CrmWhatsappCampaignRecipient_MetaMessageId] ON dbo.[CrmWhatsappCampaignRecipient] ([MetaMessageId]);
                CREATE INDEX [IX_CrmWhatsappCampaignRecipient_PhoneNumber] ON dbo.[CrmWhatsappCampaignRecipient] ([PhoneNumber]);
            END

            IF OBJECT_ID('dbo.CrmWhatsappSuppressionList', 'U') IS NULL
            BEGIN
                CREATE TABLE dbo.[CrmWhatsappSuppressionList] (
                    [Id] uniqueidentifier NOT NULL,
                    [PhoneNumber] nvarchar(50) NOT NULL,
                    [Reason] nvarchar(50) NOT NULL,
                    [ContactId] uniqueidentifier NULL,
                    [Source] nvarchar(50) NOT NULL DEFAULT 'Webhook',
                    [Notes] nvarchar(max) NULL,
                    [CreatedAt] datetime2 NOT NULL,
                    CONSTRAINT [PK_CrmWhatsappSuppressionList] PRIMARY KEY ([Id])
                );
                CREATE UNIQUE INDEX [IX_CrmWhatsappSuppressionList_PhoneNumber] ON dbo.[CrmWhatsappSuppressionList] ([PhoneNumber]);
            END

            IF OBJECT_ID('dbo.CrmWhatsappInboundMessage', 'U') IS NULL
            BEGIN
                CREATE TABLE dbo.[CrmWhatsappInboundMessage] (
                    [Id] uniqueidentifier NOT NULL,
                    [FromPhoneNumber] nvarchar(50) NOT NULL,
                    [ProfileName] nvarchar(250) NULL,
                    [MetaMessageId] nvarchar(150) NOT NULL,
                    [ContextWamid] nvarchar(150) NULL,
                    [CampaignId] uniqueidentifier NULL,
                    [ContactId] uniqueidentifier NULL,
                    [MessageType] nvarchar(50) NOT NULL DEFAULT 'text',
                    [MessageBody] nvarchar(max) NULL,
                    [ButtonPayload] nvarchar(max) NULL,
                    [IsProcessed] bit NOT NULL DEFAULT 0,
                    [FollowupStatus] nvarchar(50) NOT NULL DEFAULT 'Pending',
                    [ReceivedAt] datetime2 NOT NULL,
                    CONSTRAINT [PK_CrmWhatsappInboundMessage] PRIMARY KEY ([Id])
                );
                CREATE INDEX [IX_CrmWhatsappInboundMessage_MetaMessageId] ON dbo.[CrmWhatsappInboundMessage] ([MetaMessageId]);
                CREATE INDEX [IX_CrmWhatsappInboundMessage_ContextWamid] ON dbo.[CrmWhatsappInboundMessage] ([ContextWamid]);
                CREATE INDEX [IX_CrmWhatsappInboundMessage_FromPhoneNumber] ON dbo.[CrmWhatsappInboundMessage] ([FromPhoneNumber]);
            END

            IF OBJECT_ID('dbo.CrmWhatsappWebhookLog', 'U') IS NULL
            BEGIN
                CREATE TABLE dbo.[CrmWhatsappWebhookLog] (
                    [Id] uniqueidentifier NOT NULL,
                    [EventType] nvarchar(50) NOT NULL DEFAULT 'unknown',
                    [FromPhoneNumber] nvarchar(50) NULL,
                    [MetaMessageId] nvarchar(150) NULL,
                    [ProcessingStatus] nvarchar(50) NOT NULL DEFAULT 'Received',
                    [RawPayload] nvarchar(max) NOT NULL,
                    [SignatureHeader] nvarchar(500) NULL,
                    [ErrorMessage] nvarchar(max) NULL,
                    [CampaignId] uniqueidentifier NULL,
                    [RecipientId] uniqueidentifier NULL,
                    [ReceivedAt] datetime2 NOT NULL,
                    CONSTRAINT [PK_CrmWhatsappWebhookLog] PRIMARY KEY ([Id])
                );
                CREATE INDEX [IX_CrmWhatsappWebhookLog_ReceivedAt] ON dbo.[CrmWhatsappWebhookLog] ([ReceivedAt]);
                CREATE INDEX [IX_CrmWhatsappWebhookLog_EventType] ON dbo.[CrmWhatsappWebhookLog] ([EventType]);
                CREATE INDEX [IX_CrmWhatsappWebhookLog_ProcessingStatus] ON dbo.[CrmWhatsappWebhookLog] ([ProcessingStatus]);
                CREATE INDEX [IX_CrmWhatsappWebhookLog_MetaMessageId] ON dbo.[CrmWhatsappWebhookLog] ([MetaMessageId]);
                CREATE INDEX [IX_CrmWhatsappWebhookLog_FromPhoneNumber] ON dbo.[CrmWhatsappWebhookLog] ([FromPhoneNumber]);
            END
        ");
    }
}
catch (Exception ex)
{
    var logger = app.Services.GetRequiredService<ILogger<Program>>();
    logger.LogWarning(ex, "Could not initialize CRM tables. CRM features may be unavailable.");
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

try
{
    var calendarConn = app.Configuration.GetConnectionString("Calendar");
    if (!string.IsNullOrWhiteSpace(calendarConn))
    {
        await using var conn = new SqlConnection(calendarConn);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            IF OBJECT_ID('dbo.DriveSyncOAuthTokens', 'U') IS NULL
            BEGIN
                CREATE TABLE dbo.[DriveSyncOAuthTokens] (
                    [Id] int NOT NULL PRIMARY KEY,
                    [RefreshTokenEnc] nvarchar(max) NULL,
                    [AccessTokenEnc] nvarchar(max) NULL,
                    [AccessTokenExpiryUtc] datetime2 NULL,
                    [GoogleAccountEmail] nvarchar(256) NULL,
                    [UpdatedByUserId] nvarchar(128) NULL,
                    [UpdatedAtUtc] datetime2 NULL
                );
            END
        ";
        await cmd.ExecuteNonQueryAsync();
    }
}
catch (Exception ex)
{
    var logger = app.Services.GetRequiredService<ILogger<Program>>();
    logger.LogWarning(ex, "Could not initialize DriveSync OAuth token table.");
}

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

var wwwrootDir = System.IO.Path.Combine(app.Environment.ContentRootPath, "wwwroot");
if (System.IO.Directory.Exists(wwwrootDir))
{
    app.UseDefaultFiles();
    app.UseStaticFiles();
}

// Emit one Tyresoles.Sql log at startup so the category appears in the log file; SQL queries log when you run them (e.g. login mutation).
app.Lifetime.ApplicationStarted.Register(() =>
{
    var logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Tyresoles.Sql");
    logger.LogInformation("Query logging active. Run a GraphQL mutation (e.g. login) to see SQL entries.");
});

if (!Directory.Exists(wwwrootDir))
{
    app.MapGet("/", () => Results.Redirect("/graphql", permanent: false));
}

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
app.MapDriveSyncEndpoints();
app.MapWindowsServiceEndpoints();
app.MapControllers();
app.MapRemoteAssistWebSocket();

if (Directory.Exists(wwwrootDir))
{
    app.MapFallbackToFile("index.html");
}

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
