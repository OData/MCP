// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using System.IO;
using System.Linq;
using System.Text;
using FluentAssertions;
using Microsoft.OData.Mcp.Core.Models;
using Microsoft.OData.Mcp.Core.Parsing;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.OData.Mcp.Tests.Core.Parsing
{

    /// <summary>
    /// Additional CSDL parser coverage.
    /// </summary>
    [TestClass]
    public class CsdlParserMoreTests
    {

        #region Public Methods

        /// <summary>
        /// Stream parse matches string parse.
        /// </summary>
        [TestMethod]
        public void ParseFromStream_DocumentedCsdl_PopulatesPeople()
        {
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(CsdlParserDocumentationTests.DocumentedCsdl));
            var model = new CsdlParser().ParseFromStream(stream);

            model.EntityContainer!.EntitySets.Select(set => set.Name).Should().Contain("People");
        }

        /// <summary>
        /// File parse loads the same CSDL.
        /// </summary>
        [TestMethod]
        public void ParseFromFile_DocumentedCsdl_PopulatesPeople()
        {
            var path = Path.Combine(Path.GetTempPath(), $"odata-mcp-{Guid.NewGuid():N}.xml");
            File.WriteAllText(path, CsdlParserDocumentationTests.DocumentedCsdl);
            try
            {
                var model = new CsdlParser().ParseFromFile(path);
                model.EntityTypes.Select(type => type.Name).Should().Contain("Person");
            }
            finally
            {
                File.Delete(path);
            }
        }

        /// <summary>
        /// Missing file throws.
        /// </summary>
        [TestMethod]
        public void ParseFromFile_Missing_Throws()
        {
            var act = () => new CsdlParser().ParseFromFile(Path.Combine(Path.GetTempPath(), "does-not-exist-odata-mcp.xml"));
            act.Should().Throw<FileNotFoundException>();
        }

        /// <summary>
        /// Documentation helper returns the first non-whitespace value.
        /// </summary>
        [TestMethod]
        public void EdmDocumentation_First_SkipsNullAndBlank()
        {
            EdmDocumentation.First(null, "  ", "Summary").Should().Be("Summary");
            EdmDocumentation.First(null, " ").Should().BeNull();
        }

        /// <summary>
        /// A logger-backed parser still loads CSDL.
        /// </summary>
        [TestMethod]
        public void ParseFromString_WithLogger_ParsesRichCsdl()
        {
            var model = new CsdlParser(Microsoft.Extensions.Logging.Abstractions.NullLogger<CsdlParser>.Instance).ParseFromString(RichCsdl);

            model.Version.Should().Be("4.01");
            model.ComplexTypes.Should().Contain(type => type.Name == "Location");
            model.Functions.Should().Contain(item => item.Name == "GetNearestAirport");
            model.Actions.Should().Contain(item => item.Name == "ResetDataSource");
            model.EntityContainer!.Singletons.Should().Contain(item => item.Name == "Me");
            model.EntityContainer.FunctionImports.Should().Contain(item => item.Name == "GetNearestAirport");
            model.EntityContainer.ActionImports.Should().Contain(item => item.Name == "ResetDataSource");
        }

        /// <summary>
        /// Property facets, referential constraints, and OnDelete are parsed.
        /// </summary>
        [TestMethod]
        public void ParseFromString_RichCsdl_ParsesFacetsAndConstraints()
        {
            var model = new CsdlParser().ParseFromString(RichCsdl);
            var airline = model.EntityTypes.Single(type => type.Name == "Airline");
            var code = airline.GetProperty("AirlineCode")!;
            var name = airline.GetProperty("Name")!;
            var amount = airline.GetProperty("Score")!;
            var photo = model.EntityTypes.Single(type => type.Name == "Person").GetNavigationProperty("Photo")!;
            var flights = model.EntityTypes.Single(type => type.Name == "Airline").GetNavigationProperty("Flights")!;

            code.MaxLength.Should().BeNull();
            name.MaxLength.Should().Be(40);
            name.Unicode.Should().BeFalse();
            name.DefaultValue.Should().Be("n/a");
            amount.Precision.Should().Be(5);
            amount.Scale.Should().Be(2);
            amount.Unicode.Should().BeTrue();
            flights.OnDelete.Should().Be("Cascade");
            flights.Partner.Should().Be("Airline");
            flights.ContainsTarget.Should().BeTrue();
            flights.ReferentialConstraints.Should().ContainSingle();
            photo.Nullable.Should().BeFalse();
        }

        /// <summary>
        /// Schema-level Annotations populate documentation on every supported target.
        /// </summary>
        [TestMethod]
        public void ParseFromString_TargetedAnnotations_PopulateAllKinds()
        {
            var model = new CsdlParser().ParseFromString(RichCsdl);
            var person = model.EntityTypes.Single(type => type.Name == "Person");
            var location = model.ComplexTypes.Single(type => type.Name == "Location");
            var point = model.ComplexTypes.Single(type => type.Name == "Point");
            var function = model.Functions.Single(item => item.Name == "GetNearestAirport");
            var action = model.Actions.Single(item => item.Name == "ResetDataSource");
            var container = model.EntityContainer!;
            var me = container.Singletons.Single(item => item.Name == "Me");
            var friends = person.GetNavigationProperty("Friends")!;

            person.LongDescription.Should().Be("Person long from target.");
            location.Description.Should().Be("A location.");
            location.LongDescription.Should().Be("Street and city.");
            point.Description.Should().Be("A point.");
            point.LongDescription.Should().Be("Point long.");
            function.Description.Should().Be("Nearest airport.");
            function.LongDescription.Should().Be("Function long.");
            action.Description.Should().Be("Reset the data.");
            action.LongDescription.Should().Be("Action long.");
            container.Description.Should().Be("The container.");
            container.LongDescription.Should().Be("Container long.");
            me.Description.Should().Be("The current user.");
            me.LongDescription.Should().Be("Singleton long.");
            friends.LongDescription.Should().Be("Friends long.");
            function.Parameters.Single(item => item.Name == "lat").Description.Should().Be("Latitude.");
        }

        /// <summary>
        /// Multiple leftover Description terms keep the token-boundary match.
        /// </summary>
        [TestMethod]
        public void ParseFromString_MultipleDescriptionTerms_KeepsTokenBoundary()
        {
            var model = new CsdlParser().ParseFromString(RichCsdl);
            var airport = model.EntityTypes.Single(type => type.Name == "Airport");

            airport.Description.Should().Be("Real description.");
            airport.LongDescription.Should().Be("Airport long.");
        }

        /// <summary>
        /// Stream parse rejects a null stream.
        /// </summary>
        [TestMethod]
        public void ParseFromStream_Null_Throws()
        {
            var act = () => new CsdlParser().ParseFromStream(null!);
            act.Should().Throw<ArgumentNullException>();
        }

        /// <summary>
        /// Stream parse rejects malformed XML.
        /// </summary>
        [TestMethod]
        public void ParseFromStream_InvalidXml_Throws()
        {
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes("<not xml"));
            var act = () => new CsdlParser().ParseFromStream(stream);
            act.Should().Throw<System.Xml.XmlException>();
        }

        /// <summary>
        /// File parse rejects a blank path.
        /// </summary>
        [TestMethod]
        public void ParseFromFile_Blank_Throws()
        {
            var act = () => new CsdlParser().ParseFromFile(" ");
            act.Should().Throw<ArgumentException>();
        }

        /// <summary>
        /// File parse rejects malformed XML.
        /// </summary>
        [TestMethod]
        public void ParseFromFile_InvalidXml_Throws()
        {
            var path = Path.Combine(Path.GetTempPath(), $"odata-mcp-{Guid.NewGuid():N}.xml");
            File.WriteAllText(path, "<not xml");
            try
            {
                var act = () => new CsdlParser().ParseFromFile(path);
                act.Should().Throw<System.Xml.XmlException>();
            }
            finally
            {
                File.Delete(path);
            }
        }

        /// <summary>
        /// Well-formed XML that is not EDMX wraps as InvalidOperationException.
        /// </summary>
        [TestMethod]
        public void ParseFromString_NotEdmx_ThrowsInvalidOperation()
        {
            var act = () => new CsdlParser().ParseFromString("<root/>");
            act.Should().Throw<InvalidOperationException>();
        }

        /// <summary>
        /// EDMX without DataServices throws.
        /// </summary>
        [TestMethod]
        public void ParseFromString_MissingDataServices_Throws()
        {
            const string xml = """
                <edmx:Edmx Version="4.0" xmlns:edmx="http://docs.oasis-open.org/odata/ns/edmx" />
                """;
            var act = () => new CsdlParser().ParseFromString(xml);
            act.Should().Throw<InvalidOperationException>().WithInnerException<InvalidOperationException>().WithMessage("*DataServices*");
        }

        /// <summary>
        /// Schema without Namespace throws.
        /// </summary>
        [TestMethod]
        public void ParseFromString_SchemaMissingNamespace_Throws()
        {
            const string xml = """
                <edmx:Edmx Version="4.0" xmlns:edmx="http://docs.oasis-open.org/odata/ns/edmx">
                  <edmx:DataServices>
                    <Schema xmlns="http://docs.oasis-open.org/odata/ns/edm" />
                  </edmx:DataServices>
                </edmx:Edmx>
                """;
            var act = () => new CsdlParser().ParseFromString(xml);
            act.Should().Throw<InvalidOperationException>().WithInnerException<InvalidOperationException>().WithMessage("*Namespace*");
        }

        /// <summary>
        /// EntityType without Name throws.
        /// </summary>
        [TestMethod]
        public void ParseFromString_EntityTypeMissingName_Throws()
        {
            const string xml = """
                <edmx:Edmx Version="4.0" xmlns:edmx="http://docs.oasis-open.org/odata/ns/edmx">
                  <edmx:DataServices>
                    <Schema Namespace="T" xmlns="http://docs.oasis-open.org/odata/ns/edm">
                      <EntityType />
                    </Schema>
                  </edmx:DataServices>
                </edmx:Edmx>
                """;
            var act = () => new CsdlParser().ParseFromString(xml);
            act.Should().Throw<InvalidOperationException>().WithInnerException<InvalidOperationException>().WithMessage("*Name*");
        }

        /// <summary>
        /// Property without Type throws.
        /// </summary>
        [TestMethod]
        public void ParseFromString_PropertyMissingType_Throws()
        {
            const string xml = """
                <edmx:Edmx Version="4.0" xmlns:edmx="http://docs.oasis-open.org/odata/ns/edmx">
                  <edmx:DataServices>
                    <Schema Namespace="T" xmlns="http://docs.oasis-open.org/odata/ns/edm">
                      <EntityType Name="E">
                        <Property Name="Id" />
                      </EntityType>
                    </Schema>
                  </edmx:DataServices>
                </edmx:Edmx>
                """;
            var act = () => new CsdlParser().ParseFromString(xml);
            act.Should().Throw<InvalidOperationException>().WithInnerException<InvalidOperationException>().WithMessage("*Type*");
        }

        /// <summary>
        /// NavigationProperty without Type throws.
        /// </summary>
        [TestMethod]
        public void ParseFromString_NavigationMissingType_Throws()
        {
            const string xml = """
                <edmx:Edmx Version="4.0" xmlns:edmx="http://docs.oasis-open.org/odata/ns/edmx">
                  <edmx:DataServices>
                    <Schema Namespace="T" xmlns="http://docs.oasis-open.org/odata/ns/edm">
                      <EntityType Name="E">
                        <NavigationProperty Name="Other" />
                      </EntityType>
                    </Schema>
                  </edmx:DataServices>
                </edmx:Edmx>
                """;
            var act = () => new CsdlParser().ParseFromString(xml);
            act.Should().Throw<InvalidOperationException>().WithInnerException<InvalidOperationException>().WithMessage("*Type*");
        }

        /// <summary>
        /// FunctionImport without Function throws.
        /// </summary>
        [TestMethod]
        public void ParseFromString_FunctionImportMissingFunction_Throws()
        {
            const string xml = """
                <edmx:Edmx Version="4.0" xmlns:edmx="http://docs.oasis-open.org/odata/ns/edmx">
                  <edmx:DataServices>
                    <Schema Namespace="T" xmlns="http://docs.oasis-open.org/odata/ns/edm">
                      <EntityContainer Name="C">
                        <FunctionImport Name="F" />
                      </EntityContainer>
                    </Schema>
                  </edmx:DataServices>
                </edmx:Edmx>
                """;
            var act = () => new CsdlParser().ParseFromString(xml);
            act.Should().Throw<InvalidOperationException>().WithInnerException<InvalidOperationException>().WithMessage("*Function*");
        }

        /// <summary>
        /// ActionImport without Action throws.
        /// </summary>
        [TestMethod]
        public void ParseFromString_ActionImportMissingAction_Throws()
        {
            const string xml = """
                <edmx:Edmx Version="4.0" xmlns:edmx="http://docs.oasis-open.org/odata/ns/edmx">
                  <edmx:DataServices>
                    <Schema Namespace="T" xmlns="http://docs.oasis-open.org/odata/ns/edm">
                      <EntityContainer Name="C">
                        <ActionImport Name="A" />
                      </EntityContainer>
                    </Schema>
                  </edmx:DataServices>
                </edmx:Edmx>
                """;
            var act = () => new CsdlParser().ParseFromString(xml);
            act.Should().Throw<InvalidOperationException>().WithInnerException<InvalidOperationException>().WithMessage("*Action*");
        }

        /// <summary>
        /// File parse wraps non-XML failures as InvalidOperationException.
        /// </summary>
        [TestMethod]
        public void ParseFromFile_NotEdmx_ThrowsInvalidOperation()
        {
            var path = Path.Combine(Path.GetTempPath(), $"odata-mcp-{Guid.NewGuid():N}.xml");
            File.WriteAllText(path, "<root/>");
            try
            {
                var act = () => new CsdlParser().ParseFromFile(path);
                act.Should().Throw<InvalidOperationException>();
            }
            finally
            {
                File.Delete(path);
            }
        }

        /// <summary>
        /// EntitySet without Name throws.
        /// </summary>
        [TestMethod]
        public void ParseFromString_EntitySetMissingName_Throws()
        {
            const string xml = """
                <edmx:Edmx Version="4.0" xmlns:edmx="http://docs.oasis-open.org/odata/ns/edmx">
                  <edmx:DataServices>
                    <Schema Namespace="T" xmlns="http://docs.oasis-open.org/odata/ns/edm">
                      <EntityContainer Name="C">
                        <EntitySet EntityType="T.E" />
                      </EntityContainer>
                    </Schema>
                  </edmx:DataServices>
                </edmx:Edmx>
                """;
            var act = () => new CsdlParser().ParseFromString(xml);
            act.Should().Throw<InvalidOperationException>().WithInnerException<InvalidOperationException>().WithMessage("*Name*");
        }

        /// <summary>
        /// EntitySet without EntityType throws.
        /// </summary>
        [TestMethod]
        public void ParseFromString_EntitySetMissingEntityType_Throws()
        {
            const string xml = """
                <edmx:Edmx Version="4.0" xmlns:edmx="http://docs.oasis-open.org/odata/ns/edmx">
                  <edmx:DataServices>
                    <Schema Namespace="T" xmlns="http://docs.oasis-open.org/odata/ns/edm">
                      <EntityContainer Name="C">
                        <EntitySet Name="Es" />
                      </EntityContainer>
                    </Schema>
                  </edmx:DataServices>
                </edmx:Edmx>
                """;
            var act = () => new CsdlParser().ParseFromString(xml);
            act.Should().Throw<InvalidOperationException>().WithInnerException<InvalidOperationException>().WithMessage("*EntityType*");
        }

        /// <summary>
        /// Singleton without Type throws.
        /// </summary>
        [TestMethod]
        public void ParseFromString_SingletonMissingType_Throws()
        {
            const string xml = """
                <edmx:Edmx Version="4.0" xmlns:edmx="http://docs.oasis-open.org/odata/ns/edmx">
                  <edmx:DataServices>
                    <Schema Namespace="T" xmlns="http://docs.oasis-open.org/odata/ns/edm">
                      <EntityContainer Name="C">
                        <Singleton Name="Me" />
                      </EntityContainer>
                    </Schema>
                  </edmx:DataServices>
                </edmx:Edmx>
                """;
            var act = () => new CsdlParser().ParseFromString(xml);
            act.Should().Throw<InvalidOperationException>().WithInnerException<InvalidOperationException>().WithMessage("*Type*");
        }

        /// <summary>
        /// Invalid MaxLength is ignored and IncludeInServiceDocument is parsed.
        /// </summary>
        [TestMethod]
        public void ParseFromString_InvalidMaxLength_IsIgnored()
        {
            const string xml = """
                <edmx:Edmx Version="4.0" xmlns:edmx="http://docs.oasis-open.org/odata/ns/edmx">
                  <edmx:DataServices>
                    <Schema Namespace="T" xmlns="http://docs.oasis-open.org/odata/ns/edm">
                      <EntityType Name="E">
                        <Property Name="Name" Type="Edm.String" MaxLength="nope" Precision="x" Unicode="maybe" Scale="nope" />
                      </EntityType>
                      <EntityContainer Name="C">
                        <EntitySet Name="Es" EntityType="T.E" IncludeInServiceDocument="false">
                          <NavigationPropertyBinding Path="X" />
                        </EntitySet>
                      </EntityContainer>
                    </Schema>
                  </edmx:DataServices>
                </edmx:Edmx>
                """;
            var model = new CsdlParser().ParseFromString(xml);
            var property = model.EntityTypes[0].Properties[0];
            var set = model.EntityContainer!.EntitySets[0];

            property.MaxLength.Should().BeNull();
            property.Precision.Should().BeNull();
            property.Unicode.Should().BeNull();
            set.IncludeInServiceDocument.Should().BeFalse();
            set.NavigationPropertyBindings.Should().BeEmpty();
        }

        /// <summary>
        /// Nested Documentation with only LongDescription fills Description.
        /// </summary>
        [TestMethod]
        public void ParseFromString_LongDescriptionOnly_FillsDescription()
        {
            const string xml = """
                <edmx:Edmx Version="4.0" xmlns:edmx="http://docs.oasis-open.org/odata/ns/edmx">
                  <edmx:DataServices>
                    <Schema Namespace="T" xmlns="http://docs.oasis-open.org/odata/ns/edm">
                      <EntityType Name="E">
                        <Documentation>
                          <LongDescription>Only long.</LongDescription>
                        </Documentation>
                        <Property Name="Id" Type="Edm.Int32" />
                      </EntityType>
                    </Schema>
                  </edmx:DataServices>
                </edmx:Edmx>
                """;
            var model = new CsdlParser().ParseFromString(xml);
            model.EntityTypes[0].Description.Should().Be("Only long.");
            model.EntityTypes[0].LongDescription.Should().Be("Only long.");
        }

        /// <summary>
        /// Property without Name throws.
        /// </summary>
        [TestMethod]
        public void ParseFromString_PropertyMissingName_Throws()
        {
            const string xml = """
                <edmx:Edmx Version="4.0" xmlns:edmx="http://docs.oasis-open.org/odata/ns/edmx">
                  <edmx:DataServices>
                    <Schema Namespace="T" xmlns="http://docs.oasis-open.org/odata/ns/edm">
                      <EntityType Name="E">
                        <Property Type="Edm.String" />
                      </EntityType>
                    </Schema>
                  </edmx:DataServices>
                </edmx:Edmx>
                """;
            var act = () => new CsdlParser().ParseFromString(xml);
            act.Should().Throw<InvalidOperationException>().WithInnerException<InvalidOperationException>().WithMessage("*Name*");
        }

        /// <summary>
        /// Singleton without Name throws.
        /// </summary>
        [TestMethod]
        public void ParseFromString_SingletonMissingName_Throws()
        {
            const string xml = """
                <edmx:Edmx Version="4.0" xmlns:edmx="http://docs.oasis-open.org/odata/ns/edmx">
                  <edmx:DataServices>
                    <Schema Namespace="T" xmlns="http://docs.oasis-open.org/odata/ns/edm">
                      <EntityContainer Name="C">
                        <Singleton Type="T.E" />
                      </EntityContainer>
                    </Schema>
                  </edmx:DataServices>
                </edmx:Edmx>
                """;
            var act = () => new CsdlParser().ParseFromString(xml);
            act.Should().Throw<InvalidOperationException>().WithInnerException<InvalidOperationException>().WithMessage("*Name*");
        }

        /// <summary>
        /// FunctionImport without Name throws.
        /// </summary>
        [TestMethod]
        public void ParseFromString_FunctionImportMissingName_Throws()
        {
            const string xml = """
                <edmx:Edmx Version="4.0" xmlns:edmx="http://docs.oasis-open.org/odata/ns/edmx">
                  <edmx:DataServices>
                    <Schema Namespace="T" xmlns="http://docs.oasis-open.org/odata/ns/edm">
                      <EntityContainer Name="C">
                        <FunctionImport Function="T.F" />
                      </EntityContainer>
                    </Schema>
                  </edmx:DataServices>
                </edmx:Edmx>
                """;
            var act = () => new CsdlParser().ParseFromString(xml);
            act.Should().Throw<InvalidOperationException>().WithInnerException<InvalidOperationException>().WithMessage("*Name*");
        }

        /// <summary>
        /// ActionImport without Name throws.
        /// </summary>
        [TestMethod]
        public void ParseFromString_ActionImportMissingName_Throws()
        {
            const string xml = """
                <edmx:Edmx Version="4.0" xmlns:edmx="http://docs.oasis-open.org/odata/ns/edmx">
                  <edmx:DataServices>
                    <Schema Namespace="T" xmlns="http://docs.oasis-open.org/odata/ns/edm">
                      <EntityContainer Name="C">
                        <ActionImport Action="T.A" />
                      </EntityContainer>
                    </Schema>
                  </edmx:DataServices>
                </edmx:Edmx>
                """;
            var act = () => new CsdlParser().ParseFromString(xml);
            act.Should().Throw<InvalidOperationException>().WithInnerException<InvalidOperationException>().WithMessage("*Name*");
        }

        /// <summary>
        /// Stream parse wraps non-XML failures as InvalidOperationException.
        /// </summary>
        [TestMethod]
        public void ParseFromStream_NotEdmx_ThrowsInvalidOperation()
        {
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes("<root/>"));
            var act = () => new CsdlParser().ParseFromStream(stream);
            act.Should().Throw<InvalidOperationException>();
        }

        /// <summary>
        /// EnumType elements populate <see cref="EdmModel.EnumTypes"/> with members, flags, and underlying type.
        /// </summary>
        [TestMethod]
        public void ParseFromString_EnumType_ParsesMembersFlagsAndUnderlyingType()
        {
            var model = new CsdlParser().ParseFromString(EnumCsdl);
            var color = model.EnumTypes.Single(type => type.Name == "Color");
            var permissions = model.EnumTypes.Single(type => type.Name == "Permissions");

            model.EnumTypes.Should().HaveCount(2);
            color.FullName.Should().Be("NS.Color");
            color.IsFlags.Should().BeFalse();
            color.UnderlyingType.Should().Be("Edm.Int32");
            color.Members.Select(member => member.Name).Should().ContainInOrder("Red", "Green");
            color.Members.Single(member => member.Name == "Green").Value.Should().Be(1);
            color.Description.Should().Be("A color.");
            permissions.IsFlags.Should().BeTrue();
            permissions.UnderlyingType.Should().Be("Edm.Int64");
            permissions.Members.Single(member => member.Name == "Write").Value.Should().Be(2);
            model.GetEnumType("NS.Color").Should().BeSameAs(color);
        }

        /// <summary>
        /// A property typed as an enum keeps the qualified enum type name.
        /// </summary>
        [TestMethod]
        public void ParseFromString_EnumProperty_KeepsQualifiedType()
        {
            var model = new CsdlParser().ParseFromString(EnumCsdl);
            var widget = model.EntityTypes.Single(type => type.Name == "Widget");

            widget.GetProperty("Color")!.Type.Should().Be("NS.Color");
        }

        /// <summary>
        /// <c>Core.Computed</c> and <c>Core.ComputedDefaultValue</c> set <see cref="EdmProperty.Computed"/>; nothing else does.
        /// </summary>
        [TestMethod]
        public void ParseFromString_ComputedAnnotations_SetComputed()
        {
            var model = new CsdlParser().ParseFromString(EnumCsdl);
            var widget = model.EntityTypes.Single(type => type.Name == "Widget");

            widget.GetProperty("Id")!.Computed.Should().BeTrue("Core.Computed with no value defaults to true");
            widget.GetProperty("CreatedOn")!.Computed.Should().BeTrue("Core.ComputedDefaultValue Bool=true");
            widget.GetProperty("Version")!.Computed.Should().BeTrue("aliased Core.Computed Bool=true");
            widget.GetProperty("Name")!.Computed.Should().BeFalse("no annotation");
            widget.GetProperty("Notes")!.Computed.Should().BeFalse("Core.Computed Bool=false");
            widget.GetProperty("Sequence")!.Computed.Should().BeFalse("an Int32 key is not Computed unless annotated");
        }

        /// <summary>
        /// Schema-level Annotations targeting a property set <see cref="EdmProperty.Computed"/>.
        /// </summary>
        [TestMethod]
        public void ParseFromString_TargetedComputed_SetsComputed()
        {
            var model = new CsdlParser().ParseFromString(EnumCsdl);
            var widget = model.EntityTypes.Single(type => type.Name == "Widget");

            widget.GetProperty("RowVersion")!.Computed.Should().BeTrue();
        }

        /// <summary>
        /// An EnumType with no members is rejected at parse.
        /// </summary>
        [TestMethod]
        public void ParseFromString_EnumTypeWithoutMembers_Throws()
        {
            const string xml = """
                <edmx:Edmx Version="4.0" xmlns:edmx="http://docs.oasis-open.org/odata/ns/edmx">
                  <edmx:DataServices>
                    <Schema Namespace="T" xmlns="http://docs.oasis-open.org/odata/ns/edm">
                      <EnumType Name="Empty" />
                    </Schema>
                  </edmx:DataServices>
                </edmx:Edmx>
                """;
            var act = () => new CsdlParser().ParseFromString(xml);
            act.Should().Throw<InvalidOperationException>().WithInnerException<InvalidOperationException>().WithMessage("*Empty*member*");
        }

        /// <summary>
        /// An EnumType without a Name is rejected at parse.
        /// </summary>
        [TestMethod]
        public void ParseFromString_EnumTypeMissingName_Throws()
        {
            const string xml = """
                <edmx:Edmx Version="4.0" xmlns:edmx="http://docs.oasis-open.org/odata/ns/edmx">
                  <edmx:DataServices>
                    <Schema Namespace="T" xmlns="http://docs.oasis-open.org/odata/ns/edm">
                      <EnumType>
                        <Member Name="A" Value="0" />
                      </EnumType>
                    </Schema>
                  </edmx:DataServices>
                </edmx:Edmx>
                """;
            var act = () => new CsdlParser().ParseFromString(xml);
            act.Should().Throw<InvalidOperationException>().WithInnerException<InvalidOperationException>().WithMessage("*Name*");
        }

        /// <summary>
        /// Enum members without explicit values number from zero in declaration order.
        /// </summary>
        [TestMethod]
        public void ParseFromString_EnumMembersWithoutValues_NumberFromZero()
        {
            const string xml = """
                <edmx:Edmx Version="4.0" xmlns:edmx="http://docs.oasis-open.org/odata/ns/edmx">
                  <edmx:DataServices>
                    <Schema Namespace="T" xmlns="http://docs.oasis-open.org/odata/ns/edm">
                      <EnumType Name="Size">
                        <Member Name="Small" />
                        <Member Name="Large" />
                      </EnumType>
                    </Schema>
                  </edmx:DataServices>
                </edmx:Edmx>
                """;
            var size = new CsdlParser().ParseFromString(xml).EnumTypes.Single();

            size.Members.Select(member => member.Value).Should().ContainInOrder(0L, 1L);
        }

        #endregion

        #region Fields

        /// <summary>
        /// CSDL covering enum types and <c>Core.Computed</c> annotations in inline, aliased, and targeted forms.
        /// </summary>
        internal const string EnumCsdl = """
            <edmx:Edmx Version="4.0" xmlns:edmx="http://docs.oasis-open.org/odata/ns/edmx">
              <edmx:Reference Uri="https://oasis-tcs.github.io/odata-vocabularies/vocabularies/Org.OData.Core.V1.xml">
                <edmx:Include Namespace="Org.OData.Core.V1" Alias="Core" />
              </edmx:Reference>
              <edmx:DataServices>
                <Schema Namespace="NS" xmlns="http://docs.oasis-open.org/odata/ns/edm">
                  <EnumType Name="Color">
                    <Member Name="Red" Value="0" />
                    <Member Name="Green" Value="1" />
                    <Annotation Term="Org.OData.Core.V1.Description" String="A color." />
                  </EnumType>
                  <EnumType Name="Permissions" IsFlags="true" UnderlyingType="Edm.Int64">
                    <Member Name="Read" Value="1" />
                    <Member Name="Write" Value="2" />
                  </EnumType>
                  <EntityType Name="Widget">
                    <Key>
                      <PropertyRef Name="Id" />
                    </Key>
                    <Property Name="Id" Type="Edm.Int32" Nullable="false">
                      <Annotation Term="Org.OData.Core.V1.Computed" />
                    </Property>
                    <Property Name="Sequence" Type="Edm.Int32" Nullable="false" />
                    <Property Name="Name" Type="Edm.String" Nullable="false" />
                    <Property Name="Color" Type="NS.Color" Nullable="false" />
                    <Property Name="CreatedOn" Type="Edm.DateTimeOffset" Nullable="false">
                      <Annotation Term="Org.OData.Core.V1.ComputedDefaultValue" Bool="true" />
                    </Property>
                    <Property Name="Version" Type="Edm.Int64" Nullable="false">
                      <Annotation Term="Core.Computed" Bool="true" />
                    </Property>
                    <Property Name="Notes" Type="Edm.String">
                      <Annotation Term="Org.OData.Core.V1.Computed" Bool="false" />
                    </Property>
                    <Property Name="RowVersion" Type="Edm.Binary" />
                  </EntityType>
                  <Annotations Target="NS.Widget/RowVersion">
                    <Annotation Term="Org.OData.Core.V1.Computed" Bool="true" />
                  </Annotations>
                  <EntityContainer Name="Container">
                    <EntitySet Name="Widgets" EntityType="NS.Widget" />
                  </EntityContainer>
                </Schema>
              </edmx:DataServices>
            </edmx:Edmx>
            """;

        /// <summary>
        /// CSDL covering complex types, operations, singletons, facets, and targeted annotations.
        /// </summary>
        internal const string RichCsdl = """
            <?xml version="1.0" encoding="utf-8"?>
            <edmx:Edmx Version="4.01" xmlns:edmx="http://docs.oasis-open.org/odata/ns/edmx">
              <edmx:DataServices>
                <Schema Namespace="Trippin" xmlns="http://docs.oasis-open.org/odata/ns/edm">
                  <ComplexType Name="Point" Abstract="true" />
                  <ComplexType Name="Location" BaseType="Trippin.Point" OpenType="true">
                    <Documentation>
                      <Summary>A location.</Summary>
                      <LongDescription>Street and city.</LongDescription>
                    </Documentation>
                    <Property Name="Address" Type="Edm.String">
                      <Documentation>
                        <Summary>Street address.</Summary>
                      </Documentation>
                    </Property>
                    <NavigationProperty Name="Photos" Type="Collection(Trippin.Photo)">
                      <Documentation>
                        <LongDescription>Photos of the location.</LongDescription>
                      </Documentation>
                    </NavigationProperty>
                  </ComplexType>
                  <EntityType Name="Photo" HasStream="true">
                    <Key>
                      <PropertyRef Name="Id" />
                    </Key>
                    <Property Name="Id" Type="Edm.Int32" Nullable="false" />
                  </EntityType>
                  <EntityType Name="Person">
                    <Key>
                      <PropertyRef Name="UserName" />
                    </Key>
                    <Property Name="UserName" Type="Edm.String" Nullable="false" />
                    <NavigationProperty Name="Friends" Type="Collection(Trippin.Person)" />
                    <NavigationProperty Name="Photo" Type="Trippin.Photo" Nullable="false" />
                  </EntityType>
                  <EntityType Name="Airline">
                    <Key>
                      <PropertyRef Name="AirlineCode" />
                    </Key>
                    <Property Name="AirlineCode" Type="Edm.String" Nullable="false" MaxLength="Max" SRID="4326" />
                    <Property Name="Name" Type="Edm.String" MaxLength="40" Unicode="false" DefaultValue="n/a" />
                    <Property Name="Score" Type="Edm.Decimal" Precision="5" Scale="2" Unicode="true" />
                    <NavigationProperty Name="Flights" Type="Collection(Trippin.Flight)" Partner="Airline" ContainsTarget="true">
                      <OnDelete Action="Cascade" />
                      <ReferentialConstraint Property="AirlineCode" ReferencedProperty="AirlineCode" />
                    </NavigationProperty>
                  </EntityType>
                  <EntityType Name="Flight">
                    <Key>
                      <PropertyRef Name="Id" />
                    </Key>
                    <Property Name="Id" Type="Edm.Int32" Nullable="false" />
                    <Property Name="AirlineCode" Type="Edm.String" />
                    <NavigationProperty Name="Airline" Type="Trippin.Airline" Partner="Flights" />
                  </EntityType>
                  <EntityType Name="Airport">
                    <Key>
                      <PropertyRef Name="Icao" />
                    </Key>
                    <Property Name="Icao" Type="Edm.String" Nullable="false" />
                    <Annotation Term="Org.OData.Core.V1.LongDescription" String="Airport long." />
                    <Annotation Term="Other.V1.ShortDescription" String="Short leftover." />
                    <Annotation Term="Org.OData.Core.V1.Description" String="Real description." />
                    <Annotation Term="Org.OData.Core.V1.Computed" String="ignored" />
                  </EntityType>
                  <Function Name="GetNearestAirport" IsBound="true" IsComposable="true">
                    <Documentation>
                      <Summary>Nearest airport.</Summary>
                    </Documentation>
                    <Parameter Name="binding" Type="Trippin.Person" />
                    <Parameter Name="lat" Type="Edm.Double">
                      <Documentation>
                        <Summary>Latitude.</Summary>
                      </Documentation>
                      <Annotation Term="Org.OData.Core.V1.Description" String="Latitude." />
                    </Parameter>
                    <ReturnType Type="Trippin.Airport" />
                  </Function>
                  <Action Name="ResetDataSource" IsBound="true">
                    <Documentation>
                      <Summary>Reset the data.</Summary>
                    </Documentation>
                    <Parameter Name="binding" Type="Trippin.Container" />
                    <ReturnType Type="Edm.Boolean" />
                  </Action>
                  <EntityContainer Name="Container" Extends="Trippin.Base">
                    <EntitySet Name="People" EntityType="Trippin.Person">
                      <NavigationPropertyBinding Path="Friends" Target="People" />
                    </EntitySet>
                    <EntitySet Name="Airlines" EntityType="Trippin.Airline" />
                    <EntitySet Name="Airports" EntityType="Trippin.Airport" />
                    <Singleton Name="Me" Type="Trippin.Person">
                      <Documentation>
                        <Summary>The current user.</Summary>
                      </Documentation>
                      <NavigationPropertyBinding Path="Friends" Target="People" />
                    </Singleton>
                    <FunctionImport Name="GetNearestAirport" Function="Trippin.GetNearestAirport" EntitySet="Airports" IncludeInServiceDocument="false" />
                    <ActionImport Name="ResetDataSource" Action="Trippin.ResetDataSource" EntitySet="People" />
                  </EntityContainer>
                  <Annotations Target="Trippin.Person">
                    <Annotation Term="Org.OData.Core.V1.LongDescription" String="Person long from target." />
                  </Annotations>
                  <Annotations Target="Trippin.Person/Friends">
                    <Annotation Term="Org.OData.Core.V1.LongDescription" String="Friends long." />
                  </Annotations>
                  <Annotations Target="Trippin.Location">
                    <Annotation Term="Org.OData.Core.V1.Description" String="A location." />
                    <Annotation Term="Org.OData.Core.V1.LongDescription" String="Location long." />
                  </Annotations>
                  <Annotations Target="Trippin.Point">
                    <Annotation Term="Org.OData.Core.V1.Description" String="A point." />
                    <Annotation Term="Org.OData.Core.V1.LongDescription" String="Point long." />
                  </Annotations>
                  <Annotations Target="Trippin.GetNearestAirport">
                    <Annotation Term="Org.OData.Core.V1.Description" String="Nearest airport." />
                    <Annotation Term="Org.OData.Core.V1.LongDescription" String="Function long." />
                  </Annotations>
                  <Annotations Target="Trippin.ResetDataSource">
                    <Annotation Term="Org.OData.Core.V1.Description" String="Reset the data." />
                    <Annotation Term="Org.OData.Core.V1.LongDescription" String="Action long." />
                  </Annotations>
                  <Annotations Target="Trippin.Container">
                    <Annotation Term="Org.OData.Core.V1.Description" String="The container." />
                    <Annotation Term="Org.OData.Core.V1.LongDescription" String="Container long." />
                  </Annotations>
                  <Annotations Target="Trippin.Container/Me">
                    <Annotation Term="Org.OData.Core.V1.Description" String="The current user." />
                    <Annotation Term="Org.OData.Core.V1.LongDescription" String="Singleton long." />
                  </Annotations>
                  <Annotations Target="Trippin.Container/People">
                    <Annotation Term="Org.OData.Core.V1.Description" String="People set." />
                    <Annotation Term="Org.OData.Core.V1.LongDescription" String="People long." />
                  </Annotations>
                  <Annotations>
                    <Annotation Term="Org.OData.Core.V1.Description" String="Ignored without Target." />
                  </Annotations>
                </Schema>
              </edmx:DataServices>
            </edmx:Edmx>
            """;

        #endregion

    }

}
