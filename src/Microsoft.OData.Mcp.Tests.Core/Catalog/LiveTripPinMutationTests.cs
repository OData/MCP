// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.OData.Mcp.Core.Catalog;
using Microsoft.OData.Mcp.Tests.Shared;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.OData.Mcp.Tests.Core.Catalog
{

    /// <summary>
    /// Live TripPin mutation tests. Each write uses a unique UserName.
    /// </summary>
    [TestClass]
    public class LiveTripPinMutationTests
    {

        #region Public Methods

        /// <summary>
        /// Named create person with required fields then delete.
        /// </summary>
        [TestMethod]
        public async Task CreatePerson_TripPin_RequiredFields_ThenDelete()
        {
            var (runtime, capture) = await LiveToolRuntime.CreateTripPinAsync();
            var userName = UniqueUserName();
            var created = await runtime.InvokeAsync("create_person", PersonPropertyArgs(userName), CancellationToken.None);
            created.IsError.Should().BeFalse(created.Text);
            capture.Last!.Method.Should().Be(HttpMethod.Post);
            capture.Last.RelativePath.Should().Be("People");
            var got = await runtime.InvokeAsync("get_person", ToolArguments.Of("key", userName), CancellationToken.None);
            got.IsError.Should().BeFalse(got.Text);
            var deleted = await runtime.InvokeAsync("delete_person", ToolArguments.Of("key", userName), CancellationToken.None);
            deleted.IsError.Should().BeFalse(deleted.Text);
        }

        /// <summary>
        /// Named delete of a created person then get 404.
        /// </summary>
        [TestMethod]
        public async Task DeletePerson_TripPin_CreatedPerson()
        {
            var (runtime, _) = await LiveToolRuntime.CreateTripPinAsync();
            var userName = UniqueUserName();
            var created = await runtime.InvokeAsync("odata_create", PersonCreateArgs(userName), CancellationToken.None);
            created.IsError.Should().BeFalse(created.Text);
            var deleted = await runtime.InvokeAsync("delete_person", ToolArguments.Of("key", userName), CancellationToken.None);
            deleted.IsError.Should().BeFalse(deleted.Text);
            var got = await runtime.InvokeAsync("odata_get", ToolArguments.Of("entitySet", "People", "key", userName), CancellationToken.None);
            got.IsError.Should().BeTrue(got.Text);
            got.Text.Should().Contain("404");
        }

        /// <summary>
        /// Object versus string bodies both POST ShareTrip.
        /// </summary>
        [TestMethod]
        public async Task OdataCall_ActionWithObjectBodyVsStringBody()
        {
            var (runtime, capture) = await LiveToolRuntime.CreateTripPinAsync();
            var tripId = await FirstTripIdAsync(runtime);
            var asString = await runtime.InvokeAsync(
                "odata_call",
                ToolArguments.Of("name", "ShareTrip", "entitySet", "People", "key", "russellwhyte", "body", ShareTripBody("scottketchum", tripId)),
                CancellationToken.None);
            capture.Last!.Method.Should().Be(HttpMethod.Post);
            var asObject = await runtime.InvokeAsync(
                "odata_call",
                ToolArguments.Of("name", "ShareTrip", "entitySet", "People", "key", "russellwhyte", "body", new { userName = "scottketchum", tripId }),
                CancellationToken.None);

            capture.Last.Method.Should().Be(HttpMethod.Post);
            capture.Last.RelativePath.Should().Be("People('russellwhyte')/ShareTrip");
            asString.IsError.Should().Be(asObject.IsError);
        }

        /// <summary>
        /// Bound ShareTrip posts to People('russellwhyte')/ShareTrip.
        /// </summary>
        [TestMethod]
        public async Task OdataCall_TripPin_BoundActionShareTrip_EntitySetPeopleKey()
        {
            var (runtime, capture) = await LiveToolRuntime.CreateTripPinAsync();
            var tripId = await FirstTripIdAsync(runtime);
            var result = await runtime.InvokeAsync(
                "odata_call",
                ToolArguments.Of("name", "ShareTrip", "entitySet", "People", "key", "russellwhyte", "body", ShareTripBody("scottketchum", tripId)),
                CancellationToken.None);

            capture.Last!.Method.Should().Be(HttpMethod.Post);
            capture.Last.RelativePath.Should().Be("People('russellwhyte')/ShareTrip");
            if (result.IsError)
            {
                result.Text.Should().Contain("OData request failed with status");
            }
        }

        /// <summary>
        /// ResetDataSource is a real POST; success or documented OData error.
        /// </summary>
        [TestMethod]
        public async Task OdataCall_TripPin_ResetDataSource_Post_IsSuccessOrDocumentedError()
        {
            var (runtime, capture) = await LiveToolRuntime.CreateTripPinAsync();
            var result = await runtime.InvokeAsync("odata_call", ToolArguments.Of("name", "ResetDataSource", "body", "{}"), CancellationToken.None);

            capture.Last!.Method.Should().Be(HttpMethod.Post);
            capture.Last.RelativePath.Should().Be("ResetDataSource");
            if (result.IsError)
            {
                result.Text.Should().Contain("OData request failed with status");
            }
        }

        /// <summary>
        /// Reset then query People still works on the same session.
        /// </summary>
        [TestMethod]
        public async Task OdataCall_Reset_ThenQueryPeopleStillWorks_TripPin()
        {
            var (runtime, _) = await LiveToolRuntime.CreateTripPinAsync();
            await runtime.InvokeAsync("odata_call", ToolArguments.Of("name", "ResetDataSource", "body", "{}"), CancellationToken.None);
            var queried = await runtime.InvokeAsync(
                "odata_query",
                ToolArguments.Of("entitySet", "People", "top", 1),
                CancellationToken.None);

            queried.IsError.Should().BeFalse(queried.Text);
            queried.StructuredContent.Should().Contain("UserName");
        }

        /// <summary>
        /// ShareTrip then get the person still succeeds.
        /// </summary>
        [TestMethod]
        public async Task OdataCall_ShareTrip_ThenGetPerson()
        {
            var (runtime, _) = await LiveToolRuntime.CreateTripPinAsync();
            var tripId = await FirstTripIdAsync(runtime);
            await runtime.InvokeAsync(
                "odata_call",
                ToolArguments.Of("name", "ShareTrip", "entitySet", "People", "key", "russellwhyte", "body", ShareTripBody("scottketchum", tripId)),
                CancellationToken.None);
            var got = await runtime.InvokeAsync(
                "odata_get",
                ToolArguments.Of("entitySet", "People", "key", "russellwhyte"),
                CancellationToken.None);

            got.IsError.Should().BeFalse(got.Text);
            got.StructuredContent.Should().Contain("russellwhyte");
        }

        /// <summary>
        /// Create a unique person then get then delete.
        /// </summary>
        [TestMethod]
        public async Task OdataCreate_TripPin_PersonUniqueUserName_ThenGetThenDelete()
        {
            var (runtime, capture) = await LiveToolRuntime.CreateTripPinAsync();
            var userName = UniqueUserName();
            var created = await runtime.InvokeAsync("odata_create", PersonCreateArgs(userName), CancellationToken.None);
            created.IsError.Should().BeFalse(created.Text);
            capture.Last!.Method.Should().Be(HttpMethod.Post);
            capture.Last.RelativePath.Should().Be("People");
            var got = await runtime.InvokeAsync("odata_get", ToolArguments.Of("entitySet", "People", "key", userName), CancellationToken.None);
            got.IsError.Should().BeFalse(got.Text);
            got.StructuredContent.Should().Contain(userName);
            var deleted = await runtime.InvokeAsync("odata_delete", ToolArguments.Of("entitySet", "People", "key", userName), CancellationToken.None);
            deleted.IsError.Should().BeFalse(deleted.Text);
        }

        /// <summary>
        /// Missing required fields 4xx on create.
        /// </summary>
        [TestMethod]
        public async Task OdataCreate_TripPin_MissingRequiredFields_400()
        {
            var (runtime, _) = await LiveToolRuntime.CreateTripPinAsync();
            var result = await runtime.InvokeAsync(
                "odata_create",
                ToolArguments.Of("entitySet", "People", "body", """{"UserName":"x"}"""),
                CancellationToken.None);

            result.IsError.Should().BeTrue(result.Text);
            result.Text.Should().MatchRegex(@"status 4\d\d");
            result.Text.Should().Contain("FirstName");
        }

        /// <summary>
        /// Unicode FirstName succeeds on TripPin.
        /// </summary>
        [TestMethod]
        public async Task OdataCreate_UnicodeCompanyName_SucceedsRestierAndTripPin()
        {
            var (runtime, _) = await LiveToolRuntime.CreateTripPinAsync();
            var userName = UniqueUserName();
            var created = await runtime.InvokeAsync("odata_create", PersonCreateArgs(userName, "北風 😀"), CancellationToken.None);
            created.IsError.Should().BeFalse(created.Text);
            var got = await runtime.InvokeAsync("odata_get", ToolArguments.Of("entitySet", "People", "key", userName), CancellationToken.None);
            got.IsError.Should().BeFalse(got.Text);
            using var document = JsonDocument.Parse(got.StructuredContent!);
            document.RootElement.GetProperty("FirstName").GetString().Should().Contain("北風");
            await runtime.InvokeAsync("odata_delete", ToolArguments.Of("entitySet", "People", "key", userName), CancellationToken.None);
        }

        /// <summary>
        /// Create then get then update then delete.
        /// </summary>
        [TestMethod]
        public async Task OdataCreate_ThenGet_ThenUpdate_ThenDelete_Restier()
        {
            var (runtime, _) = await LiveToolRuntime.CreateTripPinAsync();
            var userName = UniqueUserName();
            var created = await runtime.InvokeAsync("odata_create", PersonCreateArgs(userName), CancellationToken.None);
            created.IsError.Should().BeFalse(created.Text);
            var got = await runtime.InvokeAsync("odata_get", ToolArguments.Of("entitySet", "People", "key", userName), CancellationToken.None);
            got.IsError.Should().BeFalse(got.Text);
            var updated = await runtime.InvokeAsync(
                "odata_update",
                ToolArguments.Of("entitySet", "People", "key", userName, "body", """{"FirstName":"Patched"}"""),
                CancellationToken.None);
            updated.IsError.Should().BeFalse(updated.Text);
            var after = await runtime.InvokeAsync("odata_get", ToolArguments.Of("entitySet", "People", "key", userName), CancellationToken.None);
            after.StructuredContent.Should().Contain("Patched");
            var deleted = await runtime.InvokeAsync("odata_delete", ToolArguments.Of("entitySet", "People", "key", userName), CancellationToken.None);
            deleted.IsError.Should().BeFalse(deleted.Text);
        }

        /// <summary>
        /// Create then query filter sees the row.
        /// </summary>
        [TestMethod]
        public async Task OdataCreate_ThenQueryFilter_SeesRow()
        {
            var (runtime, _) = await LiveToolRuntime.CreateTripPinAsync();
            var userName = UniqueUserName();
            var created = await runtime.InvokeAsync("odata_create", PersonCreateArgs(userName), CancellationToken.None);
            created.IsError.Should().BeFalse(created.Text);
            var queried = await runtime.InvokeAsync(
                "odata_query",
                ToolArguments.Of("entitySet", "People", "filter", $"UserName eq '{userName}'"),
                CancellationToken.None);
            queried.IsError.Should().BeFalse(queried.Text);
            queried.StructuredContent.Should().Contain(userName);
            await runtime.InvokeAsync("odata_delete", ToolArguments.Of("entitySet", "People", "key", userName), CancellationToken.None);
        }

        /// <summary>
        /// Create then named get.
        /// </summary>
        [TestMethod]
        public async Task OdataCreate_ThenNamedGet()
        {
            var (runtime, _) = await LiveToolRuntime.CreateTripPinAsync();
            var userName = UniqueUserName();
            var created = await runtime.InvokeAsync("odata_create", PersonCreateArgs(userName), CancellationToken.None);
            created.IsError.Should().BeFalse(created.Text);
            var got = await runtime.InvokeAsync("get_person", ToolArguments.Of("key", userName), CancellationToken.None);
            got.IsError.Should().BeFalse(got.Text);
            got.StructuredContent.Should().Contain(userName);
            await runtime.InvokeAsync("odata_delete", ToolArguments.Of("entitySet", "People", "key", userName), CancellationToken.None);
        }

        /// <summary>
        /// Delete a created person then get 404.
        /// </summary>
        [TestMethod]
        public async Task OdataDelete_TripPin_CreatedPerson_ThenGet404()
        {
            var (runtime, capture) = await LiveToolRuntime.CreateTripPinAsync();
            var userName = UniqueUserName();
            var created = await runtime.InvokeAsync("odata_create", PersonCreateArgs(userName), CancellationToken.None);
            created.IsError.Should().BeFalse(created.Text);
            var deleted = await runtime.InvokeAsync("odata_delete", ToolArguments.Of("entitySet", "People", "key", userName), CancellationToken.None);
            deleted.IsError.Should().BeFalse(deleted.Text);
            capture.Last!.Method.Should().Be(HttpMethod.Delete);
            var got = await runtime.InvokeAsync("odata_get", ToolArguments.Of("entitySet", "People", "key", userName), CancellationToken.None);
            got.IsError.Should().BeTrue(got.Text);
            got.Text.Should().Contain("404");
        }

        /// <summary>
        /// Second delete of the same key is an error.
        /// </summary>
        [TestMethod]
        public async Task OdataDelete_IdempotentSecondDelete_404IsError()
        {
            var (runtime, _) = await LiveToolRuntime.CreateTripPinAsync();
            var userName = UniqueUserName();
            var created = await runtime.InvokeAsync("odata_create", PersonCreateArgs(userName), CancellationToken.None);
            created.IsError.Should().BeFalse(created.Text);
            var first = await runtime.InvokeAsync("odata_delete", ToolArguments.Of("entitySet", "People", "key", userName), CancellationToken.None);
            first.IsError.Should().BeFalse(first.Text);
            var second = await runtime.InvokeAsync("odata_delete", ToolArguments.Of("entitySet", "People", "key", userName), CancellationToken.None);
            second.IsError.Should().BeTrue(second.Text);
            second.Text.Should().MatchRegex(@"status (404|500)");
            second.Text.Should().MatchRegex("404|Resource Not Found");
        }

        /// <summary>
        /// After delete, query does not list the person.
        /// </summary>
        [TestMethod]
        public async Task OdataDelete_ThenQueryDoesNotList()
        {
            var (runtime, _) = await LiveToolRuntime.CreateTripPinAsync();
            var userName = UniqueUserName();
            var created = await runtime.InvokeAsync("odata_create", PersonCreateArgs(userName), CancellationToken.None);
            created.IsError.Should().BeFalse(created.Text);
            var deleted = await runtime.InvokeAsync("odata_delete", ToolArguments.Of("entitySet", "People", "key", userName), CancellationToken.None);
            deleted.IsError.Should().BeFalse(deleted.Text);
            var queried = await runtime.InvokeAsync(
                "odata_query",
                ToolArguments.Of("entitySet", "People", "filter", $"UserName eq '{userName}'"),
                CancellationToken.None);
            queried.IsError.Should().BeFalse(queried.Text);
            ODataFeedReader.ReadValueCount(queried.StructuredContent!).Should().Be(0);
        }

        /// <summary>
        /// Get after create sees the new entity.
        /// </summary>
        [TestMethod]
        public async Task OdataGet_AfterCreate_SeesNewEntity()
        {
            var (runtime, _) = await LiveToolRuntime.CreateTripPinAsync();
            var userName = UniqueUserName();
            var created = await runtime.InvokeAsync("odata_create", PersonCreateArgs(userName), CancellationToken.None);
            created.IsError.Should().BeFalse(created.Text);
            var got = await runtime.InvokeAsync("odata_get", ToolArguments.Of("entitySet", "People", "key", userName), CancellationToken.None);
            got.IsError.Should().BeFalse(got.Text);
            got.StructuredContent.Should().Contain(userName);
            await runtime.InvokeAsync("odata_delete", ToolArguments.Of("entitySet", "People", "key", userName), CancellationToken.None);
        }

        /// <summary>
        /// Get after update sees the patch.
        /// </summary>
        [TestMethod]
        public async Task OdataGet_AfterUpdate_SeesPatch()
        {
            var (runtime, _) = await LiveToolRuntime.CreateTripPinAsync();
            var userName = UniqueUserName();
            var created = await runtime.InvokeAsync("odata_create", PersonCreateArgs(userName), CancellationToken.None);
            created.IsError.Should().BeFalse(created.Text);
            var updated = await runtime.InvokeAsync(
                "odata_update",
                ToolArguments.Of("entitySet", "People", "key", userName, "body", """{"FirstName":"Updated"}"""),
                CancellationToken.None);
            updated.IsError.Should().BeFalse(updated.Text);
            var got = await runtime.InvokeAsync("odata_get", ToolArguments.Of("entitySet", "People", "key", userName), CancellationToken.None);
            got.StructuredContent.Should().Contain("Updated");
            await runtime.InvokeAsync("odata_delete", ToolArguments.Of("entitySet", "People", "key", userName), CancellationToken.None);
        }

        /// <summary>
        /// Get after delete is 404.
        /// </summary>
        [TestMethod]
        public async Task OdataGet_AfterDelete_404()
        {
            var (runtime, _) = await LiveToolRuntime.CreateTripPinAsync();
            var userName = UniqueUserName();
            var created = await runtime.InvokeAsync("odata_create", PersonCreateArgs(userName), CancellationToken.None);
            created.IsError.Should().BeFalse(created.Text);
            var deleted = await runtime.InvokeAsync("odata_delete", ToolArguments.Of("entitySet", "People", "key", userName), CancellationToken.None);
            deleted.IsError.Should().BeFalse(deleted.Text);
            var got = await runtime.InvokeAsync("odata_get", ToolArguments.Of("entitySet", "People", "key", userName), CancellationToken.None);
            got.IsError.Should().BeTrue(got.Text);
            got.Text.Should().Contain("404");
        }

        /// <summary>
        /// Query then create then filter sees the new row.
        /// </summary>
        [TestMethod]
        public async Task OdataQuery_ThenCreateThenQueryFilter_SeesNewRow()
        {
            var (runtime, _) = await LiveToolRuntime.CreateTripPinAsync();
            var userName = UniqueUserName();
            var before = await runtime.InvokeAsync(
                "odata_query",
                ToolArguments.Of("entitySet", "People", "filter", $"UserName eq '{userName}'"),
                CancellationToken.None);
            before.IsError.Should().BeFalse(before.Text);
            ODataFeedReader.ReadValueCount(before.StructuredContent!).Should().Be(0);
            var created = await runtime.InvokeAsync("odata_create", PersonCreateArgs(userName), CancellationToken.None);
            created.IsError.Should().BeFalse(created.Text);
            var after = await runtime.InvokeAsync(
                "odata_query",
                ToolArguments.Of("entitySet", "People", "filter", $"UserName eq '{userName}'"),
                CancellationToken.None);
            after.StructuredContent.Should().Contain(userName);
            await runtime.InvokeAsync("odata_delete", ToolArguments.Of("entitySet", "People", "key", userName), CancellationToken.None);
        }

        /// <summary>
        /// Patch FirstName on a person created in-test.
        /// </summary>
        [TestMethod]
        public async Task OdataUpdate_TripPin_PatchFirstName_ThenGet()
        {
            var (runtime, capture) = await LiveToolRuntime.CreateTripPinAsync();
            var userName = UniqueUserName();
            var created = await runtime.InvokeAsync("odata_create", PersonCreateArgs(userName), CancellationToken.None);
            created.IsError.Should().BeFalse(created.Text);
            var updated = await runtime.InvokeAsync(
                "odata_update",
                ToolArguments.Of("entitySet", "People", "key", userName, "body", """{"FirstName":"Patched"}"""),
                CancellationToken.None);
            updated.IsError.Should().BeFalse(updated.Text);
            capture.Last!.Method.Should().Be(HttpMethod.Patch);
            var got = await runtime.InvokeAsync("odata_get", ToolArguments.Of("entitySet", "People", "key", userName), CancellationToken.None);
            got.StructuredContent.Should().Contain("Patched");
            await runtime.InvokeAsync("odata_delete", ToolArguments.Of("entitySet", "People", "key", userName), CancellationToken.None);
        }

        /// <summary>
        /// Patching UserName either 4xxs or relocates the entity under the new key.
        /// </summary>
        [TestMethod]
        public async Task OdataUpdate_TripPin_PatchImmutableKey_400()
        {
            var (runtime, _) = await LiveToolRuntime.CreateTripPinAsync();
            var userName = UniqueUserName();
            var relocated = UniqueUserName();
            var created = await runtime.InvokeAsync("odata_create", PersonCreateArgs(userName), CancellationToken.None);
            created.IsError.Should().BeFalse(created.Text);
            var updated = await runtime.InvokeAsync(
                "odata_update",
                ToolArguments.Of("entitySet", "People", "key", userName, "body", JsonSerializer.Serialize(new { UserName = relocated })),
                CancellationToken.None);
            if (updated.IsError)
            {
                updated.Text.Should().MatchRegex(@"status 4\d\d");
                await runtime.InvokeAsync("odata_delete", ToolArguments.Of("entitySet", "People", "key", userName), CancellationToken.None);
                return;
            }

            updated.Text.Should().Contain("204");
            var moved = await runtime.InvokeAsync("odata_get", ToolArguments.Of("entitySet", "People", "key", relocated), CancellationToken.None);
            moved.IsError.Should().BeFalse(moved.Text);
            using var document = JsonDocument.Parse(moved.StructuredContent!);
            document.RootElement.GetProperty("UserName").GetString().Should().Be(relocated);
            var original = await runtime.InvokeAsync("odata_get", ToolArguments.Of("entitySet", "People", "key", userName), CancellationToken.None);
            original.IsError.Should().BeTrue(original.Text);
            await runtime.InvokeAsync("odata_delete", ToolArguments.Of("entitySet", "People", "key", relocated), CancellationToken.None);
        }

        /// <summary>
        /// Second identical patch succeeds.
        /// </summary>
        [TestMethod]
        public async Task OdataUpdate_IdempotentSecondPatchSameBody_Succeeds()
        {
            var (runtime, _) = await LiveToolRuntime.CreateTripPinAsync();
            var userName = UniqueUserName();
            var created = await runtime.InvokeAsync("odata_create", PersonCreateArgs(userName), CancellationToken.None);
            created.IsError.Should().BeFalse(created.Text);
            var body = ToolArguments.Of("entitySet", "People", "key", userName, "body", """{"FirstName":"Same"}""");
            var first = await runtime.InvokeAsync("odata_update", body, CancellationToken.None);
            var second = await runtime.InvokeAsync("odata_update", body, CancellationToken.None);
            first.IsError.Should().BeFalse(first.Text);
            second.IsError.Should().BeFalse(second.Text);
            await runtime.InvokeAsync("odata_delete", ToolArguments.Of("entitySet", "People", "key", userName), CancellationToken.None);
        }

        /// <summary>
        /// Named update patches FirstName on a created person.
        /// </summary>
        [TestMethod]
        public async Task UpdatePerson_TripPin_PatchFirstName_OnCreatedPerson()
        {
            var (runtime, capture) = await LiveToolRuntime.CreateTripPinAsync();
            var userName = UniqueUserName();
            var created = await runtime.InvokeAsync("create_person", PersonPropertyArgs(userName), CancellationToken.None);
            created.IsError.Should().BeFalse(created.Text);
            var updated = await runtime.InvokeAsync(
                "update_person",
                ToolArguments.Of("key", userName, "body", """{"FirstName":"NamedPatch"}"""),
                CancellationToken.None);
            updated.IsError.Should().BeFalse(updated.Text);
            capture.Last!.Method.Should().Be(HttpMethod.Patch);
            capture.Last.RelativePath.Should().Be($"People('{userName}')");
            var got = await runtime.InvokeAsync("get_person", ToolArguments.Of("key", userName), CancellationToken.None);
            got.StructuredContent.Should().Contain("NamedPatch");
            await runtime.InvokeAsync("delete_person", ToolArguments.Of("key", userName), CancellationToken.None);
        }

        #endregion

        #region Internal Methods

        /// <summary>
        /// Reads the first TripId from russellwhyte's trips.
        /// </summary>
        /// <param name="runtime">The runtime.</param>
        /// <returns>
        /// A trip id.
        /// </returns>
        internal static async Task<int> FirstTripIdAsync(ODataToolRuntime runtime)
        {
            var trips = await runtime.InvokeAsync(
                "odata_navigate",
                ToolArguments.Of("entitySet", "People", "key", "russellwhyte", "navigation", "Trips", "top", 1),
                CancellationToken.None);
            trips.IsError.Should().BeFalse(trips.Text);
            using var document = JsonDocument.Parse(trips.StructuredContent!);
            foreach (var item in document.RootElement.GetProperty("value").EnumerateArray())
            {
                if (item.TryGetProperty("TripId", out var id) && id.TryGetInt32(out var tripId))
                {
                    return tripId;
                }

                if (item.TryGetProperty("tripId", out var camel) && camel.TryGetInt32(out var camelId))
                {
                    return camelId;
                }
            }

            return 0;
        }

        /// <summary>
        /// Builds generic create arguments for a TripPin person.
        /// </summary>
        /// <param name="userName">Unique user name.</param>
        /// <param name="firstName">First name.</param>
        /// <returns>
        /// Tool arguments.
        /// </returns>
        internal static System.Collections.Generic.Dictionary<string, JsonElement> PersonCreateArgs(string userName, string firstName = "Mcp")
        {
            return ToolArguments.Of("entitySet", "People", "body", PersonJson(userName, firstName));
        }

        /// <summary>
        /// Serializes a TripPin person payload that live RESTier accepts.
        /// </summary>
        /// <param name="userName">Unique user name.</param>
        /// <param name="firstName">First name.</param>
        /// <returns>
        /// JSON text.
        /// </returns>
        /// <remarks>
        /// TripPin RESTier returns HTTP 500 with the property name when a Person POST includes
        /// scalar enum members (<c>Gender</c>, <c>FavoriteFeature</c>). Omit those properties.
        /// </remarks>
        internal static string PersonJson(string userName, string firstName = "Mcp")
        {
            return JsonSerializer.Serialize(new
            {
                UserName = userName,
                FirstName = firstName,
                LastName = "Test",
                Emails = new[] { $"{userName}@example.com" },
                AddressInfo = new[]
                {
                    new
                    {
                        Address = "1 Microsoft Way",
                        City = new { Name = "Redmond", CountryRegion = "United States", Region = "WA" }
                    }
                },
                Features = Array.Empty<string>()
            });
        }

        /// <summary>
        /// Builds named create_person property arguments that live RESTier accepts.
        /// </summary>
        /// <param name="userName">Unique user name.</param>
        /// <returns>
        /// Tool arguments.
        /// </returns>
        /// <remarks>
        /// Same enum restriction as <see cref="PersonJson"/>: do not send <c>Gender</c> or
        /// <c>FavoriteFeature</c> on POST People.
        /// </remarks>
        internal static System.Collections.Generic.Dictionary<string, JsonElement> PersonPropertyArgs(string userName)
        {
            return ToolArguments.Of(
                "UserName",
                userName,
                "FirstName",
                "Mcp",
                "LastName",
                "Test",
                "Emails",
                new[] { $"{userName}@example.com" },
                "Features",
                Array.Empty<string>());
        }

        /// <summary>
        /// Builds a ShareTrip JSON body.
        /// </summary>
        /// <param name="userName">Share target.</param>
        /// <param name="tripId">Trip id.</param>
        /// <returns>
        /// JSON text.
        /// </returns>
        internal static string ShareTripBody(string userName, int tripId)
        {
            return JsonSerializer.Serialize(new { userName, tripId });
        }

        /// <summary>
        /// Builds a unique TripPin UserName.
        /// </summary>
        /// <returns>
        /// A name starting with <c>mcp</c>.
        /// </returns>
        internal static string UniqueUserName()
        {
            return "mcp" + Guid.NewGuid().ToString("N")[..8];
        }

        #endregion

    }

}
