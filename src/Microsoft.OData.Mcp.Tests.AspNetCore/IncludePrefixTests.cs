// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System.Linq;
using System.Net;
using System.Threading.Tasks;
using CloudNimble.Breakdance.AspNetCore;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.OData;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.OData.Mcp.AspNetCore.Hosting;
using Microsoft.OData.Mcp.Tests.AspNetCore.Security;
using Microsoft.OData.Mcp.Tests.Shared.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.OData.Mcp.Tests.AspNetCore
{

    /// <summary>
    /// Host tests for version-neutral prefix opt-in.
    /// </summary>
    [TestClass]
    public class IncludePrefixTests : AspNetCoreBreakdanceTestBase
    {

        #region Test Lifecycle

        /// <summary>
        /// Builds a host that exposes only the odata route over MCP while both OData prefixes remain queryable.
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
                        options.AddRouteComponents("internal", TestModels.GetMinimalModel());
                    });

                services.AddODataMcp(options =>
                {
                    options.IncludePrefixes.Add("odata");
                });
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
        /// Tears down the test host.
        /// </summary>
        [TestCleanup]
        public void TearDown()
        {
            TestTearDown();
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Only the included prefix is registered as an MCP session.
        /// </summary>
        [TestMethod]
        public void IncludePrefixes_RegistersOnlyListedPrefix()
        {
            var factory = TestServer.Services.GetRequiredService<ODataMcpSessionFactory>();

            factory.Sessions.Keys.Should().Contain("odata");
            factory.Sessions.Keys.Should().NotContain("internal");
            factory.Sessions["odata"].Catalog.Tools.Select(tool => tool.Name).Should().Contain("odata_query");
        }

        /// <summary>
        /// The opted-in MCP endpoint exists and the hidden prefix has no MCP endpoint.
        /// </summary>
        [TestMethod]
        public async Task IncludePrefixes_McpEndpoint_OnlyOnIncludedPrefix()
        {
            using var client = TestServer.CreateClient();
            var enabled = await client.PostAsync("/odata/mcp", McpJsonContent.EmptyObject());
            var hidden = await client.PostAsync("/internal/mcp", McpJsonContent.EmptyObject());

            enabled.StatusCode.Should().NotBe(HttpStatusCode.NotFound);
            hidden.StatusCode.Should().BeOneOf(HttpStatusCode.NotFound, HttpStatusCode.MethodNotAllowed);
        }

        /// <summary>
        /// Both conventional OData prefixes still serve Customers while only one has MCP.
        /// </summary>
        [TestMethod]
        public async Task IncludePrefixes_ODataRoutes_RemainQueryable()
        {
            using var client = TestServer.CreateClient();
            var odata = await client.GetAsync("/odata/Customers");
            var internalCustomers = await client.GetAsync("/internal/Customers");
            var odataBody = await odata.Content.ReadAsStringAsync();
            var internalBody = await internalCustomers.Content.ReadAsStringAsync();

            odata.IsSuccessStatusCode.Should().BeTrue("status {0} body {1}", (int)odata.StatusCode, odataBody);
            internalCustomers.IsSuccessStatusCode.Should().BeTrue("status {0} body {1}", (int)internalCustomers.StatusCode, internalBody);
            odataBody.Should().Contain("Contoso");
            internalBody.Should().Contain("Contoso");
        }

        #endregion

    }

}
