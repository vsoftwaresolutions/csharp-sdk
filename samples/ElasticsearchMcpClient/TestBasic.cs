using Microsoft.Extensions.Logging;
using ModelContextProtocol.Client;

public class TestBasic
{
    public static async Task TestConnection()
    {
        Console.WriteLine("🧪 Prueba básica de conectividad MCP");
        Console.WriteLine("===================================");
        Console.WriteLine();

        // Configurar logging más detallado
        using var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddConsole().SetMinimumLevel(LogLevel.Information);
        });

        try
        {
            Console.WriteLine("📡 Conectando al servidor...");
            var transport = new StdioClientTransport(new()
            {
                Name = "Basic Test Client",
                Command = "dotnet",
                Arguments = ["run", "--project", "..\\ElasticsearchMcpServer\\ElasticsearchMcpServer.csproj"],
            });

            await using var mcpClient = await McpClientFactory.CreateAsync(transport, loggerFactory: loggerFactory);
            Console.WriteLine("✅ ¡Conectado exitosamente!");

            // Solo listar herramientas, no ejecutarlas
            Console.WriteLine("\n🛠️ Herramientas disponibles:");
            var tools = await mcpClient.ListToolsAsync();
            
            if (tools.Count == 0)
            {
                Console.WriteLine("❌ No se encontraron herramientas.");
            }
            else
            {
                foreach (var tool in tools)
                {
                    Console.WriteLine($"  ✓ {tool.Name} - {tool.Description}");
                }
                Console.WriteLine($"\n🎉 ¡Servidor MCP funcionando! Encontradas {tools.Count} herramientas.");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error: {ex.Message}");
            Console.WriteLine("\n💡 Detalles del error:");
            Console.WriteLine(ex.ToString());
        }

        Console.WriteLine("\nPresiona cualquier tecla para continuar...");
        Console.ReadKey();
    }
}

