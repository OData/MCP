// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
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
using Microsoft.OData.Mcp.Tests.Shared;
using Microsoft.OData.Mcp.Tests.Shared.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.OData.Mcp.Tests.AspNetCore.Tools
{


    /// <summary>
    /// <c>odata_list_operations</c> cases that require the partitioned rate limiter.
    /// </summary>
    [TestClass]
    public class OdataListOperationsRateLimitedHostTests : RateLimitedRichHost
    {

        #region Public Methods

        /// <summary>
        /// Spending the MostValuable budget does not prevent listing operations.
        /// </summary>
        [TestMethod]
        public async Task ListOperations_FunctionRateLimitSpent_StillLists()
        {
            var call = await InvokeAsync("odata_call", ToolArguments.Of("name", "MostValuable"));
            call.IsError.Should().BeFalse(call.Text);

            var listed = await InvokeAsync("odata_list_operations");
            listed.IsError.Should().BeFalse(listed.Text);
            listed.StructuredContent.Should().Contain("MostValuable");
        }

        /// <summary>
        /// A second POST to <c>/odata/mcp</c> is HTTP 429.
        /// </summary>
        [TestMethod]
        public async Task ListOperations_McpHttp429()
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
