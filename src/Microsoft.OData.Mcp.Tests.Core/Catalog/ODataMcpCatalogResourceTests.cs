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
    /// Resource catalog tests against live Northwind metadata.
    /// </summary>
    [TestClass]
    public class ODataMcpCatalogResourceTests
    {

        #region Public Methods

        /// <summary>
        /// Northwind catalog advertises metadata, Products, and key templates.
        /// </summary>
        [TestMethod]
        public async Task Catalog_Northwind_ListsMetadataAndProductsResource()
        {
            var model = await LiveMetadata.LoadNorthwindModelAsync();
            var catalog = new ODataMcpCatalog(
                model,
                new ODataMcpCatalogOptions
                {
                    RouteName = "remote"
                });

            catalog.Resources.Select(resource => resource.Uri).Should().Contain("odata://remote/$metadata");
            catalog.Resources.Select(resource => resource.Uri).Should().Contain("odata://remote/Products");
            catalog.ResourceTemplates.Select(template => template.UriTemplate).Should().Contain("odata://remote/{entitySet}({key})");
            catalog.CompleteEntitySetNames("Pro").Should().Contain("Products");
            catalog.CompleteEntitySetNames(string.Empty).Should().Contain("Products");

            var products = catalog.Resources.Single(resource => resource.Uri.EndsWith("/Products", StringComparison.Ordinal));

            products.Description.Should().NotBeNullOrWhiteSpace();
            products.ReadContents.Should().Contain("ProductID");
            products.ReadContents.Should().NotContain("NotInCsdl");
        }

        #endregion

    }

}
