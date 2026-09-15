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
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.OData;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OData.Mcp.Core.Catalog;
using Microsoft.OData.Mcp.Tests.AspNetCore.Fixtures;
using Microsoft.OData.Mcp.Tests.AspNetCore.RateLimit;
using Microsoft.OData.Mcp.Tests.Shared;
using Microsoft.OData.Mcp.Tests.Shared.Entities;
using Microsoft.OData.Mcp.Tests.Shared.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.OData.Mcp.Tests.AspNetCore.Tools
{


    /// <summary>
    /// <c>odata_get</c> cases that require the partitioned rate limiter.
    /// </summary>
    [TestClass]
    public class OdataGetRateLimitedHostTests : RateLimitedRichHost
    {

        #region Public Methods

        /// <summary>
        /// The second get of Customers spends the set budget and is tool <c>IsError</c> 429.
        /// </summary>
        [TestMethod]
        public async Task OdataGet_429OnSet_RetryAfter()
        {
            var first = await InvokeAsync("odata_get", ToolArguments.Of("entitySet", "Customers", "key", "1"));
            first.IsError.Should().BeFalse(first.Text);
            var second = await InvokeAsync("odata_get", ToolArguments.Of("entitySet", "Customers", "key", "1"));
            second.IsError.Should().BeTrue(second.Text);
            second.Text.Should().Contain("status 429");
        }

        /// <summary>
        /// A second POST to <c>/odata/mcp</c> is HTTP 429, not a tool result.
        /// </summary>
        [TestMethod]
        public async Task OdataGet_McpHttp429()
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
