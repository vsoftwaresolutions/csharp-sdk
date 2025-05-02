using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Protocol.Auth;
using ModelContextProtocol.Protocol.Types;
using ModelContextProtocol.Server;
using ModelContextProtocol.Utils;

namespace ModelContextProtocol.Configuration;

/// <summary>
/// Extension methods for configuring authorization in MCP servers.
/// </summary>
public static class McpServerAuthorizationExtensions
{    
    /// <summary>
    /// Adds authorization support to the MCP server and automatically configures the required middleware.
    /// You don't need to call UseMcpAuthorization() separately - it will be handled automatically.
    /// </summary>
    /// <param name="builder">The <see cref="IMcpServerBuilder"/> to configure.</param>
    /// <param name="authorizationProvider">The authorization provider that will validate tokens and provide metadata.</param>
    /// <returns>The <see cref="IMcpServerBuilder"/> so that additional calls can be chained.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="builder"/> or <paramref name="authorizationProvider"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// This method automatically configures all the necessary components for authorization:
    /// 1. Registers the authorization provider in the DI container
    /// 2. Configures authorization middleware to serve the protected resource metadata
    /// 3. Adds authorization to MCP endpoints when they are mapped
    /// 
    /// You no longer need to call app.UseMcpAuthorization() explicitly.
    /// </remarks>
    public static IMcpServerBuilder WithAuthorization(
        this IMcpServerBuilder builder,
        IServerAuthorizationProvider authorizationProvider)
    {
        Throw.IfNull(builder);
        Throw.IfNull(authorizationProvider);        
        
        // Register the authorization provider in the DI container
        builder.Services.AddSingleton(authorizationProvider);

        builder.Services.Configure<McpServerOptions>(options =>
        {
            options.Capabilities ??= new ServerCapabilities();
        });

        return builder;
    }
}