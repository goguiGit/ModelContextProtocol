using System.ClientModel;
using Azure.AI.OpenAI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.Agents.Orchestration.Sequential;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel.Agents.Runtime.InProcess;
using ModelContextProtocol.MCP.ClientOrchestrationSDK.Extensions;
using ModelContextProtocol.MCP.ClientOrchestrationSDK.Plugins;

var config = new ConfigurationBuilder()
    .AddUserSecrets("SemanticKernel.Secrets")
    .Build();

var endpoint = new Uri(config["EndPointUrl"]!);
var model = config["Model"];
var deploymentName = config["DeploymentName"]!;
var openIdKey = config["OpenIdKey"]!;

var builder = Kernel.CreateBuilder()
    .AddAzureOpenAIChatCompletion(deploymentName, new AzureOpenAIClient(endpoint, new ApiKeyCredential(openIdKey)),
        modelId: model);

builder.Services.AddLogging(c => c.AddConsole().SetMinimumLevel(LogLevel.Information));
ChatHistory history = [];

//reader agent
var readerAgentKernel = builder.Build();

var settings = new OpenAIPromptExecutionSettings
{
    Temperature = 0.2,
    MaxTokens = 10000,
    FunctionChoiceBehavior = FunctionChoiceBehavior.Auto()
};

ChatCompletionAgent readerAgent = new()
{
    Name = "Reader",
    Instructions = "You are a reader agent.Your job is to read a file and process it based on available functions.",
    Kernel = readerAgentKernel,
    Description = "File Reader Agent specialized in reading text from various formats and summarizing tasks.",
    Arguments = new KernelArguments(settings)
};

readerAgent.Kernel.Plugins.Add(KernelPluginFactory.CreateFromObject(new ReaderPlugin()));

//translator agent
var translatorAgentKernel = builder.Build();
ChatCompletionAgent translatorAgent = new()
{
    Name = "Translator",
    Instructions = "You are a translator. You handle tasks related to languages processing.",
    Kernel = translatorAgentKernel,
    Description = "Translator that translate given text into languages.",
    Arguments = new KernelArguments(settings)
};

translatorAgent.Kernel.Plugins.Add(KernelPluginFactory.CreateFromObject(new TranslatePlugin()));
#pragma warning disable

//orchestration
var orchestration = new SequentialOrchestration(readerAgent, translatorAgent)
{
    ResponseCallback =  ResponseCallback,
    Description = "Process file from reading it to summary and translation",
    Name = "Read and Translate file",
};

var runtime = new InProcessRuntime();
await runtime.StartAsync();
while (true)
{
    var usersRequest = Console.ReadLine();
    var result = await orchestration.InvokeAsync(usersRequest, runtime);
    var text = await result.GetValueAsync(TimeSpan.FromSeconds(120));
    text.Dump();
    history.Dump();
    break; //if you want a single task performed
}

await runtime.RunUntilIdleAsync();
return;


ValueTask ResponseCallback(ChatMessageContent response)
{
    history.Add(response);
    return ValueTask.CompletedTask;
}