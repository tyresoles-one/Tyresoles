using System.Reflection;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Tyresoles.Sql;
using Tyresoles.Sql.Abstractions;
using Tyresoles.Sql.Core;
using Tyresoles.Sql.Cli;
using Tyresoles.Sql.SqlServer;

// Tyresoles.Sql.Cli - Database Management Tool
Console.WriteLine("Tyresoles SQL CLI Tool v1.0");
Console.WriteLine("---------------------------");

if (args.Length == 0)
{
    Console.WriteLine("Usage: tyresoles [command]");
    Console.WriteLine("Commands:");
    Console.WriteLine("  generate          Generate C# models from Tyresoles.config.json");
    Console.WriteLine("  test-connection   Test database connection");
    Console.WriteLine("  schema            List detected schema tables");
    Console.WriteLine();
    Console.WriteLine("Run as: tyresoles generate  (after: dotnet tool install -g --add-source ./nupkg Tyresoles.Cli)");
    Console.WriteLine("Or:     dotnet run --project Tyresoles.Sql.Cli -- generate");
    return;
}

var command = args[0];

// Setup Service Provider
var builder = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", optional: true)
    .AddEnvironmentVariables();

var config = builder.Build();

var services = new ServiceCollection();
services.AddLogging(l => l.AddConsole());
services.AddTyresolesSql(config);

var provider = services.BuildServiceProvider();

// Execute Command
try 
{
    if (command == "generate")
    {
        RunGenerate(args.Length > 1 ? args[1] : null, args.Contains("--no-introspect", StringComparer.OrdinalIgnoreCase));
        return;
    }
    if (command == "test-connection")
    {
        Console.WriteLine("Testing connection...");
        var dataverse = provider.GetRequiredService<IDataverse>();
        var tenant = dataverse.DefaultTenant; // Uses default or throws
        
        Console.WriteLine($"Tenant: {tenant.TenantKey}");
        // We need a simple query to test.
        // Try SELECT 1 using raw SQL since specific tables might not exist
        var scope = (TenantScope)tenant; // Cast to access internal/low-level if needed? No, use ExecuteScalarAsync from interface
        
        var res = await tenant.ExecuteScalarAsync<int>("SELECT 1");
        Console.WriteLine(res == 1 ? "Connection Successful!" : "Connection Failed (Unexpected result)");
    }
    else if (command == "schema")
    {
        Console.WriteLine("Scanning assembly for NavTable attributes...");
        // Assuming models are in Tyresoles.Sql or another assembly loaded.
        // Currently we only have Tyresoles.Sql.
        var asm = typeof(IDataverse).Assembly; 
        
        var tables = asm.GetTypes()
            .Where(t => t.GetCustomAttribute<NavTableAttribute>() != null)
            .Select(t => new { Type = t.Name, Attr = t.GetCustomAttribute<NavTableAttribute>() })
            .ToList();
            
        Console.WriteLine($"Found {tables.Count} tables.");
        foreach(var t in tables)
        {
            Console.WriteLine($"- {t.Type} -> {t.Attr!.Name} (Shared: {t.Attr.IsShared})");
        }
    }
    else
    {
        Console.WriteLine("Unknown command.");
    }
}
catch (Exception ex)
{
    Console.WriteLine($"Error: {ex.Message}");
    if (ex.InnerException != null) Console.WriteLine($"Inner: {ex.InnerException.Message}");
}

static void RunGenerate(string? configPath, bool noIntrospect)
{
    var baseDir = Directory.GetCurrentDirectory();
    var configFile = configPath ?? Path.Combine(baseDir, "Tyresoles.config.json");

    if (!File.Exists(configFile))
    {
        Console.WriteLine($"Config not found: {configFile}");
        Console.WriteLine("Create Tyresoles.config.json with tenant(s), Tables and SharedTables.");
        return;
    }

    var json = File.ReadAllText(configFile);
    using var doc = JsonDocument.Parse(json);
    var root = doc.RootElement;

    var tables = ModelGenerator.LoadTableList(root);
    var schemaSource = ModelGenerator.GetSchemaSource(root) ?? "Generated";
    var modelNamespace = "Dataverse." + schemaSource;
    var outputDir = Path.Combine(baseDir, "Generated");
    Directory.CreateDirectory(outputDir);

    if (!noIntrospect)
    {
        var connStr = ModelGenerator.GetConnectionString(root);
        if (!string.IsNullOrEmpty(connStr))
        {
            try
            {
                using var conn = new SqlConnection(connStr);
                foreach (var table in tables)
                {
                    ModelGenerator.FillSchema(conn, table);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"DB introspection failed: {ex.Message}. Use --no-introspect for placeholder models.");
                foreach (var table in tables)
                {
                    table.PhysicalName = table.LogicalName;
                    table.Columns.Clear();
                    table.KeyColumns.Clear();
                }
            }
        }
    }
    else
    {
        foreach (var table in tables)
        {
            table.PhysicalName = table.LogicalName;
        }
    }

    var csharp = ModelGenerator.GenerateCSharp(tables, modelNamespace);
    var outPath = Path.Combine(outputDir, "NavModels.g.cs");
    File.WriteAllText(outPath, csharp);
    var withCols = tables.Count(t => t.Columns.Count > 0);
    Console.WriteLine($"Generated {tables.Count} model(s) ({withCols} with DB columns) -> {outPath}");
}
