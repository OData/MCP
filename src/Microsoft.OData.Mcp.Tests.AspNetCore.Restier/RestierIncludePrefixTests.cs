// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OData.Mcp.AspNetCore.Hosting;
using Microsoft.Restier.Breakdance;
using Microsoft.Restier.Core;
using Microsoft.Restier.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.OData.Mcp.Tests.AspNetCore.Restier
{

    /// <summary>
    /// IncludePrefixes on a Restier host: OData stays on both prefixes, MCP only on odata.
    /// </summary>
    [TestClass]
    public class RestierIncludePrefixTests : RestierBreakdanceTestBase<McpCustomerApi>
    {

        #region Fields

        internal readonly string _customerDatabase = "RestierIncludeCustomers-" + Guid.NewGuid().ToString("N");

        internal readonly string _productDatabase = "RestierIncludeProducts-" + Guid.NewGuid().ToString("N");

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="RestierIncludePrefixTests"/> class using endpoint routing.
        /// </summary>
        public RestierIncludePrefixTests()
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
        }

        #endregion

        #region Test Lifecycle

        /// <summary>
        /// Enables MCP only for the odata prefix.
        /// </summary>
        [TestInitialize]
        public void Setup()
        {
            TestHostBuilder.ConfigureServices((_, services) =>
            {
                services.AddODataMcp(options =>
                {
                    options.IncludePrefixes.Add("odata");
                });
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
        /// Only the included prefix has an MCP session.
        /// </summary>
        [TestMethod]
        public void IncludePrefixes_OnlyOdataSession()
        {
            var factory = TestServer.Services.GetRequiredService<ODataMcpSessionFactory>();

            factory.Sessions.Keys.Should().Equal("odata");
            factory.Sessions["odata"].Catalog.Tools.Select(tool => tool.Name).Should().Contain("list_customers");
        }

        /// <summary>
        /// Both Restier OData routes still work.
        /// </summary>
        [TestMethod]
        public async Task IncludePrefixes_ODataRoutes_RemainQueryable()
        {
            using var client = TestServer.CreateClient();
            var customers = await client.GetAsync("odata/Customers");
            var products = await client.GetAsync("shop/Products");
            var customerBody = await customers.Content.ReadAsStringAsync();
            var productBody = await products.Content.ReadAsStringAsync();

            customers.IsSuccessStatusCode.Should().BeTrue("status {0} body {1}", (int)customers.StatusCode, customerBody);
            products.IsSuccessStatusCode.Should().BeTrue("status {0} body {1}", (int)products.StatusCode, productBody);
            customerBody.Should().Contain("Contoso");
            productBody.Should().Contain("Widget");
        }

        /// <summary>
        /// MCP is mapped on odata and not on shop.
        /// </summary>
        [TestMethod]
        public async Task IncludePrefixes_McpOnlyOnOdata()
        {
            using var client = TestServer.CreateClient();
            var enabled = await client.PostAsync("odata/mcp", RestierJsonContent.Json("{}"));
            var hidden = await client.PostAsync("shop/mcp", RestierJsonContent.Json("{}"));

            enabled.StatusCode.Should().NotBe(HttpStatusCode.NotFound);
            hidden.StatusCode.Should().BeOneOf(HttpStatusCode.NotFound, HttpStatusCode.MethodNotAllowed);
        }

        #endregion

    }

}
