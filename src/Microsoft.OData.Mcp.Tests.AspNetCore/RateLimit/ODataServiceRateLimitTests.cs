// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Net;
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
using Microsoft.OData.Mcp.AspNetCore.Hosting;
using Microsoft.OData.Mcp.Core.Catalog;
using Microsoft.OData.Mcp.Core.Execution;
using Microsoft.OData.Mcp.Tests.AspNetCore.Security;
using Microsoft.OData.Mcp.Tests.Shared.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.OData.Mcp.Tests.AspNetCore.RateLimit
{

    /// <summary>
    /// Independent rate limits on MCP transport, Customers, Products, and MostValuable, exercised in combination.
    /// </summary>
    [TestClass]
    public class ODataServiceRateLimitTests : AspNetCoreBreakdanceTestBase
    {

        #region Test Lifecycle

        /// <summary>
        /// Customers: 1, Products: 2, MostValuable: 1, /odata/mcp: 1. Other paths unlimited.
        /// </summary>
        [TestInitialize]
        public void Setup()
        {
            TestHostBuilder.ConfigureServices((_, services) =>
            {
                services.AddRateLimiter(options => ODataPartitionedLimiter.Apply(options, new RateLimitBudget
                {
                    Customers = 1,
                    Function = 1,
                    Mcp = 1,
                    Products = 2
                }));
                services
                    .AddControllers()
                    .AddApplicationPart(typeof(CustomersController).Assembly)
                    .AddOData(options =>
                    {
                        options.EnableQueryFeatures();
                        options.AddRouteComponents("odata", TestModels.GetRateLimitModel());
                    });
                services.AddODataMcp();
            });
            AddMinimalMvc();
            TestHostBuilder.ConfigureWebHost(web =>
            {
                web.Configure(app =>
                {
                    app.UseRouting();
                    app.UseRateLimiter();
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
        /// Each OData surface has its own budget: Customers 1, Products 2, function 1, independently of MCP POST.
        /// </summary>
        [TestMethod]
        public async Task Combination_ODataBudgetsAreIndependent()
        {
            using var client = TestServer.CreateClient();

            (await client.GetAsync("/odata/Customers")).StatusCode.Should().Be(HttpStatusCode.OK);
            (await client.GetAsync("/odata/Customers")).StatusCode.Should().Be(HttpStatusCode.TooManyRequests);

            (await client.GetAsync("/odata/Products")).StatusCode.Should().Be(HttpStatusCode.OK);
            (await client.GetAsync("/odata/Products")).StatusCode.Should().Be(HttpStatusCode.OK);
            (await client.GetAsync("/odata/Products")).StatusCode.Should().Be(HttpStatusCode.TooManyRequests);

            (await client.GetAsync("/odata/MostValuable")).StatusCode.Should().Be(HttpStatusCode.OK);
            (await client.GetAsync("/odata/MostValuable")).StatusCode.Should().Be(HttpStatusCode.TooManyRequests);

            var mcp = await client.PostAsync("/odata/mcp", McpJsonContent.EmptyObject());
            mcp.StatusCode.Should().NotBe(HttpStatusCode.TooManyRequests);
            mcp.StatusCode.Should().NotBe(HttpStatusCode.NotFound);
        }

        /// <summary>
        /// Exhausting Customers does not spend Products or the function.
        /// </summary>
        [TestMethod]
        public async Task Combination_Customers429_ProductsAndFunctionStillOk()
        {
            using var client = TestServer.CreateClient();
            (await client.GetAsync("/odata/Customers")).StatusCode.Should().Be(HttpStatusCode.OK);
            (await client.GetAsync("/odata/Customers")).StatusCode.Should().Be(HttpStatusCode.TooManyRequests);

            var products = await client.GetAsync("/odata/Products");
            var productBody = await products.Content.ReadAsStringAsync();
            products.IsSuccessStatusCode.Should().BeTrue("status {0} body {1}", (int)products.StatusCode, productBody);
            productBody.Should().Contain("Widget");

            var function = await client.GetAsync("/odata/MostValuable");
            var functionBody = await function.Content.ReadAsStringAsync();
            function.IsSuccessStatusCode.Should().BeTrue("status {0} body {1}", (int)function.StatusCode, functionBody);
            functionBody.Should().Contain("42");
        }

        /// <summary>
        /// Exhausting the function leaves entity sets available.
        /// </summary>
        [TestMethod]
        public async Task Combination_Function429_CustomersAndProductsStillOk()
        {
            using var client = TestServer.CreateClient();
            (await client.GetAsync("/odata/MostValuable")).StatusCode.Should().Be(HttpStatusCode.OK);
            (await client.GetAsync("/odata/MostValuable")).StatusCode.Should().Be(HttpStatusCode.TooManyRequests);

            (await client.GetAsync("/odata/Customers")).StatusCode.Should().Be(HttpStatusCode.OK);
            (await client.GetAsync("/odata/Products")).StatusCode.Should().Be(HttpStatusCode.OK);
        }

        /// <summary>
        /// Exhausting MCP POST does not block OData GET or in-process queries.
        /// </summary>
        [TestMethod]
        public async Task Combination_McpTransport429_ODataStillOk()
        {
            using var client = TestServer.CreateClient();
            (await client.PostAsync("/odata/mcp", McpJsonContent.EmptyObject())).StatusCode.Should().NotBe(HttpStatusCode.TooManyRequests);
            (await client.PostAsync("/odata/mcp", McpJsonContent.EmptyObject())).StatusCode.Should().Be(HttpStatusCode.TooManyRequests);

            var customers = await client.GetAsync("/odata/Customers");
            var body = await customers.Content.ReadAsStringAsync();
            customers.IsSuccessStatusCode.Should().BeTrue("status {0} body {1}", (int)customers.StatusCode, body);
            body.Should().Contain("Contoso");

            var products = await QueryAsync(InProcessRuntime(), "odata_query", "Products");
            products.IsError.Should().BeFalse(products.Text);
            products.StructuredContent.Should().Contain("Widget");
        }

        /// <summary>
        /// In-process MCP query uses the Customers budget; Products queries still succeed.
        /// </summary>
        [TestMethod]
        public async Task Combination_InProcessQueryCustomers429_ProductsAndCallStillOk()
        {
            var runtime = InProcessRuntime();
            var first = await QueryAsync(runtime, "odata_query", "Customers");
            first.IsError.Should().BeFalse(first.Text);
            first.StructuredContent.Should().Contain("Contoso");

            var second = await QueryAsync(runtime, "odata_query", "Customers");
            second.IsError.Should().BeTrue();
            second.Text.Should().Contain("429");

            var products = await QueryAsync(runtime, "odata_query", "Products");
            products.IsError.Should().BeFalse(products.Text);
            products.StructuredContent.Should().Contain("Widget");

            var call = await CallMostValuableAsync(runtime);
            call.IsError.Should().BeFalse(call.Text);
            call.StructuredContent.Should().Contain("42");

            using var client = TestServer.CreateClient();
            (await client.GetAsync("/odata/Customers")).StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
            (await client.GetAsync("/odata/Products")).StatusCode.Should().Be(HttpStatusCode.OK);
        }

        /// <summary>
        /// Remote (Tools) executor sees the same per-set budgets as in-process MCP.
        /// </summary>
        [TestMethod]
        public async Task Combination_RemoteQueryCustomers429_ProductsStillOk()
        {
            var runtime = RemoteRuntime();
            var first = await QueryAsync(runtime, "odata_query", "Customers");
            first.IsError.Should().BeFalse(first.Text);

            var second = await QueryAsync(runtime, "odata_query", "Customers");
            second.IsError.Should().BeTrue();
            second.Text.Should().Contain("429");

            var products = await QueryAsync(runtime, "odata_query", "Products");
            products.IsError.Should().BeFalse(products.Text);
            products.StructuredContent.Should().Contain("Widget");
        }

        /// <summary>
        /// MCP call of MostValuable spends the function budget, not Customers.
        /// </summary>
        [TestMethod]
        public async Task Combination_InProcessCall429_QueryCustomersStillOk()
        {
            var runtime = InProcessRuntime();
            var first = await CallMostValuableAsync(runtime);
            first.IsError.Should().BeFalse(first.Text);

            var second = await CallMostValuableAsync(runtime);
            second.IsError.Should().BeTrue();
            second.Text.Should().Contain("429");

            var customers = await QueryAsync(runtime, "odata_query", "Customers");
            customers.IsError.Should().BeFalse(customers.Text);
            customers.StructuredContent.Should().Contain("Contoso");
        }

        /// <summary>
        /// Direct GET spends the Customers budget; MCP query then 429 while MCP POST still works.
        /// </summary>
        [TestMethod]
        public async Task Combination_GetThenMcpQuery_SharesCustomersBudget_McpPostSeparate()
        {
            using var client = TestServer.CreateClient();
            var get = await client.GetAsync("/odata/Customers");
            var getBody = await get.Content.ReadAsStringAsync();
            get.IsSuccessStatusCode.Should().BeTrue("status {0} body {1}", (int)get.StatusCode, getBody);

            var query = await QueryAsync(InProcessRuntime(), "odata_query", "Customers");
            query.IsError.Should().BeTrue();
            query.Text.Should().Contain("429");

            var mcp = await client.PostAsync("/odata/mcp", McpJsonContent.EmptyObject());
            mcp.StatusCode.Should().NotBe(HttpStatusCode.TooManyRequests);
        }

        /// <summary>
        /// Products allow two MCP queries before 429; the second query is still success.
        /// </summary>
        [TestMethod]
        public async Task Combination_ProductsPermitTwo_ThirdMcpQueryIs429()
        {
            var runtime = InProcessRuntime();
            (await QueryAsync(runtime, "odata_query", "Products")).IsError.Should().BeFalse();
            (await QueryAsync(runtime, "odata_query", "Products")).IsError.Should().BeFalse();
            var third = await QueryAsync(runtime, "odata_query", "Products");
            third.IsError.Should().BeTrue();
            third.Text.Should().Contain("429");

            (await QueryAsync(runtime, "odata_query", "Customers")).IsError.Should().BeFalse();
        }

        #endregion

        #region Internal Methods

        /// <summary>
        /// Invokes <c>odata_call</c> for MostValuable.
        /// </summary>
        /// <param name="runtime">The runtime.</param>
        /// <returns>
        /// The tool result.
        /// </returns>
        internal static async Task<ODataToolInvocationResult> CallMostValuableAsync(ODataToolRuntime runtime)
        {
            return await runtime.InvokeAsync(
                "odata_call",
                new Dictionary<string, JsonElement>
                {
                    ["name"] = JsonSerializer.SerializeToElement("MostValuable")
                },
                CancellationToken.None);
        }

        /// <summary>
        /// Gets the in-process MCP runtime.
        /// </summary>
        /// <returns>
        /// The runtime.
        /// </returns>
        internal ODataToolRuntime InProcessRuntime()
        {
            return TestServer.Services.GetRequiredService<ODataMcpSessionFactory>().Sessions["odata"].Runtime;
        }

        /// <summary>
        /// Queries an entity set through <c>odata_query</c>.
        /// </summary>
        /// <param name="runtime">The runtime.</param>
        /// <param name="tool">The tool name.</param>
        /// <param name="entitySet">The entity set.</param>
        /// <returns>
        /// The tool result.
        /// </returns>
        internal static async Task<ODataToolInvocationResult> QueryAsync(ODataToolRuntime runtime, string tool, string entitySet)
        {
            return await runtime.InvokeAsync(
                tool,
                new Dictionary<string, JsonElement>
                {
                    ["entitySet"] = JsonSerializer.SerializeToElement(entitySet)
                },
                CancellationToken.None);
        }

        /// <summary>
        /// Builds a Tools-style remote runtime against this TestServer.
        /// </summary>
        /// <returns>
        /// The runtime.
        /// </returns>
        internal ODataToolRuntime RemoteRuntime()
        {
            var handler = TestServer.CreateHandler();
            var catalog = TestServer.Services.GetRequiredService<ODataMcpSessionFactory>().Sessions["odata"].Catalog;

            return new ODataToolRuntime(catalog, new RemoteODataExecutor(new LocalhostTestServerFactory(handler!, new Uri("http://localhost/odata/"))));
        }

        #endregion

    }

}
