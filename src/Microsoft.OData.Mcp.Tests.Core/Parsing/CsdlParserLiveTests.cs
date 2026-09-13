// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.OData.Mcp.Tests.Core.Parsing
{

    /// <summary>
    /// Live CSDL parser tests against official OData sample services.
    /// </summary>
    [TestClass]
    public class CsdlParserLiveTests
    {

        #region Public Methods

        /// <summary>
        /// Northwind metadata contains Products with declared properties only.
        /// </summary>
        [TestMethod]
        public async Task Parse_Northwind_ContainsProductsEntitySet()
        {
            var model = await LiveMetadata.LoadNorthwindModelAsync();

            model.EntityContainer.Should().NotBeNull();
            model.EntityContainer!.EntitySets.Select(set => set.Name).Should().Contain("Products");

            var product = model.EntityTypes.Single(type => type.Name == "Product");

            product.Key.Should().NotBeEmpty();
            product.Properties.Should().Contain(property => property.Name == "ProductName");
            product.Properties.Select(property => property.Name).Should().NotContain("NotInCsdl");
        }

        /// <summary>
        /// TripPin metadata contains People and schema-level operations.
        /// </summary>
        [TestMethod]
        public async Task Parse_TripPin_ContainsPeople()
        {
            var model = await LiveMetadata.LoadTripPinModelAsync();

            model.EntityContainer.Should().NotBeNull();
            model.EntityContainer!.EntitySets.Select(set => set.Name).Should().Contain("People");
            model.Functions.Should().NotBeNull();
            model.Actions.Should().NotBeNull();
            (model.Functions.Count + model.Actions.Count).Should().BeGreaterThan(0);
        }

        #endregion

    }

}
