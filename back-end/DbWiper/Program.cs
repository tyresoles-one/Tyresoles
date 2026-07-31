using System;
using Microsoft.Data.SqlClient;

class Program
{
    static void Main()
    {
        string connString = "Server=tcp:10.10.10.9,1433;Database=Db_Extra;User Id=postman;Password=Tyre@$tr0ng2026;TrustServerCertificate=True";
        
        using (SqlConnection conn = new SqlConnection(connString))
        {
            conn.Open();
            Console.WriteLine("Connected to database.");

            string sql = @"
                DELETE FROM CrmCallLog;
                DELETE FROM CrmCallReminder;
                DELETE FROM CrmAgentContact;
            ";

            using (SqlCommand cmd = new SqlCommand(sql, conn))
            {
                int rows = cmd.ExecuteNonQuery();
                Console.WriteLine($"Successfully deleted {rows} rows.");
            }
        }
    }
}
