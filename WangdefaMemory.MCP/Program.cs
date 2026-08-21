using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using WangdefaMemory.MCP.Tools;

Console.SetOut(Console.Error);

var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices(services =>
    {
        services.AddMcpServer()
            .WithStdioServerTransport()
            .WithTools<MemoryTools>();
    })
    .Build();

await host.RunAsync();