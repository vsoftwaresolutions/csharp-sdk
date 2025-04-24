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
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    public async Task InvokeAsync(HttpContext context, IOptions<McpServerOptions> serverOptions)
    {
        // Check if authorization is configured
        var authCapability = serverOptions.Value.Capabilities?.Authorization;
        var authProvider = authCapability?.AuthorizationProvider;

        if (authProvider == null)
        {
            // Authorization is not configured, proceed to the next middleware
            await _next(context);
            return;
        }

        // Handle the PRM document endpoint
        if (context.Request.Path.StartsWithSegments("/.well-known/oauth-protected-resource"))
        {
            _logger.LogDebug("Serving Protected Resource Metadata document");
            context.Response.ContentType = "application/json";
            await JsonSerializer.SerializeAsync(
                context.Response.Body, 
                authProvider.GetProtectedResourceMetadata(),
                McpJsonUtilities.DefaultOptions.GetTypeInfo(typeof(ProtectedResourceMetadata)));
            return;
        }

        // Serve SSE and message endpoints with authorization
        if (context.Request.Path.StartsWithSegments("/sse") || 
            (context.Request.Path.Value?.EndsWith("/message") == true))
        {
            // Check if the Authorization header is present
            if (!context.Request.Headers.TryGetValue("Authorization", out var authHeader) || string.IsNullOrEmpty(authHeader))
            {
                // No Authorization header present, return 401 Unauthorized
                var prm = authProvider.GetProtectedResourceMetadata();
                var prmUrl = GetPrmUrl(context, prm.Resource);
                
                _logger.LogDebug("Authorization required, returning 401 Unauthorized with WWW-Authenticate header");
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                context.Response.Headers.Append("WWW-Authenticate", $"Bearer resource_metadata=\"{prmUrl}\"");
                return;
            }

            // Validate the token - ensuring authHeader is a non-null string
            string authHeaderValue = authHeader.ToString();
            bool isValid = await authProvider.ValidateTokenAsync(authHeaderValue);
            if (!isValid)
            {
                // Invalid token, return 401 Unauthorized
                var prm = authProvider.GetProtectedResourceMetadata();
                var prmUrl = GetPrmUrl(context, prm.Resource);
                
                _logger.LogDebug("Invalid authorization token, returning 401 Unauthorized");
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                context.Response.Headers.Append("WWW-Authenticate", $"Bearer resource_metadata=\"{prmUrl}\"");
                return;
            }
        }

        // Token is valid or endpoint doesn't require authentication, proceed to the next middleware
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