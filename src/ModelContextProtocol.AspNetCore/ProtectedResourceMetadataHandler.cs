using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Protocol.Auth;
using ModelContextProtocol.Utils.Json;
using System.Text.Json;

namespace ModelContextProtocol.AspNetCore;

/// <summary>
/// Handler for the Protected Resource Metadata document endpoint.
/// </summary>
internal class ProtectedResourceMetadataHandler
{
    private readonly ILogger<ProtectedResourceMetadataHandler> _logger;
    private readonly IServerAuthorizationProvider _authProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="ProtectedResourceMetadataHandler"/> class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="authProvider">The authorization provider.</param>
    public ProtectedResourceMetadataHandler(
        ILogger<ProtectedResourceMetadataHandler> logger,
        IServerAuthorizationProvider authProvider)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _authProvider = authProvider ?? throw new ArgumentNullException(nameof(authProvider));
    }

    /// <summary>
    /// Handles the request for the Protected Resource Metadata document.
    /// </summary>
    /// <param name="context">The HTTP context.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task HandleAsync(HttpContext context)
    {
        _logger.LogDebug("Serving Protected Resource Metadata document");
        context.Response.ContentType = "application/json";
        await JsonSerializer.SerializeAsync(
            context.Response.Body, 
            _authProvider.GetProtectedResourceMetadata(),
            McpJsonUtilities.DefaultOptions.GetTypeInfo(typeof(ProtectedResourceMetadata)));
    }
}
