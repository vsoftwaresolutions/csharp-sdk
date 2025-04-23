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
/// You can provide an <see cref="AuthorizeCallback"/> in the transport options to handle the user authentication part
/// of the OAuth flow.
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
    /// Creates a delegate that can handle the OAuth 2.0 authorization code flow.
    /// </summary>
    /// <param name="openBrowser">A function that opens a URL in the browser.</param>
    /// <param name="listenPort">The local port to listen on for the redirect URI.</param>
    /// <param name="redirectPath">The path for the redirect URI.</param>
    /// <returns>A delegate that can be used for the <see cref="SseClientTransportOptions.AuthorizeCallback"/> property.</returns>
    /// <remarks>
    /// <para>
    /// This method creates a delegate that implements a complete local OAuth 2.0 authorization code flow. 
    /// When called, it will:
    /// </para>
    /// <list type="bullet">
    /// <item><description>Open the authorization URL in the browser</description></item>
    /// <item><description>Start a local HTTP server to listen for the authorization code</description></item>
    /// <item><description>Return the redirect URI and authorization code when received</description></item>
    /// </list>
    /// <para>
    /// You can customize the port and path for the redirect URI. By default, it uses port 8888 and path "/callback".
    /// </para>
    /// </remarks>
    public static Func<ClientMetadata, Task<(string RedirectUri, string Code)>> CreateLocalServerAuthorizeCallback(
        Func<string, Task> openBrowser,
        int listenPort = 8888,
        string redirectPath = "/callback")
    {
        return async (ClientMetadata clientMetadata) =>
        {
            var redirectUri = $"http://localhost:{listenPort}{redirectPath}";
            
            // Use a TaskCompletionSource to wait for the authorization code
            var authCodeTcs = new TaskCompletionSource<string>();
            
            // Start a local HTTP server to listen for the authorization code
            using var listener = new System.Net.HttpListener();
            listener.Prefixes.Add($"http://localhost:{listenPort}/");
            listener.Start();
            
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
                    var responseHtml = "<html><body><h1>Authorization Successful</h1><p>You can now close this window and return to the application.</p></body></html>";
                    
                    if (!string.IsNullOrEmpty(error))
                    {
                        responseHtml = $"<html><body><h1>Authorization Failed</h1><p>Error: {error}</p></body></html>";
                        authCodeTcs.SetException(new McpException($"Authorization failed: {error}", McpErrorCode.AuthenticationFailed));
                    }
                    else if (string.IsNullOrEmpty(code))
                    {
                        responseHtml = "<html><body><h1>Authorization Failed</h1><p>No authorization code received.</p></body></html>";
                        authCodeTcs.SetException(new McpException("No authorization code received", McpErrorCode.AuthenticationFailed));
                    }
                    else
                    {
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
            foreach (var uri in clientMetadata.RedirectUris)
            {
                if (uri.StartsWith("http://localhost"))
                {
                    redirectUri = uri;
                    break;
                }
            }
            
            // We need to actually open the browser with the authorization URL
            // Find the auth URL from client metadata and pass to openBrowser
            if (clientMetadata.ClientUri != null)
            {
                await openBrowser(clientMetadata.ClientUri);
            }
            
            // Wait for the authorization code
            var code = await authCodeTcs.Task;
            
            return (redirectUri, code);
        };
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