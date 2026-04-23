using System;
using System.IO;
using Microsoft.Data.Sqlite;

class Program
{
    static void Main()
    {
        string dbPath = @"C:\Users\ACER\Downloads\MLNDex\MLNDex-BE\mlndex-backend\mlndex.db";
        using (var connection = new SqliteConnection($"Data Source={dbPath}"))
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
