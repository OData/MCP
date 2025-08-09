using System.Xml;
using FluentAssertions;
using Microsoft.OData.Mcp.Core.Parsing;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.OData.Mcp.Tests.Core.Parsing
{
    /// <summary>
    /// Unit tests for the CSDL parser.
    /// </summary>
    [TestClass]
    public class CsdlParserTests
    {
        private const string SampleCsdlXml = """
            <?xml version="1.0" encoding="utf-8"?>
            <edmx:Edmx Version="4.0" xmlns:edmx="http://docs.oasis-open.org/odata/ns/edmx">
              <edmx:DataServices>
                <Schema Namespace="TestService" xmlns="http://docs.oasis-open.org/odata/ns/edm">
                  <EntityType Name="Product">
                    <Key>
                      <PropertyRef Name="Id" />
                    </Key>
                    <Property Name="Id" Type="Edm.Int32" Nullable="false" />
                    <Property Name="Name" Type="Edm.String" />
                    <Property Name="Price" Type="Edm.Decimal" />
                  </EntityType>
                  <EntityContainer Name="Container">
                    <EntitySet Name="Products" EntityType="TestService.Product" />
                  </EntityContainer>
                </Schema>
              </edmx:DataServices>
            </edmx:Edmx>
            """;

        /// <summary>
        /// Tests that the parser can parse a simple CSDL document.
        /// </summary>
        [TestMethod]
        public void ParseFromString_ValidCsdl_ShouldReturnModel()
        {
            // Arrange
            var parser = new CsdlParser();

            // Act
            var model = parser.ParseFromString(SampleCsdlXml);

            // Assert
            model.Should().NotBeNull();
            model.EntityTypes.Should().HaveCount(1);
            model.EntityTypes[0].Name.Should().Be("Product");
            model.EntityTypes[0].Properties.Should().HaveCount(3);
            model.EntityContainers.Should().HaveCount(1);
            model.AllEntitySets.Should().HaveCount(1);
        }

        /// <summary>
        /// Tests that the parser throws when given null input.
        /// </summary>
        [TestMethod]
        public void ParseFromString_NullInput_ShouldThrow()
        {
            // Arrange
            var parser = new CsdlParser();

            // Act & Assert
            Assert.ThrowsExactly<ArgumentNullException>(() => parser.ParseFromString(null!));
        }

        /// <summary>
        /// Tests that the parser throws when given empty input.
        /// </summary>
        [TestMethod]
        public void ParseFromString_EmptyInput_ShouldThrow()
        {
            // Arrange
            var parser = new CsdlParser();

            // Act & Assert
            Assert.ThrowsExactly<ArgumentException>(() => parser.ParseFromString(""));
        }

        /// <summary>
        /// Tests that the parser throws when given invalid XML.
        /// </summary>
        [TestMethod]
        public void ParseFromString_InvalidXml_ShouldThrow()
        {
            // Arrange
            var parser = new CsdlParser();
            var invalidXml = "<invalid xml>";

            // Act & Assert
            Assert.ThrowsExactly<XmlException>(() => parser.ParseFromString(invalidXml));
        }
    }
}
