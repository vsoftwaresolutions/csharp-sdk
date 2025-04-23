using System.Net;

namespace ModelContextProtocol.Protocol.Auth;

/// <summary>
/// Defines methods for handling authorization in an MCP client.
/// </summary>
public interface IAuthorizationHandler
{
    /// <summary>
    /// Handles authentication for HTTP requests.
    /// </summary>
    /// <param name="request">The HTTP request to authenticate.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    Task AuthenticateRequestAsync(HttpRequestMessage request);

    /// <summary>
    /// Handles a 401 Unauthorized response.
    /// </summary>
    /// <param name="response">The HTTP response that contains the 401 status code.</param>
    /// <param name="serverUri">The URI of the server that returned the 401 status code.</param>
    /// <returns>A <see cref="Task"/> that represents the asynchronous operation. The task result contains true if the authentication was successful and the request should be retried, otherwise false.</returns>
    Task<bool> HandleUnauthorizedResponseAsync(HttpResponseMessage response, Uri serverUri);
}