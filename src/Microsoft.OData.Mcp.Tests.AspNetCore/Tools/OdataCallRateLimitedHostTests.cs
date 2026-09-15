// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.OData.Mcp.Core.Catalog;
using Microsoft.OData.Mcp.Tests.AspNetCore.Fixtures;
using Microsoft.OData.Mcp.Tests.Shared;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.OData.Mcp.Tests.AspNetCore.Tools
{


    /// <summary>
    /// <c>odata_call</c> cases that require the partitioned rate limiter.
    /// </summary>
    [TestClass]
    public class OdataCallRateLimitedHostTests : RateLimitedRichHost
    {

        #region Public Methods

        /// <summary>
        /// The second MostValuable call is tool <c>IsError</c> 429 while Customers query still works.
        /// </summary>
        [TestMethod]
        public async Task OdataCall_429FunctionBudget_MostValuableSecondCall()
        {
            var first = await InvokeAsync("odata_call", ToolArguments.Of("name", "MostValuable"));
            first.IsError.Should().BeFalse(first.Text);
            var second = await InvokeAsync("odata_call", ToolArguments.Of("name", "MostValuable"));
            second.IsError.Should().BeTrue(second.Text);
            second.Text.Should().Contain("status 429");
            var customers = await InvokeAsync("odata_query", ToolArguments.Of("entitySet", "Customers"));
            customers.IsError.Should().BeFalse(customers.Text);
            customers.StructuredContent.Should().Contain("Contoso");
        }

        /// <summary>
        /// Concurrent MostValuable calls eventually 429.
        /// </summary>
        [TestMethod]
        public async Task OdataCall_ConcurrentMostValuable_Until429()
        {
            var tasks = Enumerable.Range(0, 5).Select(_ => InvokeAsync("odata_call", ToolArguments.Of("name", "MostValuable"))).ToArray();
            var results = await Task.WhenAll(tasks);
            results.Should().Contain(result => result.IsError && result.Text.Contains("429", StringComparison.Ordinal));
        }

        /// <summary>
        /// MCP HTTP 429 and function-budget 429 are different layers.
        /// </summary>
        [TestMethod]
        public async Task OdataCall_McpHttp429VsFunction429_AreDifferentLayers()
        {
            var first = await InvokeAsync("odata_call", ToolArguments.Of("name", "MostValuable"));
            first.IsError.Should().BeFalse(first.Text);
            var second = await InvokeAsync("odata_call", ToolArguments.Of("name", "MostValuable"));
            second.IsError.Should().BeTrue(second.Text);
            second.Text.Should().Contain("status 429");
            using var client = CreateClient();
            using var mcpFirst = await client.PostAsync("/odata/mcp", McpJsonRpc.Content("{}"));
            using var mcpSecond = await client.PostAsync("/odata/mcp", McpJsonRpc.Content("{}"));
            mcpFirst.StatusCode.Should().NotBe(HttpStatusCode.NotFound);
            mcpSecond.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        }

        #endregion

    }

}
