using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Elasticsearch.Net;
using ElasticsearchMcpServer.Tools;
using ElasticsearchMcpServer;

var builder = Host.CreateApplicationBuilder(args);

// Configurar logging para que vaya a stderr
builder.Logging.AddConsole(options =>
{
    options.LogToStandardErrorThreshold = LogLevel.Trace;
});

// Configurar Elasticsearch con inicialización lazy
builder.Services.AddTransient<IElasticLowLevelClient>(serviceProvider =>
{
    var configuration = serviceProvider.GetRequiredService<IConfiguration>();
    var logger = serviceProvider.GetRequiredService<ILogger<ElasticLowLevelClient>>();
    var elasticSection = configuration.GetSection("ElasticSearch");
    
    var uri = elasticSection["Uri"];
    var apiKey = elasticSection["ApiKey"];
    var username = elasticSection["Username"];
    var password = elasticSection["Password"];
    
    if (string.IsNullOrEmpty(uri))
    {
        logger.LogError("ElasticSearch:Uri no está configurado en appsettings.json");
        throw new InvalidOperationException("ElasticSearch:Uri no está configurado en appsettings.json");
    }
    
    logger.LogInformation("Configurando cliente de Elasticsearch para {Uri}", uri);
    
    var settings = new ConnectionConfiguration(new Uri(uri));
    
    if (!string.IsNullOrEmpty(apiKey))
    {
        settings = settings.ApiKeyAuthentication(new Elasticsearch.Net.ApiKeyAuthenticationCredentials(apiKey));
        logger.LogInformation("Usando autenticación con API Key");
    }
    else if (!string.IsNullOrEmpty(username) && !string.IsNullOrEmpty(password))
    {
        settings = settings.BasicAuthentication(username, password);
        logger.LogInformation("Usando autenticación básica");
    }
    
    // Configuraciones optimizadas basadas en configuración
    var requestTimeout = configuration.GetValue<int>("ElasticSearch:RequestTimeoutSeconds", 30);
    var maxRetries = configuration.GetValue<int>("ElasticSearch:MaxRetries", 3);
    
    settings = settings
        .RequestTimeout(TimeSpan.FromSeconds(requestTimeout))
        .PingTimeout(TimeSpan.FromSeconds(10))
        .MaximumRetries(maxRetries)
        .DisableDirectStreaming() // Para poder leer las respuestas como string
        .PrettyJson()
        .EnableHttpCompression() // Mejora performance para grandes respuestas
        .ServerCertificateValidationCallback((sender, certificate, chain, errors) => true); // Para conexiones HTTPS
    
    return new ElasticLowLevelClient(settings);
});

// Configurar el servidor MCP con herramientas mejoradas
builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithTools<ImprovedElasticsearchTools>();

var app = builder.Build();

var logger = app.Services.GetRequiredService<ILogger<Program>>();
var configuration = app.Services.GetRequiredService<IConfiguration>();

logger.LogInformation("🚀 Iniciando servidor MCP de Elasticsearch...");

// Diagnóstico opcional basado en variable de entorno o configuración
var runDiagnostic = Environment.GetEnvironmentVariable("ELASTICSEARCH_DIAGNOSTIC") == "true" || 
                   configuration.GetValue<bool>("ElasticSearch:RunDiagnosticOnStartup", false);

if (runDiagnostic)
{
    logger.LogInformation("🔍 Ejecutando diagnóstico de conexión...");
    
    var connectionTest = await DiagnosticTool.TestElasticsearchConnection(configuration, logger);
    
    if (!connectionTest)
    {
        logger.LogError("❌ Las pruebas de conexión fallaron. Revisa la configuración en appsettings.json");
        logger.LogError("Verifica:");
        logger.LogError("  - URI de Elasticsearch");
        logger.LogError("  - API Key válida");  
        logger.LogError("  - Conectividad de red");
        logger.LogInformation("💡 Tip: El servidor continuará iniciándose. Las herramientas mostrarán errores específicos si hay problemas.");
    }
    else
    {
        logger.LogInformation("✅ Conexión a Elasticsearch verificada.");
    }
}

logger.LogInformation("🎯 Servidor MCP iniciado y listo para recibir solicitudes.");

await app.RunAsync();

