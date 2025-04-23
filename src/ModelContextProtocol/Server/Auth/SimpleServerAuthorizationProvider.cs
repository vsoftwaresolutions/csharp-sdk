using ModelContextProtocol.Protocol.Auth;
using System.Security.Claims;

namespace ModelContextProtocol.Server.Auth;

/// <summary>
/// A simple implementation of <see cref="IMcpServerAuthorizationProvider"/> that validates bearer tokens.
/// </summary>
/// <remarks>
/// This implementation is intended as a starting point for server developers. In production environments,
/// it should be extended or replaced with a more robust implementation that integrates with your 
/// authentication system (e.g., OAuth 2.0 server, identity provider, etc.)
/// </remarks>
public class SimpleServerAuthorizationProvider : IMcpServerAuthorizationProvider
{
    private readonly ProtectedResourceMetadata _resourceMetadata;
    private readonly Func<string, Task<bool>> _tokenValidator;

    /// <summary>
    /// Initializes a new instance of the <see cref="SimpleServerAuthorizationProvider"/> class
    /// with the specified resource metadata and token validator.
    /// </summary>
    /// <param name="resourceMetadata">The protected resource metadata.</param>
    /// <param name="tokenValidator">A function that validates access tokens. If not provided, a function that always returns true will be used.</param>
    public SimpleServerAuthorizationProvider(
        ProtectedResourceMetadata resourceMetadata,
        Func<string, Task<bool>>? tokenValidator = null)
    {
        _resourceMetadata = resourceMetadata ?? throw new ArgumentNullException(nameof(resourceMetadata));
        _tokenValidator = tokenValidator ?? (_ => Task.FromResult(true));
    }

    /// <inheritdoc />
    public ProtectedResourceMetadata GetProtectedResourceMetadata() => _resourceMetadata;

    /// <inheritdoc />
    public async Task<bool> ValidateTokenAsync(string authorizationHeader)
    {
        // Extract the token from the Authorization header
        if (string.IsNullOrEmpty(authorizationHeader) || !authorizationHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var token = authorizationHeader["Bearer ".Length..].Trim();
        if (string.IsNullOrEmpty(token))
        {
            return false;
        }

        // Validate the token
        return await _tokenValidator(token);
    }
}