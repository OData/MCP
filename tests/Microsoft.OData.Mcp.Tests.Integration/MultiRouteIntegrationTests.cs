using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using CloudNimble.Breakdance.AspNetCore;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.OData;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.OData.Mcp.Tests.Shared.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.OData.Mcp.Tests.Integration
{
    /// <summary>
    /// Integration tests for multi-route OData MCP scenarios.
    /// </summary>
    [TestClass]
    public class MultiRouteIntegrationTests : McpServerIntegrationTestBase
    {

        #region Test Methods

        //[TestMethod]
        //public async Task MultipleODataRoutes_AutomaticRegistration_CreatesMcpEndpoints()
        //{
        //    // Arrange - Setup is done automatically via TestInitialize

        //    // Act & Assert - Route 1 (simple model)
        //    var response1 = await TestServer.CreateRequest("/odata/mcp").GetAsync();
        //    response1.StatusCode.Should().Be(HttpStatusCode.OK);
        //    var content1 = await response1.Content.ReadAsStringAsync();
        //    content1.Should().Contain("MCP");

        //    // Act & Assert - Route 2 (complex model)
        //    var response2 = await TestServer.CreateRequest("/complex/mcp").GetAsync();
        //    response2.StatusCode.Should().Be(HttpStatusCode.OK);
        //    var content2 = await response2.Content.ReadAsStringAsync();
        //    content2.Should().Contain("MCP");

        //    // Act & Assert - Route 3 (edge model)
        //    var response3 = await TestServer.CreateRequest("/edge/mcp").GetAsync();
        //    response3.StatusCode.Should().Be(HttpStatusCode.OK);
        //}

        [TestMethod]
        public async Task ODataRoute_WithExclusion_DoesNotCreateMcpEndpoint()
        {
            // Arrange - Create a fresh test instance with exclusions
            var testBase = new AspNetCoreBreakdanceTestBase();
            testBase.TestHostBuilder.ConfigureServices(services =>
            {
                services
                    .AddControllers()
                    .AddOData(options => options
                        .AddRouteComponents("api/public", TestModels.GetSimpleModel())
                        .AddRouteComponents("api/internal", TestModels.GetMinimalModel()));

                services.AddODataMcp(options =>
                {
                    options.ExcludeRoutes = ["api/internal"];
                });
            });

            testBase.AddMinimalMvc(app: app =>
            {
                app.UseODataMcp();
            });
            
            testBase.TestSetup();

            // Act & Assert - Public route should work
            var publicResponse = await testBase.TestServer.CreateRequest("/api/public/mcp").GetAsync();
            publicResponse.StatusCode.Should().Be(HttpStatusCode.OK);

            // Act & Assert - Internal route should be excluded
            var internalResponse = await testBase.TestServer.CreateRequest("/api/internal/mcp").GetAsync();
            internalResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
            
            // Cleanup
            testBase.TestTearDown();
        }

        [TestMethod]
        public async Task McpMiddleware_WithoutOData_Returns404()
        {
            // Arrange - Create a test server without OData
            var testBase = new AspNetCoreBreakdanceTestBase();
            testBase.TestHostBuilder.ConfigureServices(services =>
            {
                services.AddControllers();
                // Don't add OData or MCP
            });

            testBase.AddMinimalMvc();
            testBase.TestSetup();

            // Act
            var response = await testBase.TestServer.CreateRequest("/mcp/info").GetAsync();

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.NotFound);
            
            // Cleanup
            testBase.TestTearDown();
        }

        [TestMethod]
        public void McpMiddleware_RegistersDynamicModelRefreshService()
        {
            // Arrange - Create test server with dynamic models enabled
            var testBase = new AspNetCoreBreakdanceTestBase();
            testBase.TestHostBuilder.ConfigureServices(services =>
            {
                services
                    .AddControllers()
                    .AddOData(options => options
                        .AddRouteComponents("odata", TestModels.GetSimpleModel()));

                services.AddODataMcp(options =>
                {
                    options.EnableDynamicModels = true;
                });
            });

            testBase.AddMinimalMvc(app: app =>
            {
                app.UseODataMcp();
            });
            
            testBase.TestSetup();

            // Act - Try to get the dynamic model refresh service
            var hostedServices = testBase.GetService<IEnumerable<IHostedService>>();

            // Assert - Should contain the DynamicModelRefreshService
            hostedServices.Should().NotBeNull();
            hostedServices.Should().Contain(s => s.GetType().Name.Contains("DynamicModelRefreshService"));
            
            // Cleanup
            testBase.TestTearDown();
        }

        //[TestMethod]
        //public async Task McpTools_DifferentModels_ProvideDifferentTools()
        //{
        //    // Arrange - Setup is done automatically via TestInitialize

        //    // Act - Get tools from simple model
        //    var simpleResponse = await TestServer.CreateRequest("/odata/mcp/tools").GetAsync();
        //    simpleResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        //    var simpleContent = await simpleResponse.Content.ReadAsStringAsync();

        //    // Act - Get tools from complex model
        //    var complexResponse = await TestServer.CreateRequest("/complex/mcp/tools").GetAsync();
        //    complexResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        //    var complexContent = await complexResponse.Content.ReadAsStringAsync();

        //    // Assert - Complex model should have more tools
        //    simpleContent.Should().Contain("Customer");
        //    complexContent.Should().Contain("Employee"); // Complex model has Employee entity
        //}

        //[TestMethod]
        //public async Task ODataMetadata_AccessibleWithMcp_AllRoutes()
        //{
        //    // Arrange - Setup is done automatically via TestInitialize

        //    // Act & Assert - All OData metadata endpoints should still work
        //    var odataMetadata = await TestServer.CreateRequest("/odata/$metadata").GetAsync();
        //    odataMetadata.StatusCode.Should().Be(HttpStatusCode.OK);
        //    odataMetadata.Content.Headers.ContentType?.MediaType.Should().Contain("xml");

        //    var complexMetadata = await TestServer.CreateRequest("/complex/$metadata").GetAsync();
        //    complexMetadata.StatusCode.Should().Be(HttpStatusCode.OK);

        //    var edgeMetadata = await TestServer.CreateRequest("/edge/$metadata").GetAsync();
        //    edgeMetadata.StatusCode.Should().Be(HttpStatusCode.OK);
        //}

        #endregion

    }
}
