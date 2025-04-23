using ModelContextProtocol;
using ModelContextProtocol.AspNetCore;
using ModelContextProtocol.Protocol.Auth;
using ModelContextProtocol.Protocol.Types;
using ModelContextProtocol.Server.Auth;

namespace AuthorizationServerExample;

/// <summary>
/// Example demonstrating how to implement authorization in an MCP server.
/// </summary>
public class Program
{
    public static async Task Main(string[] args)
    {
        Console.WriteLine("=== MCP Server with Authorization Support ===");
        Console.WriteLine("This example demonstrates how to implement OAuth authorization in an MCP server.");
        Console.WriteLine();

        var builder = WebApplication.CreateBuilder(args);
        
        // 1. Define the Protected Resource Metadata for the server
        // This is the information that will be provided to clients when they need to authenticate
        var prm = new ProtectedResourceMetadata
        {
            Resource = "http://localhost:7071", // Changed from HTTPS to HTTP for local development
            AuthorizationServers = ["https://auth.example.com"], // Auth servers that can issue tokens for this resource
            BearerMethodsSupported = ["header"], // We support the Authorization header
            ScopesSupported = ["mcp.tools", "mcp.prompts", "mcp.resources"], // Scopes supported by this resource
            ResourceDocumentation = "https://example.com/docs/mcp-server-auth" // Optional documentation URL
        };

        // 2. Define a token validator function
        // This function receives the token from the Authorization header and should validate it
        // In a real application, this would verify the token with your identity provider
        async Task<bool> ValidateToken(string token)
        {
            // For demo purposes, we'll accept any token that starts with "valid_"
            // In production, you would validate the token with your identity provider
            var isValid = token.StartsWith("valid_", StringComparison.OrdinalIgnoreCase);
            Console.WriteLine($"Token validation result: {(isValid ? "Valid" : "Invalid")}");
            return isValid;
        }

        // 3. Create an authorization provider with the PRM and token validator
        var authProvider = new SimpleServerAuthorizationProvider(prm, ValidateToken);

        // 4. Configure the MCP server with authorization
        builder.Services.AddMcpServer(options =>
            {
                options.ServerInstructions = "This is an MCP server with OAuth authorization enabled.";
                
                // Configure regular server capabilities like tools, prompts, resources
                options.Capabilities = new()
                {
                    Tools = new()
                    {
                        // Simple Echo tool
                        
                        CallToolHandler = (request, cancellationToken) =>
                        {
                            if (request.Params?.Name == "echo")
                            {
                                if (request.Params.Arguments?.TryGetValue("message", out var message) is not true)
                                {
                                    throw new McpException("Missing required argument 'message'");
                                }

                                return new ValueTask<CallToolResponse>(new CallToolResponse()
                                {
                                    Content = [new Content() { Text = $"Echo: {message}", Type = "text" }]
                                });
                            }
                            
                            // Protected tool that requires authorization
                            if (request.Params?.Name == "protected-data")
                            {
                                // This tool will only be accessible to authenticated clients
                                return new ValueTask<CallToolResponse>(new CallToolResponse()
                                {
                                    Content = [new Content() { Text = "This is protected data that only authorized clients can access" }]
                                });
                            }

                            throw new McpException($"Unknown tool: '{request.Params?.Name}'");
                        },
                        
                        ListToolsHandler = async (_, _) => new()
                        {
                            Tools = 
                            [
                                new() 
                                { 
                                    Name = "echo", 
                                    Description = "Echoes back the message you send" 
                                },
                                new()
                                {
                                    Name = "protected-data",
                                    Description = "Returns protected data that requires authorization"
                                }
                            ]
                        }
                    }
                };
            })
            .WithAuthorization(authProvider)  // Enable authorization with our provider
            .WithHttpTransport();             // Configure HTTP transport

        var app = builder.Build();
        
        // 5. Enable authorization middleware (this must be before MapMcp)
        // This middleware does several things:
        // - Serves the PRM document at /.well-known/oauth-protected-resource
        // - Checks Authorization header on requests
        // - Returns 401 + WWW-Authenticate when authorization is missing or invalid
        app.UseMcpAuthorization();
        
        // 6. Map MCP endpoints
        app.MapMcp();
        
        // Configure the server URL
        app.Urls.Add("http://localhost:7071");
        
        Console.WriteLine("Starting MCP server with authorization at http://localhost:7071");
        Console.WriteLine("PRM Document URL: http://localhost:7071/.well-known/oauth-protected-resource");
        
        Console.WriteLine();
        Console.WriteLine("To test the server:");
        Console.WriteLine("1. Use an MCP client that supports authorization");
        Console.WriteLine("2. When prompted for authorization, enter 'valid_token' to gain access");
        Console.WriteLine("3. Any other token value will be rejected with a 401 Unauthorized");
        Console.WriteLine();
        Console.WriteLine("Press Ctrl+C to stop the server");
        
        await app.RunAsync();
    }
}