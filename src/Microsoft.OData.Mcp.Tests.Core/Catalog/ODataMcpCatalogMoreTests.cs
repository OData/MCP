// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using System.Linq;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OData.Mcp.Core.Catalog;
using Microsoft.OData.Mcp.Core.Parsing;
using Microsoft.OData.Mcp.Tests.Core.Parsing;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.OData.Mcp.Tests.Core.Catalog
{

    /// <summary>
    /// Additional catalog, completion, and handler mapping tests.
    /// </summary>
    [TestClass]
    public class ODataMcpCatalogMoreTests
    {

        #region Public Methods

        /// <summary>
        /// Empty prefix completes every declared set.
        /// </summary>
        [TestMethod]
        public void CompleteEntitySetNames_EmptyPrefix_ReturnsAll()
        {
            var catalog = Catalog();
            var names = catalog.CompleteEntitySetNames(string.Empty);

            names.Should().Contain("People");
        }

        /// <summary>
        /// Excluded sets are omitted from resources and named tools.
        /// </summary>
        [TestMethod]
        public void Catalog_ExcludeEntitySets_OmitsResourcesAndNamedTools()
        {
            var catalog = new ODataMcpCatalog(
                new CsdlParser().ParseFromString(CsdlParserDocumentationTests.DocumentedCsdl),
                new ODataMcpCatalogOptions
                {
                    ExcludeEntitySets = ["People"]
                });

            catalog.Resources.Select(resource => resource.Name).Should().NotContain("People");
            catalog.Tools.Where(tool => tool.EntitySetName == "People").Should().BeEmpty();
        }

        /// <summary>
        /// IncludeCreate false omits create tools from a family.
        /// </summary>
        [TestMethod]
        public void Catalog_IncludeCreateFalse_OmitsCreate()
        {
            var catalog = new ODataMcpCatalog(
                new CsdlParser().ParseFromString(CsdlParserDocumentationTests.DocumentedCsdl),
                new ODataMcpCatalogOptions
                {
                    IncludeCreate = false
                });

            catalog.Tools.Select(tool => tool.Name).Should().NotContain(name => name.StartsWith("create_", StringComparison.Ordinal));
            catalog.Tools.Select(tool => tool.Name).Should().Contain("list_people");
        }

        /// <summary>
        /// Snake_case conversion inserts underscores before capitals.
        /// </summary>
        [TestMethod]
        public void ToSnakeCase_InsertsUnderscores()
        {
            ODataMcpCatalog.ToSnakeCase("OrderItem").Should().Be("order_item");
            ODataMcpCatalog.ToSnakeCase("People").Should().Be("people");
        }

        /// <summary>
        /// Handler mapping copies annotations and schema.
        /// </summary>
        [TestMethod]
        public void ToTool_CopiesHintsAndSchema()
        {
            var catalog = Catalog();
            var query = catalog.Tools.Single(tool => tool.Name == "odata_query");
            var tool = ODataMcpHandlers.ToTool(query);

            tool.Name.Should().Be("odata_query");
            tool.Annotations!.ReadOnlyHint.Should().BeTrue();
            tool.InputSchema.GetRawText().Should().Contain("filter");
            tool.InputSchema.GetRawText().Should().NotContain("$filter");
        }

        /// <summary>
        /// Resource mapping uses odata URIs.
        /// </summary>
        [TestMethod]
        public void ToResource_UsesOdataUri()
        {
            var catalog = Catalog();
            var people = catalog.Resources.Single(resource => resource.Name == "People");
            var mapped = ODataMcpHandlers.ToResource(people);

            mapped.Uri.Should().StartWith("odata://");
            mapped.Description.Should().Contain("People who travel.");
        }

        /// <summary>
        /// Core DI registers the parser and remote executor.
        /// </summary>
        [TestMethod]
        public void AddODataMcpCore_RegistersParserAndExecutor()
        {
            var services = new ServiceCollection();
            services.AddODataMcpCore();
            using var provider = services.BuildServiceProvider();

            provider.GetRequiredService<CsdlParser>().Should().NotBeNull();
            provider.GetRequiredService<Microsoft.OData.Mcp.Core.Execution.IODataExecutor>().Should().BeOfType<Microsoft.OData.Mcp.Core.Execution.RemoteODataExecutor>();
        }

        /// <summary>
        /// Binary and stream properties are omitted from generated schemas.
        /// </summary>
        [TestMethod]
        public void IsExposedProperty_OmitsBinaryAndStream()
        {
            ODataMcpCatalog.IsExposedProperty(new Microsoft.OData.Mcp.Core.Models.EdmProperty("Photo", "Edm.Binary")
            {
                Name = "Photo",
                Type = "Edm.Binary"
            }).Should().BeFalse();
            ODataMcpCatalog.IsExposedProperty(new Microsoft.OData.Mcp.Core.Models.EdmProperty("File", "Edm.Stream")
            {
                Name = "File",
                Type = "Edm.Stream"
            }).Should().BeFalse();
            ODataMcpCatalog.IsExposedProperty(new Microsoft.OData.Mcp.Core.Models.EdmProperty("Name", "Edm.String")
            {
                Name = "Name",
                Type = "Edm.String"
            }).Should().BeTrue();
        }

        /// <summary>
        /// JSON type mapping covers numbers, booleans, and empty names.
        /// </summary>
        [TestMethod]
        public void MapJsonType_CoversPrimitiveKinds()
        {
            ODataMcpCatalog.MapJsonType("Edm.Int32").Should().Be("number");
            ODataMcpCatalog.MapJsonType("Edm.Boolean").Should().Be("boolean");
            ODataMcpCatalog.MapJsonType("Edm.String").Should().Be("string");
            ODataMcpCatalog.MapJsonType(" ").Should().Be("string");
        }

        /// <summary>
        /// A cap equal to the generic count omits named families.
        /// </summary>
        [TestMethod]
        public void Catalog_MaxNamedToolsEqualsGenericCount_OmitsNamed()
        {
            var catalog = new ODataMcpCatalog(
                new CsdlParser().ParseFromString(CsdlParserDocumentationTests.DocumentedCsdl),
                new ODataMcpCatalogOptions
                {
                    MaxNamedTools = 10
                });

            catalog.Tools.Should().OnlyContain(tool => tool.Name.StartsWith("odata_", StringComparison.Ordinal));
        }

        /// <summary>
        /// Include lists are ordered first.
        /// </summary>
        [TestMethod]
        public void Catalog_IncludeEntitySets_OrdersFirst()
        {
            var catalog = new ODataMcpCatalog(
                new CsdlParser().ParseFromString(CsdlParserMoreTests.RichCsdl),
                new ODataMcpCatalogOptions
                {
                    IncludeEntitySets = ["Airports"]
                });

            catalog.Tools.First(tool => tool.Name.StartsWith("list_", StringComparison.Ordinal)).Name.Should().Be("list_airports");
        }

        /// <summary>
        /// IncludeUpdate and IncludeDelete can omit those family members.
        /// </summary>
        [TestMethod]
        public void Catalog_IncludeUpdateDeleteFalse_OmitsThoseTools()
        {
            var catalog = new ODataMcpCatalog(
                new CsdlParser().ParseFromString(CsdlParserDocumentationTests.DocumentedCsdl),
                new ODataMcpCatalogOptions
                {
                    IncludeDelete = false,
                    IncludeUpdate = false
                });

            catalog.Tools.Select(tool => tool.Name).Should().NotContain(name => name.StartsWith("update_", StringComparison.Ordinal));
            catalog.Tools.Select(tool => tool.Name).Should().NotContain(name => name.StartsWith("delete_", StringComparison.Ordinal));
        }

        /// <summary>
        /// Completions filter by prefix.
        /// </summary>
        [TestMethod]
        public void CompleteEntitySetNames_Prefix_Filters()
        {
            Catalog().CompleteEntitySetNames("Pe").Should().Equal("People");
            Catalog().CompleteEntitySetNames("z").Should().BeEmpty();
        }

        /// <summary>
        /// DescribeSet falls back when the type is missing.
        /// </summary>
        [TestMethod]
        public void DescribeSet_MissingType_UsesEntityTypeName()
        {
            var set = new Microsoft.OData.Mcp.Core.Models.EdmEntitySet("Ghosts", "Trippin.Ghost")
            {
                Name = "Ghosts",
                EntityType = "Trippin.Ghost"
            };

            ODataMcpCatalog.DescribeSet(set, null).Should().Contain("Trippin.Ghost");
        }

        /// <summary>
        /// Tool descriptions prefer CSDL documentation.
        /// </summary>
        [TestMethod]
        public void ComposeToolDescription_PrefersDocumentation()
        {
            ODataMcpCatalog.ComposeToolDescription("Query People.", "People who travel.").Should().Contain("People who travel.");
            ODataMcpCatalog.ComposeToolDescription("Query People.", "Query People.").Should().Be("Query People.");
            ODataMcpCatalog.ComposeToolDescription("Query People.", null, "No $.").Should().Be("Query People. No $.");
        }

        /// <summary>
        /// Long documented titles fall back to the operational title.
        /// </summary>
        [TestMethod]
        public void ResolveTitle_LongDocumentation_FallsBack()
        {
            ODataMcpCatalog.ResolveTitle("List People", new string('x', 81)).Should().Be("List People");
            ODataMcpCatalog.ResolveTitle("List People", "People who travel.").Should().Be("People who travel.");
        }

        /// <summary>
        /// Entity types can resolve from a short name.
        /// </summary>
        [TestMethod]
        public void ResolveEntityType_ShortName_Matches()
        {
            var catalog = Catalog();
            var set = new Microsoft.OData.Mcp.Core.Models.EdmEntitySet("People", "Person")
            {
                Name = "People",
                EntityType = "Person"
            };

            catalog.ResolveEntityType(set)!.Name.Should().Be("Person");
        }

        /// <summary>
        /// Named families are omitted when the entity type is not in the model.
        /// </summary>
        [TestMethod]
        public void Catalog_UnresolvedEntityType_OmitsNamedFamily()
        {
            var model = new Microsoft.OData.Mcp.Core.Models.EdmModel();
            var container = new Microsoft.OData.Mcp.Core.Models.EdmEntityContainer("Container", "T")
            {
                Name = "Container",
                Namespace = "T"
            };
            container.AddEntitySet(new Microsoft.OData.Mcp.Core.Models.EdmEntitySet("Ghosts", "T.Ghost")
            {
                Name = "Ghosts",
                EntityType = "T.Ghost"
            });
            model.AddEntityContainer(container);

            var catalog = new ODataMcpCatalog(model, new ODataMcpCatalogOptions());
            catalog.Tools.Should().OnlyContain(tool => tool.Name.StartsWith("odata_", StringComparison.Ordinal));
        }

        /// <summary>
        /// Hyphens become underscores in snake_case.
        /// </summary>
        [TestMethod]
        public void ToSnakeCase_Hyphen_BecomesUnderscore()
        {
            ODataMcpCatalog.ToSnakeCase("order-item").Should().Be("order_item");
        }

        /// <summary>
        /// Core DI applies named-client configuration.
        /// </summary>
        [TestMethod]
        public void AddODataMcpCore_ConfigureClient_Runs()
        {
            var configured = false;
            var services = new ServiceCollection();
            services.AddODataMcpCore(client =>
            {
                configured = true;
                client.Timeout = TimeSpan.FromSeconds(5);
            });
            using var provider = services.BuildServiceProvider();
            _ = provider.GetRequiredService<System.Net.Http.IHttpClientFactory>().CreateClient("OData");

            configured.Should().BeTrue();
        }

        /// <summary>
        /// A static model fills the type-shape cache at construction and serves the same instance afterwards.
        /// </summary>
        [TestMethod]
        public void Catalog_StaticModel_FillsTypeShapeCache()
        {
            var catalog = Catalog();
            var person = catalog._model.GetEntityType("Trippin.Person")!;

            catalog.Shapes.Should().ContainKey("Trippin.Person");
            catalog.Shapes["Trippin.Person"].EntitySet!.Name.Should().Be("People");
            catalog.GetShape(person).Should().BeSameAs(catalog.Shapes["Trippin.Person"]);
        }

        /// <summary>
        /// A dynamic model skips the cache and builds a fresh shape per request.
        /// </summary>
        [TestMethod]
        public void Catalog_DynamicModel_SkipsTypeShapeCache()
        {
            var catalog = new ODataMcpCatalog(
                new CsdlParser().ParseFromString(CsdlParserDocumentationTests.DocumentedCsdl),
                new ODataMcpCatalogOptions
                {
                    IsDynamicModel = true
                });
            var person = catalog._model.GetEntityType("Trippin.Person")!;

            catalog.Shapes.Should().BeEmpty();
            var first = catalog.GetShape(person);
            var second = catalog.GetShape(person);
            first.FullName.Should().Be("Trippin.Person");
            first.Should().NotBeSameAs(second);
            catalog.Shapes.Should().BeEmpty();
        }

        /// <summary>
        /// Shape options default to a static model, automatic enum wire format, and no preface.
        /// </summary>
        [TestMethod]
        public void Options_ShapeDefaults()
        {
            var options = new ODataMcpCatalogOptions();

            options.EnumJsonFormat.Should().Be(ODataEnumJsonFormat.Auto);
            options.InstructionsPreface.Should().BeNull();
            options.IsDynamicModel.Should().BeFalse();
        }

        #endregion

        #region Internal Methods

        /// <summary>
        /// Builds a catalog from the documented CSDL fixture.
        /// </summary>
        /// <returns>
        /// The catalog.
        /// </returns>
        internal static ODataMcpCatalog Catalog()
        {
            return new ODataMcpCatalog(
                new CsdlParser().ParseFromString(CsdlParserDocumentationTests.DocumentedCsdl),
                new ODataMcpCatalogOptions());
        }

        #endregion

    }

}
