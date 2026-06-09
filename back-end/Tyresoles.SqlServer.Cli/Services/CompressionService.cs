using System;
using System.IO;
using System.IO.Compression;

namespace Tyresoles.SqlServer.Cli.Services;

public class CompressionService
{
    public string CompressFile(string filePath)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"File to compress not found: {filePath}");
        }

        string directory = Path.GetDirectoryName(filePath)!;
        string fileName = Path.GetFileNameWithoutExtension(filePath);
        string zipFilePath = Path.Combine(directory, $"{fileName}.zip");

        Console.WriteLine($"Compressing {filePath} to {zipFilePath}...");

        using (var zipToOpen = new FileStream(zipFilePath, FileMode.Create))
        {
            using (var archive = new ZipArchive(zipToOpen, ZipArchiveMode.Create))
            {
                archive.CreateEntryFromFile(filePath, Path.GetFileName(filePath));
            }
        }

        Console.WriteLine("Compression completed successfully.");
        return zipFilePath;
    }

    public string CompressFiles(string[] filePaths, string outputZipPath)
    {
        if (filePaths == null || filePaths.Length == 0)
        {
            throw new ArgumentException("No files provided for compression.");
        }

        Console.WriteLine($"Compressing {filePaths.Length} files into {outputZipPath}...");

        using (var zipToOpen = new FileStream(outputZipPath, FileMode.Create))
        {
            using (var archive = new ZipArchive(zipToOpen, ZipArchiveMode.Create))
            {
                foreach (var filePath in filePaths)
                {
                    if (File.Exists(filePath))
                    {
                        archive.CreateEntryFromFile(filePath, Path.GetFileName(filePath));
                        Console.WriteLine($"  Added {Path.GetFileName(filePath)}");
                    }
                    else
                    {
                        Console.WriteLine($"  Warning: File not found {filePath}, skipping.");
                    }
                }
            }
        }

        Console.WriteLine("Compression completed successfully.");
        return outputZipPath;
    }
}
