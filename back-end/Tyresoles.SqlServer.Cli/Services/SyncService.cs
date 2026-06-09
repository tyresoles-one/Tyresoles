using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace Tyresoles.SqlServer.Cli.Services;

public class SyncService
{
    private readonly string _rcloneExecutablePath;
    private readonly string _additionalArgs;

    public SyncService(string rcloneExecutablePath, string additionalArgs = "")
    {
        _rcloneExecutablePath = string.IsNullOrWhiteSpace(rcloneExecutablePath) ? "rclone" : rcloneExecutablePath;
        _additionalArgs = additionalArgs ?? "";
    }

    public async Task SyncFileAsync(string sourceFilePath, string destinationDirectory)
    {
        if (!File.Exists(sourceFilePath))
        {
            throw new FileNotFoundException($"Source file to sync not found: {sourceFilePath}");
        }

        Console.WriteLine($"Syncing {sourceFilePath} to {destinationDirectory} using rclone...");

        // rclone copy command: rclone copy "C:\source\file.zip" "gdrive:SqlBackups"
        string arguments = $"copy \"{sourceFilePath}\" \"{destinationDirectory}\" {_additionalArgs}".Trim();

        var processInfo = new ProcessStartInfo
        {
            FileName = _rcloneExecutablePath,
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using (var process = Process.Start(processInfo))
        {
            if (process == null)
            {
                throw new InvalidOperationException("Failed to start rclone process.");
            }

            process.OutputDataReceived += (sender, args) =>
            {
                if (!string.IsNullOrEmpty(args.Data)) Console.WriteLine(args.Data);
            };
            process.ErrorDataReceived += (sender, args) =>
            {
                if (!string.IsNullOrEmpty(args.Data)) Console.WriteLine($"ERROR: {args.Data}");
            };

            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            await process.WaitForExitAsync();

            if (process.ExitCode != 0)
            {
                throw new Exception($"rclone failed with exit code {process.ExitCode}");
            }
        }

        Console.WriteLine("Sync completed successfully.");
    }
}
