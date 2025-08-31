using Microsoft.Extensions.Logging;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using System.Text.Json;

Console.WriteLine("🔍 Cliente MCP de Elasticsearch");
Console.WriteLine("================================");
Console.WriteLine();

// Primero hacer prueba básica
Console.Write("¿Quieres hacer solo una prueba básica de conectividad? (y/n): ");
var basicTest = Console.ReadLine()?.ToLower();
if (basicTest == "y" || basicTest == "yes")
{
    await TestBasic.TestConnection();
    return;
}

// Configurar logging
using var loggerFactory = LoggerFactory.Create(builder =>
{
    builder.AddConsole().SetMinimumLevel(LogLevel.Warning);
});

try
{
    // Conectar al servidor MCP de Elasticsearch usando stdio
    Console.WriteLine("📡 Conectando al servidor MCP de Elasticsearch...");
    var transport = new StdioClientTransport(new()
    {
        Name = "Elasticsearch Test Client",
        Command = "dotnet",
        Arguments = ["run", "--project", "..\\ElasticsearchMcpServer\\ElasticsearchMcpServer.csproj"],
    });

    await using var mcpClient = await McpClientFactory.CreateAsync(transport, loggerFactory: loggerFactory);
    Console.WriteLine("✅ ¡Conectado exitosamente!\n");

    // Listar herramientas disponibles
    Console.WriteLine("🛠️ Herramientas disponibles:");
    var tools = await mcpClient.ListToolsAsync();
    if (tools.Count == 0)
    {
        Console.WriteLine("❌ No se encontraron herramientas en el servidor.");
        return;
    }

    for (int i = 0; i < tools.Count; i++)
    {
        Console.WriteLine($"  {i + 1}. {tools[i].Name} - {tools[i].Description}");
    }
    Console.WriteLine();

    Console.WriteLine("🎯 Servidor MCP conectado correctamente!");
    Console.WriteLine("Presiona cualquier tecla para continuar...");
    Console.ReadKey();
}
catch (Exception ex)
{
    Console.WriteLine($"❌ Error al conectar con el servidor: {ex.Message}");
    Console.WriteLine("💡 Posibles soluciones:");
    Console.WriteLine("1. Verificar que las credenciales estén en appsettings.json");
    Console.WriteLine("2. Ejecutar desde el directorio correcto");
    Console.WriteLine("3. Verificar conectividad a Elasticsearch");
}