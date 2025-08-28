using ModelContextProtocol.Client;
//args = path to our mcp server .csproj file
var (command, arguments) = GetCommandAndArguments(args);
static (string command, string[] arguments) GetCommandAndArguments(string[] args)
{
    return args switch
    {
        [var script] when script.EndsWith(".py") => ("python", args),
        [var script] when script.EndsWith(".js") => ("node", args),
        [var script] when Directory.Exists(script) || (File.Exists(script) && script.EndsWith(".csproj"))
            => ("dotnet", ["run", "--project", script, "--no-build"]),
        _ => throw new NotSupportedException("Supported scripts: .py, .js, .csproj")
    };
}
var clientTransport = new StdioClientTransport(new()
{
    Name = "",
    Command = command,
    Arguments = arguments,
});

await using var mcpClient = await McpClientFactory.CreateAsync(clientTransport);
var name = mcpClient.ServerInfo.Name;

Console.WriteLine(name);
var tools = await mcpClient.ListToolsAsync();
foreach (var tool in tools)
{
    Console.WriteLine($"{tool.Name} ({tool.Description})");
}