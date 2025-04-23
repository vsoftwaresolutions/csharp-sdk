using System.Text.Json.Serialization;

namespace ModelContextProtocol.Protocol.Auth;

/// <summary>
/// Represents the OAuth 2.0 client registration metadata as defined in RFC 7591.
/// </summary>
public class ClientMetadata
{
    /// <summary>
    /// Gets or sets the array of redirection URI strings for use in redirect-based flows.
    /// </summary>
    [JsonPropertyName("redirect_uris")]
    public required string[] RedirectUris { get; set; }

    /// <summary>
    /// Gets or sets the requested authentication method for the token endpoint.
    /// </summary>
    [JsonPropertyName("token_endpoint_auth_method")]
    public string? TokenEndpointAuthMethod { get; set; } = "client_secret_basic";

    /// <summary>
    /// Gets or sets the array of OAuth 2.0 grant type strings that the client can use at the token endpoint.
    /// </summary>
    [JsonPropertyName("grant_types")]
    public string[]? GrantTypes { get; set; } = ["authorization_code", "refresh_token"];

    /// <summary>
    /// Gets or sets the array of the OAuth 2.0 response type strings that the client can use at the authorization endpoint.
    /// </summary>
    [JsonPropertyName("response_types")]
    public string[]? ResponseTypes { get; set; } = ["code"];

    /// <summary>
    /// Gets or sets the human-readable string name of the client.
    /// </summary>
    [JsonPropertyName("client_name")]
    public string? ClientName { get; set; }

    /// <summary>
    /// Gets or sets the URL string of a web page providing information about the client.
    /// </summary>
    [JsonPropertyName("client_uri")]
    public string? ClientUri { get; set; }

    /// <summary>
    /// Gets or sets the URL string that references a logo for the client.
    /// </summary>
    [JsonPropertyName("logo_uri")]
    public string? LogoUri { get; set; }

    /// <summary>
    /// Gets or sets the string containing a space-separated list of scope values that the client can use.
    /// </summary>
    [JsonPropertyName("scope")]
    public string? Scope { get; set; }

    /// <summary>
    /// Gets or sets the array of strings representing ways to contact people responsible for this client.
    /// </summary>
    [JsonPropertyName("contacts")]
    public string[]? Contacts { get; set; }

    /// <summary>
    /// Gets or sets the URL string that points to a human-readable terms of service document.
    /// </summary>
    [JsonPropertyName("tos_uri")]
    public string? TosUri { get; set; }

    /// <summary>
    /// Gets or sets the URL string that points to a human-readable privacy policy document.
    /// </summary>
    [JsonPropertyName("policy_uri")]
    public string? PolicyUri { get; set; }

    /// <summary>
    /// Gets or sets the URL string referencing the client's JSON Web Key Set document.
    /// </summary>
    [JsonPropertyName("jwks_uri")]
    public string? JwksUri { get; set; }

    /// <summary>
    /// Gets or sets the client's JSON Web Key Set document value.
    /// </summary>
    [JsonPropertyName("jwks")]
    public object? Jwks { get; set; }

    /// <summary>
    /// Gets or sets a unique identifier string assigned by the client developer or software publisher.
    /// </summary>
    [JsonPropertyName("software_id")]
    public string? SoftwareId { get; set; }

    /// <summary>
    /// Gets or sets the version identifier string for the client software.
    /// </summary>
    [JsonPropertyName("software_version")]
    public string? SoftwareVersion { get; set; }
}