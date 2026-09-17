using System;
using System.IO;
using System.Linq;
using Microsoft.Data.SqlClient;

class Program
{
    static void Main()
    {
        string connString = "Server=tcp:10.10.10.9,1433;Database=Db_Live;User Id=postman;Password=Tyre@$tr0ng2026;TrustServerCertificate=True;Pooling=true;Connection Timeout=30;Command Timeout=300;";
        
        string invPath = File.Exists("invoices.txt") ? "invoices.txt" : Path.Combine("RunSql", "invoices.txt");
        string crnPath = File.Exists("crmemos.txt") ? "crmemos.txt" : Path.Combine("RunSql", "crmemos.txt");
        string rawInvoices = File.ReadAllText(invPath);
        string rawCrMemos = File.ReadAllText(crnPath);

        var invoices = rawInvoices.Split(new[] { ',', ' ', '\r', '\n', '\t' }, StringSplitOptions.RemoveEmptyEntries).Distinct().ToList();
        var crMemos = rawCrMemos.Split(new[] { ',', ' ', '\r', '\n', '\t' }, StringSplitOptions.RemoveEmptyEntries).Distinct().ToList();

        Console.WriteLine($"Parsed {invoices.Count} Invoices and {crMemos.Count} Credit Memos.");

        using var conn = new SqlConnection(connString);
        conn.Open();

        string invTable = "[dbo].[Tyresoles (India) Pvt_ Ltd_$Sales Invoice Header]";
        string crnTable = "[dbo].[Tyresoles (India) Pvt_ Ltd_$Sales Cr_Memo Header]";

        int updatedInvoices = 0;
        foreach (var batch in invoices.Chunk(500))
        {
            var paramNames = batch.Select((_, i) => $"@p{i}").ToArray();
            string inClause = string.Join(",", paramNames);
            string sql = $"UPDATE {invTable} SET [E-Inv Skip] = 1 WHERE [No_] IN ({inClause})";

            using var cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            for (int i = 0; i < batch.Length; i++)
            {
                cmd.Parameters.AddWithValue($"@p{i}", batch[i]);
            }
            updatedInvoices += cmd.ExecuteNonQuery();
        }

        int updatedCrMemos = 0;
        foreach (var batch in crMemos.Chunk(500))
        {
            var paramNames = batch.Select((_, i) => $"@p{i}").ToArray();
            string inClause = string.Join(",", paramNames);
            string sql = $"UPDATE {crnTable} SET [E-Inv Skip] = 1 WHERE [No_] IN ({inClause})";

            using var cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            for (int i = 0; i < batch.Length; i++)
            {
                cmd.Parameters.AddWithValue($"@p{i}", batch[i]);
            }
            updatedCrMemos += cmd.ExecuteNonQuery();
        }

        Console.WriteLine($"SUCCESS: Set [E-Inv Skip] = 1 on {updatedInvoices} Invoices.");
        Console.WriteLine($"SUCCESS: Set [E-Inv Skip] = 1 on {updatedCrMemos} Credit Memos.");
    }
}

