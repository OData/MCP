// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.OData.Mcp.Core.Catalog;
using Microsoft.OData.Mcp.Core.Parsing;
using Microsoft.OData.Mcp.Tests.Core.Parsing;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.OData.Mcp.Tests.Core.Catalog
{

    /// <summary>
    /// Named <c>create_*</c> / <c>update_*</c> input schemas and the list tools' output schemas follow OPTIMIZATION.md §3.
    /// </summary>
    [TestClass]
    public class NamedSchemaTests
    {

        #region Public Methods

        /// <summary>
        /// The create schema lists declared properties with JSON types, marks required-on-create, and never invents descriptions.
        /// </summary>
        [TestMethod]
        public async Task CreateSchema_Northwind_Customer_RequiredIsCustomerIdOnly()
        {
            var catalog = new ODataMcpCatalog(await LiveMetadata.LoadNorthwindModelAsync(), new ODataMcpCatalogOptions());
            var create = catalog.Tools.Single(tool => tool.Name == "create_customer");
            using var document = JsonDocument.Parse(create.InputSchema);
            var root = document.RootElement;
            var properties = root.GetProperty("properties");

            root.GetProperty("type").GetString().Should().Be("object");
            root.GetProperty("required").EnumerateArray().Select(item => item.GetString()).Should().Equal("CustomerID");
            properties.GetProperty("CustomerID").GetProperty("type").GetString().Should().Be("string");
            properties.GetProperty("CompanyName").GetProperty("type").EnumerateArray().Select(item => item.GetString()).Should().Equal("string", "null");
            create.InputSchema.Should().NotContain("\"description\"", "Northwind has no CSDL docs and names are never used as descriptions");
            create.InputSchema.Should().NotContain("\"body\"");
            create.InputSchema.Should().NotContain("\"key\"");
            create.Description.Should().StartWith("Creates a Customer.");
        }

        /// <summary>
        /// The update schema is key plus the same property map, with only key required and no body string.
        /// </summary>
        [TestMethod]
        public async Task UpdateSchema_Northwind_Customer_KeyPlusPropertiesRequiredKeyOnly()
        {
            var catalog = new ODataMcpCatalog(await LiveMetadata.LoadNorthwindModelAsync(), new ODataMcpCatalogOptions());
            var update = catalog.Tools.Single(tool => tool.Name == "update_customer");
            using var document = JsonDocument.Parse(update.InputSchema);
            var root = document.RootElement;
            var properties = root.GetProperty("properties");

            root.GetProperty("required").EnumerateArray().Select(item => item.GetString()).Should().Equal("key");
            properties.EnumerateObject().First().Name.Should().Be("key");
            properties.GetProperty("key").GetProperty("type").GetString().Should().Be("string");
            properties.GetProperty("CustomerID").GetProperty("type").GetString().Should().Be("string", "non-nullable types do not include null");
            properties.GetProperty("CompanyName").GetProperty("type").EnumerateArray().Select(item => item.GetString()).Should().Equal("string", "null");
            update.InputSchema.Should().NotContain("\"body\"");
            update.Description.Should().StartWith("PATCH a Customer. Send only fields to change; omit to keep. Do not send JSON null for required properties.");
        }

        /// <summary>
        /// Enums advertise member names by default, flags and computed keys behave, and short strings carry maxLength.
        /// </summary>
        [TestMethod]
        public void CreateSchema_EnumFixture_EnumNamesMaxLengthAndComputed()
        {
            var catalog = new ODataMcpCatalog(new CsdlParser().ParseFromString(CsdlParserMoreTests.EnumCsdl), new ODataMcpCatalogOptions());
            var create = catalog.Tools.Single(tool => tool.Name == "create_widget");
            using var document = JsonDocument.Parse(create.InputSchema);
            var root = document.RootElement;
            var properties = root.GetProperty("properties");

            root.GetProperty("required").EnumerateArray().Select(item => item.GetString()).Should().Equal("Sequence", "Name", "Color");
            properties.GetProperty("Id").GetProperty("type").GetString().Should().Be("number", "a computed key is still declared, just not required");
            properties.GetProperty("Color").GetProperty("type").GetString().Should().Be("string");
            properties.GetProperty("Color").GetProperty("enum").EnumerateArray().Select(item => item.GetString()).Should().Equal("Red", "Green");
            properties.GetProperty("Access").GetProperty("type").EnumerateArray().Select(item => item.GetString()).Should().Equal("string", "null");
            properties.GetProperty("Access").GetProperty("enum").EnumerateArray().Select(item => item.ValueKind == JsonValueKind.Null ? null : item.GetString()).Should().Equal("Read", "Write", null);
            properties.GetProperty("Code").GetProperty("maxLength").GetInt32().Should().Be(8);
            properties.GetProperty("CreatedOn").GetProperty("type").GetString().Should().Be("string");
            properties.TryGetProperty("RowVersion", out _).Should().BeFalse("binary properties are never in schemas");
            create.InputSchema.Should().NotContain("\"description\"");
        }

        /// <summary>
        /// <see cref="ODataEnumJsonFormat.Integer"/> advertises member values with the names in the description.
        /// </summary>
        [TestMethod]
        public void CreateSchema_EnumJsonFormatInteger_ValuesWithNamesInDescription()
        {
            var catalog = new ODataMcpCatalog(new CsdlParser().ParseFromString(CsdlParserMoreTests.EnumCsdl), new ODataMcpCatalogOptions { EnumJsonFormat = ODataEnumJsonFormat.Integer });
            var create = catalog.Tools.Single(tool => tool.Name == "create_widget");
            using var document = JsonDocument.Parse(create.InputSchema);
            var color = document.RootElement.GetProperty("properties").GetProperty("Color");

            color.GetProperty("type").GetString().Should().Be("number");
            color.GetProperty("enum").EnumerateArray().Select(item => item.GetInt64()).Should().Equal(0, 1);
            color.GetProperty("description").GetString().Should().Be("Red=0, Green=1");
        }

        /// <summary>
        /// Collections become arrays, complex types become objects, long MaxLength stays out, and CSDL docs flow in.
        /// </summary>
        [TestMethod]
        public async Task CreateSchema_TripPin_Person_CollectionsComplexAndEnums()
        {
            var catalog = new ODataMcpCatalog(await LiveMetadata.LoadTripPinModelAsync(), new ODataMcpCatalogOptions());
            var create = catalog.Tools.Single(tool => tool.Name == "create_person");
            using var document = JsonDocument.Parse(create.InputSchema);
            var root = document.RootElement;
            var properties = root.GetProperty("properties");

            root.GetProperty("required").EnumerateArray().Select(item => item.GetString()).Should().Equal("UserName", "FirstName", "Gender", "FavoriteFeature", "Features");
            properties.GetProperty("Emails").GetProperty("type").EnumerateArray().Select(item => item.GetString()).Should().Equal("array", "null");
            properties.GetProperty("Emails").GetProperty("items").GetProperty("type").GetString().Should().Be("string");
            properties.GetProperty("Features").GetProperty("type").GetString().Should().Be("array");
            properties.GetProperty("Features").GetProperty("items").GetProperty("enum").EnumerateArray().Select(item => item.GetString()).Should().Equal("Feature1", "Feature2", "Feature3", "Feature4");
            properties.GetProperty("HomeAddress").GetProperty("type").EnumerateArray().Select(item => item.GetString()).Should().Equal("object", "null");
            properties.GetProperty("Gender").GetProperty("enum").EnumerateArray().Select(item => item.GetString()).Should().Equal("Male", "Female", "Unknown");
            properties.GetProperty("LastName").TryGetProperty("maxLength", out _).Should().BeFalse("MaxLength 26 is above the inline threshold");
            properties.GetProperty("Age").GetProperty("type").EnumerateArray().Select(item => item.GetString()).Should().Equal("number", "null");
        }

        /// <summary>
        /// Documented properties carry their CSDL text as the schema description.
        /// </summary>
        [TestMethod]
        public void CreateSchema_Documented_PropertyDescriptionFromCsdl()
        {
            var catalog = new ODataMcpCatalog(new CsdlParser().ParseFromString(CsdlParserDocumentationTests.DocumentedCsdl), new ODataMcpCatalogOptions());
            var create = catalog.Tools.Single(tool => tool.Name == "create_person");
            using var document = JsonDocument.Parse(create.InputSchema);
            var properties = document.RootElement.GetProperty("properties");

            properties.GetProperty("UserName").GetProperty("description").GetString().Should().Be("Unique person name. Used as the entity key.");
            properties.GetProperty("FirstName").GetProperty("description").GetString().Should().Be("Given name.");
        }

        /// <summary>
        /// Only the two list tools declare an output schema; it describes exactly what they return.
        /// </summary>
        [TestMethod]
        public async Task OutputSchema_OnlyListToolsDeclareOne()
        {
            var catalog = new ODataMcpCatalog(await LiveMetadata.LoadNorthwindModelAsync(), new ODataMcpCatalogOptions());
            var withOutput = catalog.Tools.Where(tool => tool.OutputSchema is not null).Select(tool => tool.Name).ToList();
            var listSets = catalog.Tools.Single(tool => tool.Name == "odata_list_entity_sets");
            var listOperations = catalog.Tools.Single(tool => tool.Name == "odata_list_operations");

            withOutput.Should().Equal("odata_list_entity_sets", "odata_list_operations");
            using var setsSchema = JsonDocument.Parse(listSets.OutputSchema!);
            setsSchema.RootElement.GetProperty("required").EnumerateArray().Select(item => item.GetString()).Should().Equal("entitySets");
            setsSchema.RootElement.GetProperty("properties").GetProperty("entitySets").GetProperty("items").GetProperty("required").EnumerateArray().Select(item => item.GetString()).Should().Equal("name", "entityType", "keys");
            using var operationsSchema = JsonDocument.Parse(listOperations.OutputSchema!);
            operationsSchema.RootElement.GetProperty("properties").GetProperty("operations").GetProperty("additionalProperties").GetProperty("type").GetString().Should().Be("string");
            operationsSchema.RootElement.TryGetProperty("required", out _).Should().BeFalse("an empty service returns {}");
        }

        /// <summary>
        /// Descriptors with an output schema map to MCP tools with <c>outputSchema</c>; others leave it null.
        /// </summary>
        [TestMethod]
        public void ToTool_MapsOutputSchemaWhenPresent()
        {
            var with = ODataMcpHandlers.ToTool(new ODataToolDescriptor { InputSchema = """{"type":"object"}""", Name = "a", OutputSchema = """{"type":"object"}""" });
            var without = ODataMcpHandlers.ToTool(new ODataToolDescriptor { InputSchema = """{"type":"object"}""", Name = "b" });

            with.OutputSchema.Should().NotBeNull();
            with.OutputSchema!.Value.GetProperty("type").GetString().Should().Be("object");
            without.OutputSchema.Should().BeNull();
        }

        /// <summary>
        /// Null keys are never written: list_entity_sets omits description when the set has none.
        /// </summary>
        [TestMethod]
        public async Task ListEntitySets_Northwind_NoNullDescriptions()
        {
            var catalog = new ODataMcpCatalog(await LiveMetadata.LoadNorthwindModelAsync(), new ODataMcpCatalogOptions());
            var runtime = new ODataToolRuntime(catalog, new UnusedODataExecutor());
            var result = await runtime.InvokeAsync("odata_list_entity_sets", null, System.Threading.CancellationToken.None);

            result.IsError.Should().BeFalse(result.Text);
            result.StructuredContent.Should().NotContain(":null");
            result.StructuredContent.Should().Contain("\"name\":\"Customers\"");
        }

        #endregion

    }

}
