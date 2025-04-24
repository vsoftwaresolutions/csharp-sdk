using System;
using System.Collections.Generic;

namespace ModelContextProtocol.Protocol.Auth;

/// <summary>
/// Provides authorization options for MCP clients.
/// </summary>
public class McpAuthorizationOptions
{
    /// <summary>
    /// Gets or sets a delegate that handles the OAuth 2.0 authorization code flow.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This delegate is called when the server requires OAuth 2.0 authorization. It receives the client metadata
    /// and should return the redirect URI and authorization code received from the authorization server.
    /// </para>
    /// <para>
    /// If not provided, the client will not be able to authenticate with servers that require OAuth authentication.
    /// </para>
    /// </remarks>
    public Func<ClientMetadata, Task<(string RedirectUri, string Code)>>? AuthorizeCallback { get; init; }

    /// <summary>
    /// Gets or sets the client ID to use for OAuth authorization.
    /// </summary>
    /// <remarks>
    /// <para>
    /// If specified, this client ID will be used during the OAuth flow instead of performing dynamic client registration.
    /// This is useful when connecting to servers that have pre-registered clients.
    /// </para>
    /// </remarks>
    public string? ClientId { get; init; }

    /// <summary>
    /// Gets or sets the client secret associated with the client ID.
    /// </summary>
    /// <remarks>
    /// This is only required if the client was registered as a confidential client with the authorization server.
    /// Public clients don't require a client secret.
    /// </remarks>
    public string? ClientSecret { get; init; }

    /// <summary>
    /// Gets or sets the redirect URIs that can be used during the OAuth authorization flow.
    /// </summary>
    /// <remarks>
    /// <para>
    /// These URIs must match the redirect URIs registered with the authorization server for the client.
    /// </para>
    /// <para>
    /// If not specified and <see cref="ClientId"/> is set, a default value of
    /// "http://localhost:8888/callback" will be used.
    /// </para>
    /// </remarks>
    public ICollection<string>? RedirectUris { get; init; }

    /// <summary>
    /// Gets or sets the scopes to request during OAuth authorization.
    /// </summary>
    /// <remarks>
    /// <para>
    /// If not specified, the scopes will be determined from the server's resource metadata.
    /// </para>
    /// </remarks>
    public ICollection<string>? Scopes { get; init; }

    /// <summary>
    /// Gets or sets a custom authorization handler.
    /// </summary>
    /// <remarks>
    /// <para>
    /// If specified, this handler will be used to manage authorization with the server.
    /// </para>
    /// <para>
    /// If not provided, a default handler will be created using the other options.
    /// </para>
    /// </remarks>
    public IAuthorizationHandler? AuthorizationHandler { get; init; }
}