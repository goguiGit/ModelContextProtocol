using System.ClientModel;
using System.ComponentModel;
using Azure.AI.OpenAI;
using Microsoft.Extensions.Configuration;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.OpenAI;

namespace ModelContextProtocol.MCP.ClientConcurrentOrchestrationSDK.Plugins;

public sealed class CollectorPlugin
{
    [KernelFunction, Description("Given many ideas, combine their aspects and come up with the best 3")]
    public async Task<string> RefineIdeas([Description("Many Ideas from differnet experts")] string text)
    {
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
        var kernel = builder.Build();
        
        OpenAIPromptExecutionSettings settings = new OpenAIPromptExecutionSettings
        {
            Temperature = 0.4,
            MaxTokens = 10000,

        };

        var result = await kernel.InvokePromptAsync($"Given ideas from different experts, combine and refine them and propose 3 best business ideas!IDEAS:{text}", new(settings));
        return result.ToString();
    }

}