using System.Text.Json.Serialization;

namespace ModelContextProtocol.Protocol.Auth;

/// <summary>
/// Represents the OAuth 2.0 client registration response as defined in RFC 7591.
/// </summary>
internal class ClientRegistrationResponse
{
    /// <summary>
    /// Gets or sets the OAuth 2.0 client identifier string.
    /// </summary>
    [JsonPropertyName("client_id")]
    public required string ClientId { get; set; }

    /// <summary>
    /// Gets or sets the OAuth 2.0 client secret string.
    /// </summary>
    [JsonPropertyName("client_secret")]
    public string? ClientSecret { get; set; }

    /// <summary>
    /// Gets or sets the time at which the client identifier was issued.
    /// </summary>
    [JsonPropertyName("client_id_issued_at")]
    public long? ClientIdIssuedAt { get; set; }

    /// <summary>
    /// Gets or sets the time at which the client secret will expire or 0 if it will not expire.
    /// </summary>
    [JsonPropertyName("client_secret_expires_at")]
    public long? ClientSecretExpiresAt { get; set; }
}