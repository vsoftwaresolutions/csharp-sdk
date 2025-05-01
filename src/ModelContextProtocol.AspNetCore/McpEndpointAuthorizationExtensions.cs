using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Protocol.Auth;

namespace ModelContextProtocol.AspNetCore;

/// <summary>
/// Provides extension methods for adding MCP authorization to endpoints.
/// </summary>
public static class McpEndpointAuthorizationExtensions
{
    /// <summary>
    /// Adds MCP authorization filter to an endpoint.
    /// </summary>
    /// <param name="builder">The endpoint convention builder.</param>
    /// <param name="authProvider">The authorization provider.</param>
    /// <param name="serviceProvider">The service provider.</param>
    /// <returns>The builder for chaining.</returns>    
    public static IEndpointConventionBuilder AddMcpAuthorization(
        this IEndpointConventionBuilder builder, 
        IServerAuthorizationProvider authProvider,
        IServiceProvider serviceProvider)
    {
        if (authProvider == null)
        {
            return builder; // No authorization needed
        }

        var logger = serviceProvider.GetRequiredService<ILogger<McpEndpointAuthorizationFilter>>();
        var filter = new McpEndpointAuthorizationFilter(logger, authProvider);
        
        return builder.AddEndpointFilter(filter);
    }

    /// <summary>
    /// Adds MCP authorization filter to multiple endpoints.
    /// </summary>
    /// <param name="endpoints">The collection of endpoint convention builders.</param>
    /// <param name="authProvider">The authorization provider.</param>
    /// <param name="serviceProvider">The service provider.</param>
    /// <returns>The original collection for chaining.</returns>    
    public static IEnumerable<IEndpointConventionBuilder> AddMcpAuthorization(
        this IEnumerable<IEndpointConventionBuilder> endpoints,
        IServerAuthorizationProvider authProvider,
        IServiceProvider serviceProvider)
    {
        if (authProvider == null)
        {
            return endpoints; // No authorization needed
        }

        var logger = serviceProvider.GetRequiredService<ILogger<McpEndpointAuthorizationFilter>>();
        var filter = new McpEndpointAuthorizationFilter(logger, authProvider);

        foreach (var endpoint in endpoints)
        {
            endpoint.AddEndpointFilter(filter);
        }

        return endpoints;
    }
}
