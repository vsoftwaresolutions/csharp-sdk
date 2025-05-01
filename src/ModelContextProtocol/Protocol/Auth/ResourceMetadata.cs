using System.Text.Json.Serialization;

namespace ModelContextProtocol.Protocol.Auth;

/// <summary>
/// Represents the resource metadata from the WWW-Authenticate header in a 401 Unauthorized response.
/// </summary>
public class ResourceMetadata
{
    /// <summary>
    /// Gets or sets the resource identifier URI.
    /// </summary>
    [JsonPropertyName("resource")]
    public required string Resource { get; set; }

    /// <summary>
    /// Gets or sets the authorization servers that can be used for authentication.
    /// </summary>
    [JsonPropertyName("authorization_servers")]
    public required string[] AuthorizationServers { get; set; }

    /// <summary>
    /// Gets or sets the bearer token methods supported by the resource.
    /// </summary>
    [JsonPropertyName("bearer_methods_supported")]
    public string[]? BearerMethodsSupported { get; set; }

    /// <summary>
    /// Gets or sets the scopes supported by the resource.
    /// </summary>
    [JsonPropertyName("scopes_supported")]
    public string[]? ScopesSupported { get; set; }

    /// <summary>
    /// Gets or sets the URL to the resource documentation.
    /// </summary>
    [JsonPropertyName("resource_documentation")]
    public string? ResourceDocumentation { get; set; }
}