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
    {        _logger.LogDebug("Serving Protected Resource Metadata document");
        context.Response.ContentType = "application/json";
        await JsonSerializer.SerializeAsync(
            context.Response.Body, 
            _authProvider.GetProtectedResourceMetadata(),
            McpJsonUtilities.DefaultOptions.GetTypeInfo(typeof(ProtectedResourceMetadata)));
    }    /// <summary>
    /// Builds the URL for the protected resource metadata endpoint.
    /// </summary>
    /// <param name="resourceUri">The resource URI from the protected resource metadata.</param>
    /// <returns>The full URL to the protected resource metadata endpoint.</returns>
    /// <exception cref="ArgumentNullException">Thrown when resourceUri is null.</exception>
    public static string GetProtectedResourceMetadataUrl(Uri resourceUri)
    {
        if (resourceUri == null)
        {
            throw new ArgumentNullException(nameof(resourceUri), "Resource URI must be provided to build the protected resource metadata URL.");
        }
        
        // Create a new URI with the well-known path appended
        return new Uri(resourceUri, ".well-known/oauth-protected-resource").ToString();
    }
}
