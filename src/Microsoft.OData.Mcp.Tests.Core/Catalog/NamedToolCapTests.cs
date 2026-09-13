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
    /// Named tool cap and CSDL documentation tests.
    /// </summary>
    [TestClass]
    public class NamedToolCapTests
    {

        #region Public Methods

        /// <summary>
        /// Named tools never emit a partial CRUD family.
        /// </summary>
        [TestMethod]
        public async Task Catalog_MaxNamedTools_DoesNotSplitCrudFamily()
        {
            var catalog = new ODataMcpCatalog(
                await LiveMetadata.LoadNorthwindModelAsync(),
                new ODataMcpCatalogOptions
                {
                    MaxNamedTools = 16
                });
            var named = catalog.Tools.Where(tool => !tool.Name.StartsWith("odata_", StringComparison.Ordinal)).ToList();

            named.Should().NotBeEmpty();
            catalog.Tools.Count.Should().BeLessThanOrEqualTo(16);

            foreach (var group in named.GroupBy(tool => tool.EntitySetName))
            {
                var ops = group.Select(tool => tool.Name).ToList();

                if (ops.Any(name => name.StartsWith("list_", StringComparison.Ordinal)))
                {
                    ops.Should().Contain(name => name.StartsWith("get_", StringComparison.Ordinal));
                }
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
