// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.OData.Mcp.Core.Catalog;
using Microsoft.OData.Mcp.Tests.Shared;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.OData.Mcp.Tests.Core.Catalog
{

    /// <summary>
    /// Live TripPin read-tool tests against the public RESTier service.
    /// </summary>
    [TestClass]
    public class LiveTripPinReadToolTests
    {

        #region Public Methods

        /// <summary>
        /// Describe People contains UserName, Friends, and Trips.
        /// </summary>
        [TestMethod]
        public async Task DescribeType_TripPin_People_ContainsUserNameFriendsTrips()
        {
            var (runtime, _) = await LiveToolRuntime.CreateTripPinAsync();
            var result = await runtime.InvokeAsync("odata_describe_type", ToolArguments.Of("name", "People"), CancellationToken.None);

            result.IsError.Should().BeFalse(result.Text);
            result.Text.Should().Contain("UserName: string // key");
            result.Text.Should().Contain("Friends -> Person[]");
            result.Text.Should().Contain("Trips -> Trip[]");
        }

        /// <summary>
        /// Describe then navigate uses Friends from the card.
        /// </summary>
        [TestMethod]
        public async Task DescribeType_ThenNavigate_UsesANavigationFromTheCard()
        {
            var (runtime, capture) = await LiveToolRuntime.CreateTripPinAsync();
            var described = await runtime.InvokeAsync("odata_describe_type", ToolArguments.Of("name", "People"), CancellationToken.None);
            described.IsError.Should().BeFalse(described.Text);
            described.Text.Should().Contain("Friends -> Person[]");
            var navigated = await runtime.InvokeAsync(
                "odata_navigate",
                ToolArguments.Of("entitySet", "People", "key", "russellwhyte", "navigation", "Friends"),
                CancellationToken.None);

            navigated.IsError.Should().BeFalse(navigated.Text);
            capture.Last!.RelativePath.Should().Be("People('russellwhyte')/Friends");
        }

        /// <summary>
        /// Named get person quotes the string key.
        /// </summary>
        [TestMethod]
        public async Task GetPerson_StringKeyQuotedOnWire()
        {
            var (runtime, capture) = await LiveToolRuntime.CreateTripPinAsync();
            var result = await runtime.InvokeAsync("get_person", ToolArguments.Of("key", "russellwhyte"), CancellationToken.None);

            result.IsError.Should().BeFalse(result.Text);
            capture.Last!.RelativePath.Should().Be("People('russellwhyte')");
        }

        /// <summary>
        /// Named get person matches generic get.
        /// </summary>
        [TestMethod]
        public async Task GetPerson_TripPin_Russellwhyte_Matches()
        {
            var (runtime, _) = await LiveToolRuntime.CreateTripPinAsync();
            var named = await runtime.InvokeAsync("get_person", ToolArguments.Of("key", "russellwhyte"), CancellationToken.None);
            var generic = await runtime.InvokeAsync(
                "odata_get",
                ToolArguments.Of("entitySet", "People", "key", "russellwhyte"),
                CancellationToken.None);

            named.IsError.Should().BeFalse(named.Text);
            generic.IsError.Should().BeFalse(generic.Text);
            named.StructuredContent.Should().Be(generic.StructuredContent);
        }

        /// <summary>
        /// Deep expand Friends on named get is forwarded.
        /// </summary>
        [TestMethod]
        public async Task GetPerson_DeepExpandFriends()
        {
            var (runtime, capture) = await LiveToolRuntime.CreateTripPinAsync();
            var result = await runtime.InvokeAsync(
                "get_person",
                ToolArguments.Of("key", "russellwhyte", "expand", "Friends($expand=Friends)"),
                CancellationToken.None);

            capture.Last!.QueryOptions["expand"].Should().Be("Friends($expand=Friends)");
            if (result.IsError)
            {
                result.Text.Should().Contain("OData request failed with status");
            }
            else
            {
                result.StructuredContent.Should().Contain("Friends");
            }
        }

        /// <summary>
        /// List entity sets contains People, Airlines, and Airports.
        /// </summary>
        [TestMethod]
        public async Task ListEntitySets_TripPin_ContainsPeopleAirlinesAirports()
        {
            var (runtime, _) = await LiveToolRuntime.CreateTripPinAsync();
            var result = await runtime.InvokeAsync("odata_list_entity_sets", null, CancellationToken.None);

            result.IsError.Should().BeFalse(result.Text);
            result.StructuredContent.Should().Contain("People");
            result.StructuredContent.Should().Contain("Airlines");
            result.StructuredContent.Should().Contain("Airports");
            result.StructuredContent.Should().Contain("UserName");
        }

        /// <summary>
        /// TripPin lists the unbound GetNearestAirport and ResetDataSource; the bound ShareTrip is on the Person type instead.
        /// </summary>
        [TestMethod]
        public async Task ListOperations_TripPin_UnboundOnly_ShareTripIsOnDescribeType()
        {
            var (runtime, _) = await LiveToolRuntime.CreateTripPinAsync();
            var result = await runtime.InvokeAsync("odata_list_operations", null, CancellationToken.None);
            var person = await runtime.InvokeAsync("odata_describe_type", ToolArguments.Of("name", "People"), CancellationToken.None);

            result.IsError.Should().BeFalse(result.Text);
            result.StructuredContent.Should().Contain("\"GetNearestAirport\":\"(lat: number, lon: number) -> Airport\"");
            result.StructuredContent.Should().Contain("\"ResetDataSource\":\"() // writes\"");
            result.StructuredContent.Should().NotContain("ShareTrip");
            result.StructuredContent.Should().NotContain("kind");
            result.StructuredContent.Should().NotContain("isBound");
            person.IsError.Should().BeFalse(person.Text);
            person.Text.Should().Contain("ShareTrip(userName: string, tripId: int) // writes");
        }

        /// <summary>
        /// Named list people expand Friends matches generic query.
        /// </summary>
        [TestMethod]
        public async Task ListPeople_TripPin_ExpandFriends_MatchGeneric()
        {
            var (runtime, capture) = await LiveToolRuntime.CreateTripPinAsync();
            var named = await runtime.InvokeAsync(
                "list_people",
                ToolArguments.Of("expand", "Friends", "top", 2, "orderby", "UserName"),
                CancellationToken.None);
            var generic = await runtime.InvokeAsync(
                "odata_query",
                ToolArguments.Of("entitySet", "People", "expand", "Friends", "top", 2, "orderby", "UserName"),
                CancellationToken.None);

            named.IsError.Should().BeFalse(named.Text);
            generic.IsError.Should().BeFalse(generic.Text);
            capture.Last!.RelativePath.Should().Be("People");
            named.StructuredContent.Should().Be(generic.StructuredContent);
        }

        /// <summary>
        /// Bound ShareTrip without entitySet is an error.
        /// </summary>
        [TestMethod]
        public async Task OdataCall_BoundWithoutEntitySet_IsErrorMissingEntitySet()
        {
            var (runtime, capture) = await LiveToolRuntime.CreateTripPinAsync();
            var result = await runtime.InvokeAsync(
                "odata_call",
                ToolArguments.Of("name", "ShareTrip", "parameters", new { userName = "scottketchum", tripId = 0 }),
                CancellationToken.None);

            result.IsError.Should().BeTrue(result.Text);
            result.Text.Should().Be("ShareTrip is bound to Person. Pass entitySet and key. Signature: ShareTrip(userName: string, tripId: int) // writes");
            capture.Requests.Should().BeEmpty();
        }

        /// <summary>
        /// Bound ShareTrip without key is an error.
        /// </summary>
        [TestMethod]
        public async Task OdataCall_BoundWithoutKey_IsErrorMissingKey()
        {
            var (runtime, capture) = await LiveToolRuntime.CreateTripPinAsync();
            var result = await runtime.InvokeAsync(
                "odata_call",
                ToolArguments.Of("name", "ShareTrip", "entitySet", "People", "parameters", new { userName = "scottketchum", tripId = 0 }),
                CancellationToken.None);

            result.IsError.Should().BeTrue(result.Text);
            result.Text.Should().Contain("Pass entitySet and key");
            capture.Requests.Should().BeEmpty();
        }

        /// <summary>
        /// GetFavoriteAirline is called when declared.
        /// </summary>
        [TestMethod]
        public async Task OdataCall_TripPin_BoundFunctionIfDeclared_GetFavoriteAirline()
        {
            var (runtime, capture) = await LiveToolRuntime.CreateTripPinAsync();
            var described = await runtime.InvokeAsync("odata_describe_type", ToolArguments.Of("name", "People"), CancellationToken.None);
            described.IsError.Should().BeFalse(described.Text);
            described.Text.Should().Contain("GetFavoriteAirline() -> Airline");
            var result = await runtime.InvokeAsync(
                "odata_call",
                ToolArguments.Of("name", "GetFavoriteAirline", "entitySet", "People", "key", "russellwhyte"),
                CancellationToken.None);

            capture.Last!.Method.Should().Be(HttpMethod.Get);
            capture.Last.RelativePath.Should().Be("People('russellwhyte')/GetFavoriteAirline");
            if (result.IsError)
            {
                result.Text.Should().Contain("OData request failed with status");
            }
            else
            {
                result.StructuredContent.Should().NotBeNullOrWhiteSpace();
            }
        }

        /// <summary>
        /// GetNearestAirport with lat/lon matches HTTP.
        /// </summary>
        [TestMethod]
        public async Task OdataCall_TripPin_GetNearestAirport_LatLon_MatchHttp()
        {
            var (runtime, capture) = await LiveToolRuntime.CreateTripPinAsync();
            var result = await runtime.InvokeAsync(
                "odata_call",
                ToolArguments.Of("name", "GetNearestAirport", "parameters", new { lat = 33, lon = -118 }),
                CancellationToken.None);
            var odata = await TwinGetAsync("GetNearestAirport(lat=33,lon=-118)");

            result.IsError.Should().BeFalse(result.Text);
            capture.Last!.Method.Should().Be(HttpMethod.Get);
            capture.Last.RelativePath.Should().Be("GetNearestAirport(lat=33,lon=-118)");
            result.StructuredContent.Should().Contain("IcaoCode");
            odata.Should().Contain("IcaoCode");
        }

        /// <summary>
        /// Case-insensitive operation names resolve.
        /// </summary>
        [TestMethod]
        public async Task OdataCall_CaseInsensitiveName_mostvaluable()
        {
            var (runtime, capture) = await LiveToolRuntime.CreateTripPinAsync();
            var result = await runtime.InvokeAsync(
                "odata_call",
                ToolArguments.Of("name", "getnearestairport", "parameters", new { lat = 33, lon = -118 }),
                CancellationToken.None);

            result.IsError.Should().BeFalse(result.Text);
            capture.Last!.RelativePath.Should().StartWith("GetNearestAirport(");
        }

        /// <summary>
        /// An unknown parameter is an error before HTTP; nothing is scraped into the path.
        /// </summary>
        [TestMethod]
        public async Task OdataCall_ExtraUnknownParamOnFunction_IsErrorNoHttp()
        {
            var (runtime, capture) = await LiveToolRuntime.CreateTripPinAsync();
            var result = await runtime.InvokeAsync(
                "odata_call",
                ToolArguments.Of("name", "GetNearestAirport", "parameters", new { lat = 33, lon = -118, foo = 1 }),
                CancellationToken.None);

            result.IsError.Should().BeTrue();
            result.Text.Should().Be("Unknown parameter 'foo'. Declared: lat, lon.");
            capture.Requests.Should().BeEmpty();
        }

        /// <summary>
        /// String function parameters are quoted as OData literals.
        /// </summary>
        [TestMethod]
        public async Task OdataCall_StringParamUnquotedVsQuoted_FormatKeyQuotesStrings()
        {
            var (runtime, capture) = await LiveToolRuntime.CreateTripPinAsync();
            var result = await runtime.InvokeAsync(
                "odata_call",
                ToolArguments.Of("name", "GetFriendsTrips", "entitySet", "People", "key", "russellwhyte", "parameters", new { userName = "scottketchum" }),
                CancellationToken.None);

            capture.Last!.Method.Should().Be(HttpMethod.Get);
            capture.Last.RelativePath.Should().Be("People('russellwhyte')/GetFriendsTrips(userName='scottketchum')");
            if (result.IsError)
            {
                result.Text.Should().Contain("OData request failed with status");
            }
        }

        /// <summary>
        /// Unbound functions reject entitySet and key instead of silently dropping them.
        /// </summary>
        [TestMethod]
        public async Task OdataCall_UnboundWithEntitySetAndKey_IsError()
        {
            var (runtime, capture) = await LiveToolRuntime.CreateTripPinAsync();
            var result = await runtime.InvokeAsync(
                "odata_call",
                ToolArguments.Of("name", "GetNearestAirport", "entitySet", "People", "key", "russellwhyte", "parameters", new { lat = 33, lon = -118 }),
                CancellationToken.None);

            result.IsError.Should().BeTrue();
            result.Text.Should().Be("GetNearestAirport is unbound. Omit entitySet and key.");
            capture.Requests.Should().BeEmpty();
        }

        /// <summary>
        /// Call does not use an entity-set query path.
        /// </summary>
        [TestMethod]
        public async Task OdataCall_DoesNotUseOdataQueryPath()
        {
            var (runtime, capture) = await LiveToolRuntime.CreateTripPinAsync();
            var result = await runtime.InvokeAsync(
                "odata_call",
                ToolArguments.Of("name", "GetNearestAirport", "parameters", new { lat = 33, lon = -118 }),
                CancellationToken.None);

            result.IsError.Should().BeFalse(result.Text);
            capture.Last!.RelativePath.Should().StartWith("GetNearestAirport");
            capture.Last.RelativePath.Should().NotBe("People");
        }

        /// <summary>
        /// A missing non-nullable function parameter fails before HTTP with the signature.
        /// </summary>
        [TestMethod]
        public async Task OdataCall_MissingFunctionParam_FailsBeforeHttp()
        {
            var (runtime, capture) = await LiveToolRuntime.CreateTripPinAsync();
            var result = await runtime.InvokeAsync(
                "odata_call",
                ToolArguments.Of("name", "GetNearestAirport", "parameters", new { lat = 33 }),
                CancellationToken.None);

            result.IsError.Should().BeTrue();
            result.Text.Should().Be("Missing parameter 'lon'. Signature: GetNearestAirport(lat: number, lon: number) -> Airport");
            capture.Requests.Should().BeEmpty();
        }

        /// <summary>
        /// Deep expand Friends($expand=Friends) is forwarded.
        /// </summary>
        [TestMethod]
        public async Task OdataGet_DeepExpand_TripPinFriends()
        {
            var (runtime, capture) = await LiveToolRuntime.CreateTripPinAsync();
            var result = await runtime.InvokeAsync(
                "odata_get",
                ToolArguments.Of("entitySet", "People", "key", "russellwhyte", "expand", "Friends($expand=Friends)"),
                CancellationToken.None);

            capture.Last!.QueryOptions["expand"].Should().Be("Friends($expand=Friends)");
            if (result.IsError)
            {
                result.Text.Should().Contain("OData request failed with status");
            }
        }

        /// <summary>
        /// Get russellwhyte quotes the string key.
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
        /// Unknown person keys 404.
        /// </summary>
        [TestMethod]
        public async Task OdataGet_UnknownKey999_IsError404()
        {
            var (runtime, _) = await LiveToolRuntime.CreateTripPinAsync();
            var result = await runtime.InvokeAsync(
                "odata_get",
                ToolArguments.Of("entitySet", "People", "key", "no-such"),
                CancellationToken.None);

            result.IsError.Should().BeTrue(result.Text);
            result.Text.Should().Contain("404");
        }

        /// <summary>
        /// Concurrent Friends and Trips navigations succeed.
        /// </summary>
        [TestMethod]
        public async Task OdataNavigate_ConcurrentFriendsAndTrips()
        {
            var (runtime, _) = await LiveToolRuntime.CreateTripPinAsync();
            var friends = runtime.InvokeAsync(
                "odata_navigate",
                ToolArguments.Of("entitySet", "People", "key", "russellwhyte", "navigation", "Friends"),
                CancellationToken.None);
            var trips = runtime.InvokeAsync(
                "odata_navigate",
                ToolArguments.Of("entitySet", "People", "key", "russellwhyte", "navigation", "Trips"),
                CancellationToken.None);
            var results = await Task.WhenAll(friends, trips);

            results.Should().OnlyContain(result => !result.IsError);
        }

        /// <summary>
        /// Deep expand on Friends is forwarded.
        /// </summary>
        [TestMethod]
        public async Task OdataNavigate_DeepExpandFriendsFriends()
        {
            var (runtime, capture) = await LiveToolRuntime.CreateTripPinAsync();
            var result = await runtime.InvokeAsync(
                "odata_navigate",
                ToolArguments.Of("entitySet", "People", "key", "russellwhyte", "navigation", "Friends", "expand", "Friends"),
                CancellationToken.None);

            capture.Last!.QueryOptions["expand"].Should().Be("Friends");
            if (result.IsError)
            {
                result.Text.Should().Contain("OData request failed with status");
            }
        }

        /// <summary>
        /// To-many Friends does not inject default top.
        /// </summary>
        [TestMethod]
        public async Task OdataNavigate_DoesNotAddDefaultTopOnToMany()
        {
            var (runtime, capture) = await LiveToolRuntime.CreateTripPinAsync();
            var result = await runtime.InvokeAsync(
                "odata_navigate",
                ToolArguments.Of("entitySet", "People", "key", "russellwhyte", "navigation", "Friends"),
                CancellationToken.None);

            result.IsError.Should().BeFalse(result.Text);
            capture.Last!.QueryOptions.Should().NotContainKey("top");
        }

        /// <summary>
        /// Friends count=true is forwarded.
        /// </summary>
        [TestMethod]
        public async Task OdataNavigate_TripPin_Friends_CountTrue()
        {
            var (runtime, capture) = await LiveToolRuntime.CreateTripPinAsync();
            var result = await runtime.InvokeAsync(
                "odata_navigate",
                ToolArguments.Of("entitySet", "People", "key", "russellwhyte", "navigation", "Friends", "count", true),
                CancellationToken.None);

            result.IsError.Should().BeFalse(result.Text);
            capture.Last!.QueryOptions["count"].Should().Be("true");
            ODataFeedReader.ReadCount(result.StructuredContent!).Should().NotBeNull();
        }

        /// <summary>
        /// Omitting top on Friends does not inject top.
        /// </summary>
        [TestMethod]
        public async Task OdataNavigate_TripPin_Friends_OmitTop_DoesNotInjectTop_ServerMayPage()
        {
            var (runtime, capture) = await LiveToolRuntime.CreateTripPinAsync();
            var result = await runtime.InvokeAsync(
                "odata_navigate",
                ToolArguments.Of("entitySet", "People", "key", "russellwhyte", "navigation", "Friends"),
                CancellationToken.None);

            result.IsError.Should().BeFalse(result.Text);
            capture.Last!.QueryOptions.Should().NotContainKey("top");
        }

        /// <summary>
        /// Friends skip/top/filter/orderby/select match HTTP.
        /// </summary>
        [TestMethod]
        public async Task OdataNavigate_TripPin_Friends_SkipTopFilterOrderbySelect_MatchHttp()
        {
            var (runtime, capture) = await LiveToolRuntime.CreateTripPinAsync();
            var result = await runtime.InvokeAsync(
                "odata_navigate",
                ToolArguments.Of(
                    "entitySet",
                    "People",
                    "key",
                    "russellwhyte",
                    "navigation",
                    "Friends",
                    "orderby",
                    "UserName",
                    "skip",
                    0,
                    "top",
                    1,
                    "select",
                    "UserName,FirstName"),
                CancellationToken.None);
            var odata = await TwinGetAsync("People('russellwhyte')/Friends?$orderby=UserName&$skip=0&$top=1&$select=UserName,FirstName");

            result.IsError.Should().BeFalse(result.Text);
            capture.Last!.QueryOptions["top"].Should().Be("1");
            ODataFeedReader.ReadStrings(result.StructuredContent!, "UserName", "userName")
                .Should()
                .Equal(ODataFeedReader.ReadStrings(odata, "UserName", "userName"));
        }

        /// <summary>
        /// Russell Whyte Friends matches HTTP.
        /// </summary>
        [TestMethod]
        public async Task OdataNavigate_TripPin_RussellwhyteFriends_ToMany_MatchHttp()
        {
            var (runtime, capture) = await LiveToolRuntime.CreateTripPinAsync();
            var result = await runtime.InvokeAsync(
                "odata_navigate",
                ToolArguments.Of("entitySet", "People", "key", "russellwhyte", "navigation", "Friends"),
                CancellationToken.None);
            var odata = await TwinGetAsync("People('russellwhyte')/Friends");

            result.IsError.Should().BeFalse(result.Text);
            capture.Last!.RelativePath.Should().Be("People('russellwhyte')/Friends");
            ODataFeedReader.ReadStrings(result.StructuredContent!, "UserName", "userName")
                .Should()
                .Equal(ODataFeedReader.ReadStrings(odata, "UserName", "userName"));
        }

        /// <summary>
        /// Russell Whyte Trips matches HTTP.
        /// </summary>
        [TestMethod]
        public async Task OdataNavigate_TripPin_RussellwhyteTrips_ToMany_MatchHttp()
        {
            var (runtime, capture) = await LiveToolRuntime.CreateTripPinAsync();
            var result = await runtime.InvokeAsync(
                "odata_navigate",
                ToolArguments.Of("entitySet", "People", "key", "russellwhyte", "navigation", "Trips"),
                CancellationToken.None);
            var odata = await TwinGetAsync("People('russellwhyte')/Trips");

            result.IsError.Should().BeFalse(result.Text);
            capture.Last!.RelativePath.Should().Be("People('russellwhyte')/Trips");
            ODataFeedReader.ReadValueCount(result.StructuredContent!).Should().Be(ODataFeedReader.ReadValueCount(odata));
        }

        /// <summary>
        /// Navigate after query uses the UserName key.
        /// </summary>
        [TestMethod]
        public async Task OdataNavigate_AfterQueryPeople_UsesUserNameKey()
        {
            var (runtime, capture) = await LiveToolRuntime.CreateTripPinAsync();
            var queried = await runtime.InvokeAsync(
                "odata_query",
                ToolArguments.Of("entitySet", "People", "filter", "UserName eq 'russellwhyte'"),
                CancellationToken.None);
            queried.IsError.Should().BeFalse(queried.Text);
            var navigated = await runtime.InvokeAsync(
                "odata_navigate",
                ToolArguments.Of("entitySet", "People", "key", "russellwhyte", "navigation", "Friends"),
                CancellationToken.None);

            navigated.IsError.Should().BeFalse(navigated.Text);
            capture.Last!.RelativePath.Should().Be("People('russellwhyte')/Friends");
        }

        /// <summary>
        /// Deep expand Friends($expand=Friends) on query.
        /// </summary>
        [TestMethod]
        public async Task OdataQuery_DeepExpandIfServerAllows_TripPinFriendsFriends()
        {
            var (runtime, capture) = await LiveToolRuntime.CreateTripPinAsync();
            var result = await runtime.InvokeAsync(
                "odata_query",
                ToolArguments.Of("entitySet", "People", "expand", "Friends($expand=Friends)", "top", 1),
                CancellationToken.None);

            capture.Last!.QueryOptions["expand"].Should().Be("Friends($expand=Friends)");
            if (result.IsError)
            {
                result.Text.Should().Contain("OData request failed with status");
            }
        }

        /// <summary>
        /// Filter UserName eq russellwhyte.
        /// </summary>
        [TestMethod]
        public async Task OdataQuery_TripPin_FilterUserNameEqRussellwhyte()
        {
            var (runtime, capture) = await LiveToolRuntime.CreateTripPinAsync();
            var result = await runtime.InvokeAsync(
                "odata_query",
                ToolArguments.Of("entitySet", "People", "filter", "UserName eq 'russellwhyte'"),
                CancellationToken.None);

            result.IsError.Should().BeFalse(result.Text);
            capture.Last!.QueryOptions["filter"].Should().Be("UserName eq 'russellwhyte'");
            ODataFeedReader.ReadStrings(result.StructuredContent!, "UserName", "userName").Should().Equal("russellwhyte");
        }

        /// <summary>
        /// Expand Friends matches HTTP.
        /// </summary>
        [TestMethod]
        public async Task OdataQuery_TripPin_ExpandFriends_MatchHttp()
        {
            var (runtime, capture) = await LiveToolRuntime.CreateTripPinAsync();
            var result = await runtime.InvokeAsync(
                "odata_query",
                ToolArguments.Of("entitySet", "People", "expand", "Friends", "filter", "UserName eq 'russellwhyte'"),
                CancellationToken.None);
            var odata = await TwinGetAsync("People?$expand=Friends&$filter=UserName eq 'russellwhyte'");

            result.IsError.Should().BeFalse(result.Text);
            capture.Last!.QueryOptions["expand"].Should().Be("Friends");
            result.StructuredContent.Should().Contain("Friends");
            odata.Should().Contain("Friends");
        }

        /// <summary>
        /// Orderby UserName skip/top/count.
        /// </summary>
        [TestMethod]
        public async Task OdataQuery_TripPin_OrderbyUserNameSkipTopCount()
        {
            var (runtime, capture) = await LiveToolRuntime.CreateTripPinAsync();
            var result = await runtime.InvokeAsync(
                "odata_query",
                ToolArguments.Of("entitySet", "People", "orderby", "UserName", "skip", 1, "top", 2, "count", true),
                CancellationToken.None);
            var odata = await TwinGetAsync("People?$orderby=UserName&$skip=1&$top=2&$count=true");

            result.IsError.Should().BeFalse(result.Text);
            capture.Last!.QueryOptions["skip"].Should().Be("1");
            ODataFeedReader.ReadStrings(result.StructuredContent!, "UserName", "userName")
                .Should()
                .Equal(ODataFeedReader.ReadStrings(odata, "UserName", "userName"));
            ODataFeedReader.ReadCount(result.StructuredContent!).Should().Be(ODataFeedReader.ReadCount(odata));
        }

        /// <summary>
        /// People top 2 matches HTTP.
        /// </summary>
        [TestMethod]
        public async Task OdataQuery_TripPin_PeopleTop2_MatchHttp()
        {
            var (runtime, capture) = await LiveToolRuntime.CreateTripPinAsync();
            var result = await runtime.InvokeAsync(
                "odata_query",
                ToolArguments.Of("entitySet", "People", "top", 2, "orderby", "UserName"),
                CancellationToken.None);
            var odata = await TwinGetAsync("People?$top=2&$orderby=UserName");

            result.IsError.Should().BeFalse(result.Text);
            capture.Last!.QueryOptions["top"].Should().Be("2");
            ODataFeedReader.ReadStrings(result.StructuredContent!, "UserName", "userName")
                .Should()
                .Equal(ODataFeedReader.ReadStrings(odata, "UserName", "userName"))
                .And
                .HaveCount(2);
        }

        /// <summary>
        /// Unknown TripPin sets 404.
        /// </summary>
        [TestMethod]
        public async Task OdataQuery_TripPin_UnknownSet_404()
        {
            var (runtime, _) = await LiveToolRuntime.CreateTripPinAsync();
            var result = await runtime.InvokeAsync("odata_query", ToolArguments.Of("entitySet", "DoesNotExist"), CancellationToken.None);

            result.IsError.Should().BeTrue(result.Text);
            result.Text.Should().Contain("OData request failed with status");
            result.Text.Should().MatchRegex(@"status (404|500)");
        }

        #endregion

        #region Internal Methods

        /// <summary>
        /// Asserts an OData HTTP failure with a status in the text.
        /// </summary>
        /// <param name="result">The tool result.</param>
        internal static void AssertStatusError(ODataToolInvocationResult result)
        {
            result.IsError.Should().BeTrue(result.Text);
            result.Text.Should().Contain("OData request failed with status");
        }

        /// <summary>
        /// GETs a TripPin relative URL as the HTTP twin, following the session redirect.
        /// </summary>
        /// <param name="relative">Path and query.</param>
        /// <returns>
        /// Response body.
        /// </returns>
        internal static async Task<string> TwinGetAsync(string relative)
        {
            using var http = new HttpClient();
            http.DefaultRequestHeaders.Accept.ParseAdd("application/json");

            return await http.GetStringAsync($"{LiveOData.TripPin.TrimEnd('/')}/{relative.TrimStart('/')}");
        }

        #endregion

    }

}
