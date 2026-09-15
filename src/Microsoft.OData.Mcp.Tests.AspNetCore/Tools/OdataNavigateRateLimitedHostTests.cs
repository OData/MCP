// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.OData.Mcp.Tests.AspNetCore.Fixtures;
using Microsoft.OData.Mcp.Tests.Shared;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.OData.Mcp.Tests.AspNetCore.Tools
{

    /// <summary>
    /// <c>odata_navigate</c> cases that require the partitioned rate limiter.
    /// </summary>
    [TestClass]
    public class OdataNavigateRateLimitedHostTests : RateLimitedRichHost
    {

        #region Public Methods

        /// <summary>
        /// The second navigate under Customers spends the set budget.
        /// </summary>
        [TestMethod]
        public async Task OdataNavigate_429OnCustomersPath()
        {
            var first = await InvokeAsync("odata_navigate", ToolArguments.Of("entitySet", "Customers", "key", "1", "navigation", "Orders"));
            var second = await InvokeAsync("odata_navigate", ToolArguments.Of("entitySet", "Customers", "key", "1", "navigation", "Orders"));
            if (!first.IsError)
            {
                second.IsError.Should().BeTrue(second.Text);
                second.Text.Should().Contain("429");
            }
            else
            {
                first.Text.Should().NotBeNullOrWhiteSpace();
            }
        }

        /// <summary>
        /// A second POST to <c>/odata/mcp</c> is HTTP 429.
        /// </summary>
        [TestMethod]
        public async Task OdataNavigate_McpHttp429()
        {
            using var client = CreateClient();
            using var first = await client.PostAsync("/odata/mcp", McpJsonRpc.Content("{}"));
            using var second = await client.PostAsync("/odata/mcp", McpJsonRpc.Content("{}"));
            first.StatusCode.Should().NotBe(HttpStatusCode.NotFound);
            second.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        }

        #endregion

    }

}
