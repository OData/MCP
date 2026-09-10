// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using System.Linq;
using FluentAssertions;
using Microsoft.OData.Mcp.Core.Catalog;
using Microsoft.OData.Mcp.Core.Models;
using Microsoft.OData.Mcp.Core.Parsing;
using Microsoft.OData.Mcp.Tests.Core.Parsing;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.OData.Mcp.Tests.Core.Catalog
{

    /// <summary>
    /// Tests for the cached per-type shape facts: required-on-create, exposed properties, and bound operations.
    /// </summary>
    [TestClass]
    public class EdmTypeShapeTests
    {

        #region Fields

        /// <summary>
        /// CSDL with bound, collection-bound, inherited, and unbound operations.
        /// </summary>
        internal const string OperationsCsdl = """
            <edmx:Edmx Version="4.0" xmlns:edmx="http://docs.oasis-open.org/odata/ns/edmx">
              <edmx:DataServices>
                <Schema Namespace="Trippin" xmlns="http://docs.oasis-open.org/odata/ns/edm">
                  <EntityType Name="Person">
                    <Key>
                      <PropertyRef Name="UserName" />
                    </Key>
                    <Property Name="UserName" Type="Edm.String" Nullable="false" />
                    <Property Name="FirstName" Type="Edm.String" Nullable="false" />
                    <Property Name="Age" Type="Edm.Int64" />
                  </EntityType>
                  <EntityType Name="Employee" BaseType="Trippin.Person">
                    <Property Name="Cost" Type="Edm.Int64" Nullable="false" />
                  </EntityType>
                  <Function Name="GetFavoriteAirline" IsBound="true">
                    <Parameter Name="person" Type="Trippin.Person" />
                    <ReturnType Type="Edm.String" />
                  </Function>
                  <Function Name="Top" IsBound="true">
                    <Parameter Name="people" Type="Collection(Trippin.Person)" />
                    <Parameter Name="count" Type="Edm.Int32" Nullable="false" />
                    <ReturnType Type="Collection(Trippin.Person)" />
                  </Function>
                  <Function Name="GetNearestAirport">
                    <Parameter Name="lat" Type="Edm.Double" Nullable="false" />
                    <ReturnType Type="Edm.String" />
                  </Function>
                  <Action Name="UpdateLastName" IsBound="true">
                    <Parameter Name="person" Type="Trippin.Person" />
                    <Parameter Name="lastName" Type="Edm.String" Nullable="false" />
                    <ReturnType Type="Edm.Boolean" />
                  </Action>
                  <Action Name="Promote" IsBound="true">
                    <Parameter Name="employee" Type="Trippin.Employee" />
                  </Action>
                  <Action Name="ResetDataSource" />
                  <EntityContainer Name="Container">
                    <EntitySet Name="People" EntityType="Trippin.Person" />
                    <EntitySet Name="Staff" EntityType="Trippin.Employee" />
                  </EntityContainer>
                </Schema>
              </edmx:DataServices>
            </edmx:Edmx>
            """;

        #endregion

        #region Public Methods

        /// <summary>
        /// Bound operations are matched by binding parameter type, including operations bound to a base type.
        /// </summary>
        [TestMethod]
        public void BoundOperations_MatchBindingTypeAndBaseChain()
        {
            var model = new CsdlParser().ParseFromString(OperationsCsdl);
            var person = new EdmTypeShape(model, model.GetEntityType("Trippin.Person")!, model.GetEntitySet("People"));
            var employee = new EdmTypeShape(model, model.GetEntityType("Trippin.Employee")!, model.GetEntitySet("Staff"));

            person.BoundFunctions.Select(item => item.Name).Should().Equal("GetFavoriteAirline", "Top");
            person.BoundActions.Select(item => item.Name).Should().Equal("UpdateLastName");
            employee.BoundFunctions.Select(item => item.Name).Should().Equal("GetFavoriteAirline", "Top");
            employee.BoundActions.Select(item => item.Name).Should().Equal("UpdateLastName", "Promote");
            person.BoundActions.Select(item => item.Name).Should().NotContain("ResetDataSource");
            person.BoundFunctions.Select(item => item.Name).Should().NotContain("GetNearestAirport");
        }

        /// <summary>
        /// Collection-bound is detected from the binding parameter type.
        /// </summary>
        [TestMethod]
        public void IsCollectionBound_DetectsCollectionBindingParameter()
        {
            EdmTypeShape.IsCollectionBound("Collection(Trippin.Person)").Should().BeTrue();
            EdmTypeShape.IsCollectionBound("Trippin.Person").Should().BeFalse();
            EdmTypeShape.IsCollectionBound(null).Should().BeFalse();
            EdmTypeShape.IsCollectionBound(" ").Should().BeFalse();
        }

        /// <summary>
        /// Constructor arguments are validated.
        /// </summary>
        [TestMethod]
        public void Constructor_NullArguments_Throw()
        {
            var model = new CsdlParser().ParseFromString(OperationsCsdl);
            var person = model.GetEntityType("Trippin.Person")!;

            var nullModel = () => new EdmTypeShape(null!, person, null);
            var nullType = () => new EdmTypeShape(model, null!, null);

            nullModel.Should().Throw<ArgumentNullException>();
            nullType.Should().Throw<ArgumentNullException>();
        }

        /// <summary>
        /// Exposed properties exclude binary and stream; required-on-create excludes nullable, defaulted, and computed.
        /// </summary>
        [TestMethod]
        public void RequiredOnCreate_UsesNullableDefaultAndComputed()
        {
            var model = new CsdlParser().ParseFromString(CsdlParserMoreTests.EnumCsdl);
            var shape = new EdmTypeShape(model, model.GetEntityType("NS.Widget")!, model.GetEntitySet("Widgets"));

            shape.FullName.Should().Be("NS.Widget");
            shape.EntitySet!.Name.Should().Be("Widgets");
            shape.ExposedProperties.Select(property => property.Name).Should().NotContain("RowVersion");
            shape.RequiredOnCreate.Should().Equal("Sequence", "Name", "Color");
            EdmTypeShape.IsRequiredOnCreate(shape.EntityType.GetProperty("Id")!).Should().BeFalse("Computed");
            EdmTypeShape.IsRequiredOnCreate(shape.EntityType.GetProperty("Notes")!).Should().BeFalse("nullable");
        }

        /// <summary>
        /// A default value makes a non-nullable property optional on create; only the bare non-nullable case is required.
        /// </summary>
        [TestMethod]
        public void RequiredOnCreate_DefaultValue_IsOptional()
        {
            var defaulted = new EdmProperty("Status", "Edm.String") { DefaultValue = "New", Name = "Status", Nullable = false, Type = "Edm.String" };
            var bare = new EdmProperty("Status", "Edm.String") { Name = "Status", Nullable = false, Type = "Edm.String" };
            var blankDefault = new EdmProperty("Status", "Edm.String") { DefaultValue = "  ", Name = "Status", Nullable = false, Type = "Edm.String" };

            EdmTypeShape.IsRequiredOnCreate(defaulted).Should().BeFalse();
            EdmTypeShape.IsRequiredOnCreate(bare).Should().BeTrue();
            EdmTypeShape.IsRequiredOnCreate(blankDefault).Should().BeTrue("a whitespace DefaultValue carries no default");

            var nullProperty = () => EdmTypeShape.IsRequiredOnCreate(null!);
            nullProperty.Should().Throw<ArgumentNullException>();
        }

        #endregion

    }

}
