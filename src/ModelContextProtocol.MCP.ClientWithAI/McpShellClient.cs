using Azure.AI.OpenAI;
using Microsoft.Extensions.Configuration;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.AzureOpenAI;
using System.ClientModel;
using System.Text;
using System.Text.Json;

namespace ModelContextProtocol.MCP.ClientWithAI;

public class McpShellClient
{
    private static readonly HttpClient Http = new()
    {
        Timeout = Timeout.InfiniteTimeSpan
    };

    public static async Task Main()
    {
        var cts = new CancellationTokenSource();
        var readyTcs = new TaskCompletionSource();
        var kernel = GetKernel();

        _ = Task.Run(() => ReadStream(cts.Token, readyTcs), cts.Token);

        await Post(new { jsonrpc = "2.0", id = 1, method = "initialize", @params = new { } });
        await Post(new { jsonrpc = "2.0", method = "notifications/initialized" });

        await readyTcs.Task;
        
        Console.InputEncoding = Encoding.UTF8;
        Console.Write("shell> ");
        
        const int nextId = 2;

        while (true)
        {
            var cmd = Console.ReadLine();
            if (string.IsNullOrWhiteSpace(cmd) || cmd.Equals("quit", StringComparison.OrdinalIgnoreCase))
                break;

            var settings = new AzureOpenAIPromptExecutionSettings
            {
                Temperature = 0.2f,
                MaxTokens = 512
            };

            var history = new ChatHistory();
            history.AddSystemMessage("""
                                        You need to understand users intent and invoke a tool from a specified list of tools. 
                                        You need to return the tool's name and any parameters on how to invoke it in the following format:
                                        Tools available to you are :
                                          1.weather accepts town as parameter (format: 1.weather-<TownNameNoSpaces>)
                                          2.time accepts no parameters       (format: 2.time)
                                          3.shell accepts shell command      (format: 3.shell-<command>)
                                        ###Example user query: I need weather in Las Vegas
                                        ###Example output: 1.weather-LasVegas
                                        IMPORTANT: Return ONLY the tool pattern, no explanations.
                """);
            history.AddUserMessage(cmd);

            var chatCompletionService = kernel.GetRequiredService<IChatCompletionService>();
            ChatMessageContent result;
            try
            {
                result = await chatCompletionService.GetChatMessageContentAsync(history, settings, kernel, cancellationToken: cts.Token);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\n[AI Error] {ex.Message}");
                Console.Write("shell> ");
                continue;
            }

            var aiText = result.Content?.Trim();
            if (string.IsNullOrEmpty(aiText))
            {
                Console.WriteLine("\n[AI] (respuesta vacía)");
                Console.Write("shell> ");
                continue;
            }

            // Siempre mostrar lo que devolvió el modelo para depuración
            Console.WriteLine($"\n[AI] {aiText}");

            // Normalizamos
            var normalized = aiText.ToLowerInvariant();

            // Patrones esperados:
            // 1.weather-<city>
            // 2.time
            // 3.shell-<command>
            if (normalized.StartsWith("1.weather-"))
            {
                var city = aiText.Split('-', 2, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Last();
                if (string.IsNullOrWhiteSpace(city))
                {
                    Console.WriteLine("[Parser] Ciudad ausente en weather-...");
                    Console.Write("shell> ");
                    continue;
                }

                // Convertimos posibles espacios (si el modelo los deja) a algo aceptable
                city = city.Replace(" ", "");
                // Construimos comando shell que consulta un servicio simple (wttr.in)
                var command = BuildWeatherCommand(city);
                await SendShellToolCall(command, nextId);
            }
            else if (normalized.StartsWith("2.time"))
            {
                var command = BuildTimeCommand();
                await SendShellToolCall(command, nextId);
            }
            else if (normalized.Contains("shell"))
            {
                // Aceptar tanto "3.shell-" como cualquier variante que contenga shell
                var parts = aiText.Split('-', 2, StringSplitOptions.TrimEntries);
                if (parts.Length == 2)
                {
                    await SendShellToolCall(parts[1], nextId);
                }
                else
                {
                    Console.WriteLine("[Parser] Formato shell inválido.");
                }
            }
            else
            {
                Console.WriteLine("[Parser] No coincide con ningún tool reconocido.");
            }

            Console.Write("shell> ");
        }

        await cts.CancelAsync();
    }

    private static string BuildWeatherCommand(string city)
    {
        // curl disponible en Win11+ normalmente; si no, podrías reemplazar por PowerShell Invoke-WebRequest
        return OperatingSystem.IsWindows()
            ? $"curl -s wttr.in/{city}?format=3"
            : $"curl -s 'wttr.in/{city}?format=3'";
    }

    private static string BuildTimeCommand()
        => OperatingSystem.IsWindows()
            ? "time /T"
            : "date +%T";

    private static async Task SendShellToolCall(string command, int nextId)
    {
        await Post(new
        {
            jsonrpc = "2.0",
            id = nextId++,
            method = "tools/call",
            @params = new
            {
                name = "runShell",
                arguments = new { command }
            }
        });
    }

    private static Kernel GetKernel()
    {
        var config = new ConfigurationBuilder()
            .AddUserSecrets("SemanticKernel.Secrets")
            .Build();

        var endpoint = new Uri(config["EndPointUrl"]);
        var model = config["Model"];
        var deploymentName = config["DeploymentName"];
        var openIdKey = config["OpenIdKey"];

        var kernel = Kernel.CreateBuilder()
            .AddAzureOpenAIChatCompletion(deploymentName, new AzureOpenAIClient(endpoint, new ApiKeyCredential(openIdKey)), modelId: model)
            .Build();
        return kernel;
    }

    private static async Task Post(object jsonObj) =>
        await Http.PostAsync("http://localhost:8888/messages/",
            new StringContent(JsonSerializer.Serialize(jsonObj), Encoding.UTF8, "application/json"));

    private static async Task ReadStream(CancellationToken token, TaskCompletionSource ready)
    {
        await using var stream = await Http.GetStreamAsync("http://localhost:8888/stream/", token);
        using var reader = new StreamReader(stream, Encoding.UTF8);
        var buf = new StringBuilder();
        while (!reader.EndOfStream && !token.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(token);

            if (string.IsNullOrEmpty(line))
            {
                if (buf.Length == 0) continue;
                var root = JsonDocument.Parse(buf.ToString()).RootElement;
                buf.Clear();

                if (root.TryGetProperty("result", out var res) &&
                    res.TryGetProperty("serverInfo", out var si))
                {
                    Console.WriteLine($"[Initialized] {si.GetProperty("name").GetString()} v{si.GetProperty("version").GetString()}");
                    ready.TrySetResult();
                }
                else if (root.TryGetProperty("result", out var res2) &&
                         res2.TryGetProperty("content", out var cont))
                {
                    var text = cont[0].GetProperty("text").GetString();
                    Console.WriteLine("\n--- Shell Output ---");
                    Console.WriteLine(text);
                    Console.WriteLine("--------------------");
                }
                Console.Write("shell> ");
                continue;
            }
            if (line.StartsWith("data: "))
                buf.Append(line[6..]);
        }
    }
}