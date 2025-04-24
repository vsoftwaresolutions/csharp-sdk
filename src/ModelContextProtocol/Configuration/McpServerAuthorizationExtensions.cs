using ModelContextProtocol.Protocol.Auth;
using ModelContextProtocol.Protocol.Types;
using ModelContextProtocol.Server;
using ModelContextProtocol.Utils;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for configuring authorization in MCP servers.
/// </summary>
public static class McpServerAuthorizationExtensions
{
    /// <summary>
    /// Adds authorization support to the MCP server.
    /// </summary>
    /// <param name="builder">The <see cref="IMcpServerBuilder"/> to configure.</param>
    /// <param name="authorizationProvider">The authorization provider that will validate tokens and provide metadata.</param>
    /// <returns>The <see cref="IMcpServerBuilder"/> so that additional calls can be chained.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="builder"/> or <paramref name="authorizationProvider"/> is <see langword="null"/>.</exception>
    public static IMcpServerBuilder WithAuthorization(
        this IMcpServerBuilder builder,
        IServerAuthorizationProvider authorizationProvider)
    {
        Throw.IfNull(builder);
        Throw.IfNull(authorizationProvider);

        builder.Services.Configure<McpServerOptions>(options =>
        {
            options.Capabilities ??= new ServerCapabilities();
            options.Capabilities.Authorization = new AuthorizationCapability
            {
                AuthorizationProvider = authorizationProvider
            };
        });

        return builder;
    }
}