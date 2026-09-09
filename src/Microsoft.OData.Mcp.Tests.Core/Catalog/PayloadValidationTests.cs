// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.OData.Mcp.Core.Catalog;
using Microsoft.OData.Mcp.Core.Parsing;
using Microsoft.OData.Mcp.Tests.Core.Parsing;
using Microsoft.OData.Mcp.Tests.Shared;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.OData.Mcp.Tests.Core.Catalog
{

    /// <summary>
    /// Create and update bodies are validated against the declared type before any OData HTTP.
    /// </summary>
    [TestClass]
    public class PayloadValidationTests
    {

        #region Public Methods

        /// <summary>
        /// A create that omits a required-on-create property fails before HTTP and names the required list.
        /// </summary>
        [TestMethod]
        public async Task Create_MissingRequired_FailsBeforeHttp()
        {
            var (runtime, executor) = OdataCallTests.Runtime(CsdlParserMoreTests.EnumCsdl);
            var named = await runtime.InvokeAsync("create_widget", ToolArguments.Of("Name", "W"), CancellationToken.None);
            var generic = await runtime.InvokeAsync("odata_create", ToolArguments.Of("entitySet", "Widgets", "body", """{"Name":"W"}"""), CancellationToken.None);

            named.IsError.Should().BeTrue();
            named.Text.Should().Be("Missing required properties on Widget: Sequence, Color. Required on create: Sequence, Name, Color.");
            generic.IsError.Should().BeTrue();
            generic.Text.Should().Be(named.Text);
            executor.Last.Should().BeNull();
        }

        /// <summary>
        /// A complete create posts the body unchanged; computed keys may be omitted.
        /// </summary>
        [TestMethod]
        public async Task Create_Valid_PostsBody()
        {
            var (runtime, executor) = OdataCallTests.Runtime(CsdlParserMoreTests.EnumCsdl);
            var result = await runtime.InvokeAsync("create_widget", ToolArguments.Of("Sequence", 1, "Name", "W", "Color", "Red", "Access", "Read,Write", "Code", "ABC"), CancellationToken.None);

            result.IsError.Should().BeFalse(result.Text);
            executor.Last!.Method.Should().Be(HttpMethod.Post);
            executor.Last.RelativePath.Should().Be("Widgets");
            executor.Last.JsonBody.Should().Be("""{"Sequence":1,"Name":"W","Color":"Red","Access":"Read,Write","Code":"ABC"}""");
        }

        /// <summary>
        /// Wrong JSON kinds, unknown enum members, over-long strings, and unknown properties on a closed type fail before HTTP.
        /// </summary>
        [TestMethod]
        public async Task Create_TypeEnumLengthAndUnknownProperty_FailBeforeHttp()
        {
            var (runtime, executor) = OdataCallTests.Runtime(CsdlParserMoreTests.EnumCsdl);
            var kind = await runtime.InvokeAsync("create_widget", ToolArguments.Of("Sequence", "one", "Name", "W", "Color", "Red"), CancellationToken.None);
            var member = await runtime.InvokeAsync("create_widget", ToolArguments.Of("Sequence", 1, "Name", "W", "Color", "Blue"), CancellationToken.None);
            var length = await runtime.InvokeAsync("create_widget", ToolArguments.Of("Sequence", 1, "Name", "W", "Color", "Red", "Code", "TOO-LONG-CODE"), CancellationToken.None);
            var unknown = await runtime.InvokeAsync("create_widget", ToolArguments.Of("Sequence", 1, "Name", "W", "Color", "Red", "Colour", "Red"), CancellationToken.None);
            var nullRequired = await runtime.InvokeAsync("odata_update", ToolArguments.Of("entitySet", "Widgets", "key", "1", "body", """{"Name":null}"""), CancellationToken.None);

            kind.Text.Should().Be("Property 'Sequence' on Widget must be a JSON integer.");
            member.Text.Should().Be("Property 'Color' on Widget must be one of Red, Green; 'Blue' is not a member.");
            length.Text.Should().Be("Property 'Code' on Widget exceeds MaxLength 8.");
            unknown.Text.Should().StartWith("Unknown property 'Colour' on Widget. Declared: Id, Sequence, Name, Color, CreatedOn, Version, Notes, Access, Code, RowVersion.");
            nullRequired.Text.Should().Be("Property 'Name' on Widget cannot be null.");
            executor.Last.Should().BeNull();
        }

        /// <summary>
        /// Update validates only what is sent: no required check, omitted fields are fine, nullable null is fine.
        /// </summary>
        [TestMethod]
        public async Task Update_PartialBody_IsValid()
        {
            var (runtime, executor) = OdataCallTests.Runtime(CsdlParserMoreTests.EnumCsdl);
            var result = await runtime.InvokeAsync("update_widget", ToolArguments.Of("key", "1", "Notes", null, "Color", 1), CancellationToken.None);

            result.IsError.Should().BeFalse(result.Text);
            executor.Last!.Method.Should().Be(HttpMethod.Patch);
            executor.Last.RelativePath.Should().Be("Widgets(1)");
            executor.Last.JsonBody.Should().Be("""{"Notes":null,"Color":1}""", "Auto accepts a member value; the body is forwarded as sent");
        }

        /// <summary>
        /// Bodies that are not JSON objects, and sets that are not declared, are forwarded for the service to answer.
        /// </summary>
        [TestMethod]
        public async Task Create_NonObjectBodyOrUnknownSet_IsForwarded()
        {
            var (runtime, executor) = OdataCallTests.Runtime(CsdlParserMoreTests.EnumCsdl);
            var malformed = await runtime.InvokeAsync("odata_create", ToolArguments.Of("entitySet", "Widgets", "body", "{"), CancellationToken.None);
            malformed.IsError.Should().BeFalse(malformed.Text);
            executor.Last!.JsonBody.Should().Be("{");

            var unknownSet = await runtime.InvokeAsync("odata_create", ToolArguments.Of("entitySet", "Ghosts", "body", """{"Name":"W"}"""), CancellationToken.None);
            unknownSet.IsError.Should().BeFalse(unknownSet.Text);
            executor.Last!.RelativePath.Should().Be("Ghosts");
        }

        /// <summary>
        /// <see cref="ODataMcpCatalogOptions.EnforceRequiredOnCreate"/> off skips only the required check.
        /// </summary>
        [TestMethod]
        public async Task Create_EnforceRequiredOff_StillValidatesTypes()
        {
            var executor = new RecordingODataExecutor();
            var catalog = new ODataMcpCatalog(new CsdlParser().ParseFromString(CsdlParserMoreTests.EnumCsdl), new ODataMcpCatalogOptions { EnforceRequiredOnCreate = false });
            var runtime = new ODataToolRuntime(catalog, executor);
            var partial = await runtime.InvokeAsync("create_widget", ToolArguments.Of("Name", "W"), CancellationToken.None);
            var wrong = await runtime.InvokeAsync("create_widget", ToolArguments.Of("Name", 5), CancellationToken.None);

            partial.IsError.Should().BeFalse(partial.Text);
            executor.Last!.JsonBody.Should().Be("""{"Name":"W"}""");
            wrong.IsError.Should().BeTrue();
            wrong.Text.Should().Be("Property 'Name' on Widget must be a JSON string.");
        }

        /// <summary>
        /// Open types accept undeclared properties; declared ones are still checked.
        /// </summary>
        [TestMethod]
        public async Task Create_OpenType_AllowsExtraProperties()
        {
            const string csdl = """
                <edmx:Edmx Version="4.0" xmlns:edmx="http://docs.oasis-open.org/odata/ns/edmx">
                  <edmx:DataServices>
                    <Schema Namespace="O" xmlns="http://docs.oasis-open.org/odata/ns/edm">
                      <EntityType Name="Bag" OpenType="true">
                        <Key><PropertyRef Name="Id" /></Key>
                        <Property Name="Id" Type="Edm.String" Nullable="false" />
                        <Property Name="Count" Type="Edm.Int32" />
                      </EntityType>
                      <EntityContainer Name="C"><EntitySet Name="Bags" EntityType="O.Bag" /></EntityContainer>
                    </Schema>
                  </edmx:DataServices>
                </edmx:Edmx>
                """;
            var (runtime, executor) = OdataCallTests.Runtime(csdl);
            var extra = await runtime.InvokeAsync("create_bag", ToolArguments.Of("Id", "a", "Anything", new { nested = true }), CancellationToken.None);
            var declared = await runtime.InvokeAsync("create_bag", ToolArguments.Of("Id", "a", "Count", "many"), CancellationToken.None);

            extra.IsError.Should().BeFalse(extra.Text);
            executor.Last!.JsonBody.Should().Contain("Anything");
            declared.IsError.Should().BeTrue();
            declared.Text.Should().Be("Property 'Count' on Bag must be a JSON integer.");
        }

        /// <summary>
        /// Live Northwind: creating a Customer without CustomerID never reaches the read-only service.
        /// </summary>
        [TestMethod]
        public async Task Create_Northwind_CustomerWithoutKey_FailsBeforeHttp()
        {
            var (runtime, capture) = await LiveToolRuntime.CreateNorthwindAsync();
            var result = await runtime.InvokeAsync("create_customer", ToolArguments.Of("CompanyName", "Nope"), CancellationToken.None);

            result.IsError.Should().BeTrue();
            result.Text.Should().Be("Missing required properties on Customer: CustomerID. Required on create: CustomerID.");
            capture.Requests.Should().BeEmpty();
        }

        #endregion

    }

}
