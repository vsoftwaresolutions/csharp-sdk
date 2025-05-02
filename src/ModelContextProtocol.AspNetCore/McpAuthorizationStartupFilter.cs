// filepath: c:\Users\ddelimarsky\source\csharp-sdk\src\ModelContextProtocol.AspNetCore\McpAuthorizationStartupFilter.cs
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Protocol.Auth;
using System;

namespace ModelContextProtocol.AspNetCore;

/// <summary>
/// StartupFilter that automatically adds the MCP authorization middleware when authorization is configured.
/// </summary>
internal class McpAuthorizationStartupFilter : IStartupFilter
{
    /// <summary>
    /// Configures the middleware pipeline to include MCP authorization middleware when needed.
    /// </summary>
    /// <param name="next">The next configurator in the chain.</param>
    /// <returns>A new pipeline configuration action.</returns>
    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
    {
        return app =>
        {
            // Check if authorization provider is registered
            bool hasAuthProvider = app.ApplicationServices.GetService<IServerAuthorizationProvider>() != null;
            
            // If authorization is configured, add the middleware
            if (hasAuthProvider)
            {
                app.UseMcpAuthorization();
            }
            
            // Continue with the rest of the pipeline configuration
            next(app);
        };
    }
}
