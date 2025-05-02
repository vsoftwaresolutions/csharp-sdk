using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using ModelContextProtocol.Utils;
using System.Net;
using System.Net.Http.Headers;

namespace ModelContextProtocol.Protocol.Auth;

/// <summary>
/// Provides authorization handling for MCP clients.
/// </summary>
internal class DefaultAuthorizationHandler : IAuthorizationHandler
{
    private readonly ILogger _logger;
    private readonly SynchronizedValue<AuthorizationContext> _authContext = new(new AuthorizationContext());
    private readonly Func<ClientMetadata, Task<(string RedirectUri, string Code)>>? _authorizeCallback;
    private readonly string? _clientId;
    private readonly string? _clientSecret;
    private readonly ICollection<string>? _redirectUris;
    private readonly ICollection<string>? _scopes;

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultAuthorizationHandler"/> class.
    /// </summary>
    /// <param name="loggerFactory">The logger factory.</param>
    /// <param name="options">The authorization options.</param>
    public DefaultAuthorizationHandler(ILoggerFactory? loggerFactory = null, AuthorizationOptions? options = null)
    {
        _logger = loggerFactory != null 
            ? loggerFactory.CreateLogger<DefaultAuthorizationHandler>() 
            : NullLogger<DefaultAuthorizationHandler>.Instance;
        
        if (options != null)
        {
            _authorizeCallback = options.AuthorizeCallback;
            _clientId = options.ClientId;
            _clientSecret = options.ClientSecret;
            _redirectUris = options.RedirectUris;
            _scopes = options.Scopes;
        }
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultAuthorizationHandler"/> class.
    /// </summary>
    /// <param name="loggerFactory">The logger factory.</param>
    /// <param name="authorizeCallback">A callback function that handles the authorization code flow.</param>
    public DefaultAuthorizationHandler(ILoggerFactory? loggerFactory = null, Func<ClientMetadata, Task<(string RedirectUri, string Code)>>? authorizeCallback = null)
        : this(loggerFactory, new AuthorizationOptions { AuthorizeCallback = authorizeCallback })
    {
    }

    /// <inheritdoc />
    public async Task AuthenticateRequestAsync(HttpRequestMessage request)
    {
        // Try to get a valid token, refreshing if necessary
        var token = await GetValidTokenAsync();

        if (!string.IsNullOrEmpty(token))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }
    }

    /// <inheritdoc />
    public async Task<bool> HandleUnauthorizedResponseAsync(HttpResponseMessage response, Uri serverUri)
    {
        if (response.StatusCode != HttpStatusCode.Unauthorized)
        {
            return false;
        }

        _logger.LogDebug("Received 401 Unauthorized response from {ServerUri}", serverUri);

        using var authContext = await _authContext.LockAsync();

        // If we already have a valid token, it might be that the token was just revoked
        // or has other issues - we need to clear our state and retry the authorization flow
        if (authContext.Value.HasValidToken)
        {
            _logger.LogWarning("Server rejected our authentication token. Clearing authentication state and reauthorizing.");
            authContext.Value = new AuthorizationContext();
        }

        // Try to get resource metadata from the response
        var resourceMetadata = await AuthorizationService.GetResourceMetadataFromResponseAsync(response);
        if (resourceMetadata == null)
        {
            _logger.LogWarning("Failed to extract resource metadata from 401 response");

            // Create a more specific exception
            var exception = new AuthorizationException("Authorization required but no resource metadata available")
            {
                ResourceUri = serverUri.ToString()
            };
            throw exception;
        }        // Store the resource metadata in the context before validating the resource URL
        authContext.Value.ResourceMetadata = resourceMetadata;

        // Validate that the resource matches the server FQDN
        if (!authContext.Value.ValidateResourceUrl(serverUri))
        {
            _logger.LogWarning("Resource URL mismatch: expected {Expected}, got {Actual}", 
                serverUri, resourceMetadata.Resource);
            
            var exception = new AuthorizationException($"Resource URL mismatch: expected {serverUri}, got {resourceMetadata.Resource}");
            exception.ResourceUri = resourceMetadata.Resource.ToString();
            throw exception;
        }

        // Get the first authorization server from the metadata
        if (resourceMetadata.AuthorizationServers == null || resourceMetadata.AuthorizationServers.Length == 0)
        {            _logger.LogWarning("No authorization servers found in resource metadata");
            
            var exception = new AuthorizationException("No authorization servers available");
            exception.ResourceUri = resourceMetadata.Resource.ToString();
            throw exception;
        }

        var authServerUrl = resourceMetadata.AuthorizationServers[0];
        _logger.LogDebug("Using authorization server: {AuthServerUrl}", authServerUrl);        try
        {
            // Discover authorization server metadata
            var authServerMetadata = await AuthorizationService.DiscoverAuthorizationServerMetadataAsync(authServerUrl);
            authContext.Value.AuthorizationServerMetadata = authServerMetadata;
            _logger.LogDebug("Successfully retrieved authorization server metadata");

            // Create client metadata
            string[] redirectUris = _redirectUris?.ToArray() ?? new[] { "http://localhost:8888/callback" };
            var clientMetadata = new ClientMetadata
            {
                RedirectUris = redirectUris,
                ClientName = "MCP C# SDK Client",
                Scope = string.Join(" ", _scopes ?? resourceMetadata.ScopesSupported ?? Array.Empty<string>())
            };
            
            // Register client if needed, or use pre-configured client ID
            if (!string.IsNullOrEmpty(_clientId))
            {                
                _logger.LogDebug("Using pre-configured client ID: {ClientId}", _clientId);
                
                // Create a client registration response to store in the context
                var clientRegistration = new ClientRegistrationResponse
                {
                    ClientId = _clientId!, // Using null-forgiving operator since we've already checked it's not null
                    ClientSecret = _clientSecret,
                };
                
                authContext.Value.ClientRegistration = clientRegistration;
            }
            else if (authServerMetadata.RegistrationEndpoint != null)
            {
                // Register client dynamically
                _logger.LogDebug("Registering client with authorization server");
                var clientRegistration = await AuthorizationService.RegisterClientAsync(authServerMetadata, clientMetadata);
                authContext.Value.ClientRegistration = clientRegistration;
                _logger.LogDebug("Client registered successfully with ID: {ClientId}", clientRegistration.ClientId);
            }
            else
            {
                _logger.LogWarning("Authorization server does not support dynamic client registration and no client ID was provided");
                  var exception = new AuthorizationException(
                    "Authorization server does not support dynamic client registration and no client ID was provided. " +
                    "Use McpAuthorizationOptions.ClientId to provide a pre-registered client ID.");
                exception.ResourceUri = resourceMetadata.Resource.ToString();
                exception.AuthorizationServerUri = authServerUrl.ToString();
                throw exception;
            }

            // If we have no way to handle user authorization, we can't proceed
            if (_authorizeCallback == null)
            {
                _logger.LogWarning("No authorization callback provided, can't proceed with OAuth flow");
                  var exception = new AuthorizationException(
                    "Authentication is required but no authorization callback was provided. " +
                    "Use McpAuthorizationOptions.AuthorizeCallback to provide a callback function.");
                exception.ResourceUri = resourceMetadata.Resource.ToString();
                exception.AuthorizationServerUri = authServerUrl.ToString();
                throw exception;
            }

            // Generate PKCE values
            var (codeVerifier, codeChallenge) = AuthorizationService.GeneratePkceValues();
            authContext.Value.CodeVerifier = codeVerifier;

            // Initiate authorization code flow
            _logger.LogDebug("Initiating authorization code flow");
            
            // Get the authorization URL that the user needs to visit
            var authUrl = AuthorizationService.CreateAuthorizationUrl(
                authServerMetadata,
                authContext.Value.ClientRegistration.ClientId,
                clientMetadata.RedirectUris[0],
                codeChallenge,
                _scopes?.ToArray() ?? resourceMetadata.ScopesSupported);

            _logger.LogDebug("Authorization URL: {AuthUrl}", authUrl);
            
            // Set the authorization URL in the client metadata
            clientMetadata.ClientUri = authUrl;

            // Let the callback handle the user authorization
            var (redirectUri, code) = await _authorizeCallback(clientMetadata);
            authContext.Value.RedirectUri = redirectUri;

            // Exchange the code for tokens
            _logger.LogDebug("Exchanging authorization code for tokens");
            var tokenResponse = await AuthorizationService.ExchangeCodeForTokensAsync(
                authServerMetadata,
                authContext.Value.ClientRegistration.ClientId,
                authContext.Value.ClientRegistration.ClientSecret,
                redirectUri,
                code,
                codeVerifier);

            authContext.Value.TokenResponse = tokenResponse;
            authContext.Value.TokenIssuedAt = DateTimeOffset.UtcNow;
            
            _logger.LogDebug("Successfully obtained access token");
            return true;
        }
        catch (Exception ex) when (ex is not AuthorizationException)
        {
            _logger.LogError(ex, "Failed to complete authorization flow");
              var authException = new AuthorizationException(
                $"Failed to complete authorization flow: {ex.Message}", ex, McpErrorCode.InvalidRequest);
            
            authException.ResourceUri = resourceMetadata.Resource.ToString();
            authException.AuthorizationServerUri = authServerUrl.ToString();
            
            throw authException;
        }
    }

    private async Task<string?> GetValidTokenAsync()
    {
        using var authContext = await _authContext.LockAsync();
        
        // If we have a valid token, use it
        if (authContext.Value.HasValidToken)
        {
            _logger.LogDebug("Using existing valid access token");
            return authContext.Value.GetAccessToken();
        }

        // If we can refresh the token, do so
        if (authContext.Value.CanRefreshToken)
        {
            try
            {
                _logger.LogDebug("Refreshing access token");
                
                // Null checks to ensure parameters are valid
                if (authContext.Value.AuthorizationServerMetadata == null)
                {
                    _logger.LogError("Cannot refresh token: AuthorizationServerMetadata is null");
                    return null;
                }
                
                if (authContext.Value.ClientRegistration == null)
                {
                    _logger.LogError("Cannot refresh token: ClientRegistration is null");
                    return null;
                }
                
                if (authContext.Value.TokenResponse?.RefreshToken == null)
                {
                    _logger.LogError("Cannot refresh token: RefreshToken is null");
                    return null;
                }

                var tokenResponse = await AuthorizationService.RefreshTokenAsync(
                    authContext.Value.AuthorizationServerMetadata,
                    authContext.Value.ClientRegistration.ClientId,
                    authContext.Value.ClientRegistration.ClientSecret,
                    authContext.Value.TokenResponse.RefreshToken);

                authContext.Value.TokenResponse = tokenResponse;
                authContext.Value.TokenIssuedAt = DateTimeOffset.UtcNow;
                
                _logger.LogDebug("Successfully refreshed access token");
                return tokenResponse.AccessToken;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to refresh access token");
                // Clear the token so we'll try to reauthenticate on the next request
                authContext.Value.TokenResponse = null;
                authContext.Value.TokenIssuedAt = null;
            }
        }

        return null;
    }
}