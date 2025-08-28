using System.ClientModel;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using System.ComponentModel;
using Azure.AI.OpenAI;
using Microsoft.Extensions.Configuration;

namespace ModelContextProtocol.MCP.ClientOrchestrationSDK.Plugins;

sealed class ReaderPlugin
{
    [KernelFunction, Description("Read a file given file path")]
    public string ReadFile([Description("Path to a file to be read")] string path)
    {
        return File.ReadAllText(path);
    }

    [KernelFunction, Description("Summarize the text of a file")]
    public async Task<string> SummarizeFile([Description("The complete file text to summarize")] string text)
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

        var result = await kernel.InvokePromptAsync("Please summarize this text:\n" + text, new(settings));
        return result.ToString();
    }
}