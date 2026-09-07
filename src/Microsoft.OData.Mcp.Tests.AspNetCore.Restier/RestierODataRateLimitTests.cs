// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Net;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OData.Mcp.AspNetCore.Hosting;
using Microsoft.OData.Mcp.Core.Catalog;
using Microsoft.Restier.Breakdance;
using Microsoft.Restier.Core;
using Microsoft.Restier.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.OData.Mcp.Tests.AspNetCore.Restier
{

    /// <summary>
    /// Restier Customers, shop Products, and each prefix's MCP endpoint have separate rate-limit budgets.
    /// </summary>
    [TestClass]
    public class RestierODataRateLimitTests : RestierBreakdanceTestBase<McpCustomerApi>
    {

        #region Fields

        internal readonly string _customerDatabase = "RestierRateCustomers-" + Guid.NewGuid().ToString("N");

        internal readonly string _productDatabase = "RestierRateProducts-" + Guid.NewGuid().ToString("N");

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="RestierODataRateLimitTests"/> class using endpoint routing.
        /// </summary>
        public RestierODataRateLimitTests()
            : base(useEndpointRouting: true)
        {
            ApplicationBuilderAction = app => app.UseRateLimiter();
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
        /// Customers: 1, Products: 2, /odata/mcp: 1, /shop/mcp unlimited.
        /// </summary>
        [TestInitialize]
        public void Setup()
        {
            TestHostBuilder.ConfigureServices((_, services) =>
            {
                services.AddRateLimiter(options => RestierPartitionedLimiter.Apply(options, new RestierRateLimitBudget
                {
                    Customers = 1,
                    OdataMcp = 1,
                    Products = 2,
                    ShopMcp = 0
                }));
                services.AddODataMcp();
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
        /// Restier entity-set budgets are independent of each other and of MCP POST.
        /// </summary>
        [TestMethod]
        public async Task Combination_ODataBudgetsAreIndependent()
        {
            using var client = TestServer.CreateClient();

            (await client.GetAsync("odata/Customers")).StatusCode.Should().Be(HttpStatusCode.OK);
            (await client.GetAsync("odata/Customers")).StatusCode.Should().Be(HttpStatusCode.TooManyRequests);

            (await client.GetAsync("shop/Products")).StatusCode.Should().Be(HttpStatusCode.OK);
            (await client.GetAsync("shop/Products")).StatusCode.Should().Be(HttpStatusCode.OK);
            (await client.GetAsync("shop/Products")).StatusCode.Should().Be(HttpStatusCode.TooManyRequests);

            var odataMcp = await client.PostAsync("odata/mcp", RestierJsonContent.Json("{}"));
            odataMcp.StatusCode.Should().NotBe(HttpStatusCode.TooManyRequests);
            var shopMcp = await client.PostAsync("shop/mcp", RestierJsonContent.Json("{}"));
            shopMcp.StatusCode.Should().NotBe(HttpStatusCode.TooManyRequests);
            shopMcp.StatusCode.Should().NotBe(HttpStatusCode.NotFound);
        }

        /// <summary>
        /// /odata/mcp can 429 while /shop/mcp and Products remain available.
        /// </summary>
        [TestMethod]
        public async Task Combination_OdataMcp429_ShopMcpAndProductsStillOk()
        {
            using var client = TestServer.CreateClient();
            (await client.PostAsync("odata/mcp", RestierJsonContent.Json("{}"))).StatusCode.Should().NotBe(HttpStatusCode.TooManyRequests);
            (await client.PostAsync("odata/mcp", RestierJsonContent.Json("{}"))).StatusCode.Should().Be(HttpStatusCode.TooManyRequests);

            var shop = await client.PostAsync("shop/mcp", RestierJsonContent.Json("{}"));
            shop.StatusCode.Should().NotBe(HttpStatusCode.TooManyRequests);

            var products = await client.GetAsync("shop/Products");
            var body = await products.Content.ReadAsStringAsync();
            products.IsSuccessStatusCode.Should().BeTrue("status {0} body {1}", (int)products.StatusCode, body);
            body.Should().Contain("Widget");
        }

        /// <summary>
        /// MCP query of Customers spends the Customers budget; shop Products queries still succeed.
        /// </summary>
        [TestMethod]
        public async Task Combination_McpQueryCustomers429_ProductsQueryStillOk()
        {
            var customers = TestServer.Services.GetRequiredService<ODataMcpSessionFactory>().Sessions["odata"].Runtime;
            var products = TestServer.Services.GetRequiredService<ODataMcpSessionFactory>().Sessions["shop"].Runtime;

            var first = await QueryAsync(customers, "Customers");
            first.IsError.Should().BeFalse(first.Text);
            first.StructuredContent.Should().Contain("Contoso");

            var second = await QueryAsync(customers, "Customers");
            second.IsError.Should().BeTrue();
            second.Text.Should().Contain("429");

            var productQuery = await QueryAsync(products, "Products");
            productQuery.IsError.Should().BeFalse(productQuery.Text);
            productQuery.StructuredContent.Should().Contain("Widget");
        }

        /// <summary>
        /// Direct GET and MCP query share the Customers budget; shop MCP POST is a different budget.
        /// </summary>
        [TestMethod]
        public async Task Combination_GetThenMcpQuery_SharesCustomersBudget()
        {
            using var client = TestServer.CreateClient();
            var get = await client.GetAsync("odata/Customers");
            var getBody = await get.Content.ReadAsStringAsync();
            get.IsSuccessStatusCode.Should().BeTrue("status {0} body {1}", (int)get.StatusCode, getBody);

            var query = await QueryAsync(
                TestServer.Services.GetRequiredService<ODataMcpSessionFactory>().Sessions["odata"].Runtime,
                "Customers");
            query.IsError.Should().BeTrue();
            query.Text.Should().Contain("429");

            (await client.PostAsync("shop/mcp", RestierJsonContent.Json("{}"))).StatusCode.Should().NotBe(HttpStatusCode.TooManyRequests);
        }

        /// <summary>
        /// Products allow two queries; Customers still have their own remaining permit.
        /// </summary>
        [TestMethod]
        public async Task Combination_ProductsPermitTwo_CustomersUnaffected()
        {
            var factory = TestServer.Services.GetRequiredService<ODataMcpSessionFactory>();
            var products = factory.Sessions["shop"].Runtime;
            (await QueryAsync(products, "Products")).IsError.Should().BeFalse();
            (await QueryAsync(products, "Products")).IsError.Should().BeFalse();
            var third = await QueryAsync(products, "Products");
            third.IsError.Should().BeTrue();
            third.Text.Should().Contain("429");

            var customers = await QueryAsync(factory.Sessions["odata"].Runtime, "Customers");
            customers.IsError.Should().BeFalse(customers.Text);
        }

        #endregion

        #region Internal Methods

        /// <summary>
        /// Queries an entity set through <c>odata_query</c>.
        /// </summary>
        /// <param name="runtime">The runtime.</param>
        /// <param name="entitySet">The entity set.</param>
        /// <returns>
        /// The tool result.
        /// </returns>
        internal static async Task<ODataToolInvocationResult> QueryAsync(ODataToolRuntime runtime, string entitySet)
        {
            return await runtime.InvokeAsync(
                "odata_query",
                new Dictionary<string, JsonElement>
                {
                    ["entitySet"] = JsonSerializer.SerializeToElement(entitySet)
                },
                CancellationToken.None);
        }

        #endregion

    }

}
