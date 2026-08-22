using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Text;
using WangdefaMemory.MCP.Tools;

Console.OutputEncoding = Encoding.UTF8;
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