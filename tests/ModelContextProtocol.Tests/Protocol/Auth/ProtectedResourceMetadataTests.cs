using ModelContextProtocol.Protocol.Auth;
using System.Text.Json;

namespace ModelContextProtocol.Tests.Protocol.Auth;

public class ProtectedResourceMetadataTests
{
    [Fact]
    public void ProtectedResourceMetadata_JsonSerialization_Works()
    {
        // Arrange
        var metadata = new ProtectedResourceMetadata
        {
            Resource = new Uri("http://localhost:7071"),
            AuthorizationServers = [new Uri("https://login.microsoftonline.com/tenant/v2.0")],
            BearerMethodsSupported = ["header"],
            ScopesSupported = ["mcp.tools", "mcp.prompts"],
            ResourceDocumentation = new Uri("https://example.com/docs")
        };

        // Act
        var json = JsonSerializer.Serialize(metadata);
        var deserialized = JsonSerializer.Deserialize<ProtectedResourceMetadata>(json);

        // Assert
        Assert.NotNull(deserialized);
        Assert.Equal(metadata.Resource, deserialized.Resource);
        Assert.Equal(metadata.AuthorizationServers[0], deserialized.AuthorizationServers[0]);
        Assert.Equal("header", deserialized.BearerMethodsSupported![0]);
        Assert.Equal(2, deserialized.ScopesSupported!.Length);
        Assert.Contains("mcp.tools", deserialized.ScopesSupported!);
        Assert.Contains("mcp.prompts", deserialized.ScopesSupported!);
        Assert.Equal(metadata.ResourceDocumentation, deserialized.ResourceDocumentation);
    }
    
    [Fact]
    public void ProtectedResourceMetadata_JsonDeserialization_WorksWithStringProperties()
    {
        // Arrange
        var json = @"{
            ""resource"": ""http://localhost:7071"",
            ""authorization_servers"": [""https://login.microsoftonline.com/tenant/v2.0""],
            ""bearer_methods_supported"": [""header""],
            ""scopes_supported"": [""mcp.tools"", ""mcp.prompts""],
            ""resource_documentation"": ""https://example.com/docs""
        }";

        // Act
        var deserialized = JsonSerializer.Deserialize<ProtectedResourceMetadata>(json);

        // Assert
        Assert.NotNull(deserialized);
        Assert.Equal(new Uri("http://localhost:7071"), deserialized.Resource);
        Assert.Equal(new Uri("https://login.microsoftonline.com/tenant/v2.0"), deserialized.AuthorizationServers[0]);
        Assert.Equal("header", deserialized.BearerMethodsSupported![0]);
        Assert.Equal(2, deserialized.ScopesSupported!.Length);
        Assert.Contains("mcp.tools", deserialized.ScopesSupported!);
        Assert.Contains("mcp.prompts", deserialized.ScopesSupported!);
        Assert.Equal(new Uri("https://example.com/docs"), deserialized.ResourceDocumentation);
    }
}
