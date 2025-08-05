using System;
using System.Collections.Generic;
using CloudNimble.Breakdance.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.OData.Mcp.Core.Extensions;
using ModelContextProtocol.Server;

namespace Microsoft.OData.Mcp.Tests
{
    /// <summary>
    /// Base test class for OData MCP Server integration tests using Breakdance.
    /// </summary>
    public class ODataMcpServerTestBase : AspNetCoreBreakdanceTestBase
    {
        /// <summary>
        /// Sets up the test environment with OData MCP Server services.
        /// </summary>
        public override void TestSetup()
        {
            base.TestSetup();
            
            // Configure the test host with OData MCP Server services
            TestHostBuilder.ConfigureServices(services =>
            {
                // Add logging for tests
                services.AddLogging(builder => 
                    builder.AddConsole().SetMinimumLevel(LogLevel.Debug));
                
                // Create test configuration
                var configuration = CreateTestConfiguration();
                services.AddSingleton<IConfiguration>(configuration);
                
                // Add OData MCP Server core services
                services.AddODataMcpServerCore(configuration);
                
                // Add MCP Server with OData tools
                services.AddMcpServer()
                    .WithStdioServerTransport()
                    .WithODataTools();
            });
            
            // Configure minimal application pipeline for testing
            TestHostBuilder.ConfigureServices(services =>
            {
                // Application pipeline configuration will be handled by Breakdance
            });
        }
        
        /// <summary>
        /// Creates a test configuration with realistic OData service settings.
        /// </summary>
        /// <returns>A configured IConfiguration instance for testing.</returns>
        private static IConfiguration CreateTestConfiguration()
        {
            var configurationBuilder = new ConfigurationBuilder();
            
            // Add in-memory configuration with test settings
            configurationBuilder.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["McpServer:ServerInfo:Name"] = "Test OData MCP Server",
                ["McpServer:ServerInfo:Version"] = "1.0.0-test",
                ["McpServer:ServerInfo:Description"] = "Test server for integration tests",
                
                // Use the public Northwind OData service for real integration testing
                ["McpServer:ODataService:BaseUrl"] = "https://services.odata.org/V4/Northwind/Northwind.svc/",
                ["McpServer:ODataService:MetadataPath"] = "/$metadata",
                ["McpServer:ODataService:RequestTimeout"] = "00:00:30",
                ["McpServer:ODataService:MaxRetries"] = "3",
                ["McpServer:ODataService:Authentication:Type"] = "None",
                
                ["McpServer:Caching:Enabled"] = "true",
                ["McpServer:Caching:ProviderType"] = "Memory",
                ["McpServer:Caching:MetadataTtl"] = "00:05:00",
                ["McpServer:Caching:QueryResultTtl"] = "00:01:00",
                
                ["McpServer:ToolGeneration:EnableQueryTools"] = "true",
                ["McpServer:ToolGeneration:EnableCrudTools"] = "true",
                ["McpServer:ToolGeneration:EnableNavigationTools"] = "true",
                ["McpServer:ToolGeneration:NamingConvention"] = "PascalCase",
                ["McpServer:ToolGeneration:IncludeExamples"] = "true",
                ["McpServer:ToolGeneration:MaxQueryDepth"] = "3"
            });
            
            return configurationBuilder.Build();
        }
    }
}