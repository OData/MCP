// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.OData;
using Microsoft.Extensions.DependencyInjection;
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
    /// A8 paging-host tests for <c>odata_query</c> against ClientCustomers and ServerCustomers.
    /// </summary>
    [TestClass]
    public class OdataQueryPagingHostTests : ConventionRichHost
    {

        #region Public Methods

        /// <summary>
        /// MCP does not clamp <c>top=50</c> on a five-row set.
        /// </summary>
        [TestMethod]
        public async Task OdataQuery_DoesNotClampTopToMaxTop()
        {
            var (runtime, capture) = CreateCapturingRuntime();
            var result = await runtime.InvokeAsync(
                "odata_query",
                ToolArguments.Of("entitySet", "ClientCustomers", "orderby", "CustomerId", "top", 50),
                CancellationToken.None);

            result.IsError.Should().BeFalse(result.Text);
            capture.Last!.QueryOptions["top"].Should().Be("50");
            ODataFeedReader.ReadCompanyNames(result.StructuredContent!).Should().Equal(PagingCustomerSeed.CompanyNames);
            ODataFeedReader.ReadValueCount(result.StructuredContent!).Should().Be(5);
        }

        /// <summary>
        /// A <c>$top</c> argument is ignored and not injected as top on the five-row set.
        /// </summary>
        [TestMethod]
        public async Task OdataQuery_DollarTop_IsIgnoredNotInjectedAsTop()
        {
            var (runtime, capture) = CreateCapturingRuntime();
            var result = await runtime.InvokeAsync(
                "odata_query",
                ToolArguments.Of("entitySet", "ClientCustomers", "$top", 1),
                CancellationToken.None);

            result.IsError.Should().BeFalse(result.Text);
            capture.Last!.QueryOptions.Should().NotContainKey("top");
            capture.Last.QueryOptions.Should().NotContainKey("$top");
            ODataFeedReader.ReadValueCount(result.StructuredContent!).Should().Be(5);
        }

        /// <summary>
        /// Client-driven filter/skip/top/orderby/select/count matches the HTTP twin.
        /// </summary>
        [TestMethod]
        public async Task OdataQuery_PagingClient_FilterSkipTopOrderbySelect_MatchTwin()
        {
            using var client = CreateClient();
            using var twin = await client.GetAsync("/odata/ClientCustomers?$filter=CustomerId gt 1&$orderby=CustomerId&$skip=1&$top=2&$select=CompanyName&$count=true");
            var twinBody = await twin.Content.ReadAsStringAsync();
            twin.IsSuccessStatusCode.Should().BeTrue("status {0} body {1}", (int)twin.StatusCode, twinBody);

            var result = await InvokeAsync(
                "odata_query",
                ToolArguments.Of(
                    "entitySet",
                    "ClientCustomers",
                    "filter",
                    "CustomerId gt 1",
                    "orderby",
                    "CustomerId",
                    "skip",
                    1,
                    "top",
                    2,
                    "select",
                    "CompanyName",
                    "count",
                    true));
            result.IsError.Should().BeFalse(result.Text);
            ODataFeedReader.ReadCompanyNames(result.StructuredContent!).Should().Equal(ODataFeedReader.ReadCompanyNames(twinBody));
            ODataFeedReader.ReadCount(result.StructuredContent!).Should().Be(ODataFeedReader.ReadCount(twinBody));
        }

        /// <summary>
        /// Server-driven paging without top returns PageSize rows and a next link.
        /// </summary>
        [TestMethod]
        public async Task OdataQuery_PagingServer_OmitTop_ReturnsPageSizeAndNextLink()
        {
            using var client = CreateClient();
            using var twin = await client.GetAsync("/odata/ServerCustomers?$orderby=CustomerId");
            var twinBody = await twin.Content.ReadAsStringAsync();
            twin.IsSuccessStatusCode.Should().BeTrue("status {0} body {1}", (int)twin.StatusCode, twinBody);

            var (runtime, capture) = CreateCapturingRuntime();
            var result = await runtime.InvokeAsync(
                "odata_query",
                ToolArguments.Of("entitySet", "ServerCustomers", "orderby", "CustomerId"),
                CancellationToken.None);

            result.IsError.Should().BeFalse(result.Text);
            capture.Last!.QueryOptions.Should().NotContainKey("top");
            ODataFeedReader.ReadCompanyNames(result.StructuredContent!).Should().Equal(ODataFeedReader.ReadCompanyNames(twinBody)).And.Equal("Contoso", "Fabrikam");
            ODataFeedReader.ReadNextLink(result.StructuredContent!).Should().NotBeNullOrWhiteSpace();
            ODataFeedReader.ReadValueCount(result.StructuredContent!).Should().Be(2);
        }

        /// <summary>
        /// Skip/top for the second server page matches following <c>@odata.nextLink</c>.
        /// </summary>
        [TestMethod]
        public async Task OdataQuery_PagingServer_SkipTop_SecondPageMatchesNextLinkPayload()
        {
            using var client = CreateClient();
            using var first = await client.GetAsync("/odata/ServerCustomers?$orderby=CustomerId");
            var firstBody = await first.Content.ReadAsStringAsync();
            first.IsSuccessStatusCode.Should().BeTrue("status {0} body {1}", (int)first.StatusCode, firstBody);
            var next = ODataFeedReader.ReadNextLink(firstBody);
            next.Should().NotBeNullOrWhiteSpace();
            using var nextResponse = await client.GetAsync(next);
            var nextBody = await nextResponse.Content.ReadAsStringAsync();
            nextResponse.IsSuccessStatusCode.Should().BeTrue("status {0} body {1}", (int)nextResponse.StatusCode, nextBody);

            var result = await InvokeAsync(
                "odata_query",
                ToolArguments.Of("entitySet", "ServerCustomers", "orderby", "CustomerId", "skip", 2, "top", 2));
            result.IsError.Should().BeFalse(result.Text);
            ODataFeedReader.ReadCompanyNames(result.StructuredContent!).Should().Equal(ODataFeedReader.ReadCompanyNames(nextBody));
            ODataFeedReader.ReadCompanyNames(result.StructuredContent!).Should().Equal("Northwind", "AdventureWorks");
        }

        #endregion

        #region Internal Methods

        /// <inheritdoc />
        internal override void ConfigureServices(IServiceCollection services)
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
        }

        #endregion

    }

}
