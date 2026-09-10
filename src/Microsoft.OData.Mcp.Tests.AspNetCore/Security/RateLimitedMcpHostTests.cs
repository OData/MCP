// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using System.Net;
using System.Threading.Tasks;
using CloudNimble.Breakdance.AspNetCore;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.OData;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.OData.Mcp.Tests.Shared.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.OData.Mcp.Tests.AspNetCore.Security
{

    /// <summary>
    /// Rate limiting on the MCP HTTP transport (<c>/odata/mcp</c>), not on the OData service.
    /// OData-service 429 is covered by <c>ODataServiceRateLimitTests</c>.
    /// </summary>
    [TestClass]
    public class RateLimitedMcpHostTests : AspNetCoreBreakdanceTestBase
    {

        #region Test Lifecycle

        /// <summary>
        /// Builds a rate-limited MCP host that permits a single request.
        /// </summary>
        [TestInitialize]
        public void Setup()
        {
            TestHostBuilder.ConfigureServices((_, services) =>
            {
                services.AddRateLimiter(options =>
                {
                    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
                    options.AddFixedWindowLimiter("mcp", limiter =>
                    {
                        limiter.PermitLimit = 1;
                        limiter.QueueLimit = 0;
                        limiter.Window = TimeSpan.FromHours(1);
                    });
                });
                services
                    .AddControllers()
                    .AddOData(options =>
                    {
                        options.AddRouteComponents("odata", TestModels.GetMinimalModel());
                    });
                services.AddODataMcp(options =>
                {
                    options.RateLimitingPolicyName = "mcp";
                });
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
        /// A second MCP request is rejected by the host rate limiter.
        /// </summary>
        [TestMethod]
        public async Task Mcp_SecondRequest_IsTooManyRequests()
        {
            using var client = TestServer.CreateClient();
            var first = await client.PostAsync("/odata/mcp", McpJsonContent.EmptyObject());
            var second = await client.PostAsync("/odata/mcp", McpJsonContent.EmptyObject());

            first.StatusCode.Should().NotBe(HttpStatusCode.NotFound);
            second.StatusCode.Should().Be((HttpStatusCode)429);
        }

        #endregion

    }

}
