// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using FluentAssertions;
using Microsoft.OData.Mcp.Core.Parsing;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.OData.Mcp.Tests.Core.Parsing
{

    /// <summary>
    /// Verifies CSDL <c>Documentation</c> nodes and Core vocabulary annotations populate the EDM.
    /// </summary>
    [TestClass]
    public class CsdlParserDocumentationTests
    {

        #region Fields

        internal const string DocumentedCsdl = """
            <?xml version="1.0" encoding="utf-8"?>
            <edmx:Edmx Version="4.0" xmlns:edmx="http://docs.oasis-open.org/odata/ns/edmx">
              <edmx:DataServices>
                <Schema Namespace="Trippin" xmlns="http://docs.oasis-open.org/odata/ns/edm">
                  <EntityType Name="Person">
                    <Documentation>
                      <Summary>A person who travels.</Summary>
                      <LongDescription>Person records include friends and trips.</LongDescription>
                    </Documentation>
                    <Key>
                      <PropertyRef Name="UserName" />
                    </Key>
                    <Property Name="UserName" Type="Edm.String" Nullable="false">
                      <Documentation>
                        <Summary>Unique person name.</Summary>
                      </Documentation>
                    </Property>
                    <Property Name="FirstName" Type="Edm.String" Nullable="false">
                      <Annotation Term="Org.OData.Core.V1.Description" String="Given name." />
                    </Property>
                    <NavigationProperty Name="Friends" Type="Collection(Trippin.Person)">
                      <Annotation Term="Org.OData.Core.V1.Description">
                        <String>Other people this person knows.</String>
                      </Annotation>
                    </NavigationProperty>
                  </EntityType>
                  <EntityContainer Name="Container">
                    <EntitySet Name="People" EntityType="Trippin.Person">
                      <Annotation Term="Org.OData.Core.V1.Description" String="People who travel." />
                      <Annotation Term="Org.OData.Core.V1.LongDescription" String="The People entity set in TripPin." />
                    </EntitySet>
                  </EntityContainer>
                  <Annotations Target="Trippin.Person/UserName">
                    <Annotation Term="Org.OData.Core.V1.LongDescription" String="Used as the entity key." />
                  </Annotations>
                </Schema>
              </edmx:DataServices>
            </edmx:Edmx>
            """;

        #endregion

        #region Public Methods

        /// <summary>
        /// Nested CSDL Documentation and Core.Description populate types, sets, properties, and navigations.
        /// </summary>
        [TestMethod]
        public void ParseFromString_DocumentationNodes_PopulateDescriptions()
        {
            var model = new CsdlParser().ParseFromString(DocumentedCsdl);
            var person = model.EntityTypes.Should().ContainSingle(type => type.Name == "Person").Subject;
            var people = model.EntityContainer!.EntitySets.Should().ContainSingle(set => set.Name == "People").Subject;
            var userName = person.GetProperty("UserName");
            var firstName = person.GetProperty("FirstName");
            var friends = person.GetNavigationProperty("Friends");

            person.Description.Should().Be("A person who travels.");
            person.LongDescription.Should().Be("Person records include friends and trips.");
            people.Description.Should().Be("People who travel.");
            people.LongDescription.Should().Be("The People entity set in TripPin.");
            userName.Should().NotBeNull();
            userName!.Description.Should().Be("Unique person name.");
            userName.LongDescription.Should().Be("Used as the entity key.");
            firstName.Should().NotBeNull();
            firstName!.Description.Should().Be("Given name.");
            friends.Should().NotBeNull();
            friends!.Description.Should().Be("Other people this person knows.");
        }

        /// <summary>
        /// LongDescription is not stored as Description when both vocabulary terms are present.
        /// </summary>
        [TestMethod]
        public void ParseFromString_DescriptionAndLongDescription_DoNotCollide()
        {
            var model = new CsdlParser().ParseFromString(DocumentedCsdl);
            var people = model.EntityContainer!.EntitySets.Should().ContainSingle(set => set.Name == "People").Subject;

            people.Description.Should().NotBe(people.LongDescription);
            people.Description.Should().Be("People who travel.");
        }

        #endregion

    }

}
