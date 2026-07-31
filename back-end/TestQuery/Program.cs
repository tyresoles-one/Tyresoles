using System;
using Microsoft.Data.SqlClient;

class Program
{
    static void Main()
    {
        string connStr = "Server=tcp:10.10.10.9,1433;Database=Db_Extra;User Id=postman;Password=Tyre@$tr0ng2026;TrustServerCertificate=True;Pooling=true;Connection Timeout=30;Command Timeout=300;Application Name=Tyresoles.API";
        using (var conn = new SqlConnection(connStr))
        {
            conn.Open();
            
            // 1. Count total contacts
            int totalContacts = 0;
            using (var cmd = new SqlCommand("SELECT COUNT(*) FROM dbo.CrmContact", conn))
            {
                totalContacts = (int)cmd.ExecuteScalar();
            }
            Console.WriteLine($"Total Contacts: {totalContacts}");

            // 2. Count active contacts
            int activeContacts = 0;
            using (var cmd = new SqlCommand("SELECT COUNT(*) FROM dbo.CrmContact WHERE IsActive = 1", conn))
            {
                activeContacts = (int)cmd.ExecuteScalar();
            }
            Console.WriteLine($"Active Contacts: {activeContacts}");

            // 3. Count unassigned contacts
            int unassignedContacts = 0;
            using (var cmd = new SqlCommand("SELECT COUNT(*) FROM dbo.CrmContact WHERE (AssignedTo IS NULL OR AssignedTo = '') AND IsActive = 1", conn))
            {
                unassignedContacts = (int)cmd.ExecuteScalar();
            }
            Console.WriteLine($"Unassigned Active Contacts: {unassignedContacts}");

            // 4. Assignments breakdown in CrmContact
            Console.WriteLine("\nCrmContact Assignments:");
            using (var cmd = new SqlCommand("SELECT AssignedTo, COUNT(*) as Count FROM dbo.CrmContact GROUP BY AssignedTo", conn))
            using (var reader = cmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    string assignedTo = reader["AssignedTo"] == DBNull.Value || string.IsNullOrEmpty(reader["AssignedTo"].ToString()) 
                        ? "[UNASSIGNED]" 
                        : reader["AssignedTo"].ToString();
                    Console.WriteLine($" - {assignedTo}: {reader["Count"]}");
                }
            }

            // 5. Total records in CrmAgentContact
            int totalAgentContacts = 0;
            using (var cmd = new SqlCommand("SELECT COUNT(*) FROM dbo.CrmAgentContact", conn))
            {
                totalAgentContacts = (int)cmd.ExecuteScalar();
            }
            Console.WriteLine($"\nTotal CrmAgentContact Records: {totalAgentContacts}");

            // 6. Active allocations in CrmAgentContact
            int activeAgentContacts = 0;
            using (var cmd = new SqlCommand("SELECT COUNT(*) FROM dbo.CrmAgentContact WHERE DeallocatedAt IS NULL", conn))
            {
                activeAgentContacts = (int)cmd.ExecuteScalar();
            }
            Console.WriteLine($"Active CrmAgentContact Allocations: {activeAgentContacts}");
        }
    }
}
