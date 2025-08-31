using Elasticsearch.Net;
using ModelContextProtocol.Server;
using Microsoft.Extensions.Configuration;
using System.ComponentModel;
using System.Text.Json;

namespace ElasticsearchMcpServer.Tools;

[McpServerToolType]
public sealed class ElasticsearchTools
{
    [McpServerTool, Description("Buscar documentos en Elasticsearch usando una consulta simple de texto")]
    public static async Task<string> SearchDocuments(
        IElasticLowLevelClient client,
        IConfiguration configuration,
        [Description("Texto a buscar")] string query,
        [Description("Índice donde buscar (opcional, usa el índice por defecto si no se especifica)")] string? index = null)
    {
        var targetIndex = index ?? configuration["ElasticSearch:DefaultIndex"] ?? "iotslots_events";
        
        var searchBody = new
        {
            query = new
            {
                multi_match = new
                {
                    query = query,
                    fields = new[] { "*" },
                    fuzziness = "AUTO"
                }
            },
            size = 10
        };

        var response = await client.SearchAsync<StringResponse>(targetIndex, PostData.Serializable(searchBody));
        
        if (!response.Success)
        {
            return $"Error en la búsqueda: {response.OriginalException?.Message ?? response.DebugInformation}";
        }

        var result = JsonSerializer.Deserialize<JsonElement>(response.Body);
        var hits = result.GetProperty("hits").GetProperty("hits");
        
        if (hits.GetArrayLength() == 0)
        {
            return "No se encontraron documentos para la consulta especificada.";
        }

        var documents = new List<string>();
        foreach (var hit in hits.EnumerateArray())
        {
            var source = hit.GetProperty("_source");
            var id = hit.GetProperty("_id").GetString();
            var score = hit.GetProperty("_score").GetDouble();
            
            documents.Add($"ID: {id} (Score: {score:F2})\n{JsonSerializer.Serialize(source, new JsonSerializerOptions { WriteIndented = true })}\n");
        }

        return $"Encontrados {hits.GetArrayLength()} documentos:\n\n" + string.Join("\n---\n", documents);
    }

    [McpServerTool, Description("Obtener un documento específico por su ID")]
    public static async Task<string> GetDocumentById(
        IElasticLowLevelClient client,
        IConfiguration configuration,
        [Description("ID del documento a obtener")] string documentId,
        [Description("Índice donde buscar (opcional, usa el índice por defecto si no se especifica)")] string? index = null)
    {
        var targetIndex = index ?? configuration["ElasticSearch:DefaultIndex"] ?? "iotslots_events";
        
        var response = await client.GetAsync<StringResponse>(targetIndex, documentId);
        
        if (!response.Success)
        {
            if (response.HttpStatusCode == 404)
            {
                return $"No se encontró un documento con ID '{documentId}' en el índice '{targetIndex}'.";
            }
            return $"Error al obtener el documento: {response.OriginalException?.Message ?? response.DebugInformation}";
        }

        var result = JsonSerializer.Deserialize<JsonElement>(response.Body);
        var source = result.GetProperty("_source");
        var found = result.GetProperty("found").GetBoolean();

        if (!found)
        {
            return $"Documento con ID '{documentId}' no encontrado.";
        }

        return $"Documento encontrado:\nID: {documentId}\nÍndice: {targetIndex}\nContenido:\n{JsonSerializer.Serialize(source, new JsonSerializerOptions { WriteIndented = true })}";
    }

    [McpServerTool, Description("Realizar una consulta avanzada en Elasticsearch usando DSL de consulta")]
    public static async Task<string> AdvancedQuery(
        IElasticLowLevelClient client,
        IConfiguration configuration,
        [Description("Consulta DSL de Elasticsearch en formato JSON")] string queryDsl,
        [Description("Índice donde buscar (opcional, usa el índice por defecto si no se especifica)")] string? index = null)
    {
        var targetIndex = index ?? configuration["ElasticSearch:DefaultIndex"] ?? "iotslots_events";
        
        try
        {
            var queryObject = JsonSerializer.Deserialize<object>(queryDsl);
            var response = await client.SearchAsync<StringResponse>(targetIndex, PostData.Serializable(queryObject));
            
            if (!response.Success)
            {
                return $"Error en la consulta: {response.OriginalException?.Message ?? response.DebugInformation}";
            }

            var result = JsonSerializer.Deserialize<JsonElement>(response.Body);
            var hits = result.GetProperty("hits");
            var total = hits.GetProperty("total");
            var documents = hits.GetProperty("hits");

            var totalValue = total.TryGetProperty("value", out var val) ? val.GetInt32() : total.GetInt32();
            
            if (totalValue == 0)
            {
                return "No se encontraron documentos para la consulta especificada.";
            }

            var results = new List<string>();
            foreach (var hit in documents.EnumerateArray())
            {
                var source = hit.GetProperty("_source");
                var id = hit.GetProperty("_id").GetString();
                var score = hit.GetProperty("_score").GetDouble();
                
                results.Add($"ID: {id} (Score: {score:F2})\n{JsonSerializer.Serialize(source, new JsonSerializerOptions { WriteIndented = true })}");
            }

            return $"Total de documentos encontrados: {totalValue}\nMostrando los primeros {documents.GetArrayLength()} resultados:\n\n" + 
                   string.Join("\n---\n", results);
        }
        catch (JsonException ex)
        {
            return $"Error en el formato de la consulta JSON: {ex.Message}";
        }
    }

    [McpServerTool, Description("Obtener información sobre los índices disponibles en Elasticsearch")]
    public static async Task<string> GetIndexInfo(
        IElasticLowLevelClient client,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await client.DoRequestAsync<StringResponse>(Elasticsearch.Net.HttpMethod.GET, "_cat/indices?v&s=index", cancellationToken);
            
            if (!response.Success)
            {
                return $"Error al obtener información de índices: {response.OriginalException?.Message ?? response.DebugInformation}";
            }

            return $"Índices disponibles:\n\n{response.Body}";
        }
        catch (Exception ex)
        {
            return $"Error al obtener información de índices: {ex.Message}";
        }
    }

    [McpServerTool, Description("Obtener el mapeo de campos de un índice específico")]
    public static async Task<string> GetIndexMapping(
        IElasticLowLevelClient client,
        IConfiguration configuration,
        [Description("Índice para obtener el mapeo (opcional, usa el índice por defecto si no se especifica)")] string? index = null,
        CancellationToken cancellationToken = default)
    {
        var targetIndex = index ?? configuration["ElasticSearch:DefaultIndex"] ?? "iotslots_events";
        
        try
        {
            var response = await client.DoRequestAsync<StringResponse>(Elasticsearch.Net.HttpMethod.GET, $"{targetIndex}/_mapping", cancellationToken);
            
            if (!response.Success)
            {
                return $"Error al obtener el mapeo del índice '{targetIndex}': {response.OriginalException?.Message ?? response.DebugInformation}";
            }

            var result = JsonSerializer.Deserialize<JsonElement>(response.Body);
            return $"Mapeo del índice '{targetIndex}':\n{JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true })}";
        }
        catch (Exception ex)
        {
            return $"Error al obtener el mapeo del índice: {ex.Message}";
        }
    }
}

