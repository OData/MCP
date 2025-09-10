// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.OData.Mcp.Core.Models;
using Microsoft.OData.Mcp.Core.Parsing;
using Microsoft.OData.Mcp.Core.Tools;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.OData.Mcp.Tests.Integration
{

    /// <summary>
    /// Integration tests for binary field exclusion functionality.
    /// </summary>
    [TestClass]
    public class BinaryFieldExclusionTests
    {

        #region Fields

        private const string TestMetadata = @"<?xml version=""1.0"" encoding=""utf-8""?>
<edmx:Edmx Version=""4.0"" xmlns:edmx=""http://docs.oasis-open.org/odata/ns/edmx"">
  <edmx:DataServices>
    <Schema Namespace=""TestModel"" xmlns=""http://docs.oasis-open.org/odata/ns/edm"">
      <EntityType Name=""Document"">
        <Key>
          <PropertyRef Name=""Id"" />
        </Key>
        <Property Name=""Id"" Type=""Edm.Int32"" Nullable=""false"" />
        <Property Name=""Title"" Type=""Edm.String"" />
        <Property Name=""Content"" Type=""Edm.Binary"" />
        <Property Name=""Thumbnail"" Type=""Edm.Binary"" />
        <Property Name=""Preview"" Type=""Edm.Stream"" />
        <Property Name=""CreatedDate"" Type=""Edm.DateTimeOffset"" />
      </EntityType>
      <EntityType Name=""Person"">
        <Key>
          <PropertyRef Name=""Id"" />
        </Key>
        <Property Name=""Id"" Type=""Edm.Int32"" Nullable=""false"" />
        <Property Name=""Name"" Type=""Edm.String"" />
        <Property Name=""Email"" Type=""Edm.String"" />
        <Property Name=""ProfilePicture"" Type=""Edm.Binary"" />
      </EntityType>
      <EntityType Name=""Product"">
        <Key>
          <PropertyRef Name=""Id"" />
        </Key>
        <Property Name=""Id"" Type=""Edm.Int32"" Nullable=""false"" />
        <Property Name=""Name"" Type=""Edm.String"" />
        <Property Name=""Price"" Type=""Edm.Decimal"" />
        <Property Name=""Description"" Type=""Edm.String"" />
      </EntityType>
      <EntityContainer Name=""TestContext"">
        <EntitySet Name=""Documents"" EntityType=""TestModel.Document"" />
        <EntitySet Name=""People"" EntityType=""TestModel.Person"" />
        <EntitySet Name=""Products"" EntityType=""TestModel.Product"" />
      </EntityContainer>
    </Schema>
  </edmx:DataServices>
</edmx:Edmx>";

        private IHttpClientFactory _httpClientFactory = null!;
        private McpToolFactory _toolFactory = null!;
        private EdmModel _edmModel = null!;

        #endregion

        #region Public Methods

        [TestInitialize]
        public void Initialize()
        {
            var services = new ServiceCollection();
            services.AddHttpClient();
            var serviceProvider = services.BuildServiceProvider();
            _httpClientFactory = serviceProvider.GetRequiredService<IHttpClientFactory>();

            _toolFactory = new McpToolFactory(
                logger: new NullLogger<McpToolFactory>(),
                httpClientFactory: _httpClientFactory
            );

            var parser = new CsdlParser(new NullLogger<CsdlParser>());
            _edmModel = parser.ParseFromString(TestMetadata);
        }

        [TestMethod]
        public void IsBinaryOrStreamField_IdentifiesAllBinaryTypes()
        {
            // Test various binary type formats
            var testCases = new[]
            {
                new EdmProperty { Name = "Field1", Type = "Edm.Binary" },
                new EdmProperty { Name = "Field2", Type = "edm.binary" },
                new EdmProperty { Name = "Field3", Type = "Edm.Stream" },
                new EdmProperty { Name = "Field4", Type = "edm.stream" },
                new EdmProperty { Name = "Field5", Type = "Binary" },
                new EdmProperty { Name = "Field6", Type = "binary" },
                new EdmProperty { Name = "Field7", Type = "Stream" },
                new EdmProperty { Name = "Field8", Type = "stream" }
            };

            foreach (var property in testCases)
            {
                McpToolFactory.IsBinaryOrStreamField(property).Should().BeTrue(
                    $"Property {property.Name} with type {property.Type} should be identified as binary/stream");
            }
        }

        [TestMethod]
        public void IsBinaryOrStreamField_DoesNotIdentifyNonBinaryTypes()
        {
            var testCases = new[]
            {
                new EdmProperty { Name = "Field1", Type = "Edm.String" },
                new EdmProperty { Name = "Field2", Type = "Edm.Int32" },
                new EdmProperty { Name = "Field3", Type = "Edm.Decimal" },
                new EdmProperty { Name = "Field4", Type = "Edm.DateTimeOffset" },
                new EdmProperty { Name = "Field5", Type = "Edm.Boolean" }
            };

            foreach (var property in testCases)
            {
                McpToolFactory.IsBinaryOrStreamField(property).Should().BeFalse(
                    $"Property {property.Name} with type {property.Type} should not be identified as binary/stream");
            }
        }

        [TestMethod]
        public void BuildDefaultSelectForEntityType_ExcludesMultipleBinaryFields()
        {
            // Arrange
            var documentType = _edmModel.EntityTypes.First(e => e.Name == "Document");
            var options = new McpToolGenerationOptions
            {
                ExcludeBinaryFieldsByDefault = true
            };

            // Act
            var selectClause = McpToolFactory.BuildDefaultSelectForEntityType(documentType, options);

            // Assert
            selectClause.Should().NotBeNull();
            var fields = selectClause!.Split(',');
            
            fields.Should().Contain("Id");
            fields.Should().Contain("Title");
            fields.Should().Contain("CreatedDate");
            fields.Should().NotContain("Content");
            fields.Should().NotContain("Thumbnail");
            fields.Should().NotContain("Preview");
        }

        [TestMethod]
        public void BuildDefaultSelectForEntityType_WithOneBinaryField_ExcludesIt()
        {
            // Arrange
            var personType = _edmModel.EntityTypes.First(e => e.Name == "Person");
            var options = new McpToolGenerationOptions
            {
                ExcludeBinaryFieldsByDefault = true
            };

            // Act
            var selectClause = McpToolFactory.BuildDefaultSelectForEntityType(personType, options);

            // Assert
            selectClause.Should().NotBeNull();
            var fields = selectClause!.Split(',');
            
            fields.Should().Contain("Id");
            fields.Should().Contain("Name");
            fields.Should().Contain("Email");
            fields.Should().NotContain("ProfilePicture");
        }

        [TestMethod]
        public void BuildDefaultSelectForEntityType_WithNoBinaryFields_ReturnsNull()
        {
            // Arrange
            var productType = _edmModel.EntityTypes.First(e => e.Name == "Product");
            var options = new McpToolGenerationOptions
            {
                ExcludeBinaryFieldsByDefault = true
            };

            // Act
            var selectClause = McpToolFactory.BuildDefaultSelectForEntityType(productType, options);

            // Assert
            selectClause.Should().BeNull("No binary fields to exclude, so no $select needed");
        }

        [TestMethod]
        public void BuildDefaultSelectForEntityType_WithDisabledExclusion_ReturnsNull()
        {
            // Arrange
            var documentType = _edmModel.EntityTypes.First(e => e.Name == "Document");
            var options = new McpToolGenerationOptions
            {
                ExcludeBinaryFieldsByDefault = false // Disabled
            };

            // Act
            var selectClause = McpToolFactory.BuildDefaultSelectForEntityType(documentType, options);

            // Assert
            selectClause.Should().BeNull("Binary field exclusion is disabled");
        }

        [TestMethod]
        public async Task GenerateToolsAsync_AppliesBinaryExclusionToAllEntitySets()
        {
            // Arrange
            var options = new McpToolGenerationOptions
            {
                ExcludeBinaryFieldsByDefault = true,
                GenerateQueryTools = true
            };

            // Act
            var tools = await _toolFactory.GenerateToolsAsync(_edmModel, options);

            // Assert
            var listDocumentsTool = tools.FirstOrDefault(t => t.Name == "list_documents");
            listDocumentsTool.Should().NotBeNull();
            listDocumentsTool!.Metadata.Should().ContainKey("DefaultSelect");
            var docSelect = listDocumentsTool.Metadata["DefaultSelect"]?.ToString();
            docSelect.Should().NotContain("Content");
            docSelect.Should().NotContain("Thumbnail");
            docSelect.Should().NotContain("Preview");

            var listPeopleTool = tools.FirstOrDefault(t => t.Name == "list_people");
            listPeopleTool.Should().NotBeNull();
            listPeopleTool!.Metadata.Should().ContainKey("DefaultSelect");
            var peopleSelect = listPeopleTool.Metadata["DefaultSelect"]?.ToString();
            peopleSelect.Should().NotContain("ProfilePicture");

            var listProductsTool = tools.FirstOrDefault(t => t.Name == "list_products");
            listProductsTool.Should().NotBeNull();
            // Products has no binary fields, so no DefaultSelect should be added
            listProductsTool!.Metadata.Should().NotContainKey("DefaultSelect");
        }

        [TestMethod]
        public async Task GenerateToolsAsync_BinaryExclusionConsistentAcrossToolTypes()
        {
            // Arrange
            var options = new McpToolGenerationOptions
            {
                ExcludeBinaryFieldsByDefault = true,
                GenerateCrudTools = true,
                GenerateQueryTools = true
            };

            // Act
            var tools = await _toolFactory.GenerateToolsAsync(_edmModel, options);

            // Assert
            // Check that both list and get operations have consistent binary exclusion
            var listDocsTool = tools.First(t => t.Name == "list_documents");
            var getDocTool = tools.First(t => t.Name == "get_document");

            listDocsTool.Metadata.Should().ContainKey("DefaultSelect");
            getDocTool.Metadata.Should().ContainKey("DefaultSelect");

            listDocsTool.Metadata["DefaultSelect"].Should().Be(getDocTool.Metadata["DefaultSelect"],
                "List and Get operations should have the same binary field exclusion");
        }

        [TestMethod]
        public void BuildDefaultSelectForEntityType_HandlesNullEntityType()
        {
            // Act
            var result = McpToolFactory.BuildDefaultSelectForEntityType(null!, new McpToolGenerationOptions());

            // Assert
            result.Should().BeNull();
        }

        [TestMethod]
        public void BuildDefaultSelectForEntityType_HandlesEntityTypeWithNoProperties()
        {
            // Arrange
            var emptyEntityType = new EdmEntityType
            {
                Name = "Empty",
                Namespace = "TestNamespace",
                Properties = new List<EdmProperty>()
            };

            // Act
            var result = McpToolFactory.BuildDefaultSelectForEntityType(emptyEntityType, new McpToolGenerationOptions());

            // Assert
            result.Should().BeNull();
        }

        [TestMethod]
        public void IsBinaryOrStreamField_HandlesNullProperty()
        {
            // Act
            var result = McpToolFactory.IsBinaryOrStreamField(null!);

            // Assert
            result.Should().BeFalse();
        }

        [TestMethod]
        public void IsBinaryOrStreamField_HandlesNullOrEmptyType()
        {
            // Arrange
            var propertyWithNullType = new EdmProperty { Name = "Test", Type = null! };
            var propertyWithEmptyType = new EdmProperty { Name = "Test", Type = "" };
            var propertyWithWhitespaceType = new EdmProperty { Name = "Test", Type = "  " };

            // Act & Assert
            McpToolFactory.IsBinaryOrStreamField(propertyWithNullType).Should().BeFalse();
            McpToolFactory.IsBinaryOrStreamField(propertyWithEmptyType).Should().BeFalse();
            McpToolFactory.IsBinaryOrStreamField(propertyWithWhitespaceType).Should().BeFalse();
        }

        #endregion

    }

}