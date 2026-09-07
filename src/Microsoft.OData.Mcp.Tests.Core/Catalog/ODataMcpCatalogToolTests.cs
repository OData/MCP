// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.OData.Mcp.Core.Catalog;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.OData.Mcp.Tests.Core.Catalog
{

    /// <summary>
    /// Generic tool catalog tests.
    /// </summary>
    [TestClass]
    public class ODataMcpCatalogToolTests
    {

        #region Public Methods

        /// <summary>
        /// Generic query tool advertises filter without a $ prefix.
        /// </summary>
        [TestMethod]
        public async Task Catalog_GenericQueryTool_HasFilterWithoutDollar()
        {
            var catalog = new ODataMcpCatalog(await LiveMetadata.LoadNorthwindModelAsync(), new ODataMcpCatalogOptions());
            var query = catalog.Tools.Single(tool => tool.Name == "odata_query");

            query.InputSchema.Should().Contain("\"filter\"");
            query.InputSchema.Should().NotContain("\"$filter\"");
            query.ReadOnlyHint.Should().BeTrue();
        }

        /// <summary>
        /// Generic tool names are advertised in the documented order.
        /// </summary>
        [TestMethod]
        public async Task Catalog_GenericTools_HaveExpectedNames()
        {
            var catalog = new ODataMcpCatalog(await LiveMetadata.LoadNorthwindModelAsync(), new ODataMcpCatalogOptions());
            var generic = catalog.Tools.Where(tool => tool.Name.StartsWith("odata_", StringComparison.Ordinal)).Select(tool => tool.Name).ToList();

            generic.Should().ContainInOrder(
                "odata_list_entity_sets",
                "odata_describe_type",
                "odata_query",
                "odata_get",
                "odata_create",
                "odata_update",
                "odata_delete",
                "odata_navigate",
                "odata_list_operations",
                "odata_call");
        }

        #endregion

    }

}
