using System.ComponentModel;
using ModelContextProtocol.Server;

namespace ModelContextProtocol.MCP.ServerSDK.Tools;

[McpServerToolType]
public static class GetSecretNumber
{
    [McpServerTool, Description("get secret number")]
    public static string GetSecret(string message) => "42";
}