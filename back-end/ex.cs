using System;
using System.Diagnostics;
using System.IO;

class Program
{
    static void Main()
    {
        var logDir = @""d:\Work Desk\Tyresoles\back-end\Tyresoles.Web\logs"";
        var file = new DirectoryInfo(logDir).GetFiles(""\*.log"").OrderByDescending(f => f.LastWriteTime).First();
        var lines = File.ReadAllLines(file.FullName);
        for (int i = 0; i < lines.Length; i++)
        {
            if (lines[i].Contains(""Unexpected Execution Error"") || lines[i].Contains(""Exception""))
            {
                var end = Math.Min(i + 20, lines.Length);
                for (int j = i; j < end; j++) Console.WriteLine(lines[j]);
                Console.WriteLine(""------"");
                // Stop after first few
                if (i > lines.Length - 100) break;
            }
        }
    }
}
