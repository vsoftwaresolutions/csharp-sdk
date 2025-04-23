using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace ModelContextProtocol.AspNetCore;

/// <summary>
/// Extension methods for using MCP authorization in ASP.NET Core applications.
/// </summary>
public static class McpAuthorizationExtensions
{
    /// <summary>
    /// Adds MCP authorization middleware to the specified <see cref="IApplicationBuilder"/>, which enables
    /// OAuth 2.0 authorization for MCP servers.
    /// </summary>
    /// <param name="builder">The <see cref="IApplicationBuilder"/> to add the middleware to.</param>
    /// <returns>A reference to this instance after the operation has completed.</returns>
    public static IApplicationBuilder UseMcpAuthorization(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<McpAuthorizationMiddleware>();
    }
}