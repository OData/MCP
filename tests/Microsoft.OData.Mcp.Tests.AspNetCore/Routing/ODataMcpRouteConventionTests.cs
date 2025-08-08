using CloudNimble.Breakdance.AspNetCore;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.OData;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OData.Mcp.Tests.Shared.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;

namespace Microsoft.OData.Mcp.Tests.AspNetCore.Routing
{
    /// <summary>
    /// Tests for OData MCP route convention using real test servers.
    /// </summary>
    [TestClass]
    public class ODataMcpRouteConventionTests : AspNetCoreBreakdanceTestBase
    {

        #region Test Lifecycle

        [TestInitialize]
        public void Setup()
        {
            TestHostBuilder.ConfigureServices(services =>
            {
                services
                    .AddControllers()
                    .AddOData(options => options
                        .AddRouteComponents("api/v1", TestModels.GetSimpleModel())
                        .AddRouteComponents("api/v2", TestModels.GetComplexModel())
                        .AddRouteComponents("internal", TestModels.GetMinimalModel()));

                // Add OData MCP with configuration
                services.AddODataMcp(options =>
                {
                    options.AutoRegisterRoutes = true;
                    options.ExcludeRoutes = ["internal"];
                });
            });

            AddMinimalMvc();

            TestHostBuilder.Configure(app =>
            {
                app.UseODataMcp();
            });

            TestSetup();
        }

        [TestCleanup]
        public void TearDown() => TestTearDown();

        #endregion

        #region Auto Registration Tests

        [TestMethod]
        public async Task AutoRegistration_EnabledByDefault_CreatesMcpEndpoints()
        {
            // Act - Request MCP endpoint for v1 route
            var response = await TestServer.CreateRequest("/api/v1/mcp").GetAsync();

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var content = await response.Content.ReadAsStringAsync();
            content.Should().Contain("MCP");
        }

        [TestMethod]
        public async Task AutoRegistration_MultipleRoutes_CreatesEndpointsForEach()
        {
            // Act - Request MCP endpoints for both routes
            var v1Response = await TestServer.CreateRequest("/api/v1/mcp").GetAsync();
            var v2Response = await TestServer.CreateRequest("/api/v2/mcp").GetAsync();

            // Assert
            v1Response.StatusCode.Should().Be(HttpStatusCode.OK);
            v2Response.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        [TestMethod]
        public async Task AutoRegistration_ExcludedRoute_DoesNotCreateEndpoint()
        {
            // Act - Request MCP endpoint for excluded internal route
            var response = await TestServer.CreateRequest("/internal/mcp").GetAsync();

            // Assert - Should return 404 because internal route is excluded
            response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        }

        #endregion

        #region Tool Discovery Tests

        [TestMethod]
        public async Task McpTools_Endpoint_ReturnsAvailableTools()
        {
            // Act - Request tools endpoint
            var response = await TestServer.CreateRequest("/api/v1/mcp/tools").GetAsync();

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var content = await response.Content.ReadAsStringAsync();
            content.Should().Contain("tools");
            content.Should().Contain("Customer");
        }

        [TestMethod]
        public async Task McpTools_DifferentModels_ReturnsDifferentTools()
        {
            // Act - Request tools for both routes
            var v1Response = await TestServer.CreateRequest("/api/v1/mcp/tools").GetAsync();
            var v2Response = await TestServer.CreateRequest("/api/v2/mcp/tools").GetAsync();

            var v1Content = await v1Response.Content.ReadAsStringAsync();
            var v2Content = await v2Response.Content.ReadAsStringAsync();

            // Assert - V2 has complex model with more entities
            v1Response.StatusCode.Should().Be(HttpStatusCode.OK);
            v2Response.StatusCode.Should().Be(HttpStatusCode.OK);
            
            // V2 should have Employee which V1 doesn't have
            v1Content.Should().Contain("Customer");
            v2Content.Should().Contain("Employee");
        }

        #endregion

        #region Metadata Tests

        [TestMethod]
        public async Task ODataMetadata_RemainsAccessible_WithMcpEnabled()
        {
            // Act - Ensure OData metadata still works
            var response = await TestServer.CreateRequest("/api/v1/$metadata").GetAsync();

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            response.Content.Headers.ContentType?.MediaType.Should().Contain("xml");
        }

        #endregion

        #region Configuration Tests

        [TestMethod]
        public async Task McpConfiguration_DisabledRoute_ReturnsNotFound()
        {
            // Arrange - Create a new test server with MCP disabled
            var testBase = new AspNetCoreBreakdanceTestBase();
            testBase.TestHostBuilder.ConfigureServices(services =>
            {
                services.AddControllers()
                    .AddOData(options => options
                        .AddRouteComponents("api/test", TestModels.GetMinimalModel()));

                services.AddODataMcp(options =>
                {
                    options.AutoRegisterRoutes = false; // Disable auto registration
                });
            });

            testBase.AddApis();

            testBase.TestHostBuilder.Configure(app =>
            {
                app.UseODataMcp();
            });

            testBase.TestSetup();

            // Act
            var response = await testBase.TestServer.CreateRequest("/api/test/mcp").GetAsync();

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.NotFound);

            // Cleanup
            testBase.TestTearDown();
        }

        #endregion

    }
}
