// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using CloudNimble.Breakdance.AspNetCore;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.OData;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.OData.Mcp.AspNetCore.Execution;
using Microsoft.OData.Mcp.AspNetCore.Hosting;
using Microsoft.OData.Mcp.Core.Catalog;
using Microsoft.OData.Mcp.Tests.AspNetCore.Fixtures;
using Microsoft.OData.Mcp.Tests.AspNetCore.Paging;
using Microsoft.OData.Mcp.Tests.AspNetCore.RateLimit;
using Microsoft.OData.Mcp.Tests.Shared;
using Microsoft.OData.Mcp.Tests.Shared.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.OData.Mcp.Tests.AspNetCore.Tools
{


    /// <summary>
    /// Named list paging on client and server driven sets.
    /// </summary>
    [TestClass]
    public class NamedCrudPagingHostTests : AspNetCoreBreakdanceTestBase
    {

        #region Test Lifecycle

        /// <summary>
        /// Builds the paging host.
        /// </summary>
        [TestInitialize]
        public void Setup()
        {
            TestHostBuilder.ConfigureServices((_, services) =>
            {
                services
                    .AddControllers()
                    .AddApplicationPart(typeof(ClientCustomersController).Assembly)
                    .AddOData(options =>
                    {
                        options.EnableQueryFeatures(100);
                        options.AddRouteComponents("odata", TestModels.GetPagingModel());
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
        /// Named list without top returns all five client-driven rows.
        /// </summary>
        [TestMethod]
        public async Task ListClientCustomers_Paging_OmitTop_AllFive()
        {
            var runtime = TestServer.Services.GetRequiredService<ODataMcpSessionFactory>().Sessions["odata"].Runtime;
            var result = await runtime.InvokeAsync(
                "list_client_customers",
                ToolArguments.Of("orderby", "CustomerId"),
                CancellationToken.None);

            result.IsError.Should().BeFalse(result.Text);
            ODataFeedReader.ReadCompanyNames(result.StructuredContent!).Should().Equal(PagingCustomerSeed.CompanyNames);
        }

        /// <summary>
        /// Named list of server-driven set returns a page and nextLink without injected top.
        /// </summary>
        [TestMethod]
        public async Task ListServerCustomers_Paging_NextLinkWithoutInjectedTop()
        {
            var (runtime, capture) = PagingCapture();
            var result = await runtime.InvokeAsync(
                "list_server_customers",
                ToolArguments.Of("orderby", "CustomerId"),
                CancellationToken.None);

            result.IsError.Should().BeFalse(result.Text);
            ODataFeedReader.ReadValueCount(result.StructuredContent!).Should().Be(2);
            ODataFeedReader.ReadNextLink(result.StructuredContent!).Should().NotBeNullOrWhiteSpace();
            capture.Last!.QueryOptions.Should().NotContainKey("top");
        }

        /// <summary>
        /// Named list does not add a default top.
        /// </summary>
        [TestMethod]
        public async Task ListCustomers_DoesNotAddDefaultTop()
        {
            var (runtime, capture) = PagingCapture();
            await runtime.InvokeAsync("list_client_customers", null, CancellationToken.None);

            capture.Last!.QueryOptions.Should().NotContainKey("top");
        }

        #endregion

        #region Internal Methods

        /// <summary>
        /// Creates an HTTP client for direct OData calls against the host under test.
        /// </summary>
        /// <returns>
        /// The client.
        /// </returns>
        internal HttpClient CreateClient()
        {
            return TestServer.CreateClient();
        }

        /// <summary>
        /// Creates a capturing runtime against the paging TestServer.
        /// </summary>
        /// <returns>
        /// Runtime and capture.
        /// </returns>
        internal (ODataToolRuntime Runtime, CapturingODataExecutor Capture) PagingCapture()
        {
            var session = TestServer.Services.GetRequiredService<ODataMcpSessionFactory>().Sessions["odata"];
            var accessor = TestServer.Services.GetRequiredService<McpHttpContextAccessor>();
            if (accessor.HttpContext is null)
            {
                accessor.HttpContext = new DefaultHttpContext
                {
                    RequestServices = TestServer.Services
                };
            }

            var inner = new InProcessODataExecutor(
                TestServer.Services.GetRequiredService<ODataMcpPipeline>(),
                accessor,
                TestServer.Services.GetRequiredService<IServiceScopeFactory>(),
                "odata");
            var capture = new CapturingODataExecutor(inner);

            return (new ODataToolRuntime(session.Catalog, capture), capture);
        }

        #endregion

    }

}
