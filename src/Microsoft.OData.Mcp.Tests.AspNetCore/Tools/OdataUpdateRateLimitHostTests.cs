// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System.Net;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OData.Mcp.AspNetCore.Hosting;
using Microsoft.OData.Mcp.Tests.AspNetCore.Fixtures;
using Microsoft.OData.Mcp.Tests.AspNetCore.RateLimit;
using Microsoft.OData.Mcp.Tests.Shared;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.OData.Mcp.Tests.AspNetCore.Tools
{

    /// <summary>
    /// Update tests against partitioned MCP and Customers rate limits.
    /// </summary>
    [TestClass]
    public class OdataUpdateRateLimitHostTests : ConventionRichHost
    {

        #region Public Methods

        /// <summary>
        /// A second PATCH of Customers is tool 429.
        /// </summary>
        [TestMethod]
        public async Task OdataUpdate_429RetryAfter()
        {
            var (runtime, _) = CreateCapturingRuntime();
            Authenticate();
            var first = await runtime.InvokeAsync(
                "odata_update",
                ToolArguments.Of("entitySet", "Customers", "key", "1", "body", """{"CompanyName":"RatePatch1"}"""),
                CancellationToken.None);
            first.IsError.Should().BeFalse(first.Text);

            var second = await runtime.InvokeAsync(
                "odata_update",
                ToolArguments.Of("entitySet", "Customers", "key", "1", "body", """{"CompanyName":"RatePatch2"}"""),
                CancellationToken.None);
            second.IsError.Should().BeTrue();
            second.Text.Should().Contain("429");
        }

        /// <summary>
        /// A second MCP POST is HTTP 429.
        /// </summary>
        [TestMethod]
        public async Task OdataUpdate_McpHttp429()
        {
            using var client = CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "test");
            using var first = await client.PostAsync("/odata/mcp", McpJsonRpc.Content(McpJsonRpc.InitializePayload()));
            first.StatusCode.Should().NotBe((HttpStatusCode)429);
            using var second = await client.PostAsync("/odata/mcp", McpJsonRpc.Content(McpJsonRpc.InitializePayload()));
            second.StatusCode.Should().Be((HttpStatusCode)429);
        }

        #endregion

        #region Internal Methods

        /// <inheritdoc />
        internal override void ConfigureApp(IApplicationBuilder app)
        {
            app.UseRouting();
            app.UseRateLimiter();
            app.UseEndpoints(endpoints => endpoints.MapControllers());
            app.UseODataMcp();
        }

        /// <inheritdoc />
        internal override void ConfigureServices(IServiceCollection services)
        {
            services.AddRateLimiter(options => ODataPartitionedLimiter.Apply(options, new RateLimitBudget
            {
                Customers = 1,
                Function = 1,
                Mcp = 1,
                Products = 2
            }));
            base.ConfigureServices(services);
        }

        #endregion

    }

}
