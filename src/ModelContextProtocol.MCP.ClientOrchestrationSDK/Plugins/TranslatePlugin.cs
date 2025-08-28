using System.ClientModel;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using System.ComponentModel;
using Azure.AI.OpenAI;
using Microsoft.Extensions.Configuration;

namespace ModelContextProtocol.MCP.ClientOrchestrationSDK.Plugins;

sealed class TranslatePlugin
{
    [KernelFunction, Description("Translate given text to a given language")]
    public async Task<string> TranslateText([Description("text to translate")] string text, [Description("language to translate to")] string language)
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
        var settings = new OpenAIPromptExecutionSettings
        {
            Temperature = 0.2,
            MaxTokens = 10000,
        };
        var result = await kernel.InvokePromptAsync("Please translate this  text:\n" + text + " TO THIS LANGUAGE: " + language, new(settings));
        return result.ToString();
    }
}