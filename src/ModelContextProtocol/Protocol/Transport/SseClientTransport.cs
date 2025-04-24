using Microsoft.Extensions.Logging;
using ModelContextProtocol.Protocol.Auth;
using ModelContextProtocol.Utils;
using System.Net;
using System.Text;

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
    /// Creates a callback function for handling OAuth 2.0 authorization flows using an HTTP listener.
    /// </summary>
    /// <param name="openBrowser">A function to open the browser to the authorization URL.</param>
    /// <param name="hostname">The hostname for the HTTP listener. Defaults to "localhost".</param>
    /// <param name="listenPort">The port for the HTTP listener. Defaults to 8888.</param>
    /// <param name="redirectPath">The redirect path for the HTTP listener. Defaults to "/callback".</param>
    /// <returns>
    /// A function that takes <see cref="ClientMetadata"/> and returns a task that resolves to a tuple containing
    /// the redirect URI and the authorization code.
    /// </returns>
    public static Func<ClientMetadata, Task<(string RedirectUri, string Code)>> CreateHttpListenerAuthorizeCallback(
        Func<string, Task> openBrowser,
        string hostname = "localhost",
        int listenPort = 8888,
        string redirectPath = "/callback")
    {
        return async (ClientMetadata clientMetadata) =>
        {
            string redirectUri = $"http://{hostname}:{listenPort}{redirectPath}";

            foreach (var uri in clientMetadata.RedirectUris)
            {
                if (uri.StartsWith($"http://{hostname}", StringComparison.OrdinalIgnoreCase) &&
                    Uri.TryCreate(uri, UriKind.Absolute, out var parsedUri))
                {
                    redirectUri = uri;
                    listenPort = parsedUri.IsDefaultPort ? 80 : parsedUri.Port;
                    redirectPath = parsedUri.AbsolutePath;
                    break;
                }
            }

            var authCodeTcs = new TaskCompletionSource<string>();
            // Ensure the path has a trailing slash for the HttpListener prefix
            string listenerPrefix = $"http://{hostname}:{listenPort}{redirectPath}";
            if (!listenerPrefix.EndsWith("/"))
            {
                listenerPrefix += "/";
            }

            using var listener = new HttpListener();
            listener.Prefixes.Add(listenerPrefix);
            
            // Start the listener BEFORE opening the browser
            try
            {
                listener.Start();
            }
            catch (HttpListenerException ex)
            {
                throw new McpException($"Failed to start HTTP listener on {listenerPrefix}: {ex.Message}", McpErrorCode.InvalidRequest);
            }

            // Create a cancellation token source with a timeout
            using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(5));
            
            _ = Task.Run(async () =>
            {
                try
                {
                    // GetContextAsync doesn't accept a cancellation token, so we need to handle cancellation manually
                    var contextTask = listener.GetContextAsync();
                    var completedTask = await Task.WhenAny(contextTask, Task.Delay(Timeout.Infinite, cts.Token));
                    
                    if (completedTask == contextTask)
                    {
                        var context = await contextTask;
                        var request = context.Request;
                        var response = context.Response;

                        string? code = request.QueryString["code"];
                        string? error = request.QueryString["error"];
                        string html;
                        string? resultCode = null;

                        if (!string.IsNullOrEmpty(error))
                        {
                            html = $"<html><body><h1>Authorization Failed</h1><p>Error: {WebUtility.HtmlEncode(error)}</p></body></html>";
                        }
                        else if (string.IsNullOrEmpty(code))
                        {
                            html = "<html><body><h1>Authorization Failed</h1><p>No authorization code received.</p></body></html>";
                        }
                        else
                        {
                            html = "<html><body><h1>Authorization Successful</h1><p>You may now close this window.</p></body></html>";
                            resultCode = code;
                        }

                        try
                        {
                            // Send response to browser
                            byte[] buffer = Encoding.UTF8.GetBytes(html);
                            response.ContentType = "text/html";
                            response.ContentLength64 = buffer.Length;
                            response.OutputStream.Write(buffer, 0, buffer.Length);
                            
                            // IMPORTANT: Explicitly close the response to ensure it's fully sent
                            response.Close();
                            
                            // Now that we've finished processing the browser response,
                            // we can safely signal completion or failure with the auth code
                            if (resultCode != null)
                            {
                                authCodeTcs.TrySetResult(resultCode);
                            }
                            else if (!string.IsNullOrEmpty(error))
                            {
                                authCodeTcs.TrySetException(new McpException($"Authorization failed: {error}", McpErrorCode.InvalidRequest));
                            }
                            else
                            {
                                authCodeTcs.TrySetException(new McpException("No authorization code received", McpErrorCode.InvalidRequest));
                            }
                        }
                        catch (Exception ex)
                        {
                            authCodeTcs.TrySetException(new McpException($"Error processing browser response: {ex.Message}", McpErrorCode.InvalidRequest));
                        }
                    }
                }
                catch (Exception ex)
                {
                    authCodeTcs.TrySetException(ex);
                }
            });

            // Now open the browser AFTER the listener is started
            if (!string.IsNullOrEmpty(clientMetadata.ClientUri))
            {
                await openBrowser(clientMetadata.ClientUri!);
            }
            else
            {
                // Stop the listener before throwing
                listener.Stop();
                throw new McpException("Client URI is missing in metadata.", McpErrorCode.InvalidRequest);
            }

            try
            {
                // Use a timeout to avoid hanging indefinitely
                string authCode = await authCodeTcs.Task.WaitAsync(cts.Token);
                return (redirectUri, authCode);
            }
            catch (OperationCanceledException)
            {
                throw new McpException("Authorization timed out after 5 minutes.", McpErrorCode.InvalidRequest);
            }
            finally
            {
                // Ensure the listener is stopped when we're done
                listener.Stop();
            }
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