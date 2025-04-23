using System.Text.Json;

namespace ModelContextProtocol.Protocol.Auth;

/// <summary>
/// Defines the interface for MCP server authorization providers.
/// </summary>
/// <remarks>
/// This interface is implemented by authorization providers that enable MCP servers to validate tokens
/// and control access to protected resources.
/// </remarks>
public interface IMcpServerAuthorizationProvider
{
    /// <summary>
    /// Gets the Protected Resource Metadata (PRM) for the server.
    /// </summary>
    /// <returns>The protected resource metadata.</returns>
    ProtectedResourceMetadata GetProtectedResourceMetadata();

    /// <summary>
    /// Validates the provided authorization token.
    /// </summary>
    /// <param name="authorizationHeader">The authorization header value.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous validation operation. The task result contains <see langword="true"/> if the token is valid; otherwise, <see langword="false"/>.</returns>
    Task<bool> ValidateTokenAsync(string authorizationHeader);
}