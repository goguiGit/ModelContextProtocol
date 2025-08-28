using Azure.AI.OpenAI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.Agents.Orchestration.Concurrent;
using Microsoft.SemanticKernel.Agents.Runtime.InProcess;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using ModelContextProtocol.MCP.ClientConcurrentOrchestrationSDK.Extensions;
using System.ClientModel;
using ModelContextProtocol.MCP.ClientConcurrentOrchestrationSDK.Plugins;

#pragma warning disable
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

var technologistAgentKernel = builder.Build();
var settings = new OpenAIPromptExecutionSettings
{
    Temperature = 0.2,
    MaxTokens = 10000
};

ChatCompletionAgent technologistAgent = new()
{
    Name = "Technologist",
    Instructions = "You are a tech. Your job is to come up with innovative ideas given a subject, coming from the tech perspective only.",
    Kernel = technologistAgentKernel,
    Description = "Tech whose job is to come up with innovative ideas given a subject, coming from the tech perspective only.",
    Arguments = new KernelArguments(settings)
};

var economistAgentKernel = builder.Build();
ChatCompletionAgent economistAgent = new()
{
    Name = "Economist",
    Instructions = "You are a highly skilled economist.Given a subject you need to come up with ideas to make profit from that subject, like a company or startup ideas.",
    Kernel = economistAgentKernel,
    Description = "Economist that tries to think of ways to make revenue on a given subject.",
    Arguments = new KernelArguments(settings)

};
ConcurrentOrchestration orchestration = new(technologistAgent, economistAgent)
{
    ResponseCallback = ResponseCallback,
    Description = "Coming up with ideas from different perspectives  given a subject",
    Name = "Generate business Ideas",
};

var runtime = new InProcessRuntime();

await runtime.StartAsync();
while (true)
{
    var usersRequest = Console.ReadLine();
    var result = await orchestration.InvokeAsync(usersRequest, runtime);
    var output = await result.GetValueAsync(TimeSpan.FromSeconds(60));
    var collector = new CollectorPlugin();
    
    var topIdeas = await collector.RefineIdeas(string.Join("\n\n", output.Select(text => $"{text}")));
    topIdeas.Dump("Final Ideas");
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






