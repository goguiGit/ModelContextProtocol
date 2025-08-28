using System.Diagnostics;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;

namespace ModelContextProtocol.MCP.Server;

public class McpShellServer
{
    private static bool _clientConnected;
    private static StreamWriter? _sse;
    private const int Port = 8888;

    private static void Main()
    {
        var http = new HttpListener();
        http.Prefixes.Add($"http://localhost:{Port}/stream/");   // GET  (SSE)
        http.Prefixes.Add($"http://localhost:{Port}/messages/"); // POST (JSON‑RPC)
        http.Start();

        Console.WriteLine($"MCP‑Shell server:\n  SSE  → http://localhost:{Port}/stream/\n  POST → http://localhost:{Port}/messages/\n");

        while (true)
        {
            var ctx = http.GetContext();
            if (ctx.Request.HttpMethod == "GET") _ = Task.Run(() => HandleSse(ctx));
            else if (ctx.Request.HttpMethod == "POST") HandleRpc(ctx);
            else
            {
                ctx.Response.StatusCode = 405;
                ctx.Response.Close();
            }
        }

    }
    
    private static void HandleSse(HttpListenerContext ctx)
    {
        if (_clientConnected)
        {
            ctx.Response.StatusCode = 409;
            ctx.Response.Close();
            return;
        }

        _clientConnected = true;
        ctx.Response.ContentType = "text/event-stream";
        ctx.Response.Headers.Add("Cache-Control", "no-cache");
        ctx.Response.SendChunked = true;

        _sse = new StreamWriter(ctx.Response.OutputStream, Encoding.UTF8) { AutoFlush = true };
        Console.WriteLine("SSE client connected");

        try
        {
            while (_clientConnected && ctx.Response.OutputStream.CanWrite) Thread.Sleep(1000);
        }
        catch
        {
            // ignored
        }
        finally
        {
            _clientConnected = false;
            _sse.Dispose();
            ctx.Response.Close();
            Console.WriteLine("SSE client disconnected");
        }
    }

    private static void HandleRpc(HttpListenerContext ctx)
    {
        try
        {
            using var r = new StreamReader(ctx.Request.InputStream, ctx.Request.ContentEncoding);
            var body = r.ReadToEnd();

            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;
            var mth = root.GetProperty("method").GetString()!;
            var idElem = root.TryGetProperty("id", out var tmp) ? tmp : default;

            if (mth == "initialize")
            {
                SendSse(new
                {
                    jsonrpc = "2.0",
                    id = idElem,
                    result = new
                    {
                        protocolVersion = "2025-05-10",
                        capabilities = new { },
                        serverInfo = new { name = "MinimalShellServer", version = "1.0" }
                    }
                });
            }
            else if (mth == "notifications/initialized")
            {
                /* no response required */
            }
            else if (mth == "tools/call")
            {
                var tool = root.GetProperty("params").GetProperty("name").GetString()!;
                if (tool != "runShell")
                {
                    ctx.Response.StatusCode = 400;
                    ctx.Response.Close();
                    return;
                }

                var cmd = root.GetProperty("params").GetProperty("arguments").GetProperty("command").GetString();
                var output = RunShell(cmd);
                SendSse(new
                {
                    jsonrpc = "2.0",
                    id = idElem,
                    result = new
                    {
                        content = new[] { new { type = "text", text = output } },
                        isError = false
                    }
                });
            }
            else
            {
                ctx.Response.StatusCode = 400;
                ctx.Response.Close();
                return;
            }

            ctx.Response.StatusCode = 200;
            ctx.Response.Close();
        }
        catch
        {
            ctx.Response.StatusCode = 500;
            ctx.Response.Close();
        }
    }

    private static void SendSse(object obj)
    {
        if (!_clientConnected || _sse == null) return;
        _sse.Write("data: ");
        _sse.Write(JsonSerializer.Serialize(obj));
        _sse.Write("\n\n");
    }

    private static string RunShell(string cmd)
    {
        var win = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
        var psi = new ProcessStartInfo
        {
            FileName = win ? "cmd.exe" : "/bin/bash",
            Arguments = win ? $"/c {cmd}" : $"-c \"{cmd.Replace("\"", "\\\"")}\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var p = Process.Start(psi);
        var txt = p.StandardOutput.ReadToEnd() + p.StandardError.ReadToEnd();
        p.WaitForExit();
        return string.IsNullOrWhiteSpace(txt) ? "(no output)" : txt.Trim();
    }

}