using System.Diagnostics;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol.Auth;
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
        var serverEndpoint = new Uri("http://localhost:7071/sse");

        // Configuration values for OAuth redirect
        string hostname = "localhost";
        int port = 8888;
        string callbackPath = "/oauth/callback";

        // Set up the SSE transport with authorization support
        var transportOptions = new SseClientTransportOptions
        {
            Endpoint = serverEndpoint,
            AuthorizationOptions = new AuthorizationOptions
            {
                // Pre-registered client credentials (if applicable)
                ClientId = "04f79824-ab56-4511-a7cb-d7deaea92dc0",

                // Setting some pre-defined scopes the client requests.
                Scopes = ["User.Read"],

                // Specify the exact same redirect URIs that are registered with the OAuth server
                RedirectUris = new[] 
                { 
                    $"http://{hostname}:{port}{callbackPath}" 
                },

                // Configure the authorize callback with the same hostname, port, and path
                AuthorizeCallback = SseClientTransport.CreateHttpListenerAuthorizeCallback(
                    openBrowser: async (url) =>
                    {
                        Console.WriteLine($"Opening browser to authorize at: {url}");
                        Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
                    },
                    hostname: hostname,
                    listenPort: port,
                    redirectPath: callbackPath,
                    successHtml: "<html><body><h1>Authorization Successful</h1><p>You have successfully authorized the application. You can close this window and return to the app.</p><script>window.close();</script></body></html>"
                )
            }
        };
        
        Console.WriteLine("Connecting to MCP server...");
        
        try
        {
            // Create the client with authorization-enabled transport
            var transport = new SseClientTransport(transportOptions);
            var client = await McpClientFactory.CreateAsync(transport);

            Console.WriteLine("Successfully connected and authorized!");
            
            // Print the list of tools available from the server.
            Console.WriteLine("\nAvailable tools:");
            foreach (var tool in await client.ListToolsAsync())
            {
                Console.WriteLine($"  - {tool.Name}: {tool.Description}");
            }

            // Execute a tool (this would normally be driven by LLM tool invocations).
            Console.WriteLine("\nCalling 'echo' tool...");
            var result = await client.CallToolAsync(
                "echo",
                new Dictionary<string, object?>() { ["message"] = "Hello MCP!" },
                cancellationToken: CancellationToken.None);

            // echo always returns one and only one text content object
            Console.WriteLine($"Tool response: {result.Content.First(c => c.Type == "text").Text}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
            if (ex.InnerException != null)
            {
                Console.WriteLine($"Inner Error: {ex.InnerException.Message}");
            }
        }
    }
}