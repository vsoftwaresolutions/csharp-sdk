using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ModelContextProtocol.Utils;

namespace ModelContextProtocol.Protocol.Auth;

/// <summary>
/// Provides OAuth 2.0 authorization services for MCP clients.
/// </summary>
internal class AuthorizationService
{
    private static readonly HttpClient s_httpClient = new()
    {
        DefaultRequestHeaders =
        {
            Accept = { new MediaTypeWithQualityHeaderValue("application/json") }
        }
    };

    /// <summary>
    /// Gets resource metadata from a 401 Unauthorized response.
    /// </summary>
    /// <param name="response">The HTTP response that contains the WWW-Authenticate header.</param>
    /// <returns>A <see cref="Task"/> that represents the asynchronous operation. The task result contains the resource metadata if available.</returns>
    public static async Task<ResourceMetadata?> GetResourceMetadataFromResponseAsync(HttpResponseMessage response)
    {
        if (response.StatusCode != HttpStatusCode.Unauthorized)
        {
            return null;
        }

        // Get the WWW-Authenticate header
        if (!response.Headers.TryGetValues("WWW-Authenticate", out var authenticateValues))
        {
            return null;
        }

        // Parse the WWW-Authenticate header
        string? resourceMetadataUrl = null;
        foreach (var value in authenticateValues)
        {
            if (value.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            {
                var parameters = ParseAuthHeaderParameters(value["Bearer ".Length..].Trim());
                
                if (parameters.TryGetValue("resource_metadata", out var metadataUrl))
                {
                    resourceMetadataUrl = metadataUrl;
                    break;
                }
            }
        }

        if (string.IsNullOrEmpty(resourceMetadataUrl))
        {
            return null;
        }

        // Fetch the resource metadata document
        try
        {
            using var metadataResponse = await s_httpClient.GetAsync(resourceMetadataUrl);
            metadataResponse.EnsureSuccessStatusCode();
            
            return await JsonSerializer.DeserializeAsync<ResourceMetadata>(
                await metadataResponse.Content.ReadAsStreamAsync(),
                new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
        }
        catch (Exception)
        {
            // Failed to get resource metadata
            return null;
        }
    }

    /// <summary>
    /// Discovers authorization server metadata from a well-known endpoint.
    /// </summary>
    /// <param name="authorizationServerUrl">The base URL of the authorization server.</param>
    /// <returns>A <see cref="Task"/> that represents the asynchronous operation. The task result contains the authorization server metadata.</returns>
    /// <exception cref="InvalidOperationException">Thrown when both well-known endpoints return errors.</exception>
    public static async Task<AuthorizationServerMetadata> DiscoverAuthorizationServerMetadataAsync(string authorizationServerUrl)
    {
        Throw.IfNullOrWhiteSpace(authorizationServerUrl);

        // Remove trailing slash if present
        if (authorizationServerUrl.EndsWith("/"))
        {
            authorizationServerUrl = authorizationServerUrl[..^1];
        }

        // Try OpenID Connect discovery endpoint
        var openIdConfigUrl = $"{authorizationServerUrl}/.well-known/openid-configuration";
        try
        {
            using var openIdResponse = await s_httpClient.GetAsync(openIdConfigUrl);
            if (openIdResponse.IsSuccessStatusCode)
            {
                return await JsonSerializer.DeserializeAsync<AuthorizationServerMetadata>(
                    await openIdResponse.Content.ReadAsStreamAsync(),
                    new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    }) ?? throw new InvalidOperationException("Failed to parse authorization server metadata");
            }
        }
        catch (Exception ex) when (ex is not InvalidOperationException)
        {
            // Failed to get OpenID configuration, try OAuth endpoint
        }

        // Try OAuth 2.0 Authorization Server Metadata endpoint
        var oauthConfigUrl = $"{authorizationServerUrl}/.well-known/oauth-authorization-server";
        try
        {
            using var oauthResponse = await s_httpClient.GetAsync(oauthConfigUrl);
            if (oauthResponse.IsSuccessStatusCode)
            {
                return await JsonSerializer.DeserializeAsync<AuthorizationServerMetadata>(
                    await oauthResponse.Content.ReadAsStreamAsync(),
                    new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    }) ?? throw new InvalidOperationException("Failed to parse authorization server metadata");
            }
        }
        catch (Exception ex) when (ex is not InvalidOperationException)
        {
            // Failed to get OAuth configuration
        }

        throw new InvalidOperationException(
            "Failed to discover authorization server metadata. " +
            "Neither OpenID Connect nor OAuth 2.0 well-known endpoints are available.");
    }

    /// <summary>
    /// Registers a client with the authorization server.
    /// </summary>
    /// <param name="metadata">The authorization server metadata.</param>
    /// <param name="clientMetadata">The client metadata for registration.</param>
    /// <returns>A <see cref="Task"/> that represents the asynchronous operation. The task result contains the client registration response.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the authorization server does not support dynamic client registration.</exception>
    public static async Task<ClientRegistrationResponse> RegisterClientAsync(
        AuthorizationServerMetadata metadata,
        ClientMetadata clientMetadata)
    {
        Throw.IfNull(metadata);
        Throw.IfNull(clientMetadata);

        if (metadata.RegistrationEndpoint == null)
        {
            throw new InvalidOperationException("The authorization server does not support dynamic client registration.");
        }

        var content = new StringContent(
            JsonSerializer.Serialize(clientMetadata),
            Encoding.UTF8,
            "application/json");

        using var response = await s_httpClient.PostAsync(metadata.RegistrationEndpoint, content);
        response.EnsureSuccessStatusCode();

        return await JsonSerializer.DeserializeAsync<ClientRegistrationResponse>(
            await response.Content.ReadAsStreamAsync(),
            new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }) ?? throw new InvalidOperationException("Failed to parse client registration response");
    }

    /// <summary>
    /// Generates a code verifier and code challenge for PKCE.
    /// </summary>
    /// <returns>A tuple containing the code verifier and code challenge.</returns>
    public static (string CodeVerifier, string CodeChallenge) GeneratePkceValues()
    {
        // Generate a random code verifier
        var bytes = new byte[32];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(bytes);
        var codeVerifier = Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

        // Generate the code challenge (S256)
        using var sha256 = SHA256.Create();
        var challengeBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(codeVerifier));
        var codeChallenge = Convert.ToBase64String(challengeBytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

        return (codeVerifier, codeChallenge);
    }

    /// <summary>
    /// Creates an authorization URL for the OAuth authorization code flow with PKCE.
    /// </summary>
    /// <param name="metadata">The authorization server metadata.</param>
    /// <param name="clientId">The client identifier.</param>
    /// <param name="redirectUri">The redirect URI.</param>
    /// <param name="codeChallenge">The code challenge for PKCE.</param>
    /// <param name="scopes">The requested scopes.</param>
    /// <param name="state">A value used to maintain state between the request and callback.</param>
    /// <returns>The authorization URL.</returns>
    public static string CreateAuthorizationUrl(
        AuthorizationServerMetadata metadata,
        string clientId,
        string redirectUri,
        string codeChallenge,
        string[]? scopes = null,
        string? state = null)
    {
        Throw.IfNull(metadata);
        Throw.IfNullOrWhiteSpace(clientId);
        Throw.IfNullOrWhiteSpace(redirectUri);
        Throw.IfNullOrWhiteSpace(codeChallenge);

        var queryBuilder = new StringBuilder(metadata.AuthorizationEndpoint);
        queryBuilder.Append(metadata.AuthorizationEndpoint.Contains('?') ? '&' : '?');
        queryBuilder.Append("response_type=code");
        queryBuilder.Append($"&client_id={Uri.EscapeDataString(clientId)}");
        queryBuilder.Append($"&redirect_uri={Uri.EscapeDataString(redirectUri)}");
        queryBuilder.Append($"&code_challenge={Uri.EscapeDataString(codeChallenge)}");
        queryBuilder.Append("&code_challenge_method=S256");

        if (scopes != null && scopes.Length > 0)
        {
            queryBuilder.Append($"&scope={Uri.EscapeDataString(string.Join(" ", scopes))}");
        }

        if (!string.IsNullOrEmpty(state))
        {
            queryBuilder.Append($"&state={Uri.EscapeDataString(state)}");
        }

        return queryBuilder.ToString();
    }

    /// <summary>
    /// Exchanges an authorization code for tokens.
    /// </summary>
    /// <param name="metadata">The authorization server metadata.</param>
    /// <param name="clientId">The client identifier.</param>
    /// <param name="clientSecret">The client secret.</param>
    /// <param name="redirectUri">The redirect URI.</param>
    /// <param name="code">The authorization code received from the authorization server.</param>
    /// <param name="codeVerifier">The code verifier for PKCE.</param>
    /// <returns>A <see cref="Task"/> that represents the asynchronous operation. The task result contains the token response.</returns>
    public static async Task<TokenResponse> ExchangeCodeForTokensAsync(
        AuthorizationServerMetadata metadata,
        string clientId,
        string? clientSecret,
        string redirectUri,
        string code,
        string codeVerifier)
    {
        Throw.IfNull(metadata);
        Throw.IfNullOrWhiteSpace(clientId);
        Throw.IfNullOrWhiteSpace(redirectUri);
        Throw.IfNullOrWhiteSpace(code);
        Throw.IfNullOrWhiteSpace(codeVerifier);

        var tokenRequestContent = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["code"] = code,
            ["redirect_uri"] = redirectUri,
            ["client_id"] = clientId,
            ["code_verifier"] = codeVerifier
        });

        using var request = new HttpRequestMessage(HttpMethod.Post, metadata.TokenEndpoint)
        {
            Content = tokenRequestContent
        };

        // Add client authentication if client secret is provided
        if (!string.IsNullOrEmpty(clientSecret))
        {
            var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{clientId}:{clientSecret}"));
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);
        }

        using var response = await s_httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();

        return await JsonSerializer.DeserializeAsync<TokenResponse>(
            await response.Content.ReadAsStreamAsync(),
            new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }) ?? throw new InvalidOperationException("Failed to parse token response");
    }

    /// <summary>
    /// Refreshes an access token using a refresh token.
    /// </summary>
    /// <param name="metadata">The authorization server metadata.</param>
    /// <param name="clientId">The client identifier.</param>
    /// <param name="clientSecret">The client secret.</param>
    /// <param name="refreshToken">The refresh token.</param>
    /// <returns>A <see cref="Task"/> that represents the asynchronous operation. The task result contains the token response.</returns>
    public static async Task<TokenResponse> RefreshTokenAsync(
        AuthorizationServerMetadata metadata,
        string clientId,
        string? clientSecret,
        string refreshToken)
    {
        Throw.IfNull(metadata);
        Throw.IfNullOrWhiteSpace(clientId);
        Throw.IfNullOrWhiteSpace(refreshToken);

        var tokenRequestContent = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token",
            ["refresh_token"] = refreshToken,
            ["client_id"] = clientId
        });

        using var request = new HttpRequestMessage(HttpMethod.Post, metadata.TokenEndpoint)
        {
            Content = tokenRequestContent
        };

        // Add client authentication if client secret is provided
        if (!string.IsNullOrEmpty(clientSecret))
        {
            var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{clientId}:{clientSecret}"));
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);
        }

        using var response = await s_httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();

        return await JsonSerializer.DeserializeAsync<TokenResponse>(
            await response.Content.ReadAsStreamAsync(),
            new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }) ?? throw new InvalidOperationException("Failed to parse token response");
    }

    private static Dictionary<string, string> ParseAuthHeaderParameters(string parameters)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        
        var start = 0;
        while (start < parameters.Length)
        {
            // Find the next key=value pair
            var equalPos = parameters.IndexOf('=', start);
            if (equalPos == -1)
                break;

            var key = parameters[start..equalPos].Trim();
            start = equalPos + 1;

            // Check if the value is quoted
            if (start < parameters.Length && parameters[start] == '"')
            {
                start++; // Skip the opening quote
                
                // Find the closing quote
                var endQuote = start;
                while (endQuote < parameters.Length)
                {
                    endQuote = parameters.IndexOf('"', endQuote);
                    if (endQuote == -1)
                        break;

                    // Check if this is an escaped quote
                    if (endQuote > 0 && parameters[endQuote - 1] == '\\')
                    {
                        endQuote++; // Skip the escaped quote
                        continue;
                    }

                    break; // Found a non-escaped quote
                }

                if (endQuote == -1)
                    endQuote = parameters.Length; // No closing quote found, use the rest of the string

                var value = parameters[start..endQuote];
                result[key] = value.Replace("\\\"", "\""); // Unescape quotes

                // Move past the closing quote and any following comma
                start = endQuote + 1;
                var commaPos = parameters.IndexOf(',', start);
                if (commaPos != -1)
                    start = commaPos + 1;
                else
                    break;
            }
            else
            {
                // Unquoted value, ends at the next comma or end of string
                var commaPos = parameters.IndexOf(',', start);
                var value = commaPos != -1
                    ? parameters[start..commaPos].Trim()
                    : parameters[start..].Trim();

                result[key] = value;

                if (commaPos == -1)
                    break;

                start = commaPos + 1;
            }
        }

        return result;
    }
}