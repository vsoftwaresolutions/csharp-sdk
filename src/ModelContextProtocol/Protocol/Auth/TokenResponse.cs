using System.Text.Json.Serialization;

namespace ModelContextProtocol.Protocol.Auth;

/// <summary>
/// Represents the OAuth 2.0 token response as defined in RFC 6749.
/// </summary>
internal class TokenResponse
{
    /// <summary>
    /// Gets or sets the access token issued by the authorization server.
    /// </summary>
    [JsonPropertyName("access_token")]
    public required string AccessToken { get; set; }

    /// <summary>
    /// Gets or sets the type of the token issued.
    /// </summary>
    [JsonPropertyName("token_type")]
    public required string TokenType { get; set; }

    /// <summary>
    /// Gets or sets the lifetime in seconds of the access token.
    /// </summary>
    [JsonPropertyName("expires_in")]
    public long? ExpiresIn { get; set; }

    /// <summary>
    /// Gets or sets the refresh token, which can be used to obtain new access tokens.
    /// </summary>
    [JsonPropertyName("refresh_token")]
    public string? RefreshToken { get; set; }

    /// <summary>
    /// Gets or sets the scope of the access token.
    /// </summary>
    [JsonPropertyName("scope")]
    public string? Scope { get; set; }
}