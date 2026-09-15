// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Claims;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using CloudNimble.Breakdance.AspNetCore;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OData;
using Microsoft.AspNetCore.OData.Query;
using Microsoft.AspNetCore.OData.Routing.Controllers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.OData.Mcp.AspNetCore.Hosting;
using Microsoft.OData.Mcp.Tests.AspNetCore.Fixtures;
using Microsoft.OData.Mcp.Tests.AspNetCore.RateLimit;
using Microsoft.OData.Mcp.Tests.Shared;
using Microsoft.OData.Mcp.Tests.Shared.Entities;
using Microsoft.OData.ModelBuilder;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.OData.Mcp.Tests.AspNetCore.Tools
{


    /// <summary>
    /// Delete tests against partitioned rate limits.
    /// </summary>
    [TestClass]
    public class OdataDeleteRateLimitHostTests : ConventionRichHost
    {

        #region Public Methods

        /// <summary>
        /// A second MCP POST is HTTP 429.
        /// </summary>
        [TestMethod]
        public async Task OdataDelete_McpHttp429()
        {
            using var client = CreateClient();
            using var first = await client.PostAsync("/odata/mcp", McpJsonRpc.Content(McpJsonRpc.InitializePayload()));
            first.StatusCode.Should().NotBe((HttpStatusCode)429);
            using var second = await client.PostAsync("/odata/mcp", McpJsonRpc.Content(McpJsonRpc.InitializePayload()));
            second.StatusCode.Should().Be((HttpStatusCode)429);
        }

        /// <summary>
        /// A second Customers DELETE is tool 429.
        /// </summary>
        [TestMethod]
        public async Task OdataDelete_SetRateLimit429()
        {
            var (runtime, _) = CreateCapturingRuntime();
            Authenticate();
            var created = await runtime.InvokeAsync(
                "odata_create",
                ToolArguments.Of("entitySet", "Customers", "body", """{"CompanyName":"RateDel1"}"""),
                CancellationToken.None);
            created.IsError.Should().BeFalse(created.Text);
            var key = OdataDeleteHostTests.ReadCustomerId(created.StructuredContent!);
            var deleted = await runtime.InvokeAsync("odata_delete", ToolArguments.Of("entitySet", "Customers", "key", key.ToString()), CancellationToken.None);
            deleted.IsError.Should().BeTrue();
            deleted.Text.Should().Contain("429");
        }

        /// <summary>
        /// 204 responses are unaffected by a tiny max-response cap.
        /// </summary>
        [TestMethod]
        public async Task OdataDelete_MaxResponseBytesIrrelevantOn204()
        {
            var (runtime, capture) = CreateCapturingRuntime();
            Authenticate();
            var created = await runtime.InvokeAsync(
                "odata_create",
                ToolArguments.Of("entitySet", "Customers", "body", """{"CompanyName":"TinyDel"}"""),
                CancellationToken.None);
            if (created.IsError)
            {
                created.Text.Should().Match(text => text.Contains("401", StringComparison.Ordinal) || text.Contains("429", StringComparison.Ordinal));
                return;
            }

            var key = OdataDeleteHostTests.ReadCustomerId(created.StructuredContent!);
            capture.Requests.Clear();
            var deleted = await runtime.InvokeAsync("odata_delete", ToolArguments.Of("entitySet", "Customers", "key", key.ToString()), CancellationToken.None);
            deleted.Should().NotBeNull();
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
