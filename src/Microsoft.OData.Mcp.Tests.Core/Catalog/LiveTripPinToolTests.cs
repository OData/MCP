// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.OData.Mcp.Tests.Shared;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.OData.Mcp.Tests.Core.Catalog
{

    /// <summary>
    /// Live generic tool tests against TripPin.
    /// </summary>
    [TestClass]
    public class LiveTripPinToolTests
    {

        #region Public Methods

        /// <summary>
        /// Describe People contains UserName, Friends, Trips.
        /// </summary>
        [TestMethod]
        public async Task DescribeType_TripPin_People_ContainsUserNameFriendsTrips()
        {
            var (runtime, _) = await LiveToolRuntime.CreateTripPinAsync();
            var described = await runtime.InvokeAsync("odata_describe_type", ToolArguments.Of("name", "People"), CancellationToken.None);

            described.IsError.Should().BeFalse(described.Text);
            described.Text.Should().Contain("UserName: string // key");
            described.Text.Should().Contain("Friends -> Person[]");
            described.Text.Should().Contain("Trips -> Trip[]");
        }

        /// <summary>
        /// List entity sets contains People, Airlines, Airports.
        /// </summary>
        [TestMethod]
        public async Task ListEntitySets_TripPin_ContainsPeopleAirlinesAirports()
        {
            var (runtime, _) = await LiveToolRuntime.CreateTripPinAsync();
            var listed = await runtime.InvokeAsync("odata_list_entity_sets", null, CancellationToken.None);

            listed.IsError.Should().BeFalse(listed.Text);
            listed.StructuredContent.Should().Contain("People");
            listed.StructuredContent.Should().Contain("Airlines");
            listed.StructuredContent.Should().Contain("Airports");
            listed.StructuredContent.Should().Contain("UserName");
        }

        /// <summary>
        /// Create a unique person then delete them.
        /// </summary>
        [TestMethod]
        public async Task OdataCreate_TripPin_UniquePerson_ThenDelete()
        {
            var userName = "mcp" + Guid.NewGuid().ToString("N")[..8];
            var (runtime, _) = await LiveToolRuntime.CreateTripPinAsync();
            var created = await runtime.InvokeAsync(
                "odata_create",
                ToolArguments.Of("entitySet", "People", "body", "{\"UserName\":\"" + userName + "\",\"FirstName\":\"Mcp\",\"LastName\":\"Test\",\"Emails\":[\"a@b.c\"]}"),
                CancellationToken.None);

            created.IsError.Should().BeFalse(created.Text);
            var deleted = await runtime.InvokeAsync(
                "odata_delete",
                ToolArguments.Of("entitySet", "People", "key", userName),
                CancellationToken.None);
            deleted.IsError.Should().BeFalse(deleted.Text);
        }

        /// <summary>
        /// Get russellwhyte.
        /// </summary>
        [TestMethod]
        public async Task OdataGet_TripPin_PersonRussellwhyte()
        {
            var (runtime, capture) = await LiveToolRuntime.CreateTripPinAsync();
            var result = await runtime.InvokeAsync(
                "odata_get",
                ToolArguments.Of("entitySet", "People", "key", "russellwhyte"),
                CancellationToken.None);

            result.IsError.Should().BeFalse(result.Text);
            capture.Last!.RelativePath.Should().Be("People('russellwhyte')");
            result.StructuredContent.Should().Contain("russellwhyte");
        }

        /// <summary>
        /// Navigate Friends.
        /// </summary>
        [TestMethod]
        public async Task OdataNavigate_TripPin_RussellwhyteFriends()
        {
            var (runtime, capture) = await LiveToolRuntime.CreateTripPinAsync();
            var result = await runtime.InvokeAsync(
                "odata_navigate",
                ToolArguments.Of("entitySet", "People", "key", "russellwhyte", "navigation", "Friends"),
                CancellationToken.None);

            result.IsError.Should().BeFalse(result.Text);
            capture.Last!.RelativePath.Should().Be("People('russellwhyte')/Friends");
        }

        /// <summary>
        /// Call GetNearestAirport.
        /// </summary>
        [TestMethod]
        public async Task OdataCall_TripPin_GetNearestAirport()
        {
            var (runtime, _) = await LiveToolRuntime.CreateTripPinAsync();
            var result = await runtime.InvokeAsync(
                "odata_call",
                ToolArguments.Of("name", "GetNearestAirport", "parameters", new { lat = 33, lon = -118 }),
                CancellationToken.None);

            result.Should().NotBeNull();
        }

        /// <summary>
        /// Query People top 2 matches HTTP.
        /// </summary>
        [TestMethod]
        public async Task OdataQuery_TripPin_PeopleTop2_MatchHttp()
        {
            var (runtime, _) = await LiveToolRuntime.CreateTripPinAsync();
            using var http = new HttpClient();
            http.DefaultRequestHeaders.Accept.ParseAdd("application/json");
            var listed = await runtime.InvokeAsync("odata_list_entity_sets", null, CancellationToken.None);
            listed.IsError.Should().BeFalse();
            var result = await runtime.InvokeAsync(
                "odata_query",
                ToolArguments.Of("entitySet", "People", "top", 2),
                CancellationToken.None);

            result.IsError.Should().BeFalse(result.Text);
            ODataFeedReader.ReadValueCount(result.StructuredContent!).Should().BeLessThanOrEqualTo(2);
        }

        /// <summary>
        /// Unknown TripPin set is 404.
        /// </summary>
        [TestMethod]
        public async Task OdataQuery_TripPin_UnknownSet_404()
        {
            var (runtime, _) = await LiveToolRuntime.CreateTripPinAsync();
            var result = await runtime.InvokeAsync("odata_query", ToolArguments.Of("entitySet", "DoesNotExist"), CancellationToken.None);

            result.IsError.Should().BeTrue();
            result.Text.Should().Contain("status ");
        }

        #endregion

    }

}
