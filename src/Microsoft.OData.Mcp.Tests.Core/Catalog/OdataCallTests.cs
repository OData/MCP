// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.OData.Mcp.Core.Catalog;
using Microsoft.OData.Mcp.Core.Parsing;
using Microsoft.OData.Mcp.Tests.Shared;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.OData.Mcp.Tests.Core.Catalog
{

    /// <summary>
    /// <c>odata_call</c> takes arguments in a <c>parameters</c> object and validates them against the declared signature before HTTP.
    /// </summary>
    [TestClass]
    public class OdataCallTests
    {

        #region Public Methods

        /// <summary>
        /// Unbound function arguments map onto the URL with typed literals; strings are quoted, numbers are not.
        /// </summary>
        [TestMethod]
        public async Task Call_UnboundFunction_ParametersOnUrl()
        {
            var (runtime, executor) = Runtime(EdmTypeShapeTests.OperationsCsdl);
            var result = await runtime.InvokeAsync("odata_call", ToolArguments.Of("name", "GetNearestAirport", "parameters", new { lat = 47.6 }), CancellationToken.None);

            result.IsError.Should().BeFalse(result.Text);
            executor.Last!.Method.Should().Be(HttpMethod.Get);
            executor.Last.RelativePath.Should().Be("GetNearestAirport(lat=47.6)");
            executor.Last.JsonBody.Should().BeNull();
        }

        /// <summary>
        /// Unbound action arguments become the POST body; enum numbers are normalized to member names.
        /// </summary>
        [TestMethod]
        public async Task Call_UnboundAction_ParametersAsBody()
        {
            var (runtime, executor) = Runtime(OdataDescribeModelTests.ShopCsdl);
            var result = await runtime.InvokeAsync("odata_call", ToolArguments.Of("name", "Reset", "parameters", new { hard = true }), CancellationToken.None);
            result.IsError.Should().BeFalse(result.Text);
            executor.Last!.Method.Should().Be(HttpMethod.Post);
            executor.Last.RelativePath.Should().Be("Reset");
            executor.Last.JsonBody.Should().Be("""{"hard":true}""");

            var empty = await runtime.InvokeAsync("odata_call", ToolArguments.Of("name", "Reset"), CancellationToken.None);
            empty.IsError.Should().BeFalse(empty.Text);
            executor.Last!.JsonBody.Should().Be("{}");
        }

        /// <summary>
        /// Instance-bound operations need entitySet and key; the binding parameter is never an argument.
        /// </summary>
        [TestMethod]
        public async Task Call_InstanceBound_RequiresEntitySetAndKey()
        {
            var (runtime, executor) = Runtime(EdmTypeShapeTests.OperationsCsdl);
            var missing = await runtime.InvokeAsync("odata_call", ToolArguments.Of("name", "UpdateLastName", "parameters", new { lastName = "Whyte" }), CancellationToken.None);
            var noKey = await runtime.InvokeAsync("odata_call", ToolArguments.Of("name", "UpdateLastName", "entitySet", "People", "parameters", new { lastName = "Whyte" }), CancellationToken.None);
            var bindingArgument = await runtime.InvokeAsync("odata_call", ToolArguments.Of("name", "UpdateLastName", "entitySet", "People", "key", "russellwhyte", "parameters", new { person = "x", lastName = "Whyte" }), CancellationToken.None);
            var ok = await runtime.InvokeAsync("odata_call", ToolArguments.Of("name", "UpdateLastName", "entitySet", "People", "key", "russellwhyte", "parameters", new { lastName = "Whyte" }), CancellationToken.None);

            missing.IsError.Should().BeTrue();
            missing.Text.Should().Be("UpdateLastName is bound to Person. Pass entitySet and key. Signature: UpdateLastName(lastName: string) -> bool // writes");
            noKey.IsError.Should().BeTrue();
            noKey.Text.Should().Contain("Pass entitySet and key");
            bindingArgument.IsError.Should().BeTrue();
            bindingArgument.Text.Should().Be("Unknown parameter 'person'. Declared: lastName.");
            ok.IsError.Should().BeFalse(ok.Text);
            executor.Last!.Method.Should().Be(HttpMethod.Post);
            executor.Last.RelativePath.Should().Be("People('russellwhyte')/UpdateLastName");
            executor.Last.JsonBody.Should().Be("""{"lastName":"Whyte"}""");
        }

        /// <summary>
        /// Collection-bound operations take entitySet only.
        /// </summary>
        [TestMethod]
        public async Task Call_CollectionBound_EntitySetOnly()
        {
            var (runtime, executor) = Runtime(EdmTypeShapeTests.OperationsCsdl);
            var withKey = await runtime.InvokeAsync("odata_call", ToolArguments.Of("name", "Top", "entitySet", "People", "key", "x", "parameters", new { count = 2 }), CancellationToken.None);
            var noSet = await runtime.InvokeAsync("odata_call", ToolArguments.Of("name", "Top", "parameters", new { count = 2 }), CancellationToken.None);
            var ok = await runtime.InvokeAsync("odata_call", ToolArguments.Of("name", "Top", "entitySet", "People", "parameters", new { count = 2 }), CancellationToken.None);

            withKey.IsError.Should().BeTrue();
            withKey.Text.Should().StartWith("Top is collection-bound. Pass entitySet; omit key.");
            noSet.IsError.Should().BeTrue();
            noSet.Text.Should().StartWith("Top is collection-bound. Pass entitySet; omit key.");
            ok.IsError.Should().BeFalse(ok.Text);
            executor.Last!.RelativePath.Should().Be("People/Top(count=2)");
        }

        /// <summary>
        /// Unbound operations reject entitySet and key instead of silently ignoring them.
        /// </summary>
        [TestMethod]
        public async Task Call_UnboundWithEntitySetOrKey_IsError()
        {
            var (runtime, executor) = Runtime(EdmTypeShapeTests.OperationsCsdl);
            var result = await runtime.InvokeAsync("odata_call", ToolArguments.Of("name", "ResetDataSource", "entitySet", "People", "key", "x"), CancellationToken.None);

            result.IsError.Should().BeTrue();
            result.Text.Should().Be("ResetDataSource is unbound. Omit entitySet and key.");
            executor.Last.Should().BeNull();
        }

        /// <summary>
        /// Missing, unknown, and wrongly typed parameters fail before HTTP with the signature.
        /// </summary>
        [TestMethod]
        public async Task Call_ParameterValidation_FailsBeforeHttp()
        {
            var (runtime, executor) = Runtime(EdmTypeShapeTests.OperationsCsdl);
            var missing = await runtime.InvokeAsync("odata_call", ToolArguments.Of("name", "GetNearestAirport", "parameters", new { }), CancellationToken.None);
            var unknown = await runtime.InvokeAsync("odata_call", ToolArguments.Of("name", "GetNearestAirport", "parameters", new { lat = 1.0, foo = 1 }), CancellationToken.None);
            var wrongKind = await runtime.InvokeAsync("odata_call", ToolArguments.Of("name", "GetNearestAirport", "parameters", new { lat = "north" }), CancellationToken.None);
            var topLevel = await runtime.InvokeAsync("odata_call", ToolArguments.Of("name", "GetNearestAirport", "lat", 1.0), CancellationToken.None);
            var stringified = await runtime.InvokeAsync("odata_call", ToolArguments.Of("name", "GetNearestAirport", "parameters", "{\"lat\":1}"), CancellationToken.None);
            var body = await runtime.InvokeAsync("odata_call", ToolArguments.Of("name", "ResetDataSource", "body", "{}"), CancellationToken.None);

            missing.IsError.Should().BeTrue();
            missing.Text.Should().Be("Missing parameter 'lat'. Signature: GetNearestAirport(lat: number) -> string");
            unknown.IsError.Should().BeTrue();
            unknown.Text.Should().Be("Unknown parameter 'foo'. Declared: lat.");
            wrongKind.IsError.Should().BeTrue();
            wrongKind.Text.Should().Be("Parameter 'lat' must be a JSON number. Signature: GetNearestAirport(lat: number) -> string");
            topLevel.IsError.Should().BeTrue();
            topLevel.Text.Should().Be("Unexpected argument 'lat'. Put operation arguments in parameters as a JSON object.");
            stringified.IsError.Should().BeTrue();
            stringified.Text.Should().Be("parameters must be a JSON object, not a string.");
            body.IsError.Should().BeTrue();
            body.Text.Should().StartWith("Unexpected argument 'body'.");
            executor.Last.Should().BeNull("nothing reached the executor");
        }

        /// <summary>
        /// Enum, GUID, duration, and complex arguments use OData literal forms; complex values travel as parameter aliases.
        /// </summary>
        [TestMethod]
        public async Task Call_LiteralForms_EnumDurationAndAlias()
        {
            const string csdl = """
                <edmx:Edmx Version="4.0" xmlns:edmx="http://docs.oasis-open.org/odata/ns/edmx">
                  <edmx:DataServices>
                    <Schema Namespace="Ops" xmlns="http://docs.oasis-open.org/odata/ns/edm">
                      <EnumType Name="Color"><Member Name="Red" Value="0" /><Member Name="Green" Value="1" /></EnumType>
                      <ComplexType Name="Point"><Property Name="X" Type="Edm.Double" Nullable="false" /></ComplexType>
                      <Function Name="Find">
                        <Parameter Name="color" Type="Ops.Color" Nullable="false" />
                        <Parameter Name="wait" Type="Edm.Duration" />
                        <Parameter Name="id" Type="Edm.Guid" />
                        <Parameter Name="at" Type="Ops.Point" />
                        <ReturnType Type="Edm.String" />
                      </Function>
                      <Action Name="Paint">
                        <Parameter Name="color" Type="Ops.Color" Nullable="false" />
                      </Action>
                      <EntityContainer Name="Container" />
                    </Schema>
                  </edmx:DataServices>
                </edmx:Edmx>
                """;
            var (runtime, executor) = Runtime(csdl);
            var find = await runtime.InvokeAsync("odata_call", ToolArguments.Of("name", "Find", "parameters", new { color = 1, wait = "PT5S", id = "0d5c7c7a-4d2c-4e1c-9a1b-0123456789ab", at = new { X = 1.5 } }), CancellationToken.None);
            find.IsError.Should().BeFalse(find.Text);
            executor.Last!.RelativePath.Should().Be("Find(color=Ops.Color'Green',wait=duration'PT5S',id=0d5c7c7a-4d2c-4e1c-9a1b-0123456789ab,at=@at)?@at=%7B%22X%22%3A1.5%7D");

            var paint = await runtime.InvokeAsync("odata_call", ToolArguments.Of("name", "Paint", "parameters", new { color = "Ops.Color'Green'" }), CancellationToken.None);
            paint.IsError.Should().BeFalse(paint.Text);
            executor.Last!.JsonBody.Should().Be("""{"color":"Green"}""");

            var badMember = await runtime.InvokeAsync("odata_call", ToolArguments.Of("name", "Paint", "parameters", new { color = "Blue" }), CancellationToken.None);
            badMember.IsError.Should().BeTrue();
            badMember.Text.Should().StartWith("Parameter 'color' must be one of Red, Green; 'Blue' is not a member.");
        }

        /// <summary>
        /// Live TripPin: unbound GetNearestAirport hits the service, and the bound ShareTrip signature is enforced without HTTP.
        /// </summary>
        [TestMethod]
        public async Task Call_TripPin_ParametersObject()
        {
            var (runtime, capture) = await LiveToolRuntime.CreateTripPinAsync();
            var airport = await runtime.InvokeAsync("odata_call", ToolArguments.Of("name", "GetNearestAirport", "parameters", new { lat = 33, lon = -118 }), CancellationToken.None);
            var share = await runtime.InvokeAsync("odata_call", ToolArguments.Of("name", "ShareTrip", "parameters", new { userName = "scottketchum", tripId = 0 }), CancellationToken.None);

            airport.IsError.Should().BeFalse(airport.Text);
            capture.Last!.RelativePath.Should().Be("GetNearestAirport(lat=33,lon=-118)");
            airport.StructuredContent.Should().Contain("IcaoCode");
            share.IsError.Should().BeTrue();
            share.Text.Should().Be("ShareTrip is bound to Person. Pass entitySet and key. Signature: ShareTrip(userName: string, tripId: int) // writes");
            capture.Requests.Should().HaveCount(1, "the bound error never reached OData");
        }

        #endregion

        #region Internal Methods

        /// <summary>
        /// Creates a runtime over a CSDL fixture with a recording executor.
        /// </summary>
        /// <param name="csdl">The CSDL fixture.</param>
        /// <returns>
        /// The runtime and its executor.
        /// </returns>
        internal static (ODataToolRuntime Runtime, RecordingODataExecutor Executor) Runtime(string csdl)
        {
            var executor = new RecordingODataExecutor();
            var catalog = new ODataMcpCatalog(new CsdlParser().ParseFromString(csdl), new ODataMcpCatalogOptions());

            return (new ODataToolRuntime(catalog, executor), executor);
        }

        #endregion

    }

}
