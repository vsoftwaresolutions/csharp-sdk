namespace ModelContextProtocol.Protocol.Auth;

/// <summary>
/// Represents the context for authorization in an MCP client session.
/// </summary>
internal class AuthorizationContext
{
    /// <summary>
    /// Gets or sets the resource metadata.
    /// </summary>
    public ProtectedResourceMetadata? ResourceMetadata { get; set; }

    /// <summary>
    /// Gets or sets the authorization server metadata.
    /// </summary>
    public AuthorizationServerMetadata? AuthorizationServerMetadata { get; set; }

    /// <summary>
    /// Gets or sets the client registration response.
    /// </summary>
    public ClientRegistrationResponse? ClientRegistration { get; set; }

    /// <summary>
    /// Gets or sets the token response.
    /// </summary>
    public TokenResponse? TokenResponse { get; set; }

    /// <summary>
    /// Gets or sets the code verifier for PKCE.
    /// </summary>
    public string? CodeVerifier { get; set; }

    /// <summary>
    /// Gets or sets the redirect URI used in the authorization flow.
    /// </summary>
    public string? RedirectUri { get; set; }

    /// <summary>
    /// Gets or sets the time when the access token was issued.
    /// </summary>
    public DateTimeOffset? TokenIssuedAt { get; set; }

    /// <summary>
    /// Gets a value indicating whether the access token is valid.
    /// </summary>
    public bool HasValidToken => TokenResponse != null && 
                                (TokenResponse.ExpiresIn == null || 
                                 TokenIssuedAt != null && 
                                 DateTimeOffset.UtcNow < TokenIssuedAt.Value.AddSeconds(TokenResponse.ExpiresIn.Value - 60)); // 1 minute buffer

    /// <summary>
    /// Gets the access token for authentication.
    /// </summary>
    /// <returns>The access token if available, otherwise null.</returns>
    public string? GetAccessToken()
    {
        if (!HasValidToken)
        {
            return null;
        }

        // Since HasValidToken checks that TokenResponse isn't null, we should never have null here,
        // but we'll add an explicit null check to satisfy the compiler
        return TokenResponse?.AccessToken;
    }    
    
    /// <summary>
    /// Gets a value indicating whether a refresh token is available for refreshing the access token.
    /// </summary>
    public bool CanRefreshToken => TokenResponse?.RefreshToken != null && 
                                  ClientRegistration != null &&
                                  AuthorizationServerMetadata != null;
    
    /// <summary>
    /// Validates a URI resource against the resource URI from the metadata.
    /// </summary>
    /// <param name="resourceUri">The URI to validate.</param>
    /// <returns>True if the URIs match based on scheme and server components, otherwise false.</returns>
    public bool ValidateResourceUrl(Uri resourceUri)
    {
        if (ResourceMetadata == null || ResourceMetadata.Resource == null || resourceUri == null)
        {
            return false;
        }

        // Use Uri.Compare to properly compare the scheme and server components
        // UriComponents.SchemeAndServer includes the scheme, host, port, and user info
        // This handles edge cases like default ports, IPv6 addresses, etc.
        return Uri.Compare(resourceUri, ResourceMetadata.Resource, 
            UriComponents.SchemeAndServer, UriFormat.UriEscaped, 
            StringComparison.OrdinalIgnoreCase) == 0;
    }
}