namespace ModelContextProtocol;

/// <summary>
/// Represents an exception that is thrown when an authorization or authentication error occurs in MCP.
/// </summary>
/// <remarks>
/// This exception is thrown when the client fails to authenticate with an MCP server that requires
/// authentication, such as when the OAuth authorization flow fails or when the server rejects the provided credentials.
/// </remarks>
public class McpAuthorizationException : McpException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="McpAuthorizationException"/> class.
    /// </summary>
    public McpAuthorizationException() 
        : base("Authorization failed", McpErrorCode.Unauthorized)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="McpAuthorizationException"/> class with a specified error message.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    public McpAuthorizationException(string message) 
        : base(message, McpErrorCode.Unauthorized)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="McpAuthorizationException"/> class with a specified error message and a reference to the inner exception that is the cause of this exception.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    /// <param name="innerException">The exception that is the cause of the current exception, or a null reference if no inner exception is specified.</param>
    public McpAuthorizationException(string message, Exception? innerException) 
        : base(message, innerException, McpErrorCode.Unauthorized)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="McpAuthorizationException"/> class with a specified error message and error code.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    /// <param name="errorCode">The MCP error code. Should be either <see cref="McpErrorCode.Unauthorized"/> or <see cref="McpErrorCode.AuthenticationFailed"/>.</param>
    public McpAuthorizationException(string message, McpErrorCode errorCode) 
        : base(message, errorCode)
    {
        if (errorCode != McpErrorCode.Unauthorized && errorCode != McpErrorCode.AuthenticationFailed)
        {
            throw new ArgumentException($"Error code must be either {nameof(McpErrorCode.Unauthorized)} or {nameof(McpErrorCode.AuthenticationFailed)}", nameof(errorCode));
        }
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="McpAuthorizationException"/> class with a specified error message, inner exception, and error code.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    /// <param name="innerException">The exception that is the cause of the current exception, or a null reference if no inner exception is specified.</param>
    /// <param name="errorCode">The MCP error code. Should be either <see cref="McpErrorCode.Unauthorized"/> or <see cref="McpErrorCode.AuthenticationFailed"/>.</param>
    public McpAuthorizationException(string message, Exception? innerException, McpErrorCode errorCode) 
        : base(message, innerException, errorCode)
    {
        if (errorCode != McpErrorCode.Unauthorized && errorCode != McpErrorCode.AuthenticationFailed)
        {
            throw new ArgumentException($"Error code must be either {nameof(McpErrorCode.Unauthorized)} or {nameof(McpErrorCode.AuthenticationFailed)}", nameof(errorCode));
        }
    }

    /// <summary>
    /// Gets or sets the resource that requires authorization.
    /// </summary>
    public string? ResourceUri { get; set; }

    /// <summary>
    /// Gets or sets the authorization server URI that should be used for authentication.
    /// </summary>
    public string? AuthorizationServerUri { get; set; }
}