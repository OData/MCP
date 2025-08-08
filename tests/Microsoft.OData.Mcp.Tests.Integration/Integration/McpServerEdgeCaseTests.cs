//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Net;
//using System.Net.Http;
//using System.Text;
//using System.Text.Json;
//using System.Threading;
//using System.Threading.Tasks;
//using CloudNimble.Breakdance.AspNetCore;
//using FluentAssertions;
//using Microsoft.AspNetCore.Builder;
//using Microsoft.AspNetCore.OData;
//using Microsoft.Extensions.Configuration;
//using Microsoft.Extensions.DependencyInjection;
//using Microsoft.Extensions.Logging;
//using Microsoft.OData.Mcp.Tools;
//using Microsoft.OData.Mcp.Tests.Shared.Models;
//using Microsoft.VisualStudio.TestTools.UnitTesting;

//namespace Microsoft.OData.Mcp.Tests.Integration
//{
//    /// <summary>
//    /// Edge case and creative failure scenario tests for MCP server.
//    /// Tests boundary conditions, resource limits, and unusual scenarios.
//    /// </summary>
//    [TestClass]
//    public class McpServerEdgeCaseTests : AspNetCoreBreakdanceTestBase
//    {
//        #region Test Setup

//        /// <summary>
//        /// Sets up the test environment with MCP Console server services.
//        /// </summary>
//        [TestInitialize]
//        public override void TestSetup()
//        {
//            // Configure the test host with MCP Console server services
//            TestHostBuilder.ConfigureServices(services =>
//            {
//                // Create test configuration
//                var configuration = new ConfigurationBuilder()
//                    .AddInMemoryCollection(new Dictionary<string, string?>
//                    {
//                        ["McpServer:ServerInfo:Name"] = "Test MCP Edge Case Server",
//                        ["McpServer:ServerInfo:Version"] = "1.0.0-test",
//                        ["McpServer:ServerInfo:Description"] = "Test MCP server for edge case testing",
//                        ["McpServer:ODataService:MetadataPath"] = "/$metadata",
//                        ["McpServer:Authentication:Enabled"] = "false",
//                        ["McpServer:Caching:Enabled"] = "true",
//                        ["McpServer:ToolGeneration:EnableQueryTools"] = "true",
//                        ["McpServer:ToolGeneration:EnableCrudTools"] = "true"
//                    })
//                    .Build();

//                // Configure logging for tests
//                services.AddLogging(builder =>
//                {
//                    builder.AddConsole().SetMinimumLevel(LogLevel.Debug);
//                });

//                // Use the Console server's configuration method
//                McpServerConfiguration.ConfigureMcpServices(
//                    services,
//                    configuration,
//                    "http://testserver/odata", // Mock OData service URL
//                    authToken: null);

//                // Add controllers and OData with test models that include multiple entity sets
//                services
//                    .AddControllers()
//                    .AddOData(options => options
//                        .AddRouteComponents("odata", TestModels.GetNorthwindModel()));

//                // Add OData MCP for automatic endpoint registration
//                services.AddODataMcp(options =>
//                {
//                    options.AutoRegisterRoutes = true;
//                    options.EnableDynamicModels = false;
//                });
//            });

//            // Configure the application pipeline
//            AddMinimalMvc(app: app =>
//            {
//                app.UseODataMcp();
//            });

//            // Call base setup after configuration
//            base.TestSetup();
//        }

//        /// <summary>
//        /// Cleans up after each test.
//        /// </summary>
//        [TestCleanup]
//        public override void TestTearDown()
//        {
//            base.TestTearDown();
//        }

//        #endregion

//        #region Memory and Resource Tests

//        [TestMethod]
//        public async Task LargeParameterValues_ShouldHandleCorrectly()
//        {
//            // Arrange
//            var largeFilter = string.Join(" or ", Enumerable.Range(1, 100)
//                .Select(i => $"CustomerID eq 'CUST{i:D4}'"));
            
//            var parameters = new 
//            { 
//                entitySet = "Customers",
//                filter = largeFilter,
//                top = 5
//            };
//            var cancellationToken = CreateTestCancellationToken();

//            // Act
//            var response = await McpClient!.CallToolAsync("QueryEntitySet", parameters, cancellationToken);

//            // Assert
//            response.Should().NotBeNull();
//            // Server should either handle it or return a meaningful error
//            if (response.Result!.IsError)
//            {
//                response.Result.Content![0].Text.Should().NotBeNullOrEmpty();
//            }
//            else
//            {
//                var jsonContent = response.Result.Content![0].Text;
//                jsonContent.Should().Contain("@odata.context");
//            }
//        }

//        [TestMethod]
//        public async Task VeryLongEntitySetName_ShouldHandleCorrectly()
//        {
//            // Arrange
//            var veryLongName = new string('A', 1000); // 1000 character entity set name
//            var parameters = new { entitySet = veryLongName };
//            var cancellationToken = CreateTestCancellationToken();

//            // Act
//            var response = await McpClient!.CallToolAsync("QueryEntitySet", parameters, cancellationToken);

//            // Assert
//            response.Should().NotBeNull();
//            response.Result!.IsError.Should().BeTrue();
//            response.Result.Content![0].Text.Should().ContainEquivalentOf("failed");
//        }

//        [TestMethod]
//        public async Task ExtremelyLargeTopValue_ShouldHandleSafely()
//        {
//            // Arrange
//            var parameters = new 
//            { 
//                entitySet = "Customers",
//                top = int.MaxValue
//            };
//            var cancellationToken = CreateTestCancellationToken(TimeSpan.FromMinutes(2));

//            // Act
//            var response = await McpClient!.CallToolAsync("QueryEntitySet", parameters, cancellationToken);

//            // Assert
//            response.Should().NotBeNull();
//            // Server should handle this gracefully, either by limiting or returning error
//            if (!response.Result!.IsError)
//            {
//                var jsonContent = response.Result.Content![0].Text;
//                var jsonDoc = JsonDocument.Parse(jsonContent);
//                var values = jsonDoc.RootElement.GetProperty("value");
//                // Should not actually return int.MaxValue items
//                values.GetArrayLength().Should().BeLessThan(100000);
//            }
//        }

//        [TestMethod]
//        public async Task RapidSequentialRequests_ShouldNotLeakResources()
//        {
//            // Arrange
//            var cancellationToken = CreateTestCancellationToken();
//            const int requestCount = 50;

//            // Act
//            for (int i = 0; i < requestCount; i++)
//            {
//                var response = await McpClient!.CallToolAsync("QueryEntitySet", 
//                    new { entitySet = "Customers", top = 1 }, cancellationToken);
                
//                response.Should().NotBeNull();
//                response.Error.Should().BeNull();
                
//                // Small delay to prevent overwhelming the server
//                await Task.Delay(50, cancellationToken);
//            }

//            // Assert - Server should still be responsive
//            var finalResponse = await McpClient!.PingAsync(cancellationToken);
//            finalResponse.Error.Should().BeNull();
//        }

//        #endregion

//        #region Unicode and Special Character Tests

//        [TestMethod]
//        public async Task UnicodeInFilter_ShouldHandleCorrectly()
//        {
//            // Arrange
//            var parameters = new 
//            { 
//                entitySet = "Customers",
//                filter = "CompanyName eq '日本語テスト'", // Japanese characters
//                top = 5
//            };
//            var cancellationToken = CreateTestCancellationToken();

//            // Act
//            var response = await McpClient!.CallToolAsync("QueryEntitySet", parameters, cancellationToken);

//            // Assert
//            response.Should().NotBeNull();
//            // Should handle Unicode gracefully, even if no results match
//            if (response.Result!.IsError)
//            {
//                // If error, should be a meaningful OData error, not a parsing error
//                response.Result.Content![0].Text.Should().NotContainEquivalentOf("encoding");
//            }
//            else
//            {
//                var jsonContent = response.Result.Content![0].Text;
//                jsonContent.Should().Contain("@odata.context");
//            }
//        }

//        [TestMethod]
//        public async Task SpecialCharactersInParameters_ShouldHandleCorrectly()
//        {
//            // Arrange
//            var parameters = new 
//            { 
//                entitySet = "Customers",
//                filter = "CompanyName eq 'Test & Company <script>alert(\"xss\")</script>'",
//                top = 1
//            };
//            var cancellationToken = CreateTestCancellationToken();

//            // Act
//            var response = await McpClient!.CallToolAsync("QueryEntitySet", parameters, cancellationToken);

//            // Assert
//            response.Should().NotBeNull();
//            // Should handle special characters safely
//            if (!response.Result!.IsError)
//            {
//                var jsonContent = response.Result.Content![0].Text;
//                jsonContent.Should().NotContain("<script>"); // Should not echo back unsafe content
//            }
//        }

//        [TestMethod]
//        public async Task NullCharactersInParameters_ShouldHandleSafely()
//        {
//            // Arrange
//            var filterWithNulls = "CompanyName eq 'Test\0Company'";
//            var parameters = new 
//            { 
//                entitySet = "Customers",
//                filter = filterWithNulls
//            };
//            var cancellationToken = CreateTestCancellationToken();

//            // Act
//            var response = await McpClient!.CallToolAsync("QueryEntitySet", parameters, cancellationToken);

//            // Assert
//            response.Should().NotBeNull();
//            // Should handle null characters gracefully without crashing
//            response.Result.Should().NotBeNull();
//        }

//        #endregion

//        #region Protocol Edge Cases

//        [TestMethod]
//        public async Task RequestWithVeryLargeId_ShouldHandleCorrectly()
//        {
//            // Arrange
//            var request = new McpRequest
//            {
//                JsonRpc = "2.0",
//                Id = int.MaxValue,
//                Method = "ping",
//                Params = new { }
//            };

//            var requestJson = JsonSerializer.Serialize(request, new JsonSerializerOptions
//            {
//                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
//            });

//            // Act
//            var rawResponse = await McpClient!.SendRawRequestAsync(requestJson, CreateTestCancellationToken());

//            // Assert
//            rawResponse.Should().NotBeNullOrEmpty();
//            var response = JsonSerializer.Deserialize<McpPingResponse>(rawResponse, new JsonSerializerOptions
//            {
//                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
//                PropertyNameCaseInsensitive = true
//            });

//            response!.Id.Should().Be(int.MaxValue);
//        }

//        [TestMethod]
//        public async Task RequestWithNegativeId_ShouldHandleCorrectly()
//        {
//            // Arrange
//            var request = new McpRequest
//            {
//                JsonRpc = "2.0",
//                Id = -999,
//                Method = "ping",
//                Params = new { }
//            };

//            var requestJson = JsonSerializer.Serialize(request, new JsonSerializerOptions
//            {
//                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
//            });

//            // Act
//            var rawResponse = await McpClient!.SendRawRequestAsync(requestJson, CreateTestCancellationToken());

//            // Assert
//            rawResponse.Should().NotBeNullOrEmpty();
//            var response = JsonSerializer.Deserialize<McpPingResponse>(rawResponse, new JsonSerializerOptions
//            {
//                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
//                PropertyNameCaseInsensitive = true
//            });

//            response!.Id.Should().Be(-999);
//        }

//        [TestMethod]
//        public async Task MultipleSimultaneousConnections_ShouldHandleCorrectly()
//        {
//            // Arrange
//            const int connectionCount = 3;
//            var clients = new McpProtocolTestClient[connectionCount];
//            var serverPath = FindServerExecutable();

//            try
//            {
//                // Act - Create multiple clients
//                for (int i = 0; i < connectionCount; i++)
//                {
//                    clients[i] = new McpProtocolTestClient(serverPath!, Logger!);
//                    await clients[i].InitializeAsync(CreateTestCancellationToken());
//                }

//                // Test concurrent operations
//                var tasks = clients.Select(client => 
//                    client.CallToolAsync("QueryEntitySet", new { entitySet = "Customers", top = 1 }, CreateTestCancellationToken()))
//                    .ToArray();

//                var responses = await Task.WhenAll(tasks);

//                // Assert
//                responses.Should().HaveCount(connectionCount);
//                responses.Should().OnlyContain(r => r.Error == null);
//            }
//            finally
//            {
//                // Cleanup
//                foreach (var client in clients)
//                {
//                    client?.Dispose();
//                }
//            }
//        }

//        [TestMethod]
//        public async Task VeryLongRunningRequest_ShouldHandleTimeout()
//        {
//            // Arrange
//            var parameters = new 
//            { 
//                entitySet = "Orders", // Large dataset
//                filter = "year(OrderDate) eq 1996", // Potentially expensive filter
//                orderBy = "Freight desc" // Additional processing
//            };
            
//            // Use a very short timeout to test timeout handling
//            var shortTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(1));

//            // Act & Assert
//            await Assert.ThrowsExactlyAsync<OperationCanceledException>(async () =>
//            {
//                await McpClient!.CallToolAsync("QueryEntitySet", parameters, shortTimeout.Token);
//            });

//            // Verify server is still responsive after timeout
//            var pingResponse = await McpClient!.PingAsync(CreateTestCancellationToken());
//            pingResponse.Error.Should().BeNull();
//        }

//        #endregion

//        #region Data Type Edge Cases

//        [TestMethod]
//        public async Task FilterWithExtremeNumbers_ShouldHandleCorrectly()
//        {
//            // Arrange
//            var parameters = new 
//            { 
//                entitySet = "Products",
//                filter = $"UnitPrice gt {decimal.MaxValue}", // Extreme decimal value
//                top = 1
//            };
//            var cancellationToken = CreateTestCancellationToken();

//            // Act
//            var response = await McpClient!.CallToolAsync("QueryEntitySet", parameters, cancellationToken);

//            // Assert
//            response.Should().NotBeNull();
//            // Should handle extreme values gracefully
//            if (!response.Result!.IsError)
//            {
//                var jsonContent = response.Result.Content![0].Text;
//                var jsonDoc = JsonDocument.Parse(jsonContent);
//                var values = jsonDoc.RootElement.GetProperty("value");
//                values.GetArrayLength().Should().Be(0); // No products should match this filter
//            }
//        }

//        [TestMethod]
//        public async Task FilterWithDateTimeEdgeCases_ShouldHandleCorrectly()
//        {
//            // Arrange
//            var parameters = new 
//            { 
//                entitySet = "Orders",
//                filter = "OrderDate eq datetime'1900-01-01T00:00:00'", // Very old date
//                top = 5
//            };
//            var cancellationToken = CreateTestCancellationToken();

//            // Act
//            var response = await McpClient!.CallToolAsync("QueryEntitySet", parameters, cancellationToken);

//            // Assert
//            response.Should().NotBeNull();
//            // Should handle edge case dates gracefully
//            if (!response.Result!.IsError)
//            {
//                var jsonContent = response.Result.Content![0].Text;
//                jsonContent.Should().Contain("@odata.context");
//            }
//        }

//        [TestMethod]
//        public async Task FilterWithBooleanEdgeCases_ShouldHandleCorrectly()
//        {
//            // Arrange
//            var parameters = new 
//            { 
//                entitySet = "Products",
//                filter = "Discontinued eq true and Discontinued eq false", // Contradiction
//                top = 1
//            };
//            var cancellationToken = CreateTestCancellationToken();

//            // Act
//            var response = await McpClient!.CallToolAsync("QueryEntitySet", parameters, cancellationToken);

//            // Assert
//            response.Should().NotBeNull();
//            if (!response.Result!.IsError)
//            {
//                var jsonContent = response.Result.Content![0].Text;
//                var jsonDoc = JsonDocument.Parse(jsonContent);
//                var values = jsonDoc.RootElement.GetProperty("value");
//                values.GetArrayLength().Should().Be(0); // Contradictory filter should return no results
//            }
//        }

//        #endregion

//        #region Network and Connectivity Edge Cases

//        [TestMethod]
//        public async Task ServerProcessKill_ShouldDetectDisconnection()
//        {
//            // Arrange
//            var initialResponse = await McpClient!.PingAsync(CreateTestCancellationToken());
//            initialResponse.Error.Should().BeNull();

//            // Act - Kill the server process externally (simulate crash)
//            var processId = McpClient.ServerProcessId;
//            var serverProcess = System.Diagnostics.Process.GetProcessById(processId);
//            serverProcess.Kill();
//            await Task.Delay(1000); // Wait for process to terminate

//            // Assert
//            McpClient.IsServerRunning.Should().BeFalse();

//            // Subsequent requests should fail
//            await Assert.ThrowsExactlyAsync<InvalidOperationException>(async () =>
//            {
//                await McpClient.PingAsync(CreateTestCancellationToken());
//            });
//        }

//        [TestMethod]
//        public async Task RequestAfterServerDispose_ShouldThrowException()
//        {
//            // Arrange
//            var initialResponse = await McpClient!.PingAsync(CreateTestCancellationToken());
//            initialResponse.Error.Should().BeNull();

//            // Act
//            McpClient.Dispose();

//            // Assert
//            await Assert.ThrowsExactlyAsync<ObjectDisposedException>(async () =>
//            {
//                await McpClient.PingAsync(CreateTestCancellationToken());
//            });
//        }

//        #endregion

//        #region JSON-RPC Protocol Edge Cases

//        [TestMethod]
//        public async Task RequestWithExtraFields_ShouldIgnoreGracefully()
//        {
//            // Arrange
//            var requestJson = """
//            {
//                "jsonrpc": "2.0",
//                "id": 1,
//                "method": "ping",
//                "params": {},
//                "extraField": "should be ignored",
//                "anotherExtra": 12345
//            }
//            """;

//            // Act
//            var rawResponse = await McpClient!.SendRawRequestAsync(requestJson, CreateTestCancellationToken());

//            // Assert
//            rawResponse.Should().NotBeNullOrEmpty();
//            var response = JsonSerializer.Deserialize<McpPingResponse>(rawResponse, new JsonSerializerOptions
//            {
//                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
//                PropertyNameCaseInsensitive = true
//            });

//            response!.Error.Should().BeNull(); // Should succeed despite extra fields
//        }

//        [TestMethod]
//        public async Task RequestWithDifferentCasing_ShouldHandleCorrectly()
//        {
//            // Arrange
//            var requestJson = """
//            {
//                "JSONRPC": "2.0",
//                "ID": 1,
//                "METHOD": "ping",
//                "PARAMS": {}
//            }
//            """;

//            // Act
//            var rawResponse = await McpClient!.SendRawRequestAsync(requestJson, CreateTestCancellationToken());

//            // Assert
//            rawResponse.Should().NotBeNullOrEmpty();
//            // Server should handle case variations gracefully
//            var response = JsonSerializer.Deserialize<JsonElement>(rawResponse);
//            response.TryGetProperty("error", out var error).Should().BeFalse("Server should handle case variations");
//        }

//        [TestMethod]
//        public async Task BatchRequests_ShouldHandleIndividually()
//        {
//            // Arrange - Send multiple requests rapidly
//            var requests = Enumerable.Range(1, 5).Select(i => new McpRequest
//            {
//                JsonRpc = "2.0",
//                Id = i,
//                Method = "ping",
//                Params = new { }
//            }).ToArray();

//            // Act
//            var tasks = requests.Select(async request =>
//            {
//                var requestJson = JsonSerializer.Serialize(request, new JsonSerializerOptions
//                {
//                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
//                });
//                return await McpClient!.SendRawRequestAsync(requestJson, CreateTestCancellationToken());
//            }).ToArray();

//            var responses = await Task.WhenAll(tasks);

//            // Assert
//            responses.Should().HaveCount(5);
//            for (int i = 0; i < responses.Length; i++)
//            {
//                var response = JsonSerializer.Deserialize<McpPingResponse>(responses[i], new JsonSerializerOptions
//                {
//                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
//                    PropertyNameCaseInsensitive = true
//                });
                
//                response!.Id.Should().Be(i + 1);
//                response.Error.Should().BeNull();
//            }
//        }

//        #endregion
//    }
//}
