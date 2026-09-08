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
    /// A8 host tests for <c>odata_query</c> on the rich convention model.
    /// </summary>
    [TestClass]
    public class OdataQueryHostTests : ConventionRichHost
    {

        #region Public Methods

        /// <summary>
        /// A leftover <c>body</c> property is ignored on query.
        /// </summary>
        [TestMethod]
        public async Task OdataQuery_BodyProperty_IgnoredOnQuery()
        {
            var (result, capture) = await QueryCapturedAsync("entitySet", "Customers", "body", """{"CompanyName":"Nope"}""");

            result.IsError.Should().BeFalse(result.Text);
            result.StructuredContent.Should().Contain("Contoso");
            capture.Last!.RelativePath.Should().Be("Customers");
            capture.Last.JsonBody.Should().BeNull();
        }

        /// <summary>
        /// Numeric <c>count</c> is forwarded raw; OData may 400 or treat it as truthy.
        /// </summary>
        [TestMethod]
        public async Task OdataQuery_CountAsNumber1_ForwardedRaw_ODataMay400OrTreatTruthy()
        {
            var (result, capture) = await QueryCapturedAsync("entitySet", "Customers", "count", 1);

            capture.Last.Should().NotBeNull();
            capture.Last!.QueryOptions["count"].Should().Be("1");
            if (result.IsError)
            {
                result.Text.Should().Contain("400");
            }
            else
            {
                result.StructuredContent.Should().NotBeNullOrWhiteSpace();
            }
        }

        /// <summary>
        /// String <c>count</c> of <c>true</c> is forwarded and succeeds.
        /// </summary>
        [TestMethod]
        public async Task OdataQuery_CountAsStringTrue()
        {
            var (result, capture) = await QueryCapturedAsync("entitySet", "Customers", "count", "true");

            result.IsError.Should().BeFalse(result.Text);
            capture.Last!.QueryOptions["count"].Should().Be("true");
            ODataFeedReader.ReadCount(result.StructuredContent!).Should().Be(1);
        }

        /// <summary>
        /// String <c>count</c> of <c>yes</c> is an OData 400.
        /// </summary>
        [TestMethod]
        public async Task OdataQuery_CountAsStringYes_OData400()
        {
            var (result, capture) = await QueryCapturedAsync("entitySet", "Customers", "count", "yes");

            capture.Last!.QueryOptions["count"].Should().Be("yes");
            ShouldBeODataStatusError(result, 400);
        }

        /// <summary>
        /// <c>resources/read</c> is a type card, not the query collection.
        /// </summary>
        [TestMethod]
        public async Task OdataQuery_DoesNotEqualResourceRead()
        {
            var query = await QueryAsync("entitySet", "Customers");
            query.IsError.Should().BeFalse(query.Text);
            query.StructuredContent.Should().Contain("Contoso");
            ODataFeedReader.ReadCompanyNames(query.StructuredContent!).Should().Contain("Contoso");

            var card = Session().Catalog.Resources.Single(resource => resource.Name == "Customers").ReadContents;
            card.Should().NotBeNullOrWhiteSpace();
            card.Should().Contain("keys");
            card.Should().Contain("properties");
            card.Should().NotContain("Contoso");
            query.StructuredContent.Should().NotBe(card);
        }

        /// <summary>
        /// A <c>$filter</c> argument is ignored, so the unfiltered feed is returned.
        /// </summary>
        [TestMethod]
        public async Task OdataQuery_DollarFilter_IsIgnoredSoUnfilteredResult()
        {
            var (odataStatus, odataBody) = await GetODataAsync("/odata/Customers");
            odataStatus.Should().Be(HttpStatusCode.OK);
            var (result, capture) = await QueryCapturedAsync("entitySet", "Customers", "$filter", "CompanyName eq 'NoSuch'");

            result.IsError.Should().BeFalse(result.Text);
            result.StructuredContent.Should().Contain("Contoso");
            ODataFeedReader.ReadCompanyNames(result.StructuredContent!).Should().Equal(ODataFeedReader.ReadCompanyNames(odataBody));
            capture.Last!.QueryOptions.Should().NotContainKey("filter");
            capture.Last.QueryOptions.Should().NotContainKey("$filter");
        }

        /// <summary>
        /// An empty filter string is forwarded as an empty <c>$filter</c>.
        /// </summary>
        [TestMethod]
        public async Task OdataQuery_EmptyFilterString_ForwardsEmptyDollarFilter()
        {
            var (result, capture) = await QueryCapturedAsync("entitySet", "Customers", "filter", string.Empty);

            capture.Last.Should().NotBeNull();
            capture.Last!.QueryOptions.Should().ContainKey("filter");
            capture.Last.QueryOptions["filter"].Should().BeEmpty();
            if (result.IsError)
            {
                result.Text.Should().Contain("400");
            }
        }

        /// <summary>
        /// <c>entity</c> is not accepted in place of <c>entitySet</c>.
        /// </summary>
        [TestMethod]
        public async Task OdataQuery_EntityInsteadOfEntitySet_IsErrorMissingEntitySet()
        {
            var result = await QueryAsync("entity", "Customers");

            result.IsError.Should().BeTrue();
            result.Text.Should().Contain("entitySet");
        }

        /// <summary>
        /// An empty <c>entitySet</c> string is a required-argument error.
        /// </summary>
        [TestMethod]
        public async Task OdataQuery_EntitySetEmptyString_IsErrorRequired()
        {
            var result = await QueryAsync("entitySet", string.Empty);

            result.IsError.Should().BeTrue();
            result.Text.Should().Contain("entitySet");
        }

        /// <summary>
        /// A JSON null <c>entitySet</c> is an error.
        /// </summary>
        [TestMethod]
        public async Task OdataQuery_EntitySetJsonNull_IsError()
        {
            var result = await QueryAsync("entitySet", null);

            result.IsError.Should().BeTrue();
            result.Text.Should().Contain("entitySet");
        }

        /// <summary>
        /// Generic query matches <c>list_customers</c> for the same skip and top.
        /// </summary>
        [TestMethod]
        public async Task OdataQuery_EqualsListCustomers_SameSkipTop()
        {
            var generic = await QueryAsync("entitySet", "Customers", "orderby", "CustomerId", "skip", 1, "top", 2);
            var named = await InvokeAsync(
                "list_customers",
                ToolArguments.Of("orderby", "CustomerId", "skip", 1, "top", 2));

            generic.IsError.Should().BeFalse(generic.Text);
            named.IsError.Should().BeFalse(named.Text);
            ODataFeedReader.ReadCompanyNames(named.StructuredContent!).Should().Equal(ODataFeedReader.ReadCompanyNames(generic.StructuredContent!));
        }

        /// <summary>
        /// An expand injection string is an OData 400.
        /// </summary>
        [TestMethod]
        public async Task OdataQuery_ExpandInjection_OData400()
        {
            var result = await QueryAsync("entitySet", "Customers", "expand", "Orders/$ref,NotANav");

            ShouldBeODataStatusError(result, 400);
        }

        /// <summary>
        /// An array filter is an OData 400.
        /// </summary>
        [TestMethod]
        public async Task OdataQuery_FilterAsArray_OData400()
        {
            var (result, capture) = await QueryCapturedAsync("entitySet", "Customers", "filter", new[] { "CompanyName eq 'Contoso'" });

            capture.Last!.QueryOptions.Should().ContainKey("filter");
            ShouldBeODataStatusError(result, 400);
        }

        /// <summary>
        /// A numeric filter is forwarded raw.
        /// </summary>
        [TestMethod]
        public async Task OdataQuery_FilterAsNumber_ForwardedRaw()
        {
            var (result, capture) = await QueryCapturedAsync("entitySet", "Customers", "filter", 1);

            capture.Last.Should().NotBeNull();
            capture.Last!.QueryOptions["filter"].Should().Be("1");
            if (result.IsError)
            {
                result.Text.Should().Contain("400");
            }
        }

        /// <summary>
        /// <c>1 eq 1</c> is valid OData or 400, never SQL injection.
        /// </summary>
        [TestMethod]
        public async Task OdataQuery_FilterInjectionOr1Eq1_StillValidODataOr400()
        {
            var (odataStatus, odataBody) = await GetODataAsync("/odata/Customers?$filter=1 eq 1");
            var result = await QueryAsync("entitySet", "Customers", "filter", "1 eq 1");

            if (odataStatus == HttpStatusCode.OK)
            {
                result.IsError.Should().BeFalse(result.Text);
                ODataFeedReader.ReadCompanyNames(result.StructuredContent!).Should().Equal(ODataFeedReader.ReadCompanyNames(odataBody));
            }
            else
            {
                result.IsError.Should().BeTrue();
                result.Text.Should().Contain($"{(int)odataStatus}");
            }
        }

        /// <summary>
        /// A SQL-injection-shaped filter is OData 400 or empty, never 500.
        /// </summary>
        [TestMethod]
        public async Task OdataQuery_FilterSqlDropTable_IsOData400OrEmptyNot500()
        {
            var result = await QueryAsync("entitySet", "Customers", "filter", "CompanyName eq 'x'; DROP TABLE Customers;--");

            result.Text.Should().NotContain("status 500");
            if (result.IsError)
            {
                result.Text.Should().Contain("400");
            }
            else
            {
                ODataFeedReader.ReadValueCount(result.StructuredContent!).Should().Be(0);
            }
        }

        /// <summary>
        /// An unclosed quote in filter is an OData 400.
        /// </summary>
        [TestMethod]
        public async Task OdataQuery_FilterUnclosedQuote_OData400()
        {
            var result = await QueryAsync("entitySet", "Customers", "filter", "CompanyName eq 'Contoso");

            ShouldBeODataStatusError(result, 400);
        }

        /// <summary>
        /// A Unicode emoji filter is empty or 400, never 500.
        /// </summary>
        [TestMethod]
        public async Task OdataQuery_FilterUnicodeEmoji_CompanyNameEq()
        {
            var result = await QueryAsync("entitySet", "Customers", "filter", "CompanyName eq 'Contoso😀'");

            result.Text.Should().NotContain("status 500");
            if (result.IsError)
            {
                result.Text.Should().Contain("400");
            }
            else
            {
                ODataFeedReader.ReadCompanyNames(result.StructuredContent!).Should().NotContain("Contoso");
            }
        }

        /// <summary>
        /// A dollar sign inside a filter value is forwarded.
        /// </summary>
        [TestMethod]
        public async Task OdataQuery_FilterWithDollarInValue_IsFine()
        {
            var (result, capture) = await QueryCapturedAsync("entitySet", "Customers", "filter", "CompanyName eq '$top'");

            capture.Last!.QueryOptions["filter"].Should().Be("CompanyName eq '$top'");
            result.Text.Should().NotContain("status 500");
            if (!result.IsError)
            {
                ODataFeedReader.ReadCompanyNames(result.StructuredContent!).Should().NotContain("Contoso");
            }
        }

        /// <summary>
        /// Invalid filter syntax is an OData 400.
        /// </summary>
        [TestMethod]
        public async Task OdataQuery_InvalidFilterSyntax_OData400()
        {
            var result = await QueryAsync("entitySet", "Customers", "filter", "this is not filter");

            ShouldBeODataStatusError(result, 400);
        }

        /// <summary>
        /// JSON-RPC <c>tools/call</c> queries Customers.
        /// </summary>
        [TestMethod]
        public async Task OdataQuery_JsonRpcToolsCall_OData8_Customers()
        {
            using var client = CreateClient();
            using var response = await McpJsonRpc.CallToolAsync(
                client,
                "/odata/mcp",
                "odata_query",
                """{"entitySet":"Customers"}""");
            var body = await McpJsonRpc.ReadBodyAsync(response);

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            body.Should().Contain("Contoso");
        }

        /// <summary>
        /// An expand longer than 512 characters is rejected without HTTP.
        /// </summary>
        [TestMethod]
        public async Task OdataQuery_MaxExpandLengthExceeded_IsErrorNoHttp()
        {
            var (result, capture) = await QueryCapturedAsync("entitySet", "Customers", "expand", new string('x', 513));

            result.IsError.Should().BeTrue();
            result.Text.Should().Contain("expand exceeds the maximum length");
            capture.Requests.Should().BeEmpty();
        }

        /// <summary>
        /// A filter of exactly 2048 characters is allowed and sent.
        /// </summary>
        [TestMethod]
        public async Task OdataQuery_MaxFilterLengthExact2048_IsAllowedAndSent()
        {
            const string prefix = "CompanyName eq '";
            const string suffix = "'";
            var filter = prefix + new string('x', 2048 - prefix.Length - suffix.Length) + suffix;
            filter.Length.Should().Be(2048);
            var (result, capture) = await QueryCapturedAsync("entitySet", "Customers", "filter", filter);

            capture.Requests.Should().NotBeEmpty();
            capture.Last!.QueryOptions["filter"].Length.Should().Be(2048);
            result.Text.Should().NotContain("exceeds the maximum length");
        }

        /// <summary>
        /// A filter longer than 2048 characters is rejected without HTTP.
        /// </summary>
        [TestMethod]
        public async Task OdataQuery_MaxFilterLengthExceeded_IsErrorNoHttp()
        {
            var (result, capture) = await QueryCapturedAsync("entitySet", "Customers", "filter", new string('x', 2049));

            result.IsError.Should().BeTrue();
            result.Text.Should().Contain("filter exceeds the maximum length");
            capture.Requests.Should().BeEmpty();
        }

        /// <summary>
        /// A select longer than 1024 characters is rejected without HTTP.
        /// </summary>
        [TestMethod]
        public async Task OdataQuery_MaxSelectLengthExceeded_IsErrorNoHttp()
        {
            var (result, capture) = await QueryCapturedAsync("entitySet", "Customers", "select", new string('x', 1025));

            result.IsError.Should().BeTrue();
            result.Text.Should().Contain("select exceeds the maximum length");
            capture.Requests.Should().BeEmpty();
        }

        /// <summary>
        /// Missing <c>entitySet</c> is an error that names the argument.
        /// </summary>
        [TestMethod]
        public async Task OdataQuery_MissingEntitySet_IsErrorContainsEntitySet()
        {
            var result = await InvokeAsync("odata_query", new Dictionary<string, JsonElement>());

            result.IsError.Should().BeTrue();
            result.Text.Should().Contain("entitySet");
        }

        /// <summary>
        /// Named <c>list_customers</c> returns the same page as generic query.
        /// </summary>
        [TestMethod]
        public async Task OdataQuery_NamedListCustomers_SamePageAsGeneric()
        {
            var generic = await QueryAsync("entitySet", "Customers", "skip", 1, "top", 2);
            var named = await InvokeAsync("list_customers", ToolArguments.Of("skip", 1, "top", 2));

            generic.IsError.Should().BeFalse(generic.Text);
            named.IsError.Should().BeFalse(named.Text);
            ODataFeedReader.ReadCompanyNames(named.StructuredContent!).Should().Equal(ODataFeedReader.ReadCompanyNames(generic.StructuredContent!));
        }

        /// <summary>
        /// A leftover <c>navigation</c> property is ignored on query.
        /// </summary>
        [TestMethod]
        public async Task OdataQuery_NavigationProperty_IgnoredOnQuery()
        {
            var (result, capture) = await QueryCapturedAsync("entitySet", "Customers", "navigation", "Orders");

            result.IsError.Should().BeFalse(result.Text);
            result.StructuredContent.Should().Contain("Contoso");
            capture.Last!.RelativePath.Should().Be("Customers");
        }

        /// <summary>
        /// A null <c>entitySet</c> is an error.
        /// </summary>
        [TestMethod]
        public async Task OdataQuery_NullEntitySet_IsError()
        {
            var result = await QueryAsync("entitySet", null);

            result.IsError.Should().BeTrue();
            result.Text.Should().Contain("entitySet");
        }

        /// <summary>
        /// Querying Maintenance is a 503 with Retry-After.
        /// </summary>
        [TestMethod]
        public async Task OdataQuery_OData503_IsErrorStatus503RetryAfter()
        {
            var result = await QueryAsync("entitySet", "Maintenance");

            ShouldBeODataStatusError(result, 503);
            result.Text.Should().Contain("Retry-After:");
        }

        /// <summary>
        /// Querying Forbidden is a 403.
        /// </summary>
        [TestMethod]
        public async Task OdataQuery_OData8_403ForbiddenSet()
        {
            var (odataStatus, _) = await GetODataAsync("/odata/Forbidden");
            odataStatus.Should().Be(HttpStatusCode.Forbidden);
            var result = await QueryAsync("entitySet", "Forbidden");

            ShouldBeODataStatusError(result, 403);
        }

        /// <summary>
        /// Querying Booms is a 500.
        /// </summary>
        [TestMethod]
        public async Task OdataQuery_OData8_500()
        {
            var result = await QueryAsync("entitySet", "Booms");

            ShouldBeODataStatusError(result, 500);
        }

        /// <summary>
        /// <c>count=true</c> returns <c>@odata.count</c> matching the HTTP twin.
        /// </summary>
        [TestMethod]
        public async Task OdataQuery_OData8_CountTrue_ReturnsOdataCount()
        {
            var (odataStatus, odataBody) = await GetODataAsync("/odata/Customers?$count=true");
            odataStatus.Should().Be(HttpStatusCode.OK);
            var (result, capture) = await QueryCapturedAsync("entitySet", "Customers", "count", true);

            result.IsError.Should().BeFalse(result.Text);
            capture.Last!.QueryOptions["count"].Should().Be("true");
            ODataFeedReader.ReadCount(result.StructuredContent!).Should().Be(ODataFeedReader.ReadCount(odataBody)).And.Be(1);
        }

        /// <summary>
        /// Querying Customers without top matches GET /odata/Customers and does not inject top.
        /// </summary>
        [TestMethod]
        public async Task OdataQuery_OData8_Customers_MatchesGetOdataCustomers()
        {
            var (odataStatus, odataBody) = await GetODataAsync("/odata/Customers");
            odataStatus.Should().Be(HttpStatusCode.OK);
            var (result, capture) = await QueryCapturedAsync("entitySet", "Customers");

            result.IsError.Should().BeFalse(result.Text);
            ODataFeedReader.ReadCompanyNames(result.StructuredContent!).Should().Equal(ODataFeedReader.ReadCompanyNames(odataBody));
            capture.Last!.QueryOptions.Should().NotContainKey("top");
            capture.Last.RelativePath.Should().Be("Customers");
        }

        /// <summary>
        /// Expand Orders matches the HTTP twin.
        /// </summary>
        [TestMethod]
        public async Task OdataQuery_OData8_ExpandOrders_Matches()
        {
            var (odataStatus, odataBody) = await GetODataAsync("/odata/Customers?$expand=Orders");
            odataStatus.Should().Be(HttpStatusCode.OK);
            var (result, capture) = await QueryCapturedAsync("entitySet", "Customers", "expand", "Orders");

            result.IsError.Should().BeFalse(result.Text);
            result.StructuredContent.Should().Contain("Orders");
            odataBody.Should().Contain("Orders");
            capture.Last!.QueryOptions["expand"].Should().Be("Orders");
        }

        /// <summary>
        /// Filter CompanyName eq Contoso matches the HTTP twin.
        /// </summary>
        [TestMethod]
        public async Task OdataQuery_OData8_FilterCompanyNameEqContoso_Matches()
        {
            var (odataStatus, odataBody) = await GetODataAsync("/odata/Customers?$filter=CompanyName eq 'Contoso'");
            odataStatus.Should().Be(HttpStatusCode.OK);
            var (result, capture) = await QueryCapturedAsync("entitySet", "Customers", "filter", "CompanyName eq 'Contoso'");

            result.IsError.Should().BeFalse(result.Text);
            ODataFeedReader.ReadCompanyNames(result.StructuredContent!).Should().Equal(ODataFeedReader.ReadCompanyNames(odataBody)).And.Equal("Contoso");
            capture.Last!.QueryOptions["filter"].Should().Be("CompanyName eq 'Contoso'");
        }

        /// <summary>
        /// Select CompanyName matches the HTTP twin.
        /// </summary>
        [TestMethod]
        public async Task OdataQuery_OData8_SelectCompanyName_Matches()
        {
            var (odataStatus, odataBody) = await GetODataAsync("/odata/Customers?$select=CompanyName");
            odataStatus.Should().Be(HttpStatusCode.OK);
            var (result, capture) = await QueryCapturedAsync("entitySet", "Customers", "select", "CompanyName");

            result.IsError.Should().BeFalse(result.Text);
            result.StructuredContent.Should().Contain("CompanyName");
            odataBody.Should().Contain("CompanyName");
            ODataFeedReader.ReadCompanyNames(result.StructuredContent!).Should().Equal(ODataFeedReader.ReadCompanyNames(odataBody));
            capture.Last!.QueryOptions["select"].Should().Be("CompanyName");
        }

        /// <summary>
        /// Skip without top is forwarded and does not inject top.
        /// </summary>
        [TestMethod]
        public async Task OdataQuery_OData8_SkipWithoutTop_Passthrough()
        {
            var (odataStatus, odataBody) = await GetODataAsync("/odata/Customers?$orderby=CustomerId&$skip=0");
            odataStatus.Should().Be(HttpStatusCode.OK);
            var (result, capture) = await QueryCapturedAsync("entitySet", "Customers", "orderby", "CustomerId", "skip", 0);

            result.IsError.Should().BeFalse(result.Text);
            ODataFeedReader.ReadCompanyNames(result.StructuredContent!).Should().Equal(ODataFeedReader.ReadCompanyNames(odataBody));
            capture.Last!.QueryOptions.Should().ContainKey("skip");
            capture.Last.QueryOptions.Should().NotContainKey("top");
        }

        /// <summary>
        /// Top 1 orderby CustomerId matches the HTTP twin.
        /// </summary>
        [TestMethod]
        public async Task OdataQuery_OData8_Top1OrderbyCustomerId_Matches()
        {
            var (odataStatus, odataBody) = await GetODataAsync("/odata/Customers?$orderby=CustomerId&$top=1");
            odataStatus.Should().Be(HttpStatusCode.OK);
            var (result, capture) = await QueryCapturedAsync("entitySet", "Customers", "top", 1, "orderby", "CustomerId");

            result.IsError.Should().BeFalse(result.Text);
            ODataFeedReader.ReadCompanyNames(result.StructuredContent!).Should().Equal(ODataFeedReader.ReadCompanyNames(odataBody)).And.Equal("Contoso");
            capture.Last!.QueryOptions["top"].Should().Be("1");
            capture.Last.QueryOptions["orderby"].Should().Be("CustomerId");
        }

        /// <summary>
        /// Select as an array of strings is forwarded raw and is an OData 400.
        /// </summary>
        [TestMethod]
        public async Task OdataQuery_SelectAsArrayOfStrings_RawTextArray_OData400()
        {
            var (result, capture) = await QueryCapturedAsync("entitySet", "Customers", "select", new[] { "CompanyName" });

            capture.Last!.QueryOptions["select"].Should().Contain("CompanyName");
            ShouldBeODataStatusError(result, 400);
        }

        /// <summary>
        /// <c>set</c> is not accepted in place of <c>entitySet</c>.
        /// </summary>
        [TestMethod]
        public async Task OdataQuery_SetInsteadOfEntitySet_IsError()
        {
            var result = await QueryAsync("set", "Customers");

            result.IsError.Should().BeTrue();
            result.Text.Should().Contain("entitySet");
        }

        /// <summary>
        /// Boolean skip is an OData 400.
        /// </summary>
        [TestMethod]
        public async Task OdataQuery_SkipAsBool_OData400()
        {
            var (result, capture) = await QueryCapturedAsync("entitySet", "Customers", "skip", true);

            capture.Last!.QueryOptions["skip"].Should().Be("true");
            ShouldBeODataStatusError(result, 400);
        }

        /// <summary>
        /// String skip is forwarded and succeeds.
        /// </summary>
        [TestMethod]
        public async Task OdataQuery_SkipAsString()
        {
            var (result, capture) = await QueryCapturedAsync("entitySet", "Customers", "orderby", "CustomerId", "skip", "1");

            capture.Last!.QueryOptions["skip"].Should().Be("1");
            result.IsError.Should().BeFalse(result.Text);
        }

        /// <summary>
        /// Swapping key and entity set queries entity set <c>1</c> and is an OData 404.
        /// </summary>
        [TestMethod]
        public async Task OdataQuery_SwappedKeyAndEntitySet_EntitySetIs1_IsError404FromOData()
        {
            var (result, capture) = await QueryCapturedAsync("entitySet", "1", "key", "Customers");

            capture.Last!.RelativePath.Should().Be("1");
            ShouldBeODataStatusError(result, 404);
        }

        /// <summary>
        /// Query then get the first key returns Contoso.
        /// </summary>
        [TestMethod]
        public async Task OdataQuery_ThenGetFirstKey_OData8()
        {
            var query = await QueryAsync("entitySet", "Customers");
            query.IsError.Should().BeFalse(query.Text);
            var keys = ODataFeedReader.ReadKeys(query.StructuredContent!);
            keys.Should().NotBeEmpty();

            var get = await InvokeAsync("odata_get", ToolArguments.Of("entitySet", "Customers", "key", keys[0].ToString()));
            get.IsError.Should().BeFalse(get.Text);
            get.StructuredContent.Should().Contain("Contoso");
        }

        /// <summary>
        /// Query then navigate Orders matches the HTTP twin for that path.
        /// </summary>
        [TestMethod]
        public async Task OdataQuery_ThenNavigateOrders_OData8OrTripPinFriends()
        {
            var query = await QueryAsync("entitySet", "Customers");
            query.IsError.Should().BeFalse(query.Text);
            var keys = ODataFeedReader.ReadKeys(query.StructuredContent!);
            keys.Should().NotBeEmpty();

            var (twinStatus, _) = await GetODataAsync($"/odata/Customers({keys[0]})/Orders");
            var navigate = await InvokeAsync(
                "odata_navigate",
                ToolArguments.Of("entitySet", "Customers", "key", keys[0].ToString(), "navigation", "Orders"));

            if (twinStatus == HttpStatusCode.OK)
            {
                navigate.IsError.Should().BeFalse(navigate.Text);
            }
            else
            {
                navigate.IsError.Should().BeTrue();
                navigate.Text.Should().Contain($"status {(int)twinStatus}");
            }
        }

        /// <summary>
        /// Array top is an OData 400.
        /// </summary>
        [TestMethod]
        public async Task OdataQuery_TopAsArray_OData400_IsError()
        {
            var (result, capture) = await QueryCapturedAsync("entitySet", "Customers", "top", new[] { 1, 2 });

            capture.Last!.QueryOptions["top"].Should().Contain("1");
            ShouldBeODataStatusError(result, 400);
        }

        /// <summary>
        /// Boolean top is an OData 400.
        /// </summary>
        [TestMethod]
        public async Task OdataQuery_TopAsBoolTrue_OData400_IsErrorStatus400()
        {
            var (result, capture) = await QueryCapturedAsync("entitySet", "Customers", "top", true);

            capture.Last!.QueryOptions["top"].Should().Be("true");
            ShouldBeODataStatusError(result, 400);
        }

        /// <summary>
        /// Object top is an OData 400.
        /// </summary>
        [TestMethod]
        public async Task OdataQuery_TopAsObject_OData400_IsError()
        {
            var (result, capture) = await QueryCapturedAsync("entitySet", "Customers", "top", new Dictionary<string, int> { ["n"] = 1 });

            capture.Last!.QueryOptions.Should().ContainKey("top");
            ShouldBeODataStatusError(result, 400);
        }

        /// <summary>
        /// String top <c>"1"</c> succeeds.
        /// </summary>
        [TestMethod]
        public async Task OdataQuery_TopAsString_1_Succeeds()
        {
            var (result, capture) = await QueryCapturedAsync("entitySet", "Customers", "top", "1");

            result.IsError.Should().BeFalse(result.Text);
            capture.Last!.QueryOptions["top"].Should().Be("1");
            ODataFeedReader.ReadCompanyNames(result.StructuredContent!).Should().Contain("Contoso");
        }

        /// <summary>
        /// An unknown entity set is not success and includes the OData status.
        /// </summary>
        [TestMethod]
        public async Task OdataQuery_UnknownEntitySet_IsErrorStatus404OrNotSuccess()
        {
            var (odataStatus, _) = await GetODataAsync("/odata/DoesNotExist");
            var result = await QueryAsync("entitySet", "DoesNotExist");

            result.IsError.Should().BeTrue();
            result.Text.Should().Contain($"status {(int)odataStatus}");
        }

        /// <summary>
        /// An unknown property is ignored and the query still succeeds.
        /// </summary>
        [TestMethod]
        public async Task OdataQuery_UnknownPropertyFoo_Ignored()
        {
            var (result, capture) = await QueryCapturedAsync("entitySet", "Customers", "foo", "bar");

            result.IsError.Should().BeFalse(result.Text);
            result.StructuredContent.Should().Contain("Contoso");
            capture.Last!.QueryOptions.Should().NotContainKey("foo");
        }

        /// <summary>
        /// A get-style key is ignored; the collection is still listed.
        /// </summary>
        [TestMethod]
        public async Task OdataQuery_UsingGetArgs_KeyWithoutBeingGet_KeyIgnored()
        {
            var (result, capture) = await QueryCapturedAsync("entitySet", "Customers", "key", "1");

            result.IsError.Should().BeFalse(result.Text);
            result.StructuredContent.Should().Contain("value");
            result.StructuredContent.Should().Contain("Contoso");
            capture.Last!.RelativePath.Should().Be("Customers");
        }

        /// <summary>
        /// A whitespace <c>entitySet</c> is an error.
        /// </summary>
        [TestMethod]
        public async Task OdataQuery_WhitespaceEntitySet_IsError()
        {
            var result = await QueryAsync("entitySet", "   ");

            result.IsError.Should().BeTrue();
            result.Text.Should().Contain("entitySet");
        }

        #endregion

        #region Internal Methods

        /// <summary>
        /// GETs an OData URL on the test server.
        /// </summary>
        /// <param name="path">The request path and query.</param>
        /// <returns>
        /// Status and body.
        /// </returns>
        internal async Task<(HttpStatusCode Status, string Body)> GetODataAsync(string path)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(path);

            using var client = CreateClient();
            using var response = await client.GetAsync(path);
            var body = await response.Content.ReadAsStringAsync();

            return (response.StatusCode, body);
        }

        /// <summary>
        /// Invokes <c>odata_query</c> with the given argument pairs.
        /// </summary>
        /// <param name="pairs">Alternating names and values.</param>
        /// <returns>
        /// The tool result.
        /// </returns>
        internal Task<ODataToolInvocationResult> QueryAsync(params object?[] pairs)
        {
            return InvokeAsync("odata_query", ToolArguments.Of(pairs));
        }

        /// <summary>
        /// Invokes <c>odata_query</c> through a capturing executor.
        /// </summary>
        /// <param name="pairs">Alternating names and values.</param>
        /// <returns>
        /// The tool result and capture wrapper.
        /// </returns>
        internal async Task<(ODataToolInvocationResult Result, CapturingODataExecutor Capture)> QueryCapturedAsync(params object?[] pairs)
        {
            var (runtime, capture) = CreateCapturingRuntime();
            var result = await runtime.InvokeAsync("odata_query", ToolArguments.Of(pairs), CancellationToken.None);

            return (result, capture);
        }

        /// <summary>
        /// Asserts a failed OData tool result includes the status phrase.
        /// </summary>
        /// <param name="result">The tool result.</param>
        /// <param name="status">The expected HTTP status.</param>
        internal static void ShouldBeODataStatusError(ODataToolInvocationResult result, int status)
        {
            ArgumentNullException.ThrowIfNull(result);

            result.IsError.Should().BeTrue(result.Text);
            result.Text.Should().Contain($"OData request failed with status {status}");
        }

        #endregion

    }

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

    /// <summary>
    /// Per-set OData rate limits around <c>odata_query</c>.
    /// </summary>
    [TestClass]
    public class OdataQueryRateLimitHostTests : ConventionRichHost
    {

        #region Public Methods

        /// <summary>
        /// Exhausting Customers leaves Products unlimited on its own budget.
        /// </summary>
        [TestMethod]
        public async Task OdataQuery_CustomersLimited_ProductsUnlimited_Independent()
        {
            var first = await InvokeAsync("odata_query", ToolArguments.Of("entitySet", "Customers"));
            first.IsError.Should().BeFalse(first.Text);
            first.StructuredContent.Should().Contain("Contoso");

            var second = await InvokeAsync("odata_query", ToolArguments.Of("entitySet", "Customers"));
            second.IsError.Should().BeTrue();
            second.Text.Should().Contain("429");

            var products = await InvokeAsync("odata_query", ToolArguments.Of("entitySet", "Products"));
            products.IsError.Should().BeFalse(products.Text);
            products.StructuredContent.Should().Contain("Widget");
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
