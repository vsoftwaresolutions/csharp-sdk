namespace ModelContextProtocol.Protocol.Transport;

using ModelContextProtocol.Protocol.Auth;

/// <summary>
/// Provides options for configuring <see cref="SseClientTransport"/> instances.
/// </summary>
public record SseClientTransportOptions
{
    /// <summary>
    /// Gets or sets the base address of the server for SSE connections.
    /// </summary>
    public required Uri Endpoint
    {
        get;
        init
        {
            if (value is null)
            {
                throw new ArgumentNullException(nameof(value), "Endpoint cannot be null.");
            }
            if (!value.IsAbsoluteUri)
            {
                throw new ArgumentException("Endpoint must be an absolute URI.", nameof(value));
            }
            if (value.Scheme != Uri.UriSchemeHttp && value.Scheme != Uri.UriSchemeHttps)
            {
                throw new ArgumentException("Endpoint must use HTTP or HTTPS scheme.", nameof(value));
            }

            field = value;
        }
    }

    /// <summary>
    /// Gets a transport identifier used for logging purposes.
    /// </summary>
    public string? Name { get; init; }

    /// <summary>
    /// Gets or sets a timeout used to establish the initial connection to the SSE server.
    /// </summary>
    /// <remarks>
    /// This timeout controls how long the client waits for:
    /// <list type="bullet">
    ///   <item><description>The initial HTTP connection to be established with the SSE server</description></item>
    ///   <item><description>The endpoint event to be received, which indicates the message endpoint URL</description></item>
    /// </list>
    /// If the timeout expires before the connection is established, a <see cref="TimeoutException"/> will be thrown.
    /// </remarks>
    public TimeSpan ConnectionTimeout { get; init; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Gets custom HTTP headers to include in requests to the SSE server.
    /// </summary>
    /// <remarks>
    /// Use this property to specify custom HTTP headers that should be sent with each request to the server.
    /// </remarks>
    public Dictionary<string, string>? AdditionalHeaders { get; init; }

    /// <summary>
    /// Gets or sets a delegate that handles the OAuth 2.0 authorization code flow.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This delegate is called when the SSE server requires OAuth 2.0 authorization. It receives the client metadata
    /// and should return the redirect URI and authorization code received from the authorization server.
    /// </para>
    /// <para>
    /// If not provided, the client will not be able to authenticate with servers that require OAuth authentication.
    /// </para>
    /// </remarks>
    public Func<ClientMetadata, Task<(string RedirectUri, string Code)>>? AuthorizeCallback { get; init; }

    /// <summary>
    /// Gets or sets a custom authorization handler.
    /// </summary>
    /// <remarks>
    /// <para>
    /// If specified, this handler will be used to manage authorization with the SSE server.
    /// </para>
    /// <para>
    /// If not provided, a default handler will be created using the <see cref="AuthorizeCallback"/>.
    /// </para>
    /// </remarks>
    public IAuthorizationHandler? AuthorizationHandler { get; init; }
}