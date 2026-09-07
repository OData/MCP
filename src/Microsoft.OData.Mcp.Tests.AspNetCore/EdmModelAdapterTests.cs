// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using System.IO;
using System.Linq;
using System.Xml;
using FluentAssertions;
using Microsoft.OData.Edm.Csdl;
using Microsoft.OData.Mcp.AspNetCore.Adaptation;
using Microsoft.OData.Mcp.Tests.Shared.Entities;
using Microsoft.OData.Mcp.Tests.Shared.Models;
using Microsoft.OData.ModelBuilder;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.OData.Mcp.Tests.AspNetCore
{

    /// <summary>
    /// Adapter tests using a real convention model. No mocks.
    /// </summary>
    [TestClass]
    public class EdmModelAdapterTests
    {

        #region Public Methods

        /// <summary>
        /// Convention models map entity sets and keys.
        /// </summary>
        [TestMethod]
        public void Adapter_ConventionModel_MapsEntitySetAndKey()
        {
            var builder = new ODataConventionModelBuilder();

            builder.EntitySet<Customer>("Customers");

            var core = EdmModelAdapter.ToCoreModel(builder.GetEdmModel());
            var customer = core.EntityTypes.Single(type => type.Name == "Customer");

            core.EntityContainer!.EntitySets.Select(set => set.Name).Should().Contain("Customers");
            customer.Key.Should().NotBeEmpty();
        }

        /// <summary>
        /// Properties ignored on the OData model never appear on the Core projection.
        /// </summary>
        [TestMethod]
        public void Adapter_IgnoredProperty_IsNotOnCoreModel()
        {
            var builder = new ODataConventionModelBuilder();

            builder.EntitySet<Customer>("Customers");
            builder.EntityType<Customer>().Ignore(customer => customer.InternalSecret);

            var core = EdmModelAdapter.ToCoreModel(builder.GetEdmModel());
            var customer = core.EntityTypes.Single(type => type.Name == "Customer");

            customer.Properties.Select(property => property.Name).Should().NotContain("InternalSecret");
        }

        /// <summary>
        /// Complex convention models map inheritance, complex types, and navigations.
        /// </summary>
        [TestMethod]
        public void Adapter_ComplexModel_MapsInheritanceAndComplexTypes()
        {
            var core = EdmModelAdapter.ToCoreModel(TestModels.GetComplexModel());
            var employee = core.EntityTypes.Single(type => type.Name == "Employee");
            var address = core.ComplexTypes.Single(type => type.Name == "Address");

            employee.BaseType.Should().NotBeNullOrWhiteSpace();
            employee.NavigationProperties.Should().NotBeEmpty();
            address.Properties.Should().NotBeEmpty();
            core.EntityContainer!.EntitySets.Select(set => set.Name).Should().Contain("Employees");
        }

        /// <summary>
        /// Bound functions and actions on a convention model are copied.
        /// </summary>
        [TestMethod]
        public void Adapter_ConventionModel_MapsOperationsWhenPresent()
        {
            var builder = new ODataConventionModelBuilder();
            builder.EntitySet<Customer>("Customers");
            builder.EntityType<Customer>().Function("IsPremium").Returns<bool>();
            builder.Function("MostValuable").Returns<int>();
            builder.EntityType<Customer>().Action("Approve");
            builder.Action("Reset");

            var core = EdmModelAdapter.ToCoreModel(builder.GetEdmModel());

            core.Functions.Should().Contain(item => item.Name == "MostValuable");
            core.Actions.Should().Contain(item => item.Name == "Reset");
            core.Functions.Should().Contain(item => item.Name == "IsPremium" && item.IsBound);
            core.Actions.Should().Contain(item => item.Name == "Approve" && item.IsBound);
        }

        /// <summary>
        /// Core vocabulary annotations on an EdmLib model are copied.
        /// </summary>
        [TestMethod]
        public void Adapter_CsdlVocabulary_CopiesDescriptions()
        {
            const string csdl = """
                <?xml version="1.0" encoding="utf-8"?>
                <edmx:Edmx Version="4.0" xmlns:edmx="http://docs.oasis-open.org/odata/ns/edmx">
                  <edmx:DataServices>
                    <Schema Namespace="Trippin" xmlns="http://docs.oasis-open.org/odata/ns/edm">
                      <ComplexType Name="Location">
                        <Property Name="Address" Type="Edm.String">
                          <Annotation Term="Org.OData.Core.V1.Description" String="Street address." />
                        </Property>
                        <Annotation Term="Org.OData.Core.V1.Description" String="A location." />
                      </ComplexType>
                      <EntityType Name="Person">
                        <Key>
                          <PropertyRef Name="UserName" />
                        </Key>
                        <Property Name="UserName" Type="Edm.String" Nullable="false">
                          <Annotation Term="Org.OData.Core.V1.Description" String="Unique person name." />
                          <Annotation Term="Org.OData.Core.V1.LongDescription" String="Used as the entity key." />
                        </Property>
                        <NavigationProperty Name="Friends" Type="Collection(Trippin.Person)">
                          <Annotation Term="Org.OData.Core.V1.Description" String="Other people." />
                        </NavigationProperty>
                        <Annotation Term="Org.OData.Core.V1.Description" String="A person who travels." />
                        <Annotation Term="Org.OData.Core.V1.LongDescription" String="Person records include friends." />
                      </EntityType>
                      <Function Name="GetFriends" IsBound="true">
                        <Parameter Name="binding" Type="Trippin.Person" />
                        <ReturnType Type="Collection(Trippin.Person)" />
                        <Annotation Term="Org.OData.Core.V1.Description" String="Lists friends." />
                      </Function>
                      <Action Name="Reset" IsBound="true">
                        <Parameter Name="binding" Type="Trippin.Container" />
                        <Annotation Term="Org.OData.Core.V1.Description" String="Resets data." />
                      </Action>
                      <EntityContainer Name="Container">
                        <Annotation Term="Org.OData.Core.V1.Description" String="The container." />
                        <EntitySet Name="People" EntityType="Trippin.Person">
                          <Annotation Term="Org.OData.Core.V1.Description" String="People who travel." />
                        </EntitySet>
                        <Singleton Name="Me" Type="Trippin.Person">
                          <Annotation Term="Org.OData.Core.V1.Description" String="The current user." />
                        </Singleton>
                      </EntityContainer>
                    </Schema>
                  </edmx:DataServices>
                </edmx:Edmx>
                """;
            using var reader = XmlReader.Create(new StringReader(csdl));
            var edm = CsdlReader.Parse(reader);
            var core = EdmModelAdapter.ToCoreModel(edm);
            var person = core.EntityTypes.Single(type => type.Name == "Person");
            var people = core.EntityContainer!.EntitySets.Single(set => set.Name == "People");

            person.Description.Should().Be("A person who travels.");
            person.LongDescription.Should().Be("Person records include friends.");
            person.GetProperty("UserName")!.Description.Should().Be("Unique person name.");
            person.GetNavigationProperty("Friends")!.Description.Should().Be("Other people.");
            people.Description.Should().Be("People who travel.");
            core.ComplexTypes.Single(type => type.Name == "Location").Description.Should().Be("A location.");
            core.Functions.Single(item => item.Name == "GetFriends").Description.Should().Be("Lists friends.");
            core.Actions.Single(item => item.Name == "Reset").Description.Should().Be("Resets data.");
            core.EntityContainer!.Description.Should().Be("The container.");
            core.EntityContainer.Singletons.Single(item => item.Name == "Me").Description.Should().Be("The current user.");
        }

        /// <summary>
        /// Null models are rejected.
        /// </summary>
        [TestMethod]
        public void ToCoreModel_Null_Throws()
        {
            var act = () => EdmModelAdapter.ToCoreModel(null!);

            act.Should().Throw<ArgumentNullException>();
        }

        /// <summary>
        /// ReadString only returns string constants.
        /// </summary>
        [TestMethod]
        public void ReadString_NonString_ReturnsNull()
        {
            EdmModelAdapter.ReadString(null).Should().BeNull();
            EdmModelAdapter.ReadString(new Microsoft.OData.Edm.Vocabularies.EdmIntegerConstant(1)).Should().BeNull();
        }

        /// <summary>
        /// Vocabulary helpers reject null arguments.
        /// </summary>
        [TestMethod]
        public void ApplyVocabulary_NullArguments_Throw()
        {
            var model = TestModels.GetSimpleModel();
            var customer = model.EntityContainer.FindEntitySet("Customers");
            customer.Should().NotBeNull();

            var nullModel = () => EdmModelAdapter.ApplyVocabulary(null!, customer, _ => { }, _ => { });
            var nullTarget = () => EdmModelAdapter.ApplyVocabulary(model, null!, _ => { }, _ => { });
            var nullSet = () => EdmModelAdapter.ApplyVocabulary(model, customer, null!, _ => { });

            nullModel.Should().Throw<ArgumentNullException>();
            nullTarget.Should().Throw<ArgumentNullException>();
            nullSet.Should().Throw<ArgumentNullException>();
        }

        #endregion

    }

}
