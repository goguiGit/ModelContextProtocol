using System.Text;

namespace ModelContextProtocol.Basic.Client;

internal class Program
{
    private const string Url = "http://localhost:8888/stream";
    
    
    private static async Task Main()
    {
        using var client = new HttpClient();
        client.Timeout = Timeout.InfiniteTimeSpan;

        using var response = await client.GetAsync(Url, HttpCompletionOption.ResponseHeadersRead);
        await using var stream = await response.Content.ReadAsStreamAsync();
        using var reader = new StreamReader(stream, Encoding.UTF8);
        while (!reader.EndOfStream)
        {
            var line = await reader.ReadLineAsync();
            if (!string.IsNullOrWhiteSpace(line)) Console.WriteLine(line);
        }
    }
}