using System.Net;
using System.Text;

namespace ModelContextProtocol.Basic.Server;

public class Program
{
    private const int Port = 8888;
    private const string ServerName = "Simple Server";

    private static async Task Main()
    {
        var listener = new HttpListener();
        listener.Prefixes.Add($"http://localhost:{Port}/");
        listener.Start();
        Console.WriteLine($"Listening on {Port}");
        while (true)
        {
            var ctx = await listener.GetContextAsync();
            if (ctx.Request.Url?.AbsolutePath == "/stream")
            {
                ctx.Response.ContentType = "text/event-stream";
                ctx.Response.Headers.Add("Cache-Control", "no-cache");
                _ = Task.Run(async () => {
                    var i = 0;
                    try
                    {
                        while (listener.IsListening && ctx.Response.OutputStream.CanWrite)
                        {
                            var msg = $"data: Hello from {ServerName}------ - [{i++}]\n\n";
                            var buffer = Encoding.UTF8.GetBytes(msg);
                            await ctx.Response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
                            await ctx.Response.OutputStream.FlushAsync();
                            await Task.Delay(2000);
                        }
                    }
                    catch (Exception ex)
                    {
                        //log something...
                    }
                    finally
                    {
                        ctx.Response.Close();
                    }
                });
            }
            else
            {
                var buf = Encoding.UTF8.GetBytes("hello!");
                await ctx.Response.OutputStream.WriteAsync(buf, 0, buf.Length);
                ctx.Response.Close();
            }
        }
    }

}