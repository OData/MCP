using System.Collections.Generic;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using CloudNimble.Breakdance.AspNetCore;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.OData;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.OData.Mcp.Tools;
using Microsoft.OData.Mcp.Tests.Shared.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.OData.Mcp.Tests.Integration
{
    /// <summary>
    /// Integration tests for the MCP Console server running in-process via Breakdance.
    /// </summary>
    [TestClass]
    public class McpConsoleIntegrationTests : AspNetCoreBreakdanceTestBase
    {

        #region Test Setup

        /// <summary>
        /// Sets up the test environment with MCP Console server services.
        /// </summary>
        [TestInitialize]
        public override void TestSetup()
        {
            // Configure the test host with MCP Console server services
            TestHostBuilder.ConfigureServices(services =>
            {
                // Create test configuration
                var configuration = new ConfigurationBuilder()
                    .AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["McpServer:ServerInfo:Name"] = "Test MCP Console Server",
                        ["McpServer:ServerInfo:Version"] = "1.0.0-test",
                        ["McpServer:ServerInfo:Description"] = "Test MCP Console server for integration tests",
                        ["McpServer:ODataService:MetadataPath"] = "/$metadata",
                        ["McpServer:Authentication:Enabled"] = "false",
                        ["McpServer:Caching:Enabled"] = "true",
                        ["McpServer:ToolGeneration:EnableQueryTools"] = "true",
                        ["McpServer:ToolGeneration:EnableCrudTools"] = "true"
                    })
                    .Build();

                // Configure logging for tests
                services.AddLogging(builder =>
                {
                    builder.AddConsole().SetMinimumLevel(LogLevel.Debug);
                });

                // Use the Console server's configuration method
                McpServerConfiguration.ConfigureMcpServices(
                    services,
                    configuration,
                    "http://testserver/odata", // Mock OData service URL
                    authToken: null);

                // Add controllers and OData with test models
                // This simulates having an OData service to connect to
                services
                    .AddControllers()
                    .AddOData(options => options
                        .AddRouteComponents("odata", TestModels.GetSimpleModel()));

                // Add OData MCP for automatic endpoint registration
                services.AddODataMcp(options =>
                {
                    options.AutoRegisterRoutes = true;
                    options.EnableDynamicModels = false;
                });
            });

            // Configure the application pipeline
            AddMinimalMvc(app: app =>
            {
                app.UseODataMcp();
            });

            // Call base setup after configuration
            base.TestSetup();
        }

        /// <summary>
        /// Cleans up after each test.
        /// </summary>
        [TestCleanup]
        public override void TestTearDown()
        {
            base.TestTearDown();
        }

        #endregion

        #region Test Methods

        [TestMethod]
        public async Task ConsoleServer_Start_ProvidesServerInfo()
        {
            // Act
            var response = await TestServer.CreateRequest("/odata/mcp").GetAsync();

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var content = await response.Content.ReadAsStringAsync();
            content.Should().NotBeNullOrWhiteSpace();
            content.Should().Contain("MCP");
        }

        [TestMethod]
        public async Task ConsoleServer_Metadata_ReturnsODataMetadata()
        {
            // Act
            var response = await TestServer.CreateRequest("/odata/$metadata").GetAsync();

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            response.Content.Headers.ContentType?.MediaType.Should().Contain("xml");
            
            var content = await response.Content.ReadAsStringAsync();
            content.Should().Contain("edmx:Edmx");
            content.Should().Contain("Customer"); // From our test model
        }

        [TestMethod]
        public async Task ConsoleServer_McpTools_ReturnsAvailableTools()
        {
            // Act
            var response = await TestServer.CreateRequest("/odata/mcp/tools").GetAsync();

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            response.Content.Headers.ContentType?.MediaType.Should().Contain("json");
            
            var content = await response.Content.ReadAsStringAsync();
            
            // Parse JSON to verify structure
            using var doc = JsonDocument.Parse(content);
            doc.RootElement.TryGetProperty("tools", out var tools).Should().BeTrue();
            tools.ValueKind.Should().Be(JsonValueKind.Array);
        }

        [TestMethod]
        public async Task ConsoleServer_McpInfo_ReturnsServerCapabilities()
        {
            // Act
            var response = await TestServer.CreateRequest("/odata/mcp/info").GetAsync();

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            
            var content = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(content);
            
            // Verify expected MCP server info structure
            doc.RootElement.TryGetProperty("name", out var name).Should().BeTrue();
            doc.RootElement.TryGetProperty("version", out var version).Should().BeTrue();
            doc.RootElement.TryGetProperty("capabilities", out var capabilities).Should().BeTrue();
            
            capabilities.TryGetProperty("tools", out var toolsCapability).Should().BeTrue();
            toolsCapability.GetBoolean().Should().BeTrue();
        }

        [TestMethod]
        public async Task ConsoleServer_InvalidRoute_Returns404()
        {
            // Act
            var response = await TestServer.CreateRequest("/invalid/route").GetAsync();

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        }

        [TestMethod]
        public async Task ConsoleServer_WithTestData_GeneratesCorrectTools()
        {
            // Act - Get tools for the test OData model
            var response = await TestServer.CreateRequest("/odata/mcp/tools").GetAsync();
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            
            var content = await response.Content.ReadAsStringAsync();
            
            // Assert - Verify tools are generated for our test entities
            content.Should().Contain("Customer", "Should have tools for Customer entity");
            
            // Parse and verify tool structure
            using var doc = JsonDocument.Parse(content);
            var tools = doc.RootElement.GetProperty("tools");
            
            // Should have multiple tools generated
            tools.GetArrayLength().Should().BeGreaterThan(0, "Should generate tools from the test model");
        }

        [TestMethod]
        public async Task ConsoleServer_ParserService_IsRegistered()
        {
            // This test verifies that the CsdlParser service is properly registered
            // by attempting to use an endpoint that would require it
            
            // Act
            var response = await TestServer.CreateRequest("/odata/mcp").GetAsync();

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK, 
                "Server should start successfully with parser service registered");
        }

        #endregion

    }
}