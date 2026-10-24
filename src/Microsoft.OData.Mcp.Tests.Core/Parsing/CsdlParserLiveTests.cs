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

        /// <summary>
        /// Live TripPin (Restier sample) puts types, bound actions, and unbound imports in one schema.
        /// Bound <c>ShareTrip</c> has no ActionImport; unbound <c>ResetDataSource</c> does.
        /// </summary>
        [TestMethod]
        public async Task Parse_TripPin_OperationsShareEntitySchema()
        {
            var model = await LiveMetadata.LoadTripPinModelAsync();
            var share = model.Actions.Single(action => action.Name == "ShareTrip");
            var reset = model.Actions.Single(action => action.Name == "ResetDataSource");
            var favorite = model.Functions.Single(function => function.Name == "GetFavoriteAirline");

            share.Namespace.Should().Be("Trippin");
            share.IsBound.Should().BeTrue();
            reset.Namespace.Should().Be("Trippin");
            reset.IsBound.Should().BeFalse();
            favorite.Namespace.Should().Be("Trippin");
            favorite.IsBound.Should().BeTrue();
            model.EntityTypes.Single(type => type.Name == "Person").Namespace.Should().Be("Trippin");
            model.EntityContainer!.Namespace.Should().Be("Trippin");
            model.EntityContainer.ActionImports.Should().ContainSingle(import =>
                import.Name == "ResetDataSource" && import.Action == "Trippin.ResetDataSource");
            model.EntityContainer.ActionImports.Should().NotContain(import => import.Name == "ShareTrip");
        }

        /// <summary>
        /// Live Northwind keeps entity types in <c>NorthwindModel</c> and the container in a second schema.
        /// </summary>
        [TestMethod]
        public async Task Parse_Northwind_TypesAndContainerAreSeparateSchemas()
        {
            var model = await LiveMetadata.LoadNorthwindModelAsync();

            model.EntityTypes.Single(type => type.Name == "Product").Namespace.Should().Be("NorthwindModel");
            model.EntityContainer!.Name.Should().Be("NorthwindEntities");
            model.EntityContainer.Namespace.Should().Be("ODataWebV4.Northwind.Model");
        }

        #endregion

    }

}
