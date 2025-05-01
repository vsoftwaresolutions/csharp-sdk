using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Protocol.Auth;

namespace ModelContextProtocol.AspNetCore;

/// <summary>
/// Factory for creating <see cref="McpAuthorizationFilter"/> instances.
/// </summary>
internal class McpAuthorizationFilterFactory
{
    private readonly IServiceProvider _serviceProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="McpAuthorizationFilterFactory"/> class.
    /// </summary>
    /// <param name="serviceProvider">The service provider.</param>
    public McpAuthorizationFilterFactory(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
    }

    /// <summary>
    /// Creates an endpoint filter delegate for authorization.
    /// </summary>
    /// <param name="context">The endpoint filter factory context.</param>
    /// <param name="next">The next filter delegate in the pipeline.</param>
    /// <returns>The filter delegate.</returns>
    public EndpointFilterDelegate Create(EndpointFilterFactoryContext context, EndpointFilterDelegate next)
    {
        // This factory creates a filter that checks if the current endpoint is an SSE or message endpoint
        // and applies authorization only to those endpoints
        return async invocationContext =>
        {
            var httpContext = invocationContext.HttpContext;
            var path = httpContext.Request.Path.Value?.TrimEnd('/');

            // Only apply authorization to /sse and /message endpoints
            if (path != null && (path.EndsWith("/sse") || path.EndsWith("/message")))
            {
                var authProvider = _serviceProvider.GetRequiredService<IServerAuthorizationProvider>();
                var logger = _serviceProvider.GetRequiredService<ILogger<McpAuthorizationFilter>>();

                var filter = new McpAuthorizationFilter(logger, authProvider);
                return await filter.InvokeAsync(invocationContext, next);
            }

            // For all other endpoints, just invoke the next filter
            return await next(invocationContext);
        };
    }
}
