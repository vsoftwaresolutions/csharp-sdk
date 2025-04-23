using System.Diagnostics;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol.Transport;

namespace AuthorizationExample;

/// <summary>
/// Example demonstrating how to use the MCP C# SDK with OAuth authorization.
/// </summary>
public class Program
{
    public static async Task Main(string[] args)
    {
        // Define the MCP server endpoint that requires OAuth authentication
        var serverEndpoint = new Uri("https://example.com/mcp");

        // Set up the SSE transport with authorization support
        var transportOptions = new SseClientTransportOptions
        {
            Endpoint = serverEndpoint,
            
            // Provide a callback to handle the authorization flow
            AuthorizeCallback = async (clientMetadata) =>
            {
                Console.WriteLine("Authentication required. Opening browser for authorization...");
                
                // In a real app, you'd likely have a local HTTP server to receive the callback
                // This is just a simplified example
                Console.WriteLine("Once you've authorized in the browser, enter the code and redirect URI:");
                Console.Write("Code: ");
                var code = Console.ReadLine() ?? "";
                Console.Write("Redirect URI: ");
                var redirectUri = Console.ReadLine() ?? "http://localhost:8888/callback";
                
                return (redirectUri, code);
            }
            
            // Alternatively, use the built-in local server handler:
            // AuthorizeCallback = SseClientTransport.CreateLocalServerAuthorizeCallback(
            //     openBrowser: async (url) => 
            //     {
            //         // Open the URL in the user's default browser
            //         Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            //     }
            // )
        };
        
        try
        {
            // Create the client with authorization-enabled transport
            var transport = new SseClientTransport(transportOptions);
            var client = await McpClient.CreateAsync(transport);

            // Use the MCP client normally - authorization is handled automatically
            // If the server returns a 401 Unauthorized response, the authorization flow will be triggered
            var result = await client.PingAsync();
            Console.WriteLine($"Server ping successful: {result.ServerInfo.Name} {result.ServerInfo.Version}");
            
            // Example tool call
            var weatherPrompt = "What's the weather like today?";
            var weatherResult = await client.CompletionCompleteAsync(
                new CompletionCompleteRequestBuilder(weatherPrompt).Build());
                
            Console.WriteLine($"Response: {weatherResult.Content.Text}");
        }
        catch (McpAuthorizationException authEx)
        {
            Console.WriteLine($"Authorization error: {authEx.Message}");
            Console.WriteLine($"Resource: {authEx.ResourceUri}");
            Console.WriteLine($"Auth server: {authEx.AuthorizationServerUri}");
        }
        catch (McpException mcpEx)
        {
            Console.WriteLine($"MCP error: {mcpEx.Message} (Error code: {mcpEx.ErrorCode})");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Unexpected error: {ex.Message}");
        }
    }
}