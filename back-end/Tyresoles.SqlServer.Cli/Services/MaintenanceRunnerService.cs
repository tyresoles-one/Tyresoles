using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using Tyresoles.SqlServer.Cli.Configuration;
using Tyresoles.SqlServer.Cli.Models;

namespace Tyresoles.SqlServer.Cli.Services;

public class MaintenanceRunnerService
{
    private readonly AppSettings _appSettings;
    private readonly DatabaseService _dbService;
    private readonly EmailService _emailService;

    public MaintenanceRunnerService(AppSettings appSettings, DatabaseService dbService, EmailService emailService)
    {
        _appSettings = appSettings;
        _dbService = dbService;
        _emailService = emailService;
    }

    public async Task RunTemplateAsync(string templatePath)
    {
        if (!File.Exists(templatePath))
        {
            throw new FileNotFoundException($"Maintenance template not found: {templatePath}");
        }

        string json = await File.ReadAllTextAsync(templatePath);
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var template = JsonSerializer.Deserialize<MaintenanceTemplate>(json, options);

        if (template == null)
        {
            throw new InvalidOperationException("Failed to parse maintenance template JSON.");
        }

        Console.WriteLine($"Starting Maintenance Plan: {template.Name}");

        foreach (var task in template.Tasks)
        {
            Console.WriteLine($"\n--- Executing Task: {task.Name} ({task.Type}) ---");

            try
            {
                await ExecuteTaskAsync(task);
                Console.WriteLine($"Task {task.Name} completed successfully.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Task {task.Name} failed: {ex.Message}");
                await _emailService.SendNotificationAsync($"Maintenance Task Failed: {task.Name}", $"Error details:\n{ex.Message}\n{ex.StackTrace}");
                
                if (template.StopOnError)
                {
                    Console.WriteLine("StopOnError is true. Halting execution.");
                    Environment.ExitCode = 1;
                    return; // Stop processing further tasks
                }
            }
        }

        Console.WriteLine($"\nMaintenance Plan {template.Name} completed.");
    }

    private async Task ExecuteTaskAsync(MaintenanceTaskStep task)
    {
        switch (task.Type.ToLower())
        {
            case "sqlscript":
                await HandleSqlScriptAsync(task.Parameters);
                break;
            case "batscript":
                await HandleBatScriptAsync(task.Parameters);
                break;
            case "dotnetmethod":
                await HandleDotnetMethodAsync(task.Parameters);
                break;
            case "emailnotification":
                await HandleEmailNotificationAsync(task.Parameters);
                break;
            case "compressfile":
                await HandleCompressFileAsync(task.Parameters);
                break;
            case "compressmultiplefiles":
                await HandleCompressMultipleFilesAsync(task.Parameters);
                break;
            case "syncfile":
                await HandleSyncFileAsync(task.Parameters);
                break;
            case "cleanupfiles":
                await HandleCleanupFilesAsync(task.Parameters);
                break;
            default:
                throw new NotSupportedException($"Task type '{task.Type}' is not supported.");
        }
    }

    private string ReplaceMacros(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return input;
        
        string result = input;
        if (result.Contains("{{CURRENT_DATE}}"))
        {
            result = result.Replace("{{CURRENT_DATE}}", DateTime.Now.ToString("yyyy-MM-dd"));
        }
        if (result.Contains("{{CURRENT_DATETIME}}"))
        {
            result = result.Replace("{{CURRENT_DATETIME}}", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
        }
        return result;
    }

    private async Task HandleSqlScriptAsync(Dictionary<string, object> parameters)
    {
        string filePath = parameters.TryGetValue("FilePath", out var fp) ? fp.ToString()! : string.Empty;
        string scriptContent = parameters.TryGetValue("ScriptContent", out var sc) ? sc.ToString()! : string.Empty;

        Dictionary<string, object>? sqlParams = null;
        if (parameters.TryGetValue("SqlParameters", out var sp) && sp is JsonElement je && je.ValueKind == JsonValueKind.Object)
        {
            sqlParams = new Dictionary<string, object>();
            foreach (var prop in je.EnumerateObject())
            {
                string valueStr = ReplaceMacros(prop.Value.ToString());
                sqlParams.Add(prop.Name, valueStr);
            }
        }

        if (!string.IsNullOrWhiteSpace(filePath))
        {
            await _dbService.ExecuteScriptAsync(filePath, sqlParams);
        }
        else if (!string.IsNullOrWhiteSpace(scriptContent))
        {
            await _dbService.ExecuteScriptContentAsync(scriptContent, sqlParams);
        }
        else
        {
            throw new ArgumentException("SqlScript task must have either 'FilePath' or 'ScriptContent' parameter.");
        }
    }

    private async Task HandleBatScriptAsync(Dictionary<string, object> parameters)
    {
        if (!parameters.TryGetValue("FilePath", out var fp) || string.IsNullOrWhiteSpace(fp.ToString()))
        {
            throw new ArgumentException("BatScript task requires a 'FilePath' parameter.");
        }

        string filePath = fp.ToString()!;
        string arguments = parameters.TryGetValue("Arguments", out var args) ? args.ToString()! : string.Empty;

        var processInfo = new ProcessStartInfo
        {
            FileName = filePath,
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using (var process = Process.Start(processInfo))
        {
            if (process == null) throw new InvalidOperationException($"Failed to start process: {filePath}");

            process.OutputDataReceived += (s, e) => { if (!string.IsNullOrEmpty(e.Data)) Console.WriteLine(e.Data); };
            process.ErrorDataReceived += (s, e) => { if (!string.IsNullOrEmpty(e.Data)) Console.WriteLine($"ERROR: {e.Data}"); };

            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            await process.WaitForExitAsync();

            if (process.ExitCode != 0)
            {
                throw new Exception($"Batch script exited with code {process.ExitCode}");
            }
        }
    }

    private Task HandleDotnetMethodAsync(Dictionary<string, object> parameters)
    {
        if (!parameters.TryGetValue("AssemblyPath", out var ap) || 
            !parameters.TryGetValue("ClassName", out var cn) || 
            !parameters.TryGetValue("MethodName", out var mn))
        {
            throw new ArgumentException("DotnetMethod task requires 'AssemblyPath', 'ClassName', and 'MethodName' parameters.");
        }

        string assemblyPath = ap.ToString()!;
        string className = cn.ToString()!;
        string methodName = mn.ToString()!;

        Console.WriteLine($"Loading assembly from {assemblyPath}...");
        var assembly = Assembly.LoadFrom(assemblyPath);

        var type = assembly.GetType(className);
        if (type == null) throw new InvalidOperationException($"Class {className} not found in assembly.");

        var method = type.GetMethod(methodName, BindingFlags.Public | BindingFlags.Static);
        if (method == null) throw new InvalidOperationException($"Public static method {methodName} not found in class {className}.");

        Console.WriteLine($"Invoking {className}.{methodName}...");
        
        // Invoke assuming method has no parameters for now
        var result = method.Invoke(null, null);

        if (result is Task t)
        {
            return t; // Await the task if it returns a Task
        }

        return Task.CompletedTask;
    }

    private async Task HandleEmailNotificationAsync(Dictionary<string, object> parameters)
    {
        if (!parameters.TryGetValue("Subject", out var subj) || !parameters.TryGetValue("Body", out var bdy))
        {
            throw new ArgumentException("EmailNotification task requires 'Subject' and 'Body' parameters.");
        }

        await _emailService.SendNotificationAsync(subj.ToString()!, bdy.ToString()!);
    }

    private string ResolveFilePath(string pathPattern)
    {
        if (!pathPattern.Contains("*") && !pathPattern.Contains("?"))
        {
            return pathPattern; // Not a wildcard
        }

        string directory = Path.GetDirectoryName(pathPattern) ?? ".";
        string searchPattern = Path.GetFileName(pathPattern);

        if (!Directory.Exists(directory))
        {
            throw new DirectoryNotFoundException($"Directory not found for pattern resolution: {directory}");
        }

        var files = Directory.GetFiles(directory, searchPattern);
        if (files.Length == 0)
        {
            throw new FileNotFoundException($"No files matched the pattern: {pathPattern}");
        }

        // Get the latest modified file
        Array.Sort(files, (a, b) => File.GetLastWriteTime(b).CompareTo(File.GetLastWriteTime(a)));
        
        string resolvedPath = files[0];
        Console.WriteLine($"Resolved wildcard '{pathPattern}' to: {resolvedPath}");
        return resolvedPath;
    }

    private Task HandleCompressFileAsync(Dictionary<string, object> parameters)
    {
        if (!parameters.TryGetValue("FilePath", out var fp) || string.IsNullOrWhiteSpace(fp.ToString()))
        {
            throw new ArgumentException("CompressFile task requires a 'FilePath' parameter.");
        }

        string filePath = ResolveFilePath(fp.ToString()!);
        
        var compressService = new CompressionService();
        string zipPath = compressService.CompressFile(filePath);
        
        Console.WriteLine($"Compressed {filePath} to {zipPath}");
        return Task.CompletedTask;
    }

    private Task HandleCompressMultipleFilesAsync(Dictionary<string, object> parameters)
    {
        if (!parameters.TryGetValue("FolderPath", out var folder) || string.IsNullOrWhiteSpace(folder.ToString()))
        {
            throw new ArgumentException("CompressMultipleFiles task requires a 'FolderPath' parameter.");
        }
        if (!parameters.TryGetValue("OutputZipName", out var outZip) || string.IsNullOrWhiteSpace(outZip.ToString()))
        {
            throw new ArgumentException("CompressMultipleFiles task requires an 'OutputZipName' parameter.");
        }

        string folderPath = folder.ToString()!;
        string outputZipName = ReplaceMacros(outZip.ToString()!);
        string pattern = parameters.TryGetValue("Pattern", out var pat) && !string.IsNullOrWhiteSpace(pat.ToString()) ? ReplaceMacros(pat.ToString()!) : "*.*";

        if (!Directory.Exists(folderPath))
        {
            throw new DirectoryNotFoundException($"Directory not found: {folderPath}");
        }

        var files = Directory.GetFiles(folderPath, pattern);
        if (files.Length == 0)
        {
            Console.WriteLine($"No files found matching pattern '{pattern}' in '{folderPath}'. Skipping compression.");
            return Task.CompletedTask;
        }

        string outputZipPath = Path.Combine(folderPath, outputZipName);

        var compressService = new CompressionService();
        compressService.CompressFiles(files, outputZipPath);
        
        return Task.CompletedTask;
    }

    private async Task HandleSyncFileAsync(Dictionary<string, object> parameters)
    {
        if (!parameters.TryGetValue("FilePath", out var fp) || string.IsNullOrWhiteSpace(fp.ToString()))
        {
            throw new ArgumentException("SyncFile task requires a 'FilePath' parameter.");
        }

        string filePath = ResolveFilePath(ReplaceMacros(fp.ToString()!));
        
        // Optional override for destination
        string destination = ReplaceMacros(_appSettings.SyncSettings.DestinationPath);
        if (parameters.TryGetValue("DestinationPath", out var dp) && !string.IsNullOrWhiteSpace(dp.ToString()))
        {
            destination = ReplaceMacros(dp.ToString()!);
        }

        var syncService = new SyncService(_appSettings.SyncSettings.RcloneExecutablePath, _appSettings.SyncSettings.RcloneAdditionalArgs);
        await syncService.SyncFileAsync(filePath, destination);
        Console.WriteLine($"Synced {filePath} to {destination}");
    }

    private Task HandleCleanupFilesAsync(Dictionary<string, object> parameters)
    {
        if (!parameters.TryGetValue("FolderPath", out var fp) || string.IsNullOrWhiteSpace(fp.ToString()))
        {
            throw new ArgumentException("CleanupFiles task requires a 'FolderPath' parameter.");
        }

        string folderPath = ReplaceMacros(fp.ToString()!);
        string pattern = parameters.TryGetValue("Pattern", out var pat) && !string.IsNullOrWhiteSpace(pat.ToString()) ? ReplaceMacros(pat.ToString()!) : "*.*";
        
        int daysToKeep = 7; // default
        if (parameters.TryGetValue("DaysToKeep", out var dtk) && int.TryParse(dtk.ToString(), out int parsedDays))
        {
            daysToKeep = parsedDays;
        }

        if (!Directory.Exists(folderPath))
        {
            Console.WriteLine($"Cleanup skipped: Directory not found '{folderPath}'");
            return Task.CompletedTask;
        }

        DateTime cutoffDate = DateTime.Now.AddDays(-daysToKeep);
        var files = Directory.GetFiles(folderPath, pattern);
        int deletedCount = 0;

        foreach (var file in files)
        {
            var fileInfo = new FileInfo(file);
            if (fileInfo.LastWriteTime < cutoffDate)
            {
                try
                {
                    fileInfo.Delete();
                    Console.WriteLine($"Deleted old file: {file}");
                    deletedCount++;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to delete file {file}: {ex.Message}");
                }
            }
        }

        Console.WriteLine($"Cleanup completed. Deleted {deletedCount} files older than {daysToKeep} days matching pattern '{pattern}'.");
        return Task.CompletedTask;
    }
}
