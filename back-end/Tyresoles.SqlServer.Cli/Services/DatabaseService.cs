using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;

namespace Tyresoles.SqlServer.Cli.Services;

public class DatabaseService
{
    private readonly string _connectionString;

    public DatabaseService(string connectionString)
    {
        _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
    }

    public async Task<string> BackupDatabaseAsync(string databaseName, string backupDirectory, bool isDifferential = false)
    {
        if (!Directory.Exists(backupDirectory))
        {
            Directory.CreateDirectory(backupDirectory);
        }

        string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        string diffSuffix = isDifferential ? "_Diff" : "";
        string backupFileName = $"{databaseName}{diffSuffix}_{timestamp}.bak";
        string backupFilePath = Path.Combine(backupDirectory, backupFileName);

        string backupTypeClause = isDifferential ? "WITH DIFFERENTIAL, FORMAT, " : "WITH FORMAT, ";
        string backupQuery = $@"BACKUP DATABASE [{databaseName}] TO DISK = '{backupFilePath}' {backupTypeClause}MEDIANAME = 'DbBackup', NAME = '{(isDifferential ? "Differential" : "Full")} Backup of {databaseName}';";

        Console.WriteLine($"Starting {(isDifferential ? "differential" : "full")} backup of {databaseName} to {backupFilePath}...");

        using (var connection = new SqlConnection(_connectionString))
        {
            await connection.OpenAsync();
            using (var command = new SqlCommand(backupQuery, connection))
            {
                // Backup commands can take a while, increase timeout
                command.CommandTimeout = 3600; // 1 hour
                await command.ExecuteNonQueryAsync();
            }
        }

        Console.WriteLine("Backup completed successfully.");
        return backupFilePath;
    }

    public async Task ExecuteScriptAsync(string scriptFilePath, Dictionary<string, object>? parameters = null)
    {
        if (!File.Exists(scriptFilePath))
        {
            throw new FileNotFoundException($"SQL script not found: {scriptFilePath}");
        }

        Console.WriteLine($"Reading script: {scriptFilePath}");
        string scriptContent = await File.ReadAllTextAsync(scriptFilePath);
        
        await ExecuteScriptContentAsync(scriptContent, parameters);
    }

    public async Task ExecuteScriptContentAsync(string scriptContent, Dictionary<string, object>? parameters = null)
    {
        if (string.IsNullOrWhiteSpace(scriptContent))
            return;

        // Split script by 'GO' statements for batch execution
        var batches = Regex.Split(scriptContent, @"^\s*GO\s*$", RegexOptions.Multiline | RegexOptions.IgnoreCase);

        using (var connection = new SqlConnection(_connectionString))
        {
            await connection.OpenAsync();

            foreach (var batch in batches)
            {
                if (string.IsNullOrWhiteSpace(batch))
                    continue;

                using (var command = new SqlCommand(batch, connection))
                {
                    command.CommandTimeout = 3600; // 1 hour

                    if (parameters != null)
                    {
                        foreach (var param in parameters)
                        {
                            command.Parameters.AddWithValue(param.Key.StartsWith("@") ? param.Key : "@" + param.Key, param.Value ?? DBNull.Value);
                        }
                    }

                    await command.ExecuteNonQueryAsync();
                }
            }
        }

        Console.WriteLine("Script execution completed successfully.");
    }
}
