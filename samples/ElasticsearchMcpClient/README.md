# Cliente MCP de Elasticsearch

## 🚀 **Instrucciones de uso:**

### **1. Verificar que el servidor tenga credenciales:**
```bash
# Ir al directorio del servidor
cd ..\ElasticsearchMcpServer

# Verificar que appsettings.json existe y tiene credenciales
type appsettings.json
```

### **2. Ejecutar el cliente:**
```bash
# Volver al cliente
cd ..\ElasticsearchMcpClient

# Ejecutar cliente (básico)
dotnet run
# Elegir 'y' para prueba básica

# Ejecutar cliente (completo)
dotnet run
# Elegir 'n' para menú completo
```

### **3. Si hay problemas de credenciales:**

El servidor MCP necesita acceso a las credenciales de Elasticsearch. Si Claude tiene problemas, verifica:

1. **Archivo appsettings.json existe** en `ElasticsearchMcpServer/`
2. **Credenciales correctas** (URI, ApiKey, DefaultIndex)
3. **Directorio de trabajo** correcto

### **4. Herramientas disponibles:**

**🆕 Nuevas herramientas mejoradas:**
- `SmartSearch` - Búsqueda inteligente con resaltado
- `GetIndexStats` - Estadísticas detalladas del índice  
- `CountDocuments` - Conteo con filtros opcionales
- `ExploreFields` - Exploración de estructura de campos
- `GetRecentDocuments` - Documentos recientes con contexto

**📋 Herramientas originales:**
- `SearchDocuments` - Búsqueda básica de texto
- `GetDocumentById` - Obtener documento por ID
- `AdvancedQuery` - Consultas DSL de Elasticsearch
- `GetIndexInfo` - Información de índices
- `GetIndexMapping` - Mapeo de campos

## 🔧 **Solución de problemas:**

```bash
# Limpiar procesos antes de ejecutar
powershell -File ..\..\cleanup.ps1

# Verificar conectividad básica
dotnet run
# Elegir 'y' para prueba básica
```
