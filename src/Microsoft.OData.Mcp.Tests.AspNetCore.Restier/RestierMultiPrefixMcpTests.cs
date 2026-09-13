// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OData.Mcp.AspNetCore.Hosting;
using Microsoft.OData.Mcp.Tests.AspNetCore.Restier.Scenarios;
using Microsoft.Restier.Breakdance;
using Microsoft.Restier.Core;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.OData.Mcp.Tests.AspNetCore.Restier
{

    /// <summary>
    /// Two Restier prefixes with MCP on both, plus include/exclude filters.
    /// </summary>
    [TestClass]
    public class RestierMultiPrefixMcpTests : RestierBreakdanceTestBase<McpCustomerApi>
    {

        #region Fields

        internal readonly string _customerDatabase = "RestierMultiCustomers-" + Guid.NewGuid().ToString("N");

        internal readonly string _productDatabase = "RestierMultiProducts-" + Guid.NewGuid().ToString("N");

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="RestierMultiPrefixMcpTests"/> class using endpoint routing.
        /// </summary>
        public RestierMultiPrefixMcpTests()
            : base(useEndpointRouting: true)
        {
            AddRestierAction = apiBuilder =>
            {
                apiBuilder.AddRestierApi<McpCustomerApi>(restierServices =>
                {
                    restierServices.AddEFCoreProviderServices<McpCustomerContext>((_, options) =>
                    {
                        options.UseInMemoryDatabase(_customerDatabase);
                    });
                    RestierTestSeed.EnsureCustomers(restierServices);
                });
                apiBuilder.AddRestierApi<McpProductApi>(restierServices =>
                {
                    restierServices.AddEFCoreProviderServices<McpProductContext>((_, options) =>
                    {
                        options.UseInMemoryDatabase(_productDatabase);
                    });
                    RestierTestSeed.EnsureProducts(restierServices);
                });
            };

            MapRestierAction = routeBuilder =>
            {
                routeBuilder.MapApiRoute<McpCustomerApi>("odata", "odata");
                routeBuilder.MapApiRoute<McpProductApi>("shop", "shop");
            };

            ApplicationBuilderLastAction = app =>
            {
                app.UseODataMcp();
            };
        }

        #endregion

        #region Test Lifecycle

        /// <summary>
        /// Enables MCP for every Restier prefix.
        /// </summary>
        [TestInitialize]
        public void Setup()
        {
            TestHostBuilder.ConfigureServices((_, services) =>
            {
                services.AddODataMcp();
            });

            TestSetup();
        }

        /// <summary>
        /// Tears down the Restier host.
        /// </summary>
        [TestCleanup]
        public void TearDown()
        {
            TestTearDown();
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Both Restier prefixes are queryable over OData.
        /// </summary>
        [TestMethod]
        public async Task MultiPrefix_ODataRoutes_ReturnTheirSeeds()
        {
            using var client = TestServer.CreateClient();
            var customers = await client.GetStringAsync("odata/Customers");
            var products = await client.GetStringAsync("shop/Products");

            customers.Should().Contain("Contoso");
            products.Should().Contain("Widget");
        }

        /// <summary>
        /// MCP sessions exist for both prefixes and do not mix catalogs.
        /// </summary>
        [TestMethod]
        public void MultiPrefix_McpSessions_AreIsolated()
        {
            var factory = TestServer.Services.GetRequiredService<ODataMcpSessionFactory>();

            factory.Sessions.Keys.Should().BeEquivalentTo("odata", "shop");
            factory.Sessions["odata"].Catalog.Tools.Select(tool => tool.Name).Should().Contain("list_customers");
            factory.Sessions["odata"].Catalog.Tools.Select(tool => tool.Name).Should().NotContain("list_products");
            factory.Sessions["shop"].Catalog.Tools.Select(tool => tool.Name).Should().Contain("list_products");
            factory.Sessions["shop"].Catalog.Tools.Select(tool => tool.Name).Should().NotContain("list_customers");
        }

        /// <summary>
        /// MCP query on each prefix returns that API's seed.
        /// </summary>
        [TestMethod]
        public async Task MultiPrefix_McpQuery_MatchesEachODataRoute()
        {
            var factory = TestServer.Services.GetRequiredService<ODataMcpSessionFactory>();
            var customers = await factory.Sessions["odata"].Runtime.InvokeAsync(
                "odata_query",
                new Dictionary<string, JsonElement>
                {
                    ["entitySet"] = JsonSerializer.SerializeToElement("Customers")
                },
                CancellationToken.None);
            var products = await factory.Sessions["shop"].Runtime.InvokeAsync(
                "odata_query",
                new Dictionary<string, JsonElement>
                {
                    ["entitySet"] = JsonSerializer.SerializeToElement("Products")
                },
                CancellationToken.None);

            customers.IsError.Should().BeFalse(customers.Text);
            customers.StructuredContent.Should().Contain("Contoso");
            products.IsError.Should().BeFalse(products.Text);
            products.StructuredContent.Should().Contain("Widget");
        }

        /// <summary>
        /// MCP HTTP endpoints exist on both prefixes.
        /// </summary>
        [TestMethod]
        public async Task MultiPrefix_McpEndpoints_ExistOnBothPrefixes()
        {
            using var client = TestServer.CreateClient();
            var odata = await client.PostAsync("odata/mcp", RestierJsonContent.Json("{}"));
            var shop = await client.PostAsync("shop/mcp", RestierJsonContent.Json("{}"));

            odata.StatusCode.Should().NotBe(HttpStatusCode.NotFound);
            shop.StatusCode.Should().NotBe(HttpStatusCode.NotFound);
        }

        /// <summary>
        /// Querying Products through the customer MCP catalog is an error.
        /// </summary>
        [TestMethod]
        public async Task MultiPrefix_CrossCatalogQuery_IsError()
        {
            var factory = TestServer.Services.GetRequiredService<ODataMcpSessionFactory>();
            var result = await factory.Sessions["odata"].Runtime.InvokeAsync(
                "odata_query",
                new Dictionary<string, JsonElement>
                {
                    ["entitySet"] = JsonSerializer.SerializeToElement("Products")
                },
                CancellationToken.None);

            result.IsError.Should().BeTrue();
        }

        #endregion

    }

}
