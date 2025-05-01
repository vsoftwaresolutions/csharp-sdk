using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Protocol.Auth;

namespace ModelContextProtocol.AspNetCore;

/// <summary>
/// An endpoint filter that handles authorization for MCP endpoints.
/// </summary>
internal class McpAuthorizationFilter : IEndpointFilter
{
    private readonly ILogger<McpAuthorizationFilter> _logger;
    private readonly IServerAuthorizationProvider _authProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="McpAuthorizationFilter"/> class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="authProvider">The authorization provider.</param>
    public McpAuthorizationFilter(
        ILogger<McpAuthorizationFilter> logger,
        IServerAuthorizationProvider authProvider)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _authProvider = authProvider ?? throw new ArgumentNullException(nameof(authProvider));
    }

    /// <inheritdoc/>
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var httpContext = context.HttpContext;

        // Check if the Authorization header is present
        if (!httpContext.Request.Headers.TryGetValue("Authorization", out var authHeader) || string.IsNullOrEmpty(authHeader))
        {
            // No Authorization header present, return 401 Unauthorized
            var prm = _authProvider.GetProtectedResourceMetadata();
            var prmUrl = GetPrmUrl(httpContext, prm.Resource);
            
            _logger.LogDebug("Authorization required, returning 401 Unauthorized with WWW-Authenticate header");
            httpContext.Response.StatusCode = StatusCodes.Status401Unauthorized;
            httpContext.Response.Headers.Append("WWW-Authenticate", $"Bearer resource_metadata=\"{prmUrl}\"");
            return Results.Empty;
        }

        // Validate the token - ensuring authHeader is a non-null string
        string authHeaderValue = authHeader.ToString();
        bool isValid = await _authProvider.ValidateTokenAsync(authHeaderValue);
        if (!isValid)
        {
            // Invalid token, return 401 Unauthorized
            var prm = _authProvider.GetProtectedResourceMetadata();
            var prmUrl = GetPrmUrl(httpContext, prm.Resource);
            
            _logger.LogDebug("Invalid authorization token, returning 401 Unauthorized");
            httpContext.Response.StatusCode = StatusCodes.Status401Unauthorized;
            httpContext.Response.Headers.Append("WWW-Authenticate", $"Bearer resource_metadata=\"{prmUrl}\"");
            return Results.Empty;
        }

        // Token is valid, proceed to the next filter
        return await next(context);
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
