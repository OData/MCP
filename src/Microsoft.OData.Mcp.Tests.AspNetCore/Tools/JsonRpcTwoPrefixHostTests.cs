// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Reflection;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CloudNimble.Breakdance.AspNetCore;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.OData;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OData.Mcp.AspNetCore.Hosting;
using Microsoft.OData.Mcp.Tests.AspNetCore.Fixtures;
using Microsoft.OData.Mcp.Tests.AspNetCore.RateLimit;
using Microsoft.OData.Mcp.Tests.AspNetCore.Security;
using Microsoft.OData.Mcp.Tests.Shared;
using Microsoft.OData.Mcp.Tests.Shared.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.OData.Mcp.Tests.AspNetCore.Tools
{


    /// <summary>
    /// Two-prefix isolation and include/exclude MCP mapping.
    /// </summary>
    [TestClass]
    public class JsonRpcTwoPrefixHostTests : AspNetCoreBreakdanceTestBase
    {

        #region Test Lifecycle

        /// <summary>
        /// Builds odata (Customers) and shop (Products) prefixes.
        /// </summary>
        [TestInitialize]
        public void Setup()
        {
            TestHostBuilder.ConfigureServices((_, services) =>
            {
                services.AddSingleton<CustomerStore>();
                services
                    .AddControllers()
                    .AddApplicationPart(typeof(CustomersController).Assembly)
                    .AddOData(options =>
                    {
                        options.EnableQueryFeatures();
                        options.AddRouteComponents("odata", TestModels.GetSimpleModel());
                        options.AddRouteComponents("shop", TestModels.GetNoAuthModel());
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
        /// AddODataMcp maps every discovered prefix.
        /// </summary>
        [TestMethod]
        public void Prefix_AddODataMcp_AllDiscoveredPrefixesGetMcp()
        {
            var factory = TestServer.Services.GetRequiredService<ODataMcpSessionFactory>();
            factory.Sessions.Keys.Should().Contain(["odata", "shop"]);
        }

        /// <summary>
        /// odata catalog cannot see shop-only Products as a named exclusive set... simple model has Products too.
        /// Shop catalog is Products-only.
        /// </summary>
        [TestMethod]
        public async Task Iso_OdataQueryCustomersOnOdataPrefix_CannotSeeShopProductsInCatalog()
        {
            var factory = TestServer.Services.GetRequiredService<ODataMcpSessionFactory>();
            factory.Sessions["shop"].Catalog.Tools.Select(tool => tool.Name).Should().Contain("list_products");
            factory.Sessions["shop"].Catalog.Tools.Select(tool => tool.Name).Should().NotContain("list_customers");
            var result = await factory.Sessions["odata"].Runtime.InvokeAsync(
                "odata_query",
                ToolArguments.Of("entitySet", "Customers"),
                CancellationToken.None);
            result.StructuredContent.Should().Contain("Contoso");
        }

        /// <summary>
        /// Shop query Products 200; shop query Customers 404.
        /// </summary>
        [TestMethod]
        public async Task Iso_ShopQueryProducts_200_ShopQueryCustomers_404()
        {
            using var client = TestServer.CreateClient();
            var products = await client.GetAsync("/shop/Products");
            products.IsSuccessStatusCode.Should().BeTrue();
            var customers = await TestServer.Services.GetRequiredService<ODataMcpSessionFactory>().Sessions["shop"].Runtime.InvokeAsync(
                "odata_query",
                ToolArguments.Of("entitySet", "Customers"),
                CancellationToken.None);
            customers.IsError.Should().BeTrue();
        }

        /// <summary>
        /// Resource URIs use the own prefix.
        /// </summary>
        [TestMethod]
        public void Iso_ResourcesUrisUseOwnPrefix()
        {
            var factory = TestServer.Services.GetRequiredService<ODataMcpSessionFactory>();
            factory.Sessions["odata"].Catalog.Resources.Should().OnlyContain(item => item.Uri.StartsWith("odata://odata/", StringComparison.Ordinal));
            factory.Sessions["shop"].Catalog.Resources.Should().OnlyContain(item => item.Uri.StartsWith("odata://shop/", StringComparison.Ordinal));
        }

        /// <summary>
        /// Completions on odata include Customers.
        /// </summary>
        [TestMethod]
        public void Iso_Completions_OdataPrefixDoesNotCompleteShopOnlySets()
        {
            var factory = TestServer.Services.GetRequiredService<ODataMcpSessionFactory>();
            factory.Sessions["shop"].Catalog.CompleteEntitySetNames(string.Empty).Should().Equal("Products");
            factory.Sessions["odata"].Catalog.CompleteEntitySetNames(string.Empty).Should().Contain("Customers");
        }

        /// <summary>
        /// Named tools differ per session.
        /// </summary>
        [TestMethod]
        public void Iso_NamedToolsSameNameDifferentSessions()
        {
            var factory = TestServer.Services.GetRequiredService<ODataMcpSessionFactory>();
            factory.Sessions["shop"].Catalog.Tools.Select(tool => tool.Name).Should().Contain("list_products");
            factory.Sessions["shop"].Catalog.Tools.Select(tool => tool.Name).Should().NotContain("list_customers");
        }

        /// <summary>
        /// JSON-RPC on included prefix.
        /// </summary>
        [TestMethod]
        public async Task Prefix_JsonRpcOnIncluded_ToolsCallQuery()
        {
            using var client = TestServer.CreateClient();
            using var response = await McpJsonRpc.CallToolAsync(client, "/shop/mcp", "odata_query", """{"entitySet":"Products"}""");
            var body = await McpJsonRpc.ReadBodyAsync(response);
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            body.Should().NotContain("\"isError\":true");
        }

        /// <summary>
        /// <c>UseODataMcp</c> maps MCP; app code does not call SDK <c>MapMcp</c>.
        /// </summary>
        [TestMethod]
        public async Task Iso_UseODataMcp_MapsPrefixesWithoutAppCallingMapMcp()
        {
            using var client = TestServer.CreateClient();
            using var odata = await McpJsonRpc.CallToolAsync(client, "/odata/mcp", "odata_list_entity_sets", "{}");
            using var shop = await McpJsonRpc.CallToolAsync(client, "/shop/mcp", "odata_list_entity_sets", "{}");
            odata.StatusCode.Should().Be(HttpStatusCode.OK);
            shop.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        /// <summary>
        /// tools/list differs per prefix.
        /// </summary>
        [TestMethod]
        public void ToolsList_TwoPrefixes_DifferentNamedSets()
        {
            var factory = TestServer.Services.GetRequiredService<ODataMcpSessionFactory>();
            factory.Sessions["odata"].Catalog.Tools.Select(tool => tool.Name).Should().Contain("list_customers");
            factory.Sessions["shop"].Catalog.Tools.Select(tool => tool.Name).Should().Contain("list_products");
        }

        #endregion

    }

}
