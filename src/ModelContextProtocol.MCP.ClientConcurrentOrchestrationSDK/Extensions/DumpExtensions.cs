using Microsoft.SemanticKernel.ChatCompletion;

namespace ModelContextProtocol.MCP.ClientConcurrentOrchestrationSDK.Extensions;

public static class DumpExtensions
{
    public static void Dump<T>(this T obj, string v)
    {
        Console.WriteLine(obj?.ToString() ?? "null");
    }

    public static void Dump(this ChatHistory chatHistory) 
    {
        foreach (var message in chatHistory)
        {
            Console.WriteLine($"{message.Role}: {message.Content}");
        }
    }
}