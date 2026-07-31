using System;
using Microsoft.Data.SqlClient;

class Program
{
    static void Main(string[] args)
    {
        string liveStr = "Server=tcp:10.10.10.9,1433;Database=Db_Live;User Id=postman;Password=Tyre@$tr0ng2026;TrustServerCertificate=True";
        string extraStr = "Server=tcp:10.10.10.9,1433;Database=Db_Extra;User Id=postman;Password=Tyre@$tr0ng2026;TrustServerCertificate=True";

        AlterTable(liveStr, "Db_Live");
        AlterTable(extraStr, "Db_Extra");
    }

    static void AlterTable(string connStr, string dbName)
    {
        try
        {
            using (SqlConnection connection = new SqlConnection(connStr))
            {
                string alterSql = @"
IF NOT EXISTS(SELECT * FROM sys.columns WHERE Name = N'ActivityTypeId' AND Object_ID = Object_ID(N'dbo.CrmActivityOutcome'))
BEGIN
    ALTER TABLE [dbo].[CrmActivityOutcome] ADD [ActivityTypeId] INT NULL;
END

IF NOT EXISTS(SELECT * FROM sys.columns WHERE Name = N'IsPositive' AND Object_ID = Object_ID(N'dbo.CrmActivityOutcome'))
BEGIN
    ALTER TABLE [dbo].[CrmActivityOutcome] ADD [IsPositive] BIT NOT NULL DEFAULT 0;
END
";
                SqlCommand command = new SqlCommand(alterSql, connection);
                connection.Open();
                command.ExecuteNonQuery();
                Console.WriteLine($"[{dbName}] Success! Table CrmActivityOutcome altered.");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[{dbName}] Error: {ex.Message}");
        }
    }
}
