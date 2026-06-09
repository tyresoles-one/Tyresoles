using System;
using System.CommandLine;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Tyresoles.SqlServer.Cli.Configuration;
using Tyresoles.SqlServer.Cli.Services;

namespace Tyresoles.SqlServer.Cli;

class Program
{
    static async Task<int> Main(string[] args)
    {
        var rootCommand = new RootCommand("Tyresoles SQL Server Backup and Maintenance CLI");

        // Options
        var configFileOption = new Option<string>(
            name: "--config",
            description: "Path to appsettings.json file.",
            getDefaultValue: () => "appsettings.json");

        var scriptFileOption = new Option<string>(
            name: "--script",
            description: "Path to a specific SQL script file to run.");

        var dbNamesOption = new Option<string[]>(
            name: "--db",
            description: "Override database names from config. Can be specified multiple times.");

        var typeOption = new Option<string>(
            name: "--type",
            description: "Backup type: 'full' or 'differential'. Default is 'full'.",
            getDefaultValue: () => "full");

        var fileOption = new Option<string>(
            name: "--file",
            description: "File to compress or sync.");

        var templateOption = new Option<string>(
            name: "--template",
            description: "Path to a JSON maintenance template to execute multiple tasks.");

        // Commands
        var backupCommand = new Command("backup", "Backup the databases.");
        backupCommand.AddOption(configFileOption);
        backupCommand.AddOption(dbNamesOption);
        backupCommand.AddOption(typeOption);

        var maintenanceCommand = new Command("maintenance", "Execute maintenance tasks.");
        maintenanceCommand.AddOption(configFileOption);
        maintenanceCommand.AddOption(scriptFileOption);
        maintenanceCommand.AddOption(templateOption);

        var compressCommand = new Command("compress", "Compress a file into a zip archive.");
        compressCommand.AddOption(configFileOption);
        compressCommand.AddOption(fileOption);

        var syncCommand = new Command("sync", "Sync a file to the destination directory using rclone.");
        syncCommand.AddOption(configFileOption);
        syncCommand.AddOption(fileOption);

        var helpCommand = new Command("help", "Showcase all available commands and options.");

        rootCommand.AddCommand(backupCommand);
        rootCommand.AddCommand(maintenanceCommand);
        rootCommand.AddCommand(compressCommand);
        rootCommand.AddCommand(syncCommand);
        rootCommand.AddCommand(helpCommand);

        // Handlers
        helpCommand.SetHandler(() =>
        {
            rootCommand.InvokeAsync("--help").Wait();
        });

        backupCommand.SetHandler(async (configFile, dbNames, type) =>
        {
            var appSettings = LoadConfiguration(configFile);
            var emailService = new EmailService(appSettings.SmtpSettings.Host, appSettings.SmtpSettings.Port, appSettings.SmtpSettings.Username, appSettings.SmtpSettings.Password, appSettings.SmtpSettings.FromEmail, appSettings.SmtpSettings.ToEmail);
            
            var targetDbs = dbNames != null && dbNames.Length > 0 ? dbNames : appSettings.BackupSettings.Databases.ToArray();
            bool isDifferential = type?.ToLower() == "differential" || type?.ToLower() == "diff";

            if (targetDbs == null || targetDbs.Length == 0)
            {
                Console.WriteLine("No databases specified for backup.");
                return;
            }

            var dbService = new DatabaseService(appSettings.ConnectionStrings.DefaultConnection);
            foreach (var targetDb in targetDbs)
            {
                try
                {
                    string backupPath = await dbService.BackupDatabaseAsync(targetDb, appSettings.BackupSettings.BackupDirectory, isDifferential);
                    await emailService.SendNotificationAsync("Backup Successful", $"The database {targetDb} was backed up successfully to {backupPath}. Type: {(isDifferential ? "Differential" : "Full")}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error backing up {targetDb}: {ex.Message}");
                    await emailService.SendNotificationAsync("Backup Failed", $"The backup of database {targetDb} failed.\n\nError: {ex.Message}\n{ex.StackTrace}");
                    Environment.ExitCode = 1;
                }
            }
        }, configFileOption, dbNamesOption, typeOption);

        maintenanceCommand.SetHandler(async (configFile, scriptFile, templateFile) =>
        {
            var appSettings = LoadConfiguration(configFile);
            var emailService = new EmailService(appSettings.SmtpSettings.Host, appSettings.SmtpSettings.Port, appSettings.SmtpSettings.Username, appSettings.SmtpSettings.Password, appSettings.SmtpSettings.FromEmail, appSettings.SmtpSettings.ToEmail);
            var dbService = new DatabaseService(appSettings.ConnectionStrings.DefaultConnection);

            if (!string.IsNullOrWhiteSpace(templateFile))
            {
                var runnerService = new MaintenanceRunnerService(appSettings, dbService, emailService);
                try
                {
                    await runnerService.RunTemplateAsync(templateFile);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Template Runner Error: {ex.Message}");
                    Environment.ExitCode = 1;
                }
                return;
            }

            try
            {
                if (!string.IsNullOrWhiteSpace(scriptFile))
                {
                    await dbService.ExecuteScriptAsync(scriptFile);
                    await emailService.SendNotificationAsync("Maintenance Script Successful", $"The script {scriptFile} was executed successfully.");
                }
                else
                {
                    var scriptsDir = appSettings.MaintenanceSettings.ScriptsDirectory;
                    if (Directory.Exists(scriptsDir))
                    {
                        foreach (var file in Directory.GetFiles(scriptsDir, "*.sql"))
                        {
                            await dbService.ExecuteScriptAsync(file);
                        }
                        await emailService.SendNotificationAsync("Maintenance Scripts Successful", $"All scripts in {scriptsDir} were executed successfully.");
                    }
                    else
                    {
                        throw new DirectoryNotFoundException($"Scripts directory not found: {scriptsDir}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
                await emailService.SendNotificationAsync("Maintenance Failed", $"Maintenance execution failed.\n\nError: {ex.Message}\n{ex.StackTrace}");
                Environment.ExitCode = 1;
            }
        }, configFileOption, scriptFileOption, templateOption);

        compressCommand.SetHandler(async (configFile, file) =>
        {
            var appSettings = LoadConfiguration(configFile);
            var emailService = new EmailService(appSettings.SmtpSettings.Host, appSettings.SmtpSettings.Port, appSettings.SmtpSettings.Username, appSettings.SmtpSettings.Password, appSettings.SmtpSettings.FromEmail, appSettings.SmtpSettings.ToEmail);

            if (string.IsNullOrWhiteSpace(file))
            {
                Console.WriteLine("Error: Please specify a file to compress using --file");
                return;
            }

            try
            {
                var compressService = new CompressionService();
                string zipPath = compressService.CompressFile(file);
                await emailService.SendNotificationAsync("Compression Successful", $"The file {file} was compressed to {zipPath}.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
                await emailService.SendNotificationAsync("Compression Failed", $"Compression of {file} failed.\n\nError: {ex.Message}\n{ex.StackTrace}");
                Environment.ExitCode = 1;
            }
        }, configFileOption, fileOption);

        syncCommand.SetHandler(async (configFile, file) =>
        {
            var appSettings = LoadConfiguration(configFile);
            var emailService = new EmailService(appSettings.SmtpSettings.Host, appSettings.SmtpSettings.Port, appSettings.SmtpSettings.Username, appSettings.SmtpSettings.Password, appSettings.SmtpSettings.FromEmail, appSettings.SmtpSettings.ToEmail);
            
            if (string.IsNullOrWhiteSpace(file))
            {
                Console.WriteLine("Error: Please specify a file to sync using --file");
                return;
            }

            try
            {
                var syncService = new SyncService(appSettings.SyncSettings.RcloneExecutablePath, appSettings.SyncSettings.RcloneAdditionalArgs);
                await syncService.SyncFileAsync(file, appSettings.SyncSettings.DestinationPath);
                await emailService.SendNotificationAsync("Sync Successful", $"The file {file} was synced successfully.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
                await emailService.SendNotificationAsync("Sync Failed", $"Sync of {file} failed.\n\nError: {ex.Message}\n{ex.StackTrace}");
                Environment.ExitCode = 1;
            }
        }, configFileOption, fileOption);

        return await rootCommand.InvokeAsync(args);
    }

    private static AppSettings LoadConfiguration(string configFilePath)
    {
        if (!File.Exists(configFilePath))
        {
            Console.WriteLine($"Warning: Config file '{configFilePath}' not found. Using defaults.");
            return new AppSettings();
        }

        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile(configFilePath, optional: true, reloadOnChange: true)
            .Build();

        var appSettings = new AppSettings();
        configuration.Bind(appSettings);
        return appSettings;
    }
}
