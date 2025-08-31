using Microsoft.Extensions.Logging;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using System.Text.Json;

Console.WriteLine("🔍 Cliente MCP de Elasticsearch");
Console.WriteLine("================================");
Console.WriteLine();

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

    using var mcpClient = await McpClientFactory.CreateAsync(transport, loggerFactory: loggerFactory);
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

    // Menú interactivo
    while (true)
    {
        Console.WriteLine("╔════════════════════════════════════════════╗");
        Console.WriteLine("║             MENÚ DE PRUEBAS                ║");
        Console.WriteLine("╠════════════════════════════════════════════╣");
        Console.WriteLine("║ 1. 🔍 Buscar documentos                    ║");
        Console.WriteLine("║ 2. 📄 Obtener documento por ID             ║");
        Console.WriteLine("║ 3. ⚡ Consulta avanzada (DSL)              ║");
        Console.WriteLine("║ 4. 📊 Ver información de índices          ║");
        Console.WriteLine("║ 5. 🗂️ Ver mapeo de índice                  ║");
        Console.WriteLine("║ 6. 📋 Listar herramientas                  ║");
        Console.WriteLine("║ 0. ❌ Salir                               ║");
        Console.WriteLine("╚════════════════════════════════════════════╝");
        Console.Write("Selecciona una opción: ");

        var choice = Console.ReadLine();
        Console.WriteLine();

        try
        {
            switch (choice)
            {
                case "1":
                    await TestSearchDocuments(mcpClient);
                    break;
                case "2":
                    await TestGetDocumentById(mcpClient);
                    break;
                case "3":
                    await TestAdvancedQuery(mcpClient);
                    break;
                case "4":
                    await TestGetIndexInfo(mcpClient);
                    break;
                case "5":
                    await TestGetIndexMapping(mcpClient);
                    break;
                case "6":
                    await ListTools(mcpClient);
                    break;
                case "0":
                    Console.WriteLine("👋 ¡Hasta luego!");
                    return;
                default:
                    Console.WriteLine("❌ Opción no válida. Inténtalo de nuevo.\n");
                    break;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error: {ex.Message}\n");
        }

        Console.WriteLine("Presiona cualquier tecla para continuar...");
        Console.ReadKey();
        Console.Clear();
    }
}
catch (Exception ex)
{
    Console.WriteLine($"❌ Error al conectar con el servidor: {ex.Message}");
    Console.WriteLine("Asegúrate de que el servidor MCP de Elasticsearch esté disponible.");
}

// Funciones de prueba
static async Task TestSearchDocuments(IMcpClient client)
{
    Console.Write("🔍 Ingresa el texto a buscar: ");
    var query = Console.ReadLine();
    if (string.IsNullOrWhiteSpace(query)) return;

    Console.Write("📂 Ingresa el índice (opcional, presiona Enter para usar el por defecto): ");
    var index = Console.ReadLine();

    var args = new Dictionary<string, object?> { ["query"] = query };
    if (!string.IsNullOrWhiteSpace(index))
        args["index"] = index;

    Console.WriteLine("\n🔄 Buscando...");
    var result = await client.CallToolAsync("SearchDocuments", args);
    
    Console.WriteLine("📋 Resultado:");
    Console.WriteLine(new string('─', 60));
    foreach (var content in result.Content)
    {
        if (content.Type == "text" && content is TextContentBlock textContent)
        {
            Console.WriteLine(textContent.Text);
        }
    }
    Console.WriteLine(new string('─', 60));
    Console.WriteLine();
}

static async Task TestGetDocumentById(IMcpClient client)
{
    Console.Write("📄 Ingresa el ID del documento: ");
    var documentId = Console.ReadLine();
    if (string.IsNullOrWhiteSpace(documentId)) return;

    Console.Write("📂 Ingresa el índice (opcional): ");
    var index = Console.ReadLine();

    var args = new Dictionary<string, object?> { ["documentId"] = documentId };
    if (!string.IsNullOrWhiteSpace(index))
        args["index"] = index;

    Console.WriteLine("\n🔄 Obteniendo documento...");
    var result = await client.CallToolAsync("GetDocumentById", args);
    
    Console.WriteLine("📋 Resultado:");
    Console.WriteLine(new string('─', 60));
    foreach (var content in result.Content)
    {
        if (content.Type == "text" && content is TextContentBlock textContent)
        {
            Console.WriteLine(textContent.Text);
        }
    }
    Console.WriteLine(new string('─', 60));
    Console.WriteLine();
}

static async Task TestAdvancedQuery(IMcpClient client)
{
    Console.WriteLine("⚡ Ejemplos de consultas DSL:");
    Console.WriteLine("1. Búsqueda simple: {\"query\": {\"match\": {\"campo\": \"valor\"}}}");
    Console.WriteLine("2. Rango de fechas: {\"query\": {\"range\": {\"@timestamp\": {\"gte\": \"2024-01-01\"}}}}");
    Console.WriteLine("3. Múltiples condiciones: {\"query\": {\"bool\": {\"must\": [{\"match\": {\"status\": \"active\"}}]}}}");
    Console.WriteLine();
    Console.WriteLine("💡 Ingresa tu consulta DSL en formato JSON:");

    var queryDsl = Console.ReadLine();
    if (string.IsNullOrWhiteSpace(queryDsl)) return;

    Console.Write("📂 Ingresa el índice (opcional): ");
    var index = Console.ReadLine();

    var args = new Dictionary<string, object?> { ["queryDsl"] = queryDsl };
    if (!string.IsNullOrWhiteSpace(index))
        args["index"] = index;

    Console.WriteLine("\n🔄 Ejecutando consulta...");
    var result = await client.CallToolAsync("AdvancedQuery", args);
    
    Console.WriteLine("📋 Resultado:");
    Console.WriteLine(new string('─', 60));
    foreach (var content in result.Content)
    {
        if (content.Type == "text" && content is TextContentBlock textContent)
        {
            Console.WriteLine(textContent.Text);
        }
    }
    Console.WriteLine(new string('─', 60));
    Console.WriteLine();
}

static async Task TestGetIndexInfo(IMcpClient client)
{
    Console.WriteLine("🔄 Obteniendo información de índices...");
    var result = await client.CallToolAsync("GetIndexInfo", new Dictionary<string, object?>());
    
    Console.WriteLine("📊 Índices disponibles:");
    Console.WriteLine(new string('═', 80));
    foreach (var content in result.Content)
    {
        if (content.Type == "text" && content is TextContentBlock textContent)
        {
            Console.WriteLine(textContent.Text);
        }
    }
    Console.WriteLine(new string('═', 80));
    Console.WriteLine();
}

static async Task TestGetIndexMapping(IMcpClient client)
{
    Console.Write("🗂️ Ingresa el índice (opcional, presiona Enter para usar el por defecto): ");
    var index = Console.ReadLine();

    var args = new Dictionary<string, object?>();
    if (!string.IsNullOrWhiteSpace(index))
        args["index"] = index;

    Console.WriteLine("\n🔄 Obteniendo mapeo del índice...");
    var result = await client.CallToolAsync("GetIndexMapping", args);
    
    Console.WriteLine("📋 Mapeo del índice:");
    Console.WriteLine(new string('─', 60));
    foreach (var content in result.Content)
    {
        if (content.Type == "text" && content is TextContentBlock textContent)
        {
            // Intentar formatear JSON si es posible
            try
            {
                var jsonElement = JsonSerializer.Deserialize<JsonElement>(textContent.Text);
                var formattedJson = JsonSerializer.Serialize(jsonElement, new JsonSerializerOptions { WriteIndented = true });
                Console.WriteLine(formattedJson);
            }
            catch
            {
                Console.WriteLine(textContent.Text);
            }
        }
    }
    Console.WriteLine(new string('─', 60));
    Console.WriteLine();
}

static async Task ListTools(IMcpClient client)
{
    Console.WriteLine("🛠️ Herramientas disponibles en el servidor:");
    Console.WriteLine(new string('═', 80));
    
    var tools = await client.ListToolsAsync();
    for (int i = 0; i < tools.Count; i++)
    {
        var tool = tools[i];
        Console.WriteLine($"{i + 1}. {tool.Name}");
        Console.WriteLine($"   📝 Descripción: {tool.Description}");
        
        if (tool.InputSchema.ValueKind != JsonValueKind.Undefined)
        {
            Console.WriteLine($"   📊 Schema: {tool.InputSchema}");
        }
        Console.WriteLine();
    }
    Console.WriteLine(new string('═', 80));
    Console.WriteLine();
}