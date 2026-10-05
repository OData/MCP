// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.OData.Mcp.Core.Catalog;
using Microsoft.OData.Mcp.Core.Parsing;
using Microsoft.OData.Mcp.Tests.Core.Parsing;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.OData.Mcp.Tests.Core.Catalog
{

    /// <summary>
    /// Named-family completeness and CSDL documentation tests.
    /// </summary>
    [TestClass]
    public class NamedToolCapTests
    {

        #region Public Methods

        /// <summary>
        /// Every declared entity set gets a complete named family.
        /// </summary>
        [TestMethod]
        public async Task Catalog_NamedTools_CompleteFamilyForEverySet()
        {
            var catalog = new ODataMcpCatalog(await LiveMetadata.LoadNorthwindModelAsync(), new ODataMcpCatalogOptions());
            var setNames = catalog._model.EntityContainer!.EntitySets.Select(set => set.Name).ToList();
            setNames.Should().NotBeEmpty();

            foreach (var setName in setNames)
            {
                var family = catalog.Tools.Where(tool => tool.EntitySetName == setName).Select(tool => tool.Name).ToList();
                family.Should().Contain(name => name.StartsWith("list_", StringComparison.Ordinal));
                family.Should().Contain(name => name.StartsWith("get_", StringComparison.Ordinal));
                family.Should().Contain(name => name.StartsWith("create_", StringComparison.Ordinal));
                family.Should().Contain(name => name.StartsWith("update_", StringComparison.Ordinal));
                family.Should().Contain(name => name.StartsWith("delete_", StringComparison.Ordinal));
            }
        }

        /// <summary>
        /// Named create schemas only include declared, non-binary properties.
        /// </summary>
        [TestMethod]
        public async Task Catalog_NamedCreateSchema_UsesDeclaredPropertiesOnly()
        {
            var catalog = new ODataMcpCatalog(await LiveMetadata.LoadNorthwindModelAsync(), new ODataMcpCatalogOptions
            {
                IncludeEntitySets = ["Products"]
            });
            var create = catalog.Tools.Single(tool => tool.Name == "create_product");

            create.InputSchema.Should().Contain("ProductName");
            create.InputSchema.Should().NotContain("NotInCsdl");
            create.InputSchema.Should().NotContain("\"$filter\"");
        }

        /// <summary>
        /// CSDL documentation flows into named tool and resource descriptions.
        /// </summary>
        [TestMethod]
        public void Catalog_DocumentedPeople_UsesCsdlDocumentation()
        {
            var model = new CsdlParser().ParseFromString(CsdlParserDocumentationTests.DocumentedCsdl);
            var catalog = new ODataMcpCatalog(model, new ODataMcpCatalogOptions
            {
                RouteName = "remote"
            });
            var list = catalog.Tools.Single(tool => tool.Name == "list_people");
            var people = catalog.Resources.Single(resource => resource.Name == "People");

            list.Description.Should().Contain("People who travel.");
            list.Description.Should().Contain("Query parameter names do not include $.");
            people.Description.Should().Contain("People who travel.");
            people.ReadContents.Should().Contain("Unique person name.");
            people.ReadContents.Should().Contain("Other people this person knows.");
        }

        #endregion

    }

}
