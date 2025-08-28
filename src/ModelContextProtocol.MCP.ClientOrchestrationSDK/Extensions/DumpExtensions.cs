namespace ModelContextProtocol.MCP.ClientOrchestrationSDK.Extensions;

public static class DumpExtensions
{
    public static void Dump<T>(this T obj)
    {
        Console.WriteLine(obj?.ToString() ?? "null");
    }
}