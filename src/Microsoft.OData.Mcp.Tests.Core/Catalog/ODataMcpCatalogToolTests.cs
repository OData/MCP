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
        /// Generic and named tool descriptions are the short first-call copy from OPTIMIZATION.md §2.
        /// </summary>
        [TestMethod]
        public async Task Catalog_GenericTools_HaveFirstCallDescriptions()
        {
            var catalog = new ODataMcpCatalog(await LiveMetadata.LoadNorthwindModelAsync(), new ODataMcpCatalogOptions());
            var byName = catalog.Tools.ToDictionary(tool => tool.Name, tool => tool.Description);

            byName["odata_list_entity_sets"].Should().Be("Names, types, and keys of declared entity sets.");
            byName["odata_get"].Should().Be("Gets an entity by key.");
            byName["odata_delete"].Should().Be("Deletes an entity by key.");
            byName["odata_navigate"].Should().Be("Follows a navigation property from a key.");
            byName["odata_create"].Should().Be("JSON body in body. Include every property the type lists as required on create (odata_describe_type). Client-assigned keys are required; omit store-generated keys.");
            byName["odata_update"].Should().Be("PATCH in body. Send only fields to change. Omitted fields keep their values. Do not send JSON null for required properties.");
            byName["odata_list_operations"].Should().Be("Unbound operations on the service. Bound operations are on odata_describe_type.");
            byName["odata_query"].Should().Contain("do not include $");
            byName["odata_describe_type"].Should().StartWith("Declared properties, keys, navigations, bound operations, and enums");
            byName["odata_describe_model"].Should().Contain("Do not read $metadata to explore.");
            byName["odata_call"].Should().StartWith("Call a declared operation by name");
            byName["create_customer"].Should().Be("Creates a Customer.");
            byName["update_customer"].Should().Be("PATCH a Customer. Send only fields to change; omit to keep. Do not send JSON null for required properties.");
            byName["get_customer"].Should().Be("Gets a Customer by key.");
            byName["delete_customer"].Should().Be("Deletes a Customer.");
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
                "odata_describe_model",
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
