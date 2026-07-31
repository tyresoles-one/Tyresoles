using System;
using System.IO;
using Microsoft.Data.SqlClient;

class Program
{
    static void Main()
    {
        string connString = "Server=tcp:10.10.10.9,1433;Database=Db_Extra;User Id=postman;Password=Tyre@$tr0ng2026;TrustServerCertificate=True";
        string script = File.ReadAllText(@"d:\Work Desk\Tyresoles\back-end\RunSql\fix.sql");
        
        var statements = script.Split(new[] { "GO\r\n", "GO\n" }, StringSplitOptions.RemoveEmptyEntries);
        
        using var conn = new SqlConnection(connString);
        conn.Open();
        
        foreach (var stmt in statements)
        {
            if (string.IsNullOrWhiteSpace(stmt)) continue;
            
            try
            {
                using var cmd = conn.CreateCommand();
                cmd.CommandText = stmt;
                cmd.ExecuteNonQuery();
                Console.WriteLine("Executed batch successfully.");
            }
            catch (SqlException ex)
            {
                Console.WriteLine($"Ignored SQL error: {ex.Message}");
            }
        }
    }
}
