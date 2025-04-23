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
            AuthorizeCallback = SseClientTransport.CreateLocalServerAuthorizeCallback(
                 openBrowser: async (url) =>
                 {
                     // Open the URL in the user's default browser
                     Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
                 }
             )
        };
        
        try
        {
            // Create the client with authorization-enabled transport
            var transport = new SseClientTransport(transportOptions);
            var client = await McpClientFactory.CreateAsync(transport);

            // Print the list of tools available from the server.
            foreach (var tool in await client.ListToolsAsync())
            {
                Console.WriteLine($"{tool.Name} ({tool.Description})");
            }

            // Execute a tool (this would normally be driven by LLM tool invocations).
            var result = await client.CallToolAsync(
                "echo",
                new Dictionary<string, object?>() { ["message"] = "Hello MCP!" },
                cancellationToken: CancellationToken.None);

            // echo always returns one and only one text content object
            Console.WriteLine(result.Content.First(c => c.Type == "text").Text);
        }
}