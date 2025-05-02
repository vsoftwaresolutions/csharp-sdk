using Microsoft.AspNetCore.Builder;

namespace ModelContextProtocol.AspNetCore;

/// <summary>
/// Extension methods for using MCP authorization in ASP.NET Core applications.
/// </summary>
public static class AuthorizationExtensions
{
    /// <summary>
    /// Adds MCP authorization middleware to the specified <see cref="IApplicationBuilder"/>, which enables
    /// OAuth 2.0 authorization for MCP servers.
    /// 
    /// Note: This method is called automatically when using <c>WithAuthorization()</c>, so you typically
    /// don't need to call it directly. It's available for advanced scenarios where more control is needed.
    /// </summary>
    /// <param name="builder">The <see cref="IApplicationBuilder"/> to add the middleware to.</param>
    /// <returns>A reference to this instance after the operation has completed.</returns>
    public static IApplicationBuilder UseMcpAuthorization(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<AuthorizationMiddleware>();
    }
}