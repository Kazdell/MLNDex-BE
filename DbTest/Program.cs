using System;
using Microsoft.Data.SqlClient;

class Program
{
    static void Main()
    {
        string connStr = "Server=tcp:mlndex.database.windows.net,1433;Initial Catalog=mlndexdb_2026-04-13T13-50Z;Persist Security Info=False;User ID=mlndex-admin;Password=A@a12345;MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;";
        using (var connection = new SqlConnection(connStr))
        {
            connection.Open();
            var command = connection.CreateCommand();
            command.CommandText = "SELECT * FROM ModerationQueues WHERE QueueId = 10376";
            using (var reader = command.ExecuteReader())
            {
                if (reader.Read())
                {
                    for (int i = 0; i < reader.FieldCount; i++)
                    {
                        Console.WriteLine($"{reader.GetName(i)}: {reader.GetValue(i)}");
                    }
                }
                else
                {
                    Console.WriteLine("Queue 10376 not found in DB.");
                }
            }
        }
    }
}
