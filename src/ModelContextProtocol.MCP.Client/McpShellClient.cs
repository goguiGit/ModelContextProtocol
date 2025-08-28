using System.Text;
using System.Text.Json;

namespace ModelContextProtocol.MCP.Client;

public class McpShellClient
{
    private static readonly HttpClient Http = new() { 
        Timeout = Timeout.InfiniteTimeSpan };

    public static async Task Main()
    {
        var cts = new CancellationTokenSource();
        var readyTcs = new TaskCompletionSource();

        //start SSE reader (background)
        _ = Task.Run(() => ReadStream(cts.Token, readyTcs), cts.Token);

        //MCP handshake --------------------------------------------------
        await Post(new { jsonrpc = "2.0", id = 1, method = "initialize", @params = new { } });
        await Post(new { jsonrpc = "2.0", method = "notifications/initialized" });

        // wait until server responds to initialize
        await readyTcs.Task; // blocks here until SSE reader signals

        Console.Write("shell> ");
        var nextId = 2;
        
        while (true)
        {
            var cmd = Console.ReadLine();
            if (string.IsNullOrWhiteSpace(cmd) || cmd.Equals("quit", StringComparison.OrdinalIgnoreCase))
                break;

            await Post(new
            {
                jsonrpc = "2.0",
                id = nextId++,
                method = "tools/call",
                @params = new
                {
                    name = "runShell",
                    arguments = new { command = cmd }
                }
            });
        }
        await cts.CancelAsync(); // stop SSE task
    }

    private static async Task Post(object jsonObj) =>
        await Http.PostAsync("http://localhost:8888/messages/",
            new StringContent(JsonSerializer.Serialize(jsonObj), Encoding.UTF8, "application/json"));


    // ---------- SSE reader ---------------------------------------------------
    static async Task ReadStream(CancellationToken token, TaskCompletionSource ready)
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

                // ---------- initialize result ----------
                if (root.TryGetProperty("result", out var res) &&
                    res.TryGetProperty("serverInfo", out var si))
                {
                    Console.WriteLine($"[Initialized] {si.GetProperty("name").GetString()} v{si.GetProperty("version").GetString()}");
                    ready.TrySetResult();  // unblock Main()
                }
                
                // ---------- tool call result ----------
                else if (root.TryGetProperty("result", out var res2) &&
                         res2.TryGetProperty("content", out var cont))
                {
                    var text = cont[0].GetProperty("text").GetString();
                    Console.WriteLine("\n--- Shell Output ---");
                    Console.WriteLine(text);
                    Console.WriteLine("--------------------");
                    Console.Write("shell> ");
                }
                continue;
            }

            if (line.StartsWith("data: "))
                buf.Append(line[6..]);
        }
    }
}