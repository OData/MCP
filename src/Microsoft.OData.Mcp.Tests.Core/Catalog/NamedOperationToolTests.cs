// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.OData.Mcp.Core.Catalog;
using Microsoft.OData.Mcp.Core.Parsing;
using Microsoft.OData.Mcp.Core.Vocabularies;
using Microsoft.OData.Mcp.Tests.Shared;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.OData.Mcp.Tests.Core.Catalog
{

    /// <summary>
    /// Named <c>action_*</c> / <c>function_*</c> tools: catalog names, schemas, order, flags, and one-shot dispatch.
    /// </summary>
    [TestClass]
    public class NamedOperationToolTests
    {

        #region Fields

        internal const string OverloadCsdl = """
            <edmx:Edmx Version="4.0" xmlns:edmx="http://docs.oasis-open.org/odata/ns/edmx">
              <edmx:DataServices>
                <Schema Namespace="S" xmlns="http://docs.oasis-open.org/odata/ns/edm">
                  <Action Name="Reset">
                    <Parameter Name="hard" Type="Edm.Boolean" Nullable="false" />
                  </Action>
                  <Action Name="Reset">
                    <Parameter Name="reason" Type="Edm.String" Nullable="false" />
                  </Action>
                  <EntityContainer Name="Container" />
                </Schema>
              </edmx:DataServices>
            </edmx:Edmx>
            """;

        internal const string TwoSetsCsdl = """
            <edmx:Edmx Version="4.0" xmlns:edmx="http://docs.oasis-open.org/odata/ns/edmx">
              <edmx:DataServices>
                <Schema Namespace="S" xmlns="http://docs.oasis-open.org/odata/ns/edm">
                  <EntityType Name="Customer">
                    <Key><PropertyRef Name="Id" /></Key>
                    <Property Name="Id" Type="Edm.Int32" Nullable="false" />
                  </EntityType>
                  <Action Name="Approve" IsBound="true">
                    <Parameter Name="binding" Type="S.Customer" />
                  </Action>
                  <Action Name="Discount" IsBound="true">
                    <Parameter Name="binding" Type="Collection(S.Customer)" />
                    <Parameter Name="percent" Type="Edm.Int32" Nullable="false" />
                  </Action>
                  <EntityContainer Name="Container">
                    <EntitySet Name="Customers" EntityType="S.Customer" />
                    <EntitySet Name="VipCustomers" EntityType="S.Customer" />
                  </EntityContainer>
                </Schema>
              </edmx:DataServices>
            </edmx:Edmx>
            """;

        #endregion

        #region Public Methods

        /// <summary>
        /// Collection-bound tools use the entity-set short name, not the type stem.
        /// </summary>
        [TestMethod]
        public void Catalog_CollectionBound_UsesSetStem()
        {
            var names = Catalog(EdmTypeShapeTests.OperationsCsdl).Tools.Select(tool => tool.Name).ToList();

            names.Should().Contain("function_people_top");
            names.Should().Contain("function_staff_top");
            names.Should().NotContain("function_person_top");
        }

        /// <summary>
        /// Excluding a set drops that set's CRUD and collection-bound tools and keeps unbound ones.
        /// Remaining sets still advertise operations bound to a base type.
        /// </summary>
        [TestMethod]
        public void Catalog_ExcludeEntitySets_OmitsBoundKeepsUnbound()
        {
            var names = Catalog(EdmTypeShapeTests.OperationsCsdl, new ODataMcpCatalogOptions
            {
                ExcludeEntitySets = ["People"]
            }).Tools.Select(tool => tool.Name).ToList();

            names.Should().Contain("function_getnearestairport");
            names.Should().Contain("action_resetdatasource");
            names.Should().NotContain("list_people");
            names.Should().NotContain("function_people_top");
            names.Should().Contain("action_employee_promote");
            names.Should().Contain("action_person_updatelastname");
            names.Should().Contain("function_person_getfavoriteairline");
        }

        /// <summary>
        /// <c>IncludeActions=false</c> omits named actions and leaves functions and <c>odata_call</c>.
        /// </summary>
        [TestMethod]
        public void Catalog_IncludeActionsFalse_OmitsNamedActions()
        {
            var names = Catalog(EdmTypeShapeTests.OperationsCsdl, new ODataMcpCatalogOptions
            {
                IncludeActions = false
            }).Tools.Select(tool => tool.Name).ToList();

            names.Should().NotContain(name => name.StartsWith("action_", StringComparison.Ordinal));
            names.Should().Contain("function_getnearestairport");
            names.Should().Contain("function_person_getfavoriteairline");
            names.Should().Contain("odata_call");
        }

        /// <summary>
        /// <c>IncludeFunctions=false</c> omits named functions and leaves actions and <c>odata_call</c>.
        /// </summary>
        [TestMethod]
        public void Catalog_IncludeFunctionsFalse_OmitsNamedFunctions()
        {
            var names = Catalog(EdmTypeShapeTests.OperationsCsdl, new ODataMcpCatalogOptions
            {
                IncludeFunctions = false
            }).Tools.Select(tool => tool.Name).ToList();

            names.Should().NotContain(name => name.StartsWith("function_", StringComparison.Ordinal));
            names.Should().Contain("action_resetdatasource");
            names.Should().Contain("action_person_updatelastname");
            names.Should().Contain("odata_call");
        }

        /// <summary>
        /// Instance-bound tools use the binding type stem when that type has one included set.
        /// </summary>
        [TestMethod]
        public void Catalog_InstanceBound_UsesTypeStem()
        {
            var catalog = Catalog(EdmTypeShapeTests.OperationsCsdl);
            var names = catalog.Tools.Select(tool => tool.Name).ToList();
            var update = catalog.Tools.Single(tool => tool.Name == "action_person_updatelastname");
            var favorite = catalog.Tools.Single(tool => tool.Name == "function_person_getfavoriteairline");
            var promote = catalog.Tools.Single(tool => tool.Name == "action_employee_promote");

            names.Should().Contain("action_person_updatelastname");
            names.Should().Contain("function_person_getfavoriteairline");
            names.Should().Contain("action_employee_promote");
            names.Should().Contain("function_staff_getfavoriteairline");
            names.Should().Contain("action_staff_updatelastname");
            update.EntitySetName.Should().Be("People");
            favorite.EntitySetName.Should().Be("People");
            promote.EntitySetName.Should().Be("Staff");
            update.OperationKind.Should().Be(ODataOperationKind.Action);
            update.OperationName.Should().Be("UpdateLastName");
            update.Title.Should().Be("UpdateLastName a Person");
            favorite.OperationKind.Should().Be(ODataOperationKind.Function);
            favorite.OperationName.Should().Be("GetFavoriteAirline");
        }

        /// <summary>
        /// Named operation input schemas flatten declared parameters and forbid extra properties.
        /// </summary>
        [TestMethod]
        public void Catalog_InputSchema_FlattensParametersAndSetsAdditionalPropertiesFalse()
        {
            var catalog = Catalog(EdmTypeShapeTests.OperationsCsdl);
            using var unbound = JsonDocument.Parse(catalog.Tools.Single(tool => tool.Name == "function_getnearestairport").InputSchema);
            using var bound = JsonDocument.Parse(catalog.Tools.Single(tool => tool.Name == "action_person_updatelastname").InputSchema);
            using var collection = JsonDocument.Parse(catalog.Tools.Single(tool => tool.Name == "function_people_top").InputSchema);

            unbound.RootElement.GetProperty("additionalProperties").GetBoolean().Should().BeFalse();
            unbound.RootElement.GetProperty("required").EnumerateArray().Select(item => item.GetString()).Should().Equal("lat");
            unbound.RootElement.GetProperty("properties").GetProperty("lat").GetProperty("type").GetString().Should().Be("number");
            unbound.RootElement.GetProperty("properties").TryGetProperty("parameters", out _).Should().BeFalse();
            unbound.RootElement.GetProperty("properties").TryGetProperty("entitySet", out _).Should().BeFalse();
            unbound.RootElement.GetProperty("properties").TryGetProperty("key", out _).Should().BeFalse();

            bound.RootElement.GetProperty("additionalProperties").GetBoolean().Should().BeFalse();
            bound.RootElement.GetProperty("required").EnumerateArray().Select(item => item.GetString()).Should().Equal("key", "lastName");
            bound.RootElement.GetProperty("properties").GetProperty("key").GetProperty("type").GetString().Should().Be("string");
            bound.RootElement.GetProperty("properties").GetProperty("lastName").GetProperty("type").GetString().Should().Be("string");
            bound.RootElement.GetProperty("properties").TryGetProperty("person", out _).Should().BeFalse();
            bound.RootElement.GetProperty("properties").TryGetProperty("parameters", out _).Should().BeFalse();

            collection.RootElement.GetProperty("required").EnumerateArray().Select(item => item.GetString()).Should().Equal("count");
            collection.RootElement.GetProperty("properties").TryGetProperty("key", out _).Should().BeFalse();
            collection.RootElement.GetProperty("properties").GetProperty("count").GetProperty("type").GetString().Should().Be("number");
        }

        /// <summary>
        /// Binary and stream parameters are omitted from the named-tool schema.
        /// </summary>
        [TestMethod]
        public void Catalog_OmitsBinaryAndStreamParameters()
        {
            const string csdl = """
                <edmx:Edmx Version="4.0" xmlns:edmx="http://docs.oasis-open.org/odata/ns/edmx">
                  <edmx:DataServices>
                    <Schema Namespace="S" xmlns="http://docs.oasis-open.org/odata/ns/edm">
                      <Action Name="Upload">
                        <Parameter Name="title" Type="Edm.String" Nullable="false" />
                        <Parameter Name="blob" Type="Edm.Binary" Nullable="false" />
                        <Parameter Name="photo" Type="Edm.Stream" />
                      </Action>
                      <EntityContainer Name="Container" />
                    </Schema>
                  </edmx:DataServices>
                </edmx:Edmx>
                """;
            using var document = JsonDocument.Parse(Catalog(csdl).Tools.Single(tool => tool.Name == "action_upload").InputSchema);
            var properties = document.RootElement.GetProperty("properties");

            properties.TryGetProperty("title", out _).Should().BeTrue();
            properties.TryGetProperty("blob", out _).Should().BeFalse();
            properties.TryGetProperty("photo", out _).Should().BeFalse();
        }

        /// <summary>
        /// Unbound operations sit after the generics and before any per-set CRUD or bound tools.
        /// </summary>
        [TestMethod]
        public void Catalog_Order_UnboundBeforeBoundAndPerSetFamily()
        {
            var names = Catalog(EdmTypeShapeTests.OperationsCsdl).Tools.Select(tool => tool.Name).ToList();
            var call = names.IndexOf("odata_call");
            var nearest = names.IndexOf("function_getnearestairport");
            var reset = names.IndexOf("action_resetdatasource");
            var listPeople = names.IndexOf("list_people");
            var favorite = names.IndexOf("function_person_getfavoriteairline");
            var update = names.IndexOf("action_person_updatelastname");
            var top = names.IndexOf("function_people_top");

            call.Should().BeGreaterThanOrEqualTo(0);
            nearest.Should().Be(call + 1);
            reset.Should().Be(nearest + 1);
            listPeople.Should().Be(reset + 1);
            favorite.Should().BeGreaterThan(listPeople);
            update.Should().BeGreaterThan(favorite);
            top.Should().BeGreaterThan(update);
        }

        /// <summary>
        /// A complex return type becomes an object <c>outputSchema</c> with the declared properties.
        /// </summary>
        [TestMethod]
        public void Catalog_OutputSchema_EmitsComplexTypeProperties()
        {
            const string csdl = """
                <edmx:Edmx Version="4.0" xmlns:edmx="http://docs.oasis-open.org/odata/ns/edmx">
                  <edmx:DataServices>
                    <Schema Namespace="S" xmlns="http://docs.oasis-open.org/odata/ns/edm">
                      <ComplexType Name="Ticket">
                        <Property Name="Status" Type="Edm.String" Nullable="false" />
                        <Property Name="Uri" Type="Edm.String" />
                      </ComplexType>
                      <Action Name="Authorize">
                        <ReturnType Type="S.Ticket" />
                      </Action>
                      <EntityContainer Name="Container" />
                    </Schema>
                  </edmx:DataServices>
                </edmx:Edmx>
                """;
            using var document = JsonDocument.Parse(Catalog(csdl).Tools.Single(tool => tool.Name == "action_authorize").OutputSchema!);
            var properties = document.RootElement.GetProperty("properties");

            document.RootElement.GetProperty("type").GetString().Should().Be("object");
            properties.GetProperty("Status").GetProperty("type").GetString().Should().Be("string");
            properties.TryGetProperty("Uri", out _).Should().BeTrue();
            document.RootElement.TryGetProperty("properties", out _).Should().BeTrue();
            properties.TryGetProperty("value", out _).Should().BeFalse();
        }

        /// <summary>
        /// Overloads of the same unbound action name emit one named tool for the first declaration.
        /// </summary>
        [TestMethod]
        public void Catalog_Overloads_EmitsFirstDeclarationOnly()
        {
            var catalog = Catalog(OverloadCsdl);
            var resets = catalog.Tools.Where(tool => tool.Name == "action_reset").ToList();

            resets.Should().ContainSingle();
            using var document = JsonDocument.Parse(resets[0].InputSchema);
            document.RootElement.GetProperty("properties").TryGetProperty("hard", out _).Should().BeTrue();
            document.RootElement.GetProperty("properties").TryGetProperty("reason", out _).Should().BeFalse();
        }

        /// <summary>
        /// Two included sets of the same type put the set stem in the instance-bound name.
        /// </summary>
        [TestMethod]
        public void Catalog_TwoSetsOfSameType_UseSetStem()
        {
            var names = Catalog(TwoSetsCsdl).Tools.Select(tool => tool.Name).ToList();

            names.Should().Contain("action_customers_approve");
            names.Should().Contain("action_vipcustomers_approve");
            names.Should().NotContain("action_customer_approve");
            names.Should().Contain("action_customers_discount");
            names.Should().Contain("action_vipcustomers_discount");
        }

        /// <summary>
        /// A short CSDL description becomes the tool title and is appended to the operational sentence.
        /// </summary>
        [TestMethod]
        public void Catalog_Title_UsesCsdlDescriptionWhenPresent()
        {
            const string csdl = """
                <edmx:Edmx Version="4.0" xmlns:edmx="http://docs.oasis-open.org/odata/ns/edmx">
                  <edmx:DataServices>
                    <Schema Namespace="S" xmlns="http://docs.oasis-open.org/odata/ns/edm">
                      <Action Name="Authorize">
                        <Documentation>
                          <Summary>Authorize the connection</Summary>
                        </Documentation>
                      </Action>
                      <EntityContainer Name="Container" />
                    </Schema>
                  </edmx:DataServices>
                </edmx:Edmx>
                """;
            var tool = Catalog(csdl).Tools.Single(item => item.Name == "action_authorize");

            tool.Title.Should().Be("Authorize the connection");
            tool.Description.Should().Contain("POST Authorize.");
            tool.Description.Should().Contain("Authorize the connection");
        }

        /// <summary>
        /// An annotated task action appends the MCP task sentence to the named tool description.
        /// </summary>
        [TestMethod]
        public void Catalog_TaskAction_AppendsTaskSentence()
        {
            const string csdl = """
                <edmx:Edmx Version="4.0" xmlns:edmx="http://docs.oasis-open.org/odata/ns/edmx">
                  <edmx:DataServices>
                    <Schema Namespace="S" xmlns="http://docs.oasis-open.org/odata/ns/edm">
                      <Action Name="Authorize">
                        <Annotation Term="Org.OData.Core.V1.Description" String="YouTube opens Google's Brand Account picker." />
                        <Annotation Term="Microsoft.OData.Mcp.V1.Task" Bool="true" />
                      </Action>
                      <EntityContainer Name="Container" />
                    </Schema>
                  </edmx:DataServices>
                </edmx:Edmx>
                """;
            var tool = Catalog(csdl).Tools.Single(item => item.Name == "action_authorize");

            tool.IsTask.Should().BeTrue();
            tool.Description.Should().Contain("POST Authorize.");
            tool.Description.Should().Contain("YouTube opens Google's Brand Account picker.");
            tool.Description.Should().Contain(McpVocabulary.TaskToolDescription);
        }

        /// <summary>
        /// An unannotated action stays one-shot in the catalog.
        /// </summary>
        [TestMethod]
        public void Catalog_UnannotatedAction_IsNotATask()
        {
            const string csdl = """
                <edmx:Edmx Version="4.0" xmlns:edmx="http://docs.oasis-open.org/odata/ns/edmx">
                  <edmx:DataServices>
                    <Schema Namespace="S" xmlns="http://docs.oasis-open.org/odata/ns/edm">
                      <Action Name="Reset" />
                      <EntityContainer Name="Container" />
                    </Schema>
                  </edmx:DataServices>
                </edmx:Edmx>
                """;
            var tool = Catalog(csdl).Tools.Single(item => item.Name == "action_reset");

            tool.IsTask.Should().BeFalse();
            tool.Description.Should().NotContain(McpVocabulary.TaskToolDescription);
        }

        /// <summary>
        /// Unbound operations are <c>function_{name}</c> / <c>action_{name}</c> from the EDM short name.
        /// </summary>
        [TestMethod]
        public void Catalog_Unbound_UsesActionAndFunctionPrefix()
        {
            var catalog = Catalog(EdmTypeShapeTests.OperationsCsdl);
            var nearest = catalog.Tools.Single(tool => tool.Name == "function_getnearestairport");
            var reset = catalog.Tools.Single(tool => tool.Name == "action_resetdatasource");

            nearest.EntitySetName.Should().BeNull();
            nearest.OperationKind.Should().Be(ODataOperationKind.Function);
            nearest.OperationName.Should().Be("GetNearestAirport");
            nearest.BindingParameterType.Should().BeNull();
            nearest.ReadOnlyHint.Should().BeTrue();
            nearest.IdempotentHint.Should().BeTrue();
            nearest.DestructiveHint.Should().BeFalse();
            nearest.OpenWorldHint.Should().BeTrue();
            nearest.Description.Should().Contain("GET GetNearestAirport.");
            nearest.Title.Should().Be("GetNearestAirport");
            nearest.OutputSchema.Should().Contain("\"value\"");

            reset.EntitySetName.Should().BeNull();
            reset.OperationKind.Should().Be(ODataOperationKind.Action);
            reset.OperationName.Should().Be("ResetDataSource");
            reset.ReadOnlyHint.Should().BeFalse();
            reset.IdempotentHint.Should().BeFalse();
            reset.DestructiveHint.Should().BeTrue();
            reset.Description.Should().Contain("POST ResetDataSource.");
            reset.Title.Should().Be("ResetDataSource");
            reset.OutputSchema.Should().BeNull();
        }

        /// <summary>
        /// Collection-bound named functions inject the set and omit key.
        /// </summary>
        [TestMethod]
        public async Task Invoke_CollectionBound_OmitsKey()
        {
            var (runtime, executor) = Runtime(EdmTypeShapeTests.OperationsCsdl);
            var result = await runtime.InvokeAsync("function_people_top", ToolArguments.Of("count", 2), CancellationToken.None);

            result.IsError.Should().BeFalse(result.Text);
            executor.Last!.Method.Should().Be(HttpMethod.Get);
            executor.Last.RelativePath.Should().Be("People/Top(count=2)");
        }

        /// <summary>
        /// Instance-bound named actions inject the baked entity set and pass remaining keys as parameters.
        /// </summary>
        [TestMethod]
        public async Task Invoke_InstanceBoundAction_InjectsEntitySetAndKey()
        {
            var (runtime, executor) = Runtime(EdmTypeShapeTests.OperationsCsdl);
            var result = await runtime.InvokeAsync(
                "action_person_updatelastname",
                ToolArguments.Of("key", "russellwhyte", "lastName", "Whyte"),
                CancellationToken.None);

            result.IsError.Should().BeFalse(result.Text);
            executor.Last!.Method.Should().Be(HttpMethod.Post);
            executor.Last.RelativePath.Should().Be("People('russellwhyte')/UpdateLastName");
            executor.Last.JsonBody.Should().Be("""{"lastName":"Whyte"}""");
        }

        /// <summary>
        /// Unbound named actions POST the operation path with no entity set.
        /// </summary>
        [TestMethod]
        public async Task Invoke_UnboundAction_PostsWithoutEntitySet()
        {
            var (runtime, executor) = Runtime(EdmTypeShapeTests.OperationsCsdl);
            var result = await runtime.InvokeAsync("action_resetdatasource", null, CancellationToken.None);

            result.IsError.Should().BeFalse(result.Text);
            executor.Last!.Method.Should().Be(HttpMethod.Post);
            executor.Last.RelativePath.Should().Be("ResetDataSource");
            executor.Last.JsonBody.Should().Be("{}");
        }

        /// <summary>
        /// Unbound named functions GET with flattened parameters on the URL.
        /// </summary>
        [TestMethod]
        public async Task Invoke_UnboundFunction_GetsWithParameters()
        {
            var (runtime, executor) = Runtime(EdmTypeShapeTests.OperationsCsdl);
            var result = await runtime.InvokeAsync("function_getnearestairport", ToolArguments.Of("lat", 47.6), CancellationToken.None);

            result.IsError.Should().BeFalse(result.Text);
            executor.Last!.Method.Should().Be(HttpMethod.Get);
            executor.Last.RelativePath.Should().Be("GetNearestAirport(lat=47.6)");
            executor.Last.JsonBody.Should().BeNull();
        }

        #endregion

        #region Internal Methods

        /// <summary>
        /// Builds a catalog from CSDL.
        /// </summary>
        /// <param name="csdl">The CSDL document.</param>
        /// <param name="options">Catalog options, or <c>null</c> for defaults.</param>
        /// <returns>
        /// The catalog.
        /// </returns>
        internal static ODataMcpCatalog Catalog(string csdl, ODataMcpCatalogOptions? options = null)
        {
            return new ODataMcpCatalog(new CsdlParser().ParseFromString(csdl), options ?? new ODataMcpCatalogOptions());
        }

        /// <summary>
        /// Builds a runtime over a CSDL fixture with a recording executor.
        /// </summary>
        /// <param name="csdl">The CSDL fixture.</param>
        /// <returns>
        /// The runtime and its executor.
        /// </returns>
        internal static (ODataToolRuntime Runtime, RecordingODataExecutor Executor) Runtime(string csdl)
        {
            var executor = new RecordingODataExecutor();
            var catalog = Catalog(csdl);

            return (new ODataToolRuntime(catalog, executor), executor);
        }

        #endregion

    }

}
