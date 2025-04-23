using ModelContextProtocol.Protocol.Auth;

namespace ModelContextProtocol.Protocol.Types;

/// <summary>
/// Defines the capabilities of a server for supporting OAuth 2.0 authorization.
/// </summary>
/// <remarks>
/// This capability is advertised by servers that support OAuth 2.0 authorization flows
/// and require clients to authenticate using bearer tokens.
/// </remarks>
public class AuthorizationCapability
{
    /// <summary>
    /// Gets or sets the authorization provider that handles token validation and provides
    /// metadata about the protected resource.
    /// </summary>
    public IMcpServerAuthorizationProvider? AuthorizationProvider { get; set; }
}