namespace ModelContextProtocol.MCP.ClientOrchestrationSDK.Extensions;

public static class DumpExtensions
{
    public static void Dump<T>(this T obj)
    {
        Console.WriteLine(obj?.ToString() ?? "null");
    }

    public static void Dump<T>(this T obj, string title)
    {
        Console.WriteLine($"--- {title} ---");
        Console.WriteLine(obj?.ToString() ?? "null");
        Console.WriteLine($"--- End of {title} ---");
    }
}