using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Protocol.Auth;
using ModelContextProtocol.Server;
using ModelContextProtocol.Utils.Json;
using System.Text.Json;

namespace ModelContextProtocol.AspNetCore;

/// <summary>
/// Middleware that handles authorization for MCP servers.
/// </summary>
internal class AuthorizationMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<AuthorizationMiddleware> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="AuthorizationMiddleware"/> class.
    /// </summary>
    /// <param name="next">The next middleware in the pipeline.</param>
    /// <param name="logger">The logger factory.</param>
    public AuthorizationMiddleware(RequestDelegate next, ILogger<AuthorizationMiddleware> logger)
    {
        _next = next ?? throw new ArgumentNullException(nameof(next));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Processes a request.
    /// </summary>
    /// <param name="context">The HTTP context.</param>
    /// <param name="serverOptions">The MCP server options.</param>
    /// <param name="authProvider">The authorization provider.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    public async Task InvokeAsync(
        HttpContext context, 
        IOptions<McpServerOptions> serverOptions,
        IServerAuthorizationProvider? authProvider = null)
    {
        // Check if authorization is configured
        if (authProvider == null)
        {
            // Authorization is not configured, proceed to the next middleware
            await _next(context);
            return;
        }

        // Handle the PRM document endpoint if not handled by the endpoint
        if (context.Request.Path.StartsWithSegments("/.well-known/oauth-protected-resource") &&
            context.GetEndpoint() == null)
        {
            _logger.LogDebug("Serving Protected Resource Metadata document");
            context.Response.ContentType = "application/json";
            await JsonSerializer.SerializeAsync(
                context.Response.Body, 
                authProvider.GetProtectedResourceMetadata(),
                McpJsonUtilities.DefaultOptions.GetTypeInfo(typeof(ProtectedResourceMetadata)));
            return;
        }

        // Proceed to the next middleware - authorization for SSE and message endpoints
        // is now handled by endpoint filters
        await _next(context);
    }

    private static string GetPrmUrl(HttpContext context, string resourceUri)
    {
        // Use the actual resource URI from PRM if it's an absolute URL, otherwise build the URL
        if (Uri.TryCreate(resourceUri, UriKind.Absolute, out _))
        {
            return $"{resourceUri.TrimEnd('/')}/.well-known/oauth-protected-resource";
        }

        // Build the URL from the current request
        var request = context.Request;
        var scheme = request.Scheme;
        var host = request.Host.Value;
        return $"{scheme}://{host}/.well-known/oauth-protected-resource";
    }
}