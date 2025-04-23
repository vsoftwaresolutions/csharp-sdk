using System.Text.Json.Serialization;

namespace ModelContextProtocol.Protocol.Auth;

/// <summary>
/// Represents OAuth 2.0 authorization server metadata as defined in RFC 8414.
/// </summary>
internal class AuthorizationServerMetadata
{
    /// <summary>
    /// Gets or sets the authorization endpoint URL.
    /// </summary>
    [JsonPropertyName("authorization_endpoint")]
    public required string AuthorizationEndpoint { get; set; }

    /// <summary>
    /// Gets or sets the token endpoint URL.
    /// </summary>
    [JsonPropertyName("token_endpoint")]
    public required string TokenEndpoint { get; set; }

    /// <summary>
    /// Gets or sets the client registration endpoint URL.
    /// </summary>
    [JsonPropertyName("registration_endpoint")]
    public string? RegistrationEndpoint { get; set; }

    /// <summary>
    /// Gets or sets the token revocation endpoint URL.
    /// </summary>
    [JsonPropertyName("revocation_endpoint")]
    public string? RevocationEndpoint { get; set; }

    /// <summary>
    /// Gets or sets the response types supported by the authorization server.
    /// </summary>
    [JsonPropertyName("response_types_supported")]
    public string[]? ResponseTypesSupported { get; set; } = ["code"];

    /// <summary>
    /// Gets or sets the grant types supported by the authorization server.
    /// </summary>
    [JsonPropertyName("grant_types_supported")]
    public string[]? GrantTypesSupported { get; set; } = ["authorization_code", "refresh_token"];

    /// <summary>
    /// Gets or sets the token endpoint authentication methods supported by the authorization server.
    /// </summary>
    [JsonPropertyName("token_endpoint_auth_methods_supported")]
    public string[]? TokenEndpointAuthMethodsSupported { get; set; } = ["client_secret_basic"];

    /// <summary>
    /// Gets or sets the code challenge methods supported by the authorization server.
    /// </summary>
    [JsonPropertyName("code_challenge_methods_supported")]
    public string[]? CodeChallengeMethodsSupported { get; set; } = ["S256"];

    /// <summary>
    /// Gets or sets the issuer identifier.
    /// </summary>
    [JsonPropertyName("issuer")]
    public string? Issuer { get; set; }

    /// <summary>
    /// Gets or sets the scopes supported by the authorization server.
    /// </summary>
    [JsonPropertyName("scopes_supported")]
    public string[]? ScopesSupported { get; set; }
}