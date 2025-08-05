using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.OData.Mcp.Tests.Integration
{
    /// <summary>
    /// Integration tests for MCP server protocol communication.
    /// Tests the actual MCP protocol handshake, tool discovery, and basic communication.
    /// </summary>
    [TestClass]
    public class McpServerProtocolTests : McpServerIntegrationTestBase
    {
        [TestMethod]
        public void ServerStartup_ShouldStartSuccessfully()
        {
            // Arrange & Act
            // Server startup happens in TestSetup

            // Assert
            McpClient.Should().NotBeNull();
            McpClient!.IsServerRunning.Should().BeTrue();
            McpClient.ServerProcessId.Should().BeGreaterThan(0);
        }

        [TestMethod]
        public async Task Initialize_ShouldReturnValidServerInfo()
        {
            // Arrange
            var cancellationToken = CreateTestCancellationToken();

            // Act
            var response = await InitializeMcpConnectionAsync(cancellationToken);

            // Assert
            response.Should().NotBeNull();
            response.Error.Should().BeNull();
            response.Result.Should().NotBeNull();
            response.Result!.ProtocolVersion.Should().NotBeNullOrEmpty();
            response.Result.ServerInfo.Should().NotBeNull();
            response.Result.ServerInfo!.Name.Should().Contain("OData MCP Server");
            response.Result.ServerInfo.Version.Should().NotBeNullOrEmpty();
            response.Result.Capabilities.Should().NotBeNull();
            response.Result.Capabilities!.Tools.Should().NotBeNull();
        }

        [TestMethod]
        public async Task Initialize_WithInvalidProtocolVersion_ShouldHandleGracefully()
        {
            // Arrange
            var invalidRequest = new McpRequest
            {
                JsonRpc = "2.0",
                Id = 1,
                Method = "initialize",
                Params = new
                {
                    protocolVersion = "invalid-version",
                    capabilities = new { tools = new { } },
                    clientInfo = new { name = "Test", version = "1.0.0" }
                }
            };

            var requestJson = JsonSerializer.Serialize(invalidRequest, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            // Act
            var rawResponse = await McpClient!.SendRawRequestAsync(requestJson, CreateTestCancellationToken());

            // Assert
            rawResponse.Should().NotBeNullOrEmpty();
            var response = JsonSerializer.Deserialize<McpInitializeResponse>(rawResponse, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                PropertyNameCaseInsensitive = true
            });

            // The server should either accept it gracefully or return a proper error
            if (response!.Error != null)
            {
                response.Error.Code.Should().BeOneOf(-32600, -32602); // Invalid Request or Invalid params
                response.Error.Message.Should().NotBeNullOrEmpty();
            }
            else
            {
                response.Result.Should().NotBeNull();
            }
        }

        [TestMethod]
        public async Task ListTools_ShouldReturnAvailableTools()
        {
            // Arrange
            await InitializeMcpConnectionAsync();
            var cancellationToken = CreateTestCancellationToken();

            // Act
            var response = await McpClient!.ListToolsAsync(cancellationToken);

            // Assert
            response.Should().NotBeNull();
            response.Error.Should().BeNull();
            response.Result.Should().NotBeNull();
            response.Result!.Tools.Should().NotBeNull();
            response.Result.Tools!.Length.Should().BeGreaterThan(0);

            // Verify expected tools are present
            var toolNames = response.Result.Tools.Select(t => t.Name).ToArray();
            toolNames.Should().Contain("QueryEntitySet");
            toolNames.Should().Contain("GetEntity");
            toolNames.Should().Contain("GetMetadata");

            // Verify tool structure
            foreach (var tool in response.Result.Tools)
            {
                tool.Name.Should().NotBeNullOrEmpty();
                tool.Description.Should().NotBeNullOrEmpty();
                tool.InputSchema.Should().NotBeNull();
            }
        }

        [TestMethod]
        public async Task ListTools_MultipleRequests_ShouldReturnConsistentResults()
        {
            // Arrange
            await InitializeMcpConnectionAsync();
            var cancellationToken = CreateTestCancellationToken();

            // Act
            var response1 = await McpClient!.ListToolsAsync(cancellationToken);
            var response2 = await McpClient.ListToolsAsync(cancellationToken);

            // Assert
            response1.Result!.Tools!.Length.Should().Be(response2.Result!.Tools!.Length);
            
            var toolNames1 = response1.Result.Tools.Select(t => t.Name).OrderBy(n => n).ToArray();
            var toolNames2 = response2.Result.Tools.Select(t => t.Name).OrderBy(n => n).ToArray();
            
            toolNames1.Should().BeEquivalentTo(toolNames2);
        }

        [TestMethod]
        public async Task Ping_ShouldReturnSuccessfully()
        {
            // Arrange
            await InitializeMcpConnectionAsync();
            var cancellationToken = CreateTestCancellationToken();

            // Act
            var response = await McpClient!.PingAsync(cancellationToken);

            // Assert
            response.Should().NotBeNull();
            response.Error.Should().BeNull();
        }

        [TestMethod]
        public async Task InvalidMethod_ShouldReturnMethodNotFoundError()
        {
            // Arrange
            await InitializeMcpConnectionAsync();
            var invalidRequest = new McpRequest
            {
                JsonRpc = "2.0",
                Id = 999,
                Method = "nonexistent/method",
                Params = new { }
            };

            var requestJson = JsonSerializer.Serialize(invalidRequest, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            // Act
            var rawResponse = await McpClient!.SendRawRequestAsync(requestJson, CreateTestCancellationToken());

            // Assert
            rawResponse.Should().NotBeNullOrEmpty();
            var response = JsonSerializer.Deserialize<McpResponse<object>>(rawResponse, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                PropertyNameCaseInsensitive = true
            });

            response.Should().NotBeNull();
            response!.Error.Should().NotBeNull();
            response.Error!.Code.Should().Be(-32601); // Method not found
            response.Error.Message.Should().ContainEquivalentOf("not found");
        }

        [TestMethod]
        public async Task MalformedJson_ShouldReturnParseError()
        {
            // Arrange
            await InitializeMcpConnectionAsync();
            var malformedJson = "{ invalid json ]}";

            // Act
            var rawResponse = await McpClient!.SendRawRequestAsync(malformedJson, CreateTestCancellationToken());

            // Assert
            rawResponse.Should().NotBeNullOrEmpty();
            
            // The server should return a JSON-RPC parse error
            var response = JsonSerializer.Deserialize<McpResponse<object>>(rawResponse, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                PropertyNameCaseInsensitive = true
            });

            response.Should().NotBeNull();
            response!.Error.Should().NotBeNull();
            response.Error!.Code.Should().Be(-32700); // Parse error
        }

        [TestMethod]
        public async Task RequestWithoutId_ShouldReturnInvalidRequestError()
        {
            // Arrange
            await InitializeMcpConnectionAsync();
            var requestWithoutId = """{"jsonrpc": "2.0", "method": "ping", "params": {}}""";

            // Act
            var rawResponse = await McpClient!.SendRawRequestAsync(requestWithoutId, CreateTestCancellationToken());

            // Assert
            rawResponse.Should().NotBeNullOrEmpty();
            var response = JsonSerializer.Deserialize<McpResponse<object>>(rawResponse, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                PropertyNameCaseInsensitive = true
            });

            response.Should().NotBeNull();
            response!.Error.Should().NotBeNull();
            response.Error!.Code.Should().Be(-32600); // Invalid Request
        }

        [TestMethod]
        public async Task RequestWithInvalidJsonRpcVersion_ShouldReturnInvalidRequestError()
        {
            // Arrange
            await InitializeMcpConnectionAsync();
            var invalidRequest = new
            {
                jsonrpc = "1.0", // Invalid version
                id = 1,
                method = "ping",
                @params = new { }
            };

            var requestJson = JsonSerializer.Serialize(invalidRequest);

            // Act
            var rawResponse = await McpClient!.SendRawRequestAsync(requestJson, CreateTestCancellationToken());

            // Assert
            rawResponse.Should().NotBeNullOrEmpty();
            var response = JsonSerializer.Deserialize<McpResponse<object>>(rawResponse, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                PropertyNameCaseInsensitive = true
            });

            response.Should().NotBeNull();
            response!.Error.Should().NotBeNull();
            response.Error!.Code.Should().Be(-32600); // Invalid Request
        }

        [TestMethod]
        public async Task ConcurrentRequests_ShouldHandleCorrectly()
        {
            // Arrange
            await InitializeMcpConnectionAsync();
            var cancellationToken = CreateTestCancellationToken();
            const int requestCount = 5;

            // Act
            var tasks = Enumerable.Range(0, requestCount)
                .Select(_ => McpClient!.PingAsync(cancellationToken))
                .ToArray();

            var responses = await Task.WhenAll(tasks);

            // Assert
            responses.Should().HaveCount(requestCount);
            responses.Should().OnlyContain(r => r.Error == null);
        }

        [TestMethod]
        public async Task ServerShutdown_ShouldHandleGracefully()
        {
            // Arrange
            await InitializeMcpConnectionAsync();
            
            // Act
            McpClient!.Dispose();

            // Assert
            McpClient.IsServerRunning.Should().BeFalse();
        }

        [TestMethod]
        public async Task LargeResponse_ShouldHandleCorrectly()
        {
            // Arrange
            await InitializeMcpConnectionAsync();
            var cancellationToken = CreateTestCancellationToken(TimeSpan.FromMinutes(5)); // Longer timeout for large responses

            // Act - Request metadata which is typically large
            var response = await McpClient!.CallToolAsync("GetMetadata", new { }, cancellationToken);

            // Assert
            response.Should().NotBeNull();
            response.Error.Should().BeNull();
            response.Result.Should().NotBeNull();
            response.Result!.Content.Should().NotBeNullOrEmpty();
            response.Result.Content![0].Text.Should().Contain("<?xml");
            response.Result.Content[0].Text.Length.Should().BeGreaterThan(1000); // Metadata should be substantial
        }

        [TestMethod]
        public async Task ServerRestart_ShouldHandleCorrectly()
        {
            // Arrange
            await InitializeMcpConnectionAsync();
            
            // Verify server is working
            var initialResponse = await McpClient!.PingAsync(CreateTestCancellationToken());
            initialResponse.Error.Should().BeNull();

            // Act - Dispose and recreate client (simulating server restart)
            var serverPath = FindServerExecutable();

            RecreateClient(serverPath);

            // Re-initialize
            await InitializeMcpConnectionAsync();

            // Assert
            var restartResponse = await McpClient.PingAsync(CreateTestCancellationToken());
            restartResponse.Error.Should().BeNull();
        }
    }
}