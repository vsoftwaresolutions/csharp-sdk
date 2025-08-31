using Elasticsearch.Net;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace ElasticsearchMcpServer;

public static class DiagnosticTool
{
    public static async Task<bool> TestElasticsearchConnection(IConfiguration configuration, ILogger logger, CancellationToken cancellationToken = default)
    {
        try
        {
            logger.LogInformation("=== DIAGNÓSTICO DE CONEXIÓN A ELASTICSEARCH ===");
            
            var elasticSection = configuration.GetSection("ElasticSearch");
            var uri = elasticSection["Uri"];
            var apiKey = elasticSection["ApiKey"];
            var defaultIndex = elasticSection["DefaultIndex"];
            
            logger.LogInformation("URI configurada: {Uri}", uri);
            logger.LogInformation("API Key configurada: {HasApiKey}", !string.IsNullOrEmpty(apiKey) ? "Sí" : "No");
            logger.LogInformation("Índice por defecto: {DefaultIndex}", defaultIndex);
            
            if (string.IsNullOrEmpty(uri))
            {
                logger.LogError("❌ La URI de Elasticsearch no está configurada");
                return false;
            }
            
            if (string.IsNullOrEmpty(apiKey))
            {
                logger.LogError("❌ La API Key no está configurada");
                return false;
            }
            
            // Crear cliente
            var settings = new ConnectionConfiguration(new Uri(uri))
                .ApiKeyAuthentication(new ApiKeyAuthenticationCredentials(apiKey))
                .RequestTimeout(TimeSpan.FromSeconds(10))
                .PingTimeout(TimeSpan.FromSeconds(5))
                .DisableDirectStreaming()
                .PrettyJson();
            
            var client = new ElasticLowLevelClient(settings);
            
            // Test de conectividad básica
            logger.LogInformation("🔍 Probando conectividad básica...");
            var pingResponse = await client.PingAsync<StringResponse>();
            
            if (!pingResponse.Success)
            {
                logger.LogError("❌ Error en ping: {Error}", 
                    pingResponse.OriginalException?.Message ?? pingResponse.DebugInformation);
                return false;
            }
            
            logger.LogInformation("✅ Ping exitoso - Conexión establecida correctamente");
            
            // Test de información del cluster
            logger.LogInformation("🔍 Obteniendo información del cluster...");
            var infoResponse = await client.DoRequestAsync<StringResponse>(Elasticsearch.Net.HttpMethod.GET, "", cancellationToken);
            
            if (infoResponse.Success)
            {
                var clusterInfo = JsonSerializer.Deserialize<JsonElement>(infoResponse.Body);
                var clusterName = clusterInfo.GetProperty("cluster_name").GetString();
                var version = clusterInfo.GetProperty("version").GetProperty("number").GetString();
                
                logger.LogInformation("✅ Cluster: {ClusterName}, Versión: {Version}", clusterName, version);
            }
            
            // Test específico del índice por defecto
            if (!string.IsNullOrEmpty(defaultIndex))
            {
                logger.LogInformation("🔍 Verificando índice por defecto: {DefaultIndex}", defaultIndex);
                var countResponse = await client.CountAsync<StringResponse>(defaultIndex);
                
                if (countResponse.Success)
                {
                    var countResult = JsonSerializer.Deserialize<JsonElement>(countResponse.Body);
                    var docCount = countResult.GetProperty("count").GetInt32();
                    logger.LogInformation("✅ El índice '{DefaultIndex}' contiene {DocCount} documentos", defaultIndex, docCount);
                }
                else
                {
                    logger.LogWarning("⚠️ El índice '{DefaultIndex}' puede no existir o no tener permisos", defaultIndex);
                }
            }
            
            logger.LogInformation("=== DIAGNÓSTICO COMPLETADO EXITOSAMENTE ===");
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "❌ Error durante el diagnóstico: {Message}", ex.Message);
            return false;
        }
    }
}
