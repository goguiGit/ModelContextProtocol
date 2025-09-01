using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Extensions.Mcp;

namespace ModelContextProtocol.MCP.AzureFunctions;

public class FunctionMcpTool1
{
    [Function("FunctionMcpTool1")]
    public IActionResult Run(
        [McpToolTrigger(ToolDefinitions.Tool1.Name, ToolDefinitions.Tool1.Description)]
        ToolInvocationContext context
    )
    {
        return new OkObjectResult("Hi. I'm Tool 1!");
    }
}