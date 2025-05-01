using System.Text.Json.Serialization;

namespace ModelContextProtocol.Protocol.Auth;

/// <summary>
/// Represents the Protected Resource Metadata (PRM) document for an OAuth 2.0 protected resource.
/// </summary>
/// <remarks>
/// The PRM document describes the properties and requirements of a protected resource, including
/// the authorization servers that can be used to obtain access tokens and the scopes that are supported.
/// This document is served at the standard path "/.well-known/oauth-protected-resource" by MCP servers
/// that have authorization enabled.
/// </remarks>
public class ProtectedResourceMetadata
{
    /// <summary>
    /// Gets or sets the resource identifier URI.
    /// </summary>
    [JsonPropertyName("resource")]
    public required Uri Resource { get; set; }

    /// <summary>
    /// Gets or sets the authorization servers that can be used for authentication.
    /// </summary>
    [JsonPropertyName("authorization_servers")]
    public required Uri[] AuthorizationServers { get; set; }

    /// <summary>
    /// Gets or sets the bearer token methods supported by the resource.
    /// </summary>
    [JsonPropertyName("bearer_methods_supported")]
    public string[]? BearerMethodsSupported { get; set; } = ["header"];

    /// <summary>
    /// Gets or sets the scopes supported by the resource.
    /// </summary>
    [JsonPropertyName("scopes_supported")]
    public string[]? ScopesSupported { get; set; }

    /// <summary>
    /// Gets or sets the URL to the resource documentation.
    /// </summary>
    [JsonPropertyName("resource_documentation")]
    public Uri? ResourceDocumentation { get; set; }
}