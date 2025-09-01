using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;

namespace ModelContextProtocol.MCP.AzureFunctions;

public class StandardFunction
{
    [Function("StandardFunction")]
    public IActionResult Run(
        [HttpTrigger(AuthorizationLevel.Function, "get", "post")]
        HttpRequest req
    )
    {
        return new OkObjectResult("Welcome to Azure Functions!");
    }
}

