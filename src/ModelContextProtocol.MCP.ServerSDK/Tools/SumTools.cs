using System.ComponentModel;
using ModelContextProtocol.Server;
namespace ModelContextProtocol.MCP.ServerSDK.Tools;


[McpServerToolType]
public static class SumTools
{
    [McpServerTool, Description("Get sum of two numbers")]
    public static string GetSum([Description("firstNumber")] int a, 
        [Description("secondNumber")] int b)
    {
        return (a + b).ToString();
    }
}

