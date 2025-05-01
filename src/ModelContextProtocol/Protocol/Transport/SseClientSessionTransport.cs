using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using ModelContextProtocol.Protocol.Auth;
using ModelContextProtocol.Protocol.Messages;
using ModelContextProtocol.Utils;
using ModelContextProtocol.Utils.Json;
using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.ServerSentEvents;
using System.Text;
using System.Text.Json;

namespace ModelContextProtocol.Protocol.Transport;

/// <summary>
/// The ServerSideEvents client transport implementation
/// </summary>
internal sealed partial class SseClientSessionTransport : TransportBase
{
    private readonly HttpClient _httpClient;
    private readonly SseClientTransportOptions _options;
    private readonly Uri _sseEndpoint;
    private Uri? _messageEndpoint;
    private readonly CancellationTokenSource _connectionCts;
    private Task? _receiveTask;
    private readonly ILogger _logger;
    private readonly TaskCompletionSource<bool> _connectionEstablished;
    private readonly IAuthorizationHandler _authorizationHandler;

    /// <summary>
    /// SSE transport for client endpoints. Unlike stdio it does not launch a process, but connects to an existing server.
    /// The HTTP server can be local or remote, and must support the SSE protocol.
    /// </summary>
    /// <param name="transportOptions">Configuration options for the transport.</param>
    /// <param name="httpClient">The HTTP client instance used for requests.</param>
    /// <param name="loggerFactory">Logger factory for creating loggers.</param>
    /// <param name="endpointName">The endpoint name used for logging purposes.</param>
    public SseClientSessionTransport(SseClientTransportOptions transportOptions, HttpClient httpClient, ILoggerFactory? loggerFactory, string endpointName)
        : base(endpointName, loggerFactory)
    {
        Throw.IfNull(transportOptions);
        Throw.IfNull(httpClient);

        _options = transportOptions;
        _sseEndpoint = transportOptions.Endpoint;
        _httpClient = httpClient;
        _connectionCts = new CancellationTokenSource();
        _logger = (ILogger?)loggerFactory?.CreateLogger<SseClientTransport>() ?? NullLogger.Instance;
        _connectionEstablished = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        
        // Initialize the authorization handler
        if (transportOptions.AuthorizationOptions?.AuthorizationHandler != null)
        {
            // Use explicitly provided handler
            _authorizationHandler = transportOptions.AuthorizationOptions.AuthorizationHandler;
        }
        else
        {
            // Create default handler with auth options
            _authorizationHandler = new DefaultAuthorizationHandler(loggerFactory, transportOptions.AuthorizationOptions);
        }
    }

    /// <inheritdoc/>
    public async Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        Debug.Assert(!IsConnected);
        try
        {
            // Start message receiving loop
            _receiveTask = ReceiveMessagesAsync(_connectionCts.Token);

            await _connectionEstablished.Task.WaitAsync(_options.ConnectionTimeout, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            LogTransportConnectFailed(Name, ex);
            await CloseAsync().ConfigureAwait(false);
            throw new InvalidOperationException("Failed to connect transport", ex);
        }
    }

    /// <inheritdoc/>
    public override async Task SendMessageAsync(
        JsonRpcMessage message,
        CancellationToken cancellationToken = default)
    {
        if (_messageEndpoint == null)
            throw new InvalidOperationException("Transport not connected");

        string messageId = "(no id)";

        if (message is JsonRpcMessageWithId messageWithId)
        {
            messageId = messageWithId.Id.ToString();
        }
        
        // Send the request, handling potential auth challenges
        HttpResponseMessage? response = null;
        bool authRetry = false;
        
        do
        {
            authRetry = false;
            
            // Create a new request for each attempt
            using var currentRequest = new HttpRequestMessage(HttpMethod.Post, _messageEndpoint);
            currentRequest.Content = new StringContent(
                JsonSerializer.Serialize(message, McpJsonUtilities.JsonContext.Default.JsonRpcMessage),
                Encoding.UTF8,
                "application/json"
            );
            
            // Add authorization headers if needed - the handler will only add headers if auth is required
            await _authorizationHandler.AuthenticateRequestAsync(currentRequest).ConfigureAwait(false);
            
            // Copy additional headers
            CopyAdditionalHeaders(currentRequest.Headers);
            
            // Dispose previous response before making a new request
            response?.Dispose();
            
            response = await _httpClient.SendAsync(currentRequest, cancellationToken).ConfigureAwait(false);
            
            // Handle 401 Unauthorized response - this will only execute if the server requires auth
            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                // Try to handle the unauthorized response
                authRetry = await _authorizationHandler.HandleUnauthorizedResponseAsync(
                    response, _messageEndpoint).ConfigureAwait(false);
            }
        } while (authRetry);

        using var httpRequestMessage = new HttpRequestMessage(HttpMethod.Post, _messageEndpoint)
        {
            Content = content,
        };
        StreamableHttpClientSessionTransport.CopyAdditionalHeaders(httpRequestMessage.Headers, _options.AdditionalHeaders);
        var response = await _httpClient.SendAsync(httpRequestMessage, cancellationToken).ConfigureAwait(false);

            var responseContent = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

            // Check if the message was an initialize request
            if (message is JsonRpcRequest request && request.Method == RequestMethods.Initialize)
            {
                // If the response is not a JSON-RPC response, it is an SSE message
                if (string.IsNullOrEmpty(responseContent) || responseContent.Equals("accepted", StringComparison.OrdinalIgnoreCase))
                {
                    LogAcceptedPost(Name, messageId);
                    // The response will arrive as an SSE message
                }
                else
                {
                    JsonRpcResponse initializeResponse = JsonSerializer.Deserialize(responseContent, McpJsonUtilities.JsonContext.Default.JsonRpcResponse) ??
                        throw new InvalidOperationException("Failed to initialize client");

        if (string.IsNullOrEmpty(responseContent) || responseContent.Equals("accepted", StringComparison.OrdinalIgnoreCase))
        {
            response.Dispose();
        }
    }

    private async Task CloseAsync()
    {
        try
        {
            await _connectionCts.CancelAsync().ConfigureAwait(false);

            try
            {
                if (_receiveTask != null)
                {
                    await _receiveTask.ConfigureAwait(false);
                }
            }
            finally
            {
                _connectionCts.Dispose();
            }
        }
        finally
        {
            SetConnected(false);
        }
    }

    /// <inheritdoc/>
    public override async ValueTask DisposeAsync()
    {
        try
        {
            await CloseAsync().ConfigureAwait(false);
        }
        catch (Exception)
        {
            // Ignore exceptions on close
        }
    }

    private async Task ReceiveMessagesAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, _sseEndpoint);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));
            StreamableHttpClientSessionTransport.CopyAdditionalHeaders(request.Headers, _options.AdditionalHeaders);

            using var response = await _httpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken
            ).ConfigureAwait(false);

            response.EnsureSuccessStatusCode();

            using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);

            await foreach (SseItem<string> sseEvent in SseParser.Create(stream).EnumerateAsync(cancellationToken).ConfigureAwait(false))
            {
                switch (sseEvent.EventType)
                {
                    case "endpoint":
                        HandleEndpointEvent(sseEvent.Data);
                        break;

                    case "message":
                        await ProcessSseMessage(sseEvent.Data, cancellationToken).ConfigureAwait(false);
                        break;
                }
            }
        }
        catch (Exception ex)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                // Normal shutdown
                LogTransportReadMessagesCancelled(Name);
                _connectionEstablished.TrySetCanceled(cancellationToken);
            }
            else
            {
                LogTransportReadMessagesFailed(Name, ex);
                _connectionEstablished.TrySetException(ex);
                throw;
            }
        }
        finally
        {
            SetConnected(false);
        }
    }

    private async Task ProcessSseMessage(string data, CancellationToken cancellationToken)
    {
        if (!IsConnected)
        {
            LogTransportMessageReceivedBeforeConnected(Name);
            return;
        }

        try
        {
            var message = JsonSerializer.Deserialize(data, McpJsonUtilities.JsonContext.Default.JsonRpcMessage);
            if (message == null)
            {
                LogTransportMessageParseUnexpectedTypeSensitive(Name, data);
                return;
            }

            await WriteMessageAsync(message, cancellationToken).ConfigureAwait(false);
        }
        catch (JsonException ex)
        {
            if (_logger.IsEnabled(LogLevel.Trace))
            {
                LogTransportMessageParseFailedSensitive(Name, data, ex);
            }
            else
            {
                LogTransportMessageParseFailed(Name, ex);
            }
        }
    }

    private void HandleEndpointEvent(string data)
    {
        if (string.IsNullOrEmpty(data))
        {
            LogTransportEndpointEventInvalid(Name);
            return;
        }

        // If data is an absolute URL, the Uri will be constructed entirely from it and not the _sseEndpoint.
        _messageEndpoint = new Uri(_sseEndpoint, data);

        // Set connected state
        SetConnected(true);
        _connectionEstablished.TrySetResult(true);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "{EndpointName} accepted SSE transport POST for message ID '{MessageId}'.")]
    private partial void LogAcceptedPost(string endpointName, string messageId);

    [LoggerMessage(Level = LogLevel.Information, Message = "{EndpointName} rejected SSE transport POST for message ID '{MessageId}'.")]
    private partial void LogRejectedPost(string endpointName, string messageId);

    [LoggerMessage(Level = LogLevel.Trace, Message = "{EndpointName} rejected SSE transport POST for message ID '{MessageId}'. Server response: '{responseContent}'.")]
    private partial void LogRejectedPostSensitive(string endpointName, string messageId, string responseContent);
}