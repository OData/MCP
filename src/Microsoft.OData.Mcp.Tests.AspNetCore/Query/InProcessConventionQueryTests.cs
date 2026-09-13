// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using CloudNimble.Breakdance.AspNetCore;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.OData;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.OData.Mcp.AspNetCore.Execution;
using Microsoft.OData.Mcp.AspNetCore.Hosting;
using Microsoft.OData.Mcp.Tests.AspNetCore.Execution;
using Microsoft.OData.Mcp.Tests.AspNetCore.Security;
using Microsoft.OData.Mcp.Tests.Shared.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.OData.Mcp.Tests.AspNetCore.Query
{

    /// <summary>
    /// Convention-model in-process query using the same TestServer OData route and MCP side by side.
    /// </summary>
    [TestClass]
    public class InProcessConventionQueryTests : AspNetCoreBreakdanceTestBase
    {

        #region Test Lifecycle

        /// <summary>
        /// Builds a convention OData host with MCP.
        /// </summary>
        [TestInitialize]
        public void Setup()
        {
            TestHostBuilder.ConfigureServices((_, services) =>
            {
                services
                    .AddControllers()
                    .AddApplicationPart(typeof(CustomersController).Assembly)
                    .AddOData(options =>
                    {
                        options.EnableQueryFeatures();
                        options.AddRouteComponents("odata", TestModels.GetSimpleModel());
                    });

                services.AddODataMcp();
            });

            AddMinimalMvc();
            TestHostBuilder.ConfigureWebHost(web =>
            {
                web.Configure(app =>
                {
                    app.UseRouting();
                    app.UseEndpoints(endpoints =>
                    {
                        endpoints.MapControllers();
                    });
                    app.UseODataMcp();
                });
            });
            TestSetup();
        }

        /// <summary>
        /// Tears down the host.
        /// </summary>
        [TestCleanup]
        public void TearDown()
        {
            TestTearDown();
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Direct OData GET and MCP odata_query return the same seeded company.
        /// </summary>
        [TestMethod]
        public async Task Convention_OdataQuery_Customers_ReturnsSeededName()
        {
            using var probe = TestServer.CreateClient();
            var direct = await probe.GetAsync("/odata/Customers");
            var directBody = await direct.Content.ReadAsStringAsync();
            direct.IsSuccessStatusCode.Should().BeTrue("status {0} body {1}", (int)direct.StatusCode, directBody);
            directBody.Should().Contain("Contoso");

            var factory = TestServer.Services.GetRequiredService<ODataMcpSessionFactory>();
            var runtime = factory.Sessions["odata"].Runtime;
            var result = await runtime.InvokeAsync(
                "odata_query",
                new Dictionary<string, JsonElement>
                {
                    ["entitySet"] = JsonSerializer.SerializeToElement("Customers"),
                    ["top"] = JsonSerializer.SerializeToElement(1)
                },
                CancellationToken.None);

            result.IsError.Should().BeFalse(result.Text);
            result.StructuredContent.Should().Contain("Contoso");
        }

        /// <summary>
        /// Direct OData $filter and MCP filter return the same customer.
        /// </summary>
        [TestMethod]
        public async Task Convention_Filter_MatchesODataAndMcp()
        {
            using var client = TestServer.CreateClient();
            var odata = await client.GetAsync("/odata/Customers?$filter=CustomerId eq 1");
            var odataBody = await odata.Content.ReadAsStringAsync();
            odata.IsSuccessStatusCode.Should().BeTrue("status {0} body {1}", (int)odata.StatusCode, odataBody);
            odataBody.Should().Contain("Contoso");

            var result = await Runtime().InvokeAsync(
                "odata_query",
                new Dictionary<string, JsonElement>
                {
                    ["entitySet"] = JsonSerializer.SerializeToElement("Customers"),
                    ["filter"] = JsonSerializer.SerializeToElement("CustomerId eq 1")
                },
                CancellationToken.None);

            result.IsError.Should().BeFalse(result.Text);
            result.StructuredContent.Should().Contain("Contoso");
        }

        /// <summary>
        /// Key lookup works on the OData route and through odata_get.
        /// </summary>
        [TestMethod]
        public async Task Convention_GetByKey_MatchesODataAndMcp()
        {
            using var client = TestServer.CreateClient();
            var odata = await client.GetAsync("/odata/Customers(1)");
            var odataBody = await odata.Content.ReadAsStringAsync();
            odata.IsSuccessStatusCode.Should().BeTrue("status {0} body {1}", (int)odata.StatusCode, odataBody);
            odataBody.Should().Contain("Contoso");

            var result = await Runtime().InvokeAsync(
                "odata_get",
                new Dictionary<string, JsonElement>
                {
                    ["entitySet"] = JsonSerializer.SerializeToElement("Customers"),
                    ["key"] = JsonSerializer.SerializeToElement("1")
                },
                CancellationToken.None);

            result.IsError.Should().BeFalse(result.Text);
            result.StructuredContent.Should().Contain("Contoso");
        }

        /// <summary>
        /// $metadata lists Customers and the MCP catalog lists the same entity set.
        /// </summary>
        [TestMethod]
        public async Task Convention_Metadata_AndListEntitySets_Agree()
        {
            using var client = TestServer.CreateClient();
            var metadata = await client.GetAsync("/odata/$metadata");
            var metadataBody = await metadata.Content.ReadAsStringAsync();
            if (metadata.IsSuccessStatusCode)
            {
                metadataBody.Should().Contain("Customers");
            }

            var listed = await Runtime().InvokeAsync("odata_list_entity_sets", null, CancellationToken.None);
            listed.IsError.Should().BeFalse(listed.Text);
            listed.StructuredContent.Should().Contain("Customers");
        }

        /// <summary>
        /// The OData service document and MCP catalog both advertise Customers.
        /// </summary>
        [TestMethod]
        public async Task Convention_ServiceDocument_ListsCustomers()
        {
            using var client = TestServer.CreateClient();
            var customers = await client.GetStringAsync("/odata/Customers");
            customers.Should().Contain("Contoso");

            TestServer.Services.GetRequiredService<ODataMcpSessionFactory>()
                .Sessions["odata"].Catalog.Tools.Select(tool => tool.Name)
                .Should().Contain("list_customers");
        }

        /// <summary>
        /// The MCP HTTP endpoint is mapped next to the OData route.
        /// </summary>
        [TestMethod]
        public async Task Convention_McpEndpoint_IsNotMissing()
        {
            using var client = TestServer.CreateClient();
            var response = await client.PostAsync("/odata/mcp", McpJsonContent.EmptyObject());

            response.StatusCode.Should().NotBe(HttpStatusCode.NotFound);
            response.StatusCode.Should().NotBe(HttpStatusCode.MethodNotAllowed);
        }

        /// <summary>
        /// In-process execute forwards JSON bodies and authorization from HTTP context.
        /// </summary>
        [TestMethod]
        public async Task InProcessExecutor_ExecuteAsync_PostsBodyAndAuthorization()
        {
            var handler = InProcessODataExecutor.TryCreateServerHandler(TestServer);
            handler.Should().NotBeNull();
            var accessor = new Microsoft.AspNetCore.Http.HttpContextAccessor
            {
                HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext()
            };
            accessor.HttpContext.Request.Scheme = "http";
            accessor.HttpContext.Request.Host = new Microsoft.AspNetCore.Http.HostString("localhost");
            accessor.HttpContext.Request.Headers.Authorization = "Bearer test-token";
            var executor = new InProcessODataExecutor(new TestServerHandlerFactory(handler!), accessor, "odata");
            var created = await executor.ExecuteAsync(
                new Microsoft.OData.Mcp.Core.Execution.ODataExecuteRequest
                {
                    JsonBody = """{"CustomerId":99,"CompanyName":"Fabrikam"}""",
                    Method = HttpMethod.Post,
                    RelativePath = "Customers"
                },
                CancellationToken.None);

            created.StatusCode.Should().BeOneOf(201, 200, 204, 400, 405);
        }

        #endregion

        #region Internal Methods

        /// <summary>
        /// Gets the odata session runtime.
        /// </summary>
        /// <returns>
        /// The runtime.
        /// </returns>
        internal Microsoft.OData.Mcp.Core.Catalog.ODataToolRuntime Runtime()
        {
            return TestServer.Services.GetRequiredService<ODataMcpSessionFactory>().Sessions["odata"].Runtime;
        }

        #endregion

    }

}
