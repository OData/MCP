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
    /// Combinatorial MCP vs OData 429 tools/call variants. HTTP twins live in <c>ODataServiceRateLimitTests</c>.
    /// </summary>
    [TestClass]
    public class JsonRpcRateLimitHostTests : ConventionRichHost
    {

        #region Public Methods

        /// <summary>
        /// JSON-RPC MCP permit 1: second POST is HTTP 429 and inner OData is not invoked.
        /// </summary>
        [TestMethod]
        public async Task Rate_JsonRpcMcpPermit1_SecondToolsCallHttp429_InnerODataNotInvoked()
        {
            using var client = TestServer.CreateClient();
            using var first = await client.PostAsync("/odata/mcp", McpJsonRpc.Content(McpJsonRpc.InitializePayload()));
            first.StatusCode.Should().NotBe((HttpStatusCode)429);
            using var second = await client.PostAsync(
                "/odata/mcp",
                McpJsonRpc.Content("""{"jsonrpc":"2.0","id":"1","method":"tools/call","params":{"name":"odata_query","arguments":{"entitySet":"Customers"}}}"""));
            second.StatusCode.Should().Be((HttpStatusCode)429);
            var body = await second.Content.ReadAsStringAsync();
            body.Should().NotContain("Contoso");
        }

        /// <summary>
        /// In-process query Customers twice: second is tool 429.
        /// </summary>
        [TestMethod]
        public async Task Rate_McpQueryCustomers_ConsumesCustomersNotJustMcp()
        {
            var runtime = Session().Runtime;
            var first = await runtime.InvokeAsync("odata_query", ToolArguments.Of("entitySet", "Customers"), CancellationToken.None);
            first.IsError.Should().BeFalse(first.Text);
            var second = await runtime.InvokeAsync("odata_query", ToolArguments.Of("entitySet", "Customers"), CancellationToken.None);
            second.IsError.Should().BeTrue();
            second.Text.Should().Contain("429");
        }

        /// <summary>
        /// Products twice then third 429; Customers still ok.
        /// </summary>
        [TestMethod]
        public async Task Rate_QueryProductsTwice_Third429_CustomersStillOk()
        {
            var runtime = Session().Runtime;
            (await runtime.InvokeAsync("odata_query", ToolArguments.Of("entitySet", "Products"), CancellationToken.None)).IsError.Should().BeFalse();
            (await runtime.InvokeAsync("odata_query", ToolArguments.Of("entitySet", "Products"), CancellationToken.None)).IsError.Should().BeFalse();
            var third = await runtime.InvokeAsync("odata_query", ToolArguments.Of("entitySet", "Products"), CancellationToken.None);
            third.IsError.Should().BeTrue();
            third.Text.Should().Contain("429");
            (await runtime.InvokeAsync("odata_query", ToolArguments.Of("entitySet", "Customers"), CancellationToken.None)).IsError.Should().BeFalse();
        }

        /// <summary>
        /// MostValuable twice: second 429; Customers query still ok.
        /// </summary>
        [TestMethod]
        public async Task Rate_CallMostValuableTwice_Second429_QueryCustomersOk()
        {
            var runtime = Session().Runtime;
            var first = await runtime.InvokeAsync("odata_call", ToolArguments.Of("name", "MostValuable"), CancellationToken.None);
            first.IsError.Should().BeFalse(first.Text);
            var second = await runtime.InvokeAsync("odata_call", ToolArguments.Of("name", "MostValuable"), CancellationToken.None);
            second.IsError.Should().BeTrue();
            second.Text.Should().Contain("429");
            (await runtime.InvokeAsync("odata_query", ToolArguments.Of("entitySet", "Customers"), CancellationToken.None)).IsError.Should().BeFalse();
        }

        /// <summary>
        /// Spending Customers leaves function and Products available.
        /// </summary>
        [TestMethod]
        public async Task Rate_SpendCustomers_CallFunctionStillOk_QueryProductsStillOk()
        {
            var runtime = Session().Runtime;
            (await runtime.InvokeAsync("odata_query", ToolArguments.Of("entitySet", "Customers"), CancellationToken.None)).IsError.Should().BeFalse();
            (await runtime.InvokeAsync("odata_call", ToolArguments.Of("name", "MostValuable"), CancellationToken.None)).IsError.Should().BeFalse();
            (await runtime.InvokeAsync("odata_query", ToolArguments.Of("entitySet", "Products"), CancellationToken.None)).IsError.Should().BeFalse();
        }

        /// <summary>
        /// MCP HTTP 429 has the status on the HTTP response, not inside a tool result.
        /// </summary>
        [TestMethod]
        public async Task Rate_RetryAfterPresentOnMcpHttp429_HeaderOnHttpResponse()
        {
            using var client = TestServer.CreateClient();
            using var first = await client.PostAsync("/odata/mcp", McpJsonRpc.Content(McpJsonRpc.InitializePayload()));
            first.StatusCode.Should().NotBe((HttpStatusCode)429);
            using var second = await client.PostAsync("/odata/mcp", McpJsonRpc.Content(McpJsonRpc.InitializePayload()));
            second.StatusCode.Should().Be((HttpStatusCode)429);
            var body = await second.Content.ReadAsStringAsync();
            body.Should().NotContain("OData request failed with status");
        }

        /// <summary>
        /// odata_get Products spends the Products budget.
        /// </summary>
        [TestMethod]
        public async Task Rate_OdataGetProducts_ConsumesProductsBudget()
        {
            var runtime = Session().Runtime;
            (await runtime.InvokeAsync("odata_get", ToolArguments.Of("entitySet", "Products", "key", "1"), CancellationToken.None)).IsError.Should().BeFalse();
            (await runtime.InvokeAsync("odata_get", ToolArguments.Of("entitySet", "Products", "key", "1"), CancellationToken.None)).IsError.Should().BeFalse();
            var third = await runtime.InvokeAsync("odata_get", ToolArguments.Of("entitySet", "Products", "key", "1"), CancellationToken.None);
            third.IsError.Should().BeTrue();
            third.Text.Should().Contain("429");
        }

        /// <summary>
        /// odata_call does not consume Customers.
        /// </summary>
        [TestMethod]
        public async Task Rate_OdataCallFunction_DoesNotConsumeCustomers()
        {
            var runtime = Session().Runtime;
            (await runtime.InvokeAsync("odata_call", ToolArguments.Of("name", "MostValuable"), CancellationToken.None)).IsError.Should().BeFalse();
            (await runtime.InvokeAsync("odata_query", ToolArguments.Of("entitySet", "Customers"), CancellationToken.None)).IsError.Should().BeFalse();
        }

        /// <summary>
        /// list_entity_sets consumes neither set budget.
        /// </summary>
        [TestMethod]
        public async Task Rate_ListEntitySets_ConsumesNeitherSetBudget()
        {
            var runtime = Session().Runtime;
            (await runtime.InvokeAsync("odata_query", ToolArguments.Of("entitySet", "Customers"), CancellationToken.None)).IsError.Should().BeFalse();
            var listed = await runtime.InvokeAsync("odata_list_entity_sets", null, CancellationToken.None);
            listed.IsError.Should().BeFalse(listed.Text);
        }

        /// <summary>
        /// describe_type consumes neither set budget.
        /// </summary>
        [TestMethod]
        public async Task Rate_DescribeType_ConsumesNeither()
        {
            var runtime = Session().Runtime;
            (await runtime.InvokeAsync("odata_query", ToolArguments.Of("entitySet", "Customers"), CancellationToken.None)).IsError.Should().BeFalse();
            var described = await runtime.InvokeAsync("odata_describe_type", ToolArguments.Of("name", "Customers"), CancellationToken.None);
            described.IsError.Should().BeFalse(described.Text);
        }

        /// <summary>
        /// list_operations consumes neither set budget.
        /// </summary>
        [TestMethod]
        public async Task Rate_ListOperations_ConsumesNeither()
        {
            var runtime = Session().Runtime;
            (await runtime.InvokeAsync("odata_query", ToolArguments.Of("entitySet", "Customers"), CancellationToken.None)).IsError.Should().BeFalse();
            var listed = await runtime.InvokeAsync("odata_list_operations", null, CancellationToken.None);
            listed.IsError.Should().BeFalse(listed.Text);
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
            services.AddSingleton<CustomerStore>();
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
