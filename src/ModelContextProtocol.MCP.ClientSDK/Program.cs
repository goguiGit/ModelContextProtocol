using Azure.AI.OpenAI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using ModelContextProtocol.Client;
using System.ClientModel;


var config = new ConfigurationBuilder()
    .AddUserSecrets("SemanticKernel.Secrets")
    .Build();

var endpoint = new Uri(config["EndPointUrl"]);
var model = config["Model"];
var deploymentName = config["DeploymentName"];
var openIdKey = config["OpenIdKey"];
var builder = Kernel.CreateBuilder()
    .AddAzureOpenAIChatCompletion(deploymentName, new AzureOpenAIClient(endpoint, new ApiKeyCredential(openIdKey)),
        modelId: model);
    
builder.Services.AddLogging(c => c.AddConsole().SetMinimumLevel(LogLevel.Information));

var kernel = builder.Build();

//retrieve list of tools as before
var (command, arguments) = GetCommandAndArguments(args);
static (string command, string[] arguments) GetCommandAndArguments(string[] args)
{
    return args switch
    {
        [var script] when script.EndsWith(".py") => ("python", args),
        [var script] when script.EndsWith(".js") => ("node", args),
        [var script] when Directory.Exists(script) || (File.Exists(script) && script.EndsWith(".csproj"))
            => ("dotnet", ["run", "--project", script, "--no-build"]),
        _ => throw new NotSupportedException("Supported scripts: .py, .js, .csproj")
    };
}

var clientTransport = new StdioClientTransport(new StdioClientTransportOptions
{
    Name = "",
    Command = command,
    Arguments = arguments,
});

await using var mcpClient = await McpClientFactory.CreateAsync(clientTransport);
var name = mcpClient.ServerInfo.Name;
Console.WriteLine(name);
var tools = await mcpClient.ListToolsAsync();
foreach (var tool in tools)
{
    Console.WriteLine($"{tool.Name} ({tool.Description})");
}
kernel.Plugins.AddFromFunctions("Math", tools.Select(aiFunction => aiFunction.AsKernelFunction()));

var settings = new OpenAIPromptExecutionSettings
{
    Temperature = 0.2,
    MaxTokens = 10000,
    FunctionChoiceBehavior = FunctionChoiceBehavior.Auto()
};

while (true)
{
    var prompt = Console.ReadLine();
    var result = await kernel.InvokePromptAsync(prompt!, new KernelArguments(settings));
    Console.WriteLine($"\n\n{prompt}\n{result}");
}

