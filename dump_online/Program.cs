using System;
using Sdcb.PaddleOCR.Models.Online;
using System.Reflection;

class Program {
    static void Main() {
        Console.WriteLine("Properties:");
        foreach (var p in typeof(OnlineFullModels).GetProperties(BindingFlags.Public | BindingFlags.Static)) {
            Console.WriteLine(p.Name);
        }
        Console.WriteLine("Fields:");
        foreach (var p in typeof(OnlineFullModels).GetFields(BindingFlags.Public | BindingFlags.Static)) {
            Console.WriteLine(p.Name);
        }
        Console.WriteLine("Nested Types:");
        foreach (var p in typeof(OnlineFullModels).GetNestedTypes(BindingFlags.Public | BindingFlags.Static | BindingFlags.NonPublic)) {
            Console.WriteLine(p.Name);
        }
    }
}
