using Microsoft.Extensions.Logging;
using ModelContextProtocol.Protocol.Auth;
using ModelContextProtocol.Utils;

namespace ModelContextProtocol.Protocol.Transport;

/// <summary>
/// Provides an <see cref="IClientTransport"/> over HTTP using the Server-Sent Events (SSE) protocol.
/// </summary>
/// <remarks>
/// <para>
/// This transport connects to an MCP server over HTTP using SSE,
/// allowing for real-time server-to-client communication with a standard HTTP request.
/// Unlike the <see cref="StdioClientTransport"/>, this transport connects to an existing server
/// rather than launching a new process.
/// </para>
/// <para>
/// The SSE transport can handle OAuth 2.0 authorization flows when connecting to servers that require authentication.
/// </para>
/// </remarks>
public sealed class SseClientTransport : IClientTransport, IAsyncDisposable
{
    private readonly SseClientTransportOptions _options;
    private readonly HttpClient _httpClient;
    private readonly ILoggerFactory? _loggerFactory;
    private readonly bool _ownsHttpClient;

    /// <summary>
    /// Initializes a new instance of the <see cref="SseClientTransport"/> class.
    /// </summary>
    /// <param name="transportOptions">Configuration options for the transport.</param>
    /// <param name="loggerFactory">Logger factory for creating loggers used for diagnostic output during transport operations.</param>
    public SseClientTransport(SseClientTransportOptions transportOptions, ILoggerFactory? loggerFactory = null)
        : this(transportOptions, new HttpClient(), loggerFactory, ownsHttpClient: true)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="SseClientTransport"/> class with a provided HTTP client.
    /// </summary>
    /// <param name="transportOptions">Configuration options for the transport.</param>
    /// <param name="httpClient">The HTTP client instance used for requests.</param>
    /// <param name="loggerFactory">Logger factory for creating loggers used for diagnostic output during transport operations.</param>
    /// <param name="ownsHttpClient">
    /// <see langword="true"/> to dispose of <paramref name="httpClient"/> when the transport is disposed; 
    /// <see langword="false"/> if the caller is retaining ownership of the <paramref name="httpClient"/>'s lifetime.
    /// </param>
    public SseClientTransport(SseClientTransportOptions transportOptions, HttpClient httpClient, ILoggerFactory? loggerFactory = null, bool ownsHttpClient = false)
    {
        Throw.IfNull(transportOptions);
        Throw.IfNull(httpClient);

        _options = transportOptions;
        _httpClient = httpClient;
        _loggerFactory = loggerFactory;
        _ownsHttpClient = ownsHttpClient;
        Name = transportOptions.Name ?? transportOptions.Endpoint.ToString();
    }

    /// <inheritdoc />
    public string Name { get; }

    /// <summary>
    /// Creates a delegate that can handle the OAuth 2.0 authorization code flow using an HTTP listener.
    /// </summary>
    /// <param name="openBrowser">A function that opens a URL in the browser.</param>
    /// <param name="hostname">The hostname to listen on for the redirect URI. Default is "localhost".</param>
    /// <param name="listenPort">The port to listen on for the redirect URI. Default is 8888.</param>
    /// <param name="redirectPath">The path for the redirect URI. Default is "/callback".</param>
    /// <param name="successHtml">The HTML content to display on successful authorization. If null, a default message is shown.</param>
    /// <param name="errorHtml">The HTML template to display on failed authorization. If null, a default message is shown. Use {0} as a placeholder for the error message.</param>
    /// <returns>A delegate that can be used for the <see cref="McpAuthorizationOptions.AuthorizeCallback"/> property.</returns>
    /// <remarks>
    /// <para>
    /// This method creates a delegate that implements a complete OAuth 2.0 authorization code flow using an HTTP listener. 
    /// When called, it will:
    /// </para>
    /// <list type="bullet">
    /// <item><description>Open the authorization URL in the browser</description></item>
    /// <item><description>Start an HTTP listener to receive the authorization code</description></item>
    /// <item><description>Return the redirect URI and authorization code when received</description></item>
    /// </list>
    /// <para>
    /// You can customize the hostname, port, and path for the redirect URI to match your OAuth client configuration.
    /// </para>
    /// </remarks>
    public static Func<ClientMetadata, Task<(string RedirectUri, string Code)>> CreateHttpListenerAuthorizeCallback(
        Func<string, Task> openBrowser,
        string hostname = "localhost",
        int listenPort = 8888,
        string redirectPath = "/callback",
        string? successHtml = null,
        string? errorHtml = null)
    {
        return async (ClientMetadata clientMetadata) =>
        {
            // Default redirect URI based on parameters
            var defaultRedirectUri = $"http://{hostname}:{listenPort}{redirectPath}";
            
            // First, try to find a matching redirect URI from the client metadata
            var redirectUri = defaultRedirectUri;
            var hostPrefix = $"http://{hostname}";
            
            foreach (var uri in clientMetadata.RedirectUris)
            {
                if (uri.StartsWith(hostPrefix, StringComparison.OrdinalIgnoreCase))
                {
                    redirectUri = uri;
                    
                    // Parse the port and path from the selected URI to ensure we listen on the correct endpoint
                    if (Uri.TryCreate(uri, UriKind.Absolute, out var parsedUri))
                    {
                        listenPort = parsedUri.IsDefaultPort ? 80 : parsedUri.Port;
                        redirectPath = parsedUri.AbsolutePath;
                    }
                    
                    break;
                }
            }
            
            // Use a TaskCompletionSource to wait for the authorization code
            var authCodeTcs = new TaskCompletionSource<string>();
            
            // Start an HTTP listener to listen for the authorization code
            using var listener = new System.Net.HttpListener();
            
            // Ensure the URI format is correct for HttpListener
            var listenerPrefix = $"http://{hostname}:{listenPort}/";
            if (redirectPath.Length > 1)
            {
                // If path is something like "/callback", we need to listen on all paths that start with it
                var basePath = redirectPath.TrimEnd('/').TrimStart('/');
                listenerPrefix = $"http://{hostname}:{listenPort}/{basePath}/";
            }
            
            listener.Prefixes.Add(listenerPrefix);
            listener.Start();
            
            // Default HTML responses
            var defaultSuccessHtml = "<html><body><h1>Authorization Successful</h1><p>You can now close this window and return to the application.</p></body></html>";
            var defaultErrorHtml = "<html><body><h1>Authorization Failed</h1><p>Error: {0}</p></body></html>";
            
            // Start listening for the callback asynchronously
            var listenerTask = Task.Run(async () =>
            {
                try
                {
                    var context = await listener.GetContextAsync();
                    var request = context.Request;
                    
                    // Get the authorization code from the query string
                    var code = request.QueryString["code"];
                    var error = request.QueryString["error"];
                    
                    // Send a response to the browser
                    var response = context.Response;
                    response.ContentType = "text/html";
                    string responseHtml;
                    
                    if (!string.IsNullOrEmpty(error))
                    {
                        responseHtml = string.Format(errorHtml ?? defaultErrorHtml, error);
                        authCodeTcs.SetException(new McpException($"Authorization failed: {error}", McpErrorCode.InvalidRequest));
                    }
                    else if (string.IsNullOrEmpty(code))
                    {
                        responseHtml = string.Format(errorHtml ?? defaultErrorHtml, "No authorization code received");
                        authCodeTcs.SetException(new McpException("No authorization code received", McpErrorCode.InvalidRequest));
                    }
                    else
                    {
                        responseHtml = successHtml ?? defaultSuccessHtml;
                        authCodeTcs.SetResult(code);
                    }
                    
                    var buffer = System.Text.Encoding.UTF8.GetBytes(responseHtml);
                    response.ContentLength64 = buffer.Length;
                    await response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
                    response.Close();
                }
                catch (Exception ex)
                {
                    authCodeTcs.TrySetException(ex);
                }
                finally
                {
                    listener.Close();
                }
            });
            
            // Open the authorization URL in the browser
            if (clientMetadata.ClientUri != null)
            {
                await openBrowser(clientMetadata.ClientUri);
            }
            else
            {
                authCodeTcs.SetException(new McpException("No authorization URL provided in client metadata", McpErrorCode.InvalidRequest));
            }
            
            // Wait for the authorization code
            var code = await authCodeTcs.Task;
            
            return (redirectUri, code);
        };
    }

    /// <summary>
    /// Creates a delegate that can handle the OAuth 2.0 authorization code flow using a local HTTP listener.
    /// </summary>
    /// <param name="openBrowser">A function that opens a URL in the browser.</param>
    /// <param name="listenPort">The local port to listen on for the redirect URI.</param>
    /// <param name="redirectPath">The path for the redirect URI.</param>
    /// <returns>A delegate that can be used for the <see cref="McpAuthorizationOptions.AuthorizeCallback"/> property.</returns>
    /// <remarks>
    /// This is a convenience method that calls <see cref="CreateHttpListenerAuthorizeCallback"/> with "localhost" as the hostname.
    /// </remarks>
    [Obsolete("Use CreateHttpListenerAuthorizeCallback instead. This method will be removed in a future version.")]
    public static Func<ClientMetadata, Task<(string RedirectUri, string Code)>> CreateLocalServerAuthorizeCallback(
        Func<string, Task> openBrowser,
        int listenPort = 8888,
        string redirectPath = "/callback")
    {
        return CreateHttpListenerAuthorizeCallback(openBrowser, "localhost", listenPort, redirectPath);
    }

    /// <inheritdoc />
    public async Task<ITransport> ConnectAsync(CancellationToken cancellationToken = default)
    {
        var sessionTransport = new SseClientSessionTransport(_options, _httpClient, _loggerFactory, Name);

        try
        {
            await sessionTransport.ConnectAsync(cancellationToken).ConfigureAwait(false);
            return sessionTransport;
        }
        catch
        {
            await sessionTransport.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        if (_ownsHttpClient)
        {
            _httpClient.Dispose();
        }

        return default;
    }
}