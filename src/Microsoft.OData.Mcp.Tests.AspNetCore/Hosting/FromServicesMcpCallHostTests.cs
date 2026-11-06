// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Xml.Linq;
using CloudNimble.Breakdance.AspNetCore;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.OData;
using Microsoft.AspNetCore.OData.Routing.Controllers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.OData.Mcp.AspNetCore.Hosting;
using Microsoft.OData.Mcp.Tests.AspNetCore.Fixtures;
using Microsoft.OData.ModelBuilder;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.OData.Mcp.Tests.AspNetCore.Hosting
{

    /// <summary>
    /// <c>[FromServices]</c> <see cref="RequestContext{TParams}"/> on an OData action:
    /// CSDL must not grow a <c>call</c> parameter, direct OData stays null, MCP injects the call.
    /// </summary>
    [TestClass]
    public class FromServicesMcpCallHostTests : AspNetCoreBreakdanceTestBase
    {

        #region Test Lifecycle

        /// <summary>
        /// Builds a convention host with an unbound <c>ShipFromServices</c> action.
        /// </summary>
        [TestInitialize]
        public void Setup()
        {
            TestHostBuilder.ConfigureServices((_, services) =>
            {
                var builder = new ODataConventionModelBuilder
                {
                    Namespace = "Probe"
                };
                builder.Action("ShipFromServices")
                    .Returns<string>()
                    .Parameter<string>("trackingNumber");

                services
                    .AddControllers()
                    .AddApplicationPart(typeof(MetadataController).Assembly)
                    .AddApplicationPart(typeof(ShipFromServicesController).Assembly)
                    .AddOData(options =>
                    {
                        options.EnableQueryFeatures();
                        options.AddRouteComponents("odata", builder.GetEdmModel());
                    });
                services.AddODataMcp();
            });
            AddMinimalMvc();
            TestHostBuilder.ConfigureWebHost(web =>
            {
                web.Configure(app =>
                {
                    app.UseRouting();
                    app.UseEndpoints(endpoints => endpoints.MapControllers());
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
        /// Direct <c>POST /odata/ShipFromServices</c> does not inject a call.
        /// </summary>
        [TestMethod]
        public async Task DirectODataPost_CallIsNull()
        {
            using var client = TestServer.CreateClient();
            using var content = new StringContent("""{"trackingNumber":"1Z-direct"}""", Encoding.UTF8, "application/json");
            using var response = await client.PostAsync("/odata/ShipFromServices", content);
            var body = await response.Content.ReadAsStringAsync();

            response.IsSuccessStatusCode.Should().BeTrue("status {0} body {1}", (int)response.StatusCode, body);
            using var document = JsonDocument.Parse(body);
            var root = document.RootElement.TryGetProperty("value", out var value) ? value : document.RootElement;
            root.GetProperty("injected").GetBoolean().Should().BeFalse(body);
            root.GetProperty("trackingNumber").GetString().Should().Be("1Z-direct");
        }

        /// <summary>
        /// CSDL for <c>ShipFromServices</c> has only <c>trackingNumber</c>. The
        /// <c>[FromServices]</c> parameter is not an EDM parameter.
        /// </summary>
        [TestMethod]
        public async Task HttpMetadata_ShipAction_HasOnlyTrackingNumberParameter()
        {
            using var client = TestServer.CreateClient();
            using var response = await client.GetAsync("/odata/$metadata");
            var xml = await response.Content.ReadAsStringAsync();

            response.IsSuccessStatusCode.Should().BeTrue("status {0} body {1}", (int)response.StatusCode, xml);

            var document = XDocument.Parse(xml);
            XNamespace edm = "http://docs.oasis-open.org/odata/ns/edm";
            var action = document.Descendants(edm + "Action")
                .Single(element => element.Attribute("Name")?.Value == "ShipFromServices");
            var names = action.Elements(edm + "Parameter")
                .Select(element => element.Attribute("Name")?.Value)
                .ToList();

            names.Should().Equal("trackingNumber");
            xml.Should().NotContain("RequestContext");
            xml.Should().NotContain("CallToolRequestParams");
        }

        /// <summary>
        /// JSON-RPC <c>tools/call action_shipfromservices</c> injects the live call into
        /// <c>[FromServices]</c>.
        /// </summary>
        [TestMethod]
        public async Task McpToolsCall_CallIsInjected()
        {
            using var client = TestServer.CreateClient();
            using var response = await McpJsonRpc.CallToolAsync(
                client,
                "/odata/mcp",
                "action_shipfromservices",
                """{"trackingNumber":"1Z-mcp"}""");
            var body = await McpJsonRpc.ReadBodyAsync(response);

            response.IsSuccessStatusCode.Should().BeTrue(body);
            body.Should().Contain("\"injected\":true");
            body.Should().Contain("action_shipfromservices");
            body.Should().Contain("1Z-mcp");
        }

        #endregion

    }

}
