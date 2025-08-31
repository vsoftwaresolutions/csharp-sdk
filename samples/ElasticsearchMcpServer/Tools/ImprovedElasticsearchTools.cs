using Elasticsearch.Net;
using ModelContextProtocol.Server;
using Microsoft.Extensions.Configuration;
using System.ComponentModel;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace ElasticsearchMcpServer.Tools;

[McpServerToolType]
public sealed class ImprovedElasticsearchTools
{
    private static async Task<string> ExecuteWithErrorHandling(
        Func<Task<string>> operation, 
        string operationName,
        ILogger? logger = null)
    {
        try
        {
            return await operation();
        }
        catch (Exception ex)
        {
            var errorMessage = $"Error en {operationName}: {ex.Message}";
            logger?.LogError(ex, "Error ejecutando {OperationName}", operationName);
            return errorMessage;
        }
    }

    [McpServerTool, Description("Buscar documentos en Elasticsearch con consulta inteligente y múltiples opciones")]
    public static async Task<string> SmartSearch(
        IElasticLowLevelClient client,
        IConfiguration configuration,
        [Description("Texto a buscar")] string query,
        [Description("Número máximo de resultados (1-100, por defecto 10)")] int? maxResults = 10,
        [Description("Índice donde buscar (opcional, usa el índice por defecto)")] string? index = null,
        [Description("Ordenar por campo (opcional, ej: '@timestamp', 'eventType')")] string? sortBy = null,
        [Description("Dirección de orden: 'asc' o 'desc' (por defecto 'desc')")] string sortOrder = "desc")
    {
        return await ExecuteWithErrorHandling(async () =>
        {
            var targetIndex = index ?? configuration["ElasticSearch:DefaultIndex"] ?? "iotslots_events";
            var size = Math.Max(1, Math.Min(maxResults ?? 10, 100));
            
            var searchBody = new
            {
                query = new
                {
                    multi_match = new
                    {
                        query = query,
                        fields = new[] { "*" },
                        fuzziness = "AUTO",
                        type = "best_fields"
                    }
                },
                size = size,
                sort = !string.IsNullOrEmpty(sortBy) 
                    ? new[] { new Dictionary<string, object> { [sortBy] = new { order = sortOrder } } }
                    : new[] { new Dictionary<string, object> { ["_score"] = new { order = "desc" } } },
                highlight = new
                {
                    fields = new Dictionary<string, object>
                    {
                        ["*"] = new { }
                    },
                    pre_tags = new[] { "<mark>" },
                    post_tags = new[] { "</mark>" }
                }
            };

            var response = await client.SearchAsync<StringResponse>(targetIndex, PostData.Serializable(searchBody));
            
            if (!response.Success)
            {
                return HandleElasticsearchError(response, $"búsqueda en índice '{targetIndex}'");
            }

            var result = JsonSerializer.Deserialize<JsonElement>(response.Body);
            var hits = result.GetProperty("hits");
            var totalHits = GetTotalHits(hits);
            var documents = hits.GetProperty("hits");
            
            if (documents.GetArrayLength() == 0)
            {
                return $"🔍 No se encontraron documentos que coincidan con '{query}' en el índice '{targetIndex}'.\n\n💡 Sugerencias:\n- Intenta con términos más generales\n- Verifica la ortografía\n- Usa wildcards (ej: '*parte*')";
            }

            var results = new List<string>();
            var counter = 1;
            
            foreach (var hit in documents.EnumerateArray())
            {
                var source = hit.GetProperty("_source");
                var id = hit.GetProperty("_id").GetString();
                var score = hit.GetProperty("_score").GetDouble();
                
                // Agregar highlights si están disponibles
                var highlights = "";
                if (hit.TryGetProperty("highlight", out var highlightElement))
                {
                    var highlightTexts = new List<string>();
                    foreach (var field in highlightElement.EnumerateObject())
                    {
                        foreach (var highlight in field.Value.EnumerateArray())
                        {
                            highlightTexts.Add($"...{highlight.GetString()}...");
                        }
                    }
                    if (highlightTexts.Any())
                    {
                        highlights = $"\n📝 Coincidencias: {string.Join(" | ", highlightTexts.Take(3))}";
                    }
                }
                
                results.Add($"[{counter}] ID: {id} (Relevancia: {score:F2}){highlights}\n{FormatDocument(source, compact: true)}\n");
                counter++;
            }

            return $"🎯 Encontrados {totalHits} documentos (mostrando {documents.GetArrayLength()}):\nÍndice: {targetIndex} | Consulta: '{query}'\n\n{string.Join("---\n", results)}";
        }, "búsqueda inteligente");
    }

    [McpServerTool, Description("Obtener estadísticas detalladas de un índice")]
    public static async Task<string> GetIndexStats(
        IElasticLowLevelClient client,
        IConfiguration configuration,
        [Description("Índice para obtener estadísticas (opcional, usa el índice por defecto)")] string? index = null)
    {
        return await ExecuteWithErrorHandling(async () =>
        {
            var targetIndex = index ?? configuration["ElasticSearch:DefaultIndex"] ?? "iotslots_events";
            
            // Obtener estadísticas del índice
            var statsResponse = await client.DoRequestAsync<StringResponse>(
                Elasticsearch.Net.HttpMethod.GET, $"{targetIndex}/_stats", default);
            
            if (!statsResponse.Success)
            {
                return HandleElasticsearchError(statsResponse, $"estadísticas del índice '{targetIndex}'");
            }

            var statsResult = JsonSerializer.Deserialize<JsonElement>(statsResponse.Body);
            var indexStats = statsResult.GetProperty("indices").GetProperty(targetIndex);
            var total = indexStats.GetProperty("total");
            
            var docCount = total.GetProperty("docs").GetProperty("count").GetInt64();
            var sizeInBytes = total.GetProperty("store").GetProperty("size_in_bytes").GetInt64();
            var sizeMB = Math.Round(sizeInBytes / (1024.0 * 1024.0), 2);
            
            // Obtener mapping para información de campos
            var mappingResponse = await client.DoRequestAsync<StringResponse>(
                Elasticsearch.Net.HttpMethod.GET, $"{targetIndex}/_mapping", default);
                
            var fieldCount = 0;
            if (mappingResponse.Success)
            {
                var mappingResult = JsonSerializer.Deserialize<JsonElement>(mappingResponse.Body);
                var properties = mappingResult
                    .GetProperty(targetIndex)
                    .GetProperty("mappings");
                    
                if (properties.TryGetProperty("properties", out var propsElement))
                {
                    fieldCount = CountFields(propsElement);
                }
            }
            
            // Obtener muestra de tipos de eventos recientes
            var sampleResponse = await client.SearchAsync<StringResponse>(targetIndex, 
                PostData.Serializable(new
                {
                    size = 0,
                    aggs = new
                    {
                        event_types = new
                        {
                            terms = new
                            {
                                field = "eventType.keyword",
                                size = 10
                            }
                        },
                        recent_docs = new
                        {
                            date_histogram = new
                            {
                                field = "@timestamp",
                                calendar_interval = "1d",
                                order = new { _key = "desc" }
                            }
                        }
                    }
                }));

            var eventTypesInfo = "";
            var recentActivity = "";
            
            if (sampleResponse.Success)
            {
                var sampleResult = JsonSerializer.Deserialize<JsonElement>(sampleResponse.Body);
                var aggregations = sampleResult.GetProperty("aggregations");
                
                // Tipos de eventos
                if (aggregations.TryGetProperty("event_types", out var eventTypes))
                {
                    var buckets = eventTypes.GetProperty("buckets");
                    var types = buckets.EnumerateArray()
                        .Select(b => $"  • {b.GetProperty("key").GetString()}: {b.GetProperty("doc_count").GetInt32():N0} docs")
                        .Take(5);
                    eventTypesInfo = $"\n📊 Tipos de eventos más frecuentes:\n{string.Join("\n", types)}";
                }
                
                // Actividad reciente  
                if (aggregations.TryGetProperty("recent_docs", out var recentDocs))
                {
                    var buckets = recentDocs.GetProperty("buckets");
                    var recent = buckets.EnumerateArray()
                        .Take(3)
                        .Select(b => $"  • {DateTime.Parse(b.GetProperty("key_as_string").GetString()!):yyyy-MM-dd}: {b.GetProperty("doc_count").GetInt32():N0} docs");
                    recentActivity = $"\n📈 Actividad reciente:\n{string.Join("\n", recent)}";
                }
            }
            
            return $"📋 Estadísticas del índice '{targetIndex}':\n\n" +
                   $"📄 Documentos: {docCount:N0}\n" +
                   $"💾 Tamaño: {sizeMB:N2} MB ({sizeInBytes:N0} bytes)\n" +
                   $"🏷️  Campos mapeados: {fieldCount}\n" +
                   $"🔗 Segmentos: {total.GetProperty("segments").GetProperty("count").GetInt32()}" +
                   eventTypesInfo + recentActivity;
        }, "estadísticas del índice");
    }

    [McpServerTool, Description("Contar documentos con filtros opcionales")]
    public static async Task<string> CountDocuments(
        IElasticLowLevelClient client,
        IConfiguration configuration,
        [Description("Filtro de consulta opcional (formato JSON de Elasticsearch)")] string? queryFilter = null,
        [Description("Índice donde contar (opcional, usa el índice por defecto)")] string? index = null)
    {
        return await ExecuteWithErrorHandling(async () =>
        {
            var targetIndex = index ?? configuration["ElasticSearch:DefaultIndex"] ?? "iotslots_events";
            
            object? countBody = null;
            if (!string.IsNullOrEmpty(queryFilter))
            {
                try
                {
                    countBody = JsonSerializer.Deserialize<object>(queryFilter);
                }
                catch (JsonException)
                {
                    return "❌ El filtro de consulta no es un JSON válido. Ejemplo: {\"query\": {\"term\": {\"eventType\": \"Info\"}}}";
                }
            }
            
            var response = countBody != null
                ? await client.CountAsync<StringResponse>(targetIndex, PostData.Serializable(countBody))
                : await client.CountAsync<StringResponse>(targetIndex);
            
            if (!response.Success)
            {
                return HandleElasticsearchError(response, $"conteo en índice '{targetIndex}'");
            }

            var result = JsonSerializer.Deserialize<JsonElement>(response.Body);
            var count = result.GetProperty("count").GetInt64();
            
            var filterInfo = !string.IsNullOrEmpty(queryFilter) ? $" (con filtro aplicado)" : "";
            
            return $"📊 Total de documentos en '{targetIndex}'{filterInfo}: **{count:N0}**\n\n" +
                   (count == 0 ? "💡 El índice está vacío o no coincide con los filtros especificados." :
                    count > 10000 ? "⚡ Gran volumen de datos - considera usar filtros para consultas específicas." :
                    "✅ Volumen manejable para consultas directas.");
        }, "conteo de documentos");
    }

    [McpServerTool, Description("Explorar la estructura de campos de un índice")]
    public static async Task<string> ExploreFields(
        IElasticLowLevelClient client,
        IConfiguration configuration,
        [Description("Índice para explorar (opcional, usa el índice por defecto)")] string? index = null,
        [Description("Mostrar solo campos que contienen este texto")] string? fieldFilter = null)
    {
        return await ExecuteWithErrorHandling(async () =>
        {
            var targetIndex = index ?? configuration["ElasticSearch:DefaultIndex"] ?? "iotslots_events";
            
            var mappingResponse = await client.DoRequestAsync<StringResponse>(
                Elasticsearch.Net.HttpMethod.GET, $"{targetIndex}/_mapping", default);
                
            if (!mappingResponse.Success)
            {
                return HandleElasticsearchError(mappingResponse, $"mapping del índice '{targetIndex}'");
            }

            var result = JsonSerializer.Deserialize<JsonElement>(mappingResponse.Body);
            var mapping = result.GetProperty(targetIndex).GetProperty("mappings");
            
            if (!mapping.TryGetProperty("properties", out var properties))
            {
                return $"🔍 No se encontraron propiedades mapeadas en el índice '{targetIndex}'.";
            }
            
            var fields = ExtractFields(properties, "", fieldFilter?.ToLower());
            
            if (!fields.Any())
            {
                var filterMsg = !string.IsNullOrEmpty(fieldFilter) ? $" que coincidan con '{fieldFilter}'" : "";
                return $"🔍 No se encontraron campos{filterMsg} en el índice '{targetIndex}'.";
            }
            
            var grouped = fields.GroupBy(f => f.Key.Split('.')[0]).OrderBy(g => g.Key);
            var result_parts = new List<string>();
            
            foreach (var group in grouped)
            {
                var groupFields = group.Select(g => g.Key).OrderBy(f => f).ToList();
                var topLevel = groupFields.FirstOrDefault(f => !f.Contains('.'));
                var nested = groupFields.Where(f => f.Contains('.') && f != topLevel).ToList();
                
                if (topLevel != null)
                {
                    result_parts.Add($"🏷️  **{topLevel}** - {fields[topLevel]}");
                    foreach (var nestedField in nested.Take(3))
                    {
                        result_parts.Add($"   └── {nestedField.Split('.').Last()} - {fields[nestedField]}");
                    }
                    if (nested.Count() > 3)
                    {
                        result_parts.Add($"   └── ... (+{nested.Count() - 3} campos más)");
                    }
                } else {
                    foreach (var field in groupFields.Take(5))
                    {
                        result_parts.Add($"🏷️  **{field}** - {fields[field]}");
                    }
                }
            }
            
            var filterInfo = !string.IsNullOrEmpty(fieldFilter) ? $" (filtrados por '{fieldFilter}')" : "";
            return $"🗂️  Campos disponibles en '{targetIndex}'{filterInfo}:\n\n{string.Join("\n", result_parts.Take(20))}" +
                   (result_parts.Count() > 20 ? $"\n\n... y {result_parts.Count() - 20} campos más" : "");
        }, "exploración de campos");
    }

    [McpServerTool, Description("Obtener documentos recientes con información de contexto")]
    public static async Task<string> GetRecentDocuments(
        IElasticLowLevelClient client,
        IConfiguration configuration,
        [Description("Número de documentos recientes a obtener (1-50, por defecto 5)")] int? count = 5,
        [Description("Índice donde buscar (opcional, usa el índice por defecto)")] string? index = null,
        [Description("Campo de fecha para ordenar (por defecto '@timestamp')")] string? timestampField = "@timestamp")
    {
        return await ExecuteWithErrorHandling(async () =>
        {
            var targetIndex = index ?? configuration["ElasticSearch:DefaultIndex"] ?? "iotslots_events";
            var size = Math.Max(1, Math.Min(count ?? 5, 50));
            var timeField = timestampField ?? "@timestamp";
            
            var searchBody = new
            {
                size = size,
                sort = new[] { new Dictionary<string, object> { [timeField] = new { order = "desc" } } },
                query = new
                {
                    match_all = new { }
                }
            };

            var response = await client.SearchAsync<StringResponse>(targetIndex, PostData.Serializable(searchBody));
            
            if (!response.Success)
            {
                return HandleElasticsearchError(response, $"consulta de documentos recientes en '{targetIndex}'");
            }

            var result = JsonSerializer.Deserialize<JsonElement>(response.Body);
            var hits = result.GetProperty("hits").GetProperty("hits");
            
            if (hits.GetArrayLength() == 0)
            {
                return $"📭 No se encontraron documentos en el índice '{targetIndex}'.";
            }

            var documents = new List<string>();
            var counter = 1;
            
            foreach (var hit in hits.EnumerateArray())
            {
                var source = hit.GetProperty("_source");
                var id = hit.GetProperty("_id").GetString();
                
                // Extraer timestamp si está disponible
                var timestamp = "";
                if (source.TryGetProperty(timeField, out var timeElement))
                {
                    if (DateTime.TryParse(timeElement.GetString(), out var dateTime))
                    {
                        timestamp = $" ({dateTime:yyyy-MM-dd HH:mm:ss})";
                    }
                }
                
                documents.Add($"[{counter}] ID: {id}{timestamp}\n{FormatDocument(source, compact: true)}");
                counter++;
            }

            return $"⏰ Documentos más recientes de '{targetIndex}' (últimos {hits.GetArrayLength()}):\n\n{string.Join("\n---\n", documents)}";
        }, "documentos recientes");
    }

    // Métodos auxiliares
    private static string HandleElasticsearchError(StringResponse response, string operation)
    {
        var statusCode = response.HttpStatusCode ?? 0;
        var errorMessage = response.OriginalException?.Message ?? "Error desconocido";
        
        return statusCode switch
        {
            404 => $"❌ No encontrado: El recurso para {operation} no existe.\n💡 Verifica que el índice y los campos especificados sean correctos.",
            401 => $"🔐 Error de autenticación: Verifica tus credenciales de Elasticsearch.\n💡 Revisa la API Key en la configuración.",
            403 => $"🚫 Acceso denegado: No tienes permisos para {operation}.\n💡 Verifica que tu API Key tenga los permisos necesarios.",
            400 => $"❌ Solicitud incorrecta para {operation}: {errorMessage}\n💡 Revisa la sintaxis de tu consulta.",
            _ => $"❌ Error en {operation} (HTTP {statusCode}): {errorMessage}\n🔍 Detalles: {response.DebugInformation?.Take(200)}..."
        };
    }
    
    private static string FormatDocument(JsonElement source, bool compact = false)
    {
        var options = new JsonSerializerOptions 
        { 
            WriteIndented = !compact,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };
        
        var json = JsonSerializer.Serialize(source, options);
        
        if (compact && json.Length > 300)
        {
            var lines = json.Split('\n');
            var truncated = string.Join('\n', lines.Take(8));
            return truncated + (lines.Length > 8 ? "\n   ... (truncado)" : "");
        }
        
        return json;
    }
    
    private static long GetTotalHits(JsonElement hits)
    {
        var total = hits.GetProperty("total");
        return total.TryGetProperty("value", out var val) ? val.GetInt64() : total.GetInt64();
    }
    
    private static int CountFields(JsonElement properties, string prefix = "")
    {
        var count = 0;
        foreach (var prop in properties.EnumerateObject())
        {
            count++;
            if (prop.Value.TryGetProperty("properties", out var nestedProps))
            {
                count += CountFields(nestedProps, prefix + prop.Name + ".");
            }
        }
        return count;
    }
    
    private static Dictionary<string, string> ExtractFields(JsonElement properties, string prefix = "", string? filter = null)
    {
        var fields = new Dictionary<string, string>();
        
        foreach (var prop in properties.EnumerateObject())
        {
            var fieldName = string.IsNullOrEmpty(prefix) ? prop.Name : $"{prefix}.{prop.Name}";
            
            if (filter == null || fieldName.ToLower().Contains(filter))
            {
                var fieldType = "unknown";
                if (prop.Value.TryGetProperty("type", out var typeElement))
                {
                    fieldType = typeElement.GetString() ?? "unknown";
                }
                
                fields[fieldName] = fieldType;
            }
            
            if (prop.Value.TryGetProperty("properties", out var nestedProps))
            {
                var nestedFields = ExtractFields(nestedProps, fieldName, filter);
                foreach (var nested in nestedFields)
                {
                    fields[nested.Key] = nested.Value;
                }
            }
        }
        
        return fields;
    }
}
