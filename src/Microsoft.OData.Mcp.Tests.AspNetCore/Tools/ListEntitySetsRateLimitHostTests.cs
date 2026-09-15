// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.OData;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OData.Mcp.AspNetCore.Hosting;
using Microsoft.OData.Mcp.Tests.AspNetCore.Fixtures;
using Microsoft.OData.Mcp.Tests.AspNetCore.RateLimit;
using Microsoft.OData.Mcp.Tests.AspNetCore.Security;
using Microsoft.OData.Mcp.Tests.Shared.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.OData.Mcp.Tests.AspNetCore.Tools
{

    /// <summary>
    /// MCP transport and OData set rate limits around <c>odata_list_entity_sets</c>.
    /// </summary>
    [TestClass]
    public class ListEntitySetsRateLimitHostTests : ConventionRichHost
    {

        #region Public Methods

        /// <summary>
        /// A second MCP POST is HTTP 429; the tool result is not produced.
        /// </summary>
        [TestMethod]
        public async Task ListEntitySets_McpTransportRateLimit_SecondPost429()
        {
            using var client = CreateClient();
            using var firstContent = McpJsonRpc.Content(
                """{"jsonrpc":"2.0","id":"1","method":"tools/call","params":{"name":"odata_list_entity_sets","arguments":{}}}""");
            using var first = await client.PostAsync("/odata/mcp", firstContent);
            using var secondContent = McpJsonRpc.Content(
                """{"jsonrpc":"2.0","id":"2","method":"tools/call","params":{"name":"odata_list_entity_sets","arguments":{}}}""");
            using var second = await client.PostAsync("/odata/mcp", secondContent);

            first.StatusCode.Should().NotBe(HttpStatusCode.NotFound);
            second.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        }

        /// <summary>
        /// Spending the Customers OData budget does not affect this catalog tool.
        /// </summary>
        [TestMethod]
        public async Task ListEntitySets_ODataSetRateLimit_DoesNotAffectThisTool()
        {
            using var client = CreateClient();
            (await client.GetAsync("/odata/Customers")).StatusCode.Should().Be(HttpStatusCode.OK);
            (await client.GetAsync("/odata/Customers")).StatusCode.Should().Be(HttpStatusCode.TooManyRequests);

            var result = await InvokeAsync("odata_list_entity_sets");
            result.IsError.Should().BeFalse(result.Text);
            OdataListEntitySetsHostTests.ReadEntitySetNames(result.StructuredContent).Should().Contain("Customers");
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
            services
                .AddControllers()
                .AddApplicationPart(typeof(CustomersController).Assembly)
                .AddOData(options =>
                {
                    options.EnableQueryFeatures();
                    options.AddRouteComponents("odata", TestModels.GetRateLimitModel());
                });
            services.AddODataMcp();
        }

        #endregion

    }

}
