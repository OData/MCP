// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
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
    /// Characterization tests for McpToolFactory to capture current behavior
    /// before refactoring. These tests ensure we don't break existing functionality.
    /// </summary>
    [TestClass]
    public class McpToolFactoryCharacterizationTests
    {

        #region Fields

        private const string NorthwindMetadata = @"<?xml version=""1.0"" encoding=""utf-8""?>
<edmx:Edmx Version=""4.0"" xmlns:edmx=""http://docs.oasis-open.org/odata/ns/edmx"">
  <edmx:DataServices>
    <Schema Namespace=""NorthwindModel"" xmlns=""http://docs.oasis-open.org/odata/ns/edm"">
      <EntityType Name=""Category"">
        <Key>
          <PropertyRef Name=""CategoryID"" />
        </Key>
        <Property Name=""CategoryID"" Type=""Edm.Int32"" Nullable=""false"" />
        <Property Name=""CategoryName"" Type=""Edm.String"" MaxLength=""15"" />
        <Property Name=""Description"" Type=""Edm.String"" />
        <Property Name=""Picture"" Type=""Edm.Binary"" />
        <NavigationProperty Name=""Products"" Type=""Collection(NorthwindModel.Product)"" Partner=""Category"" />
      </EntityType>
      <EntityType Name=""Product"">
        <Key>
          <PropertyRef Name=""ProductID"" />
        </Key>
        <Property Name=""ProductID"" Type=""Edm.Int32"" Nullable=""false"" />
        <Property Name=""ProductName"" Type=""Edm.String"" MaxLength=""40"" />
        <Property Name=""SupplierID"" Type=""Edm.Int32"" />
        <Property Name=""CategoryID"" Type=""Edm.Int32"" />
        <Property Name=""QuantityPerUnit"" Type=""Edm.String"" MaxLength=""20"" />
        <Property Name=""UnitPrice"" Type=""Edm.Decimal"" />
        <Property Name=""UnitsInStock"" Type=""Edm.Int16"" />
        <Property Name=""UnitsOnOrder"" Type=""Edm.Int16"" />
        <Property Name=""ReorderLevel"" Type=""Edm.Int16"" />
        <Property Name=""Discontinued"" Type=""Edm.Boolean"" Nullable=""false"" />
        <NavigationProperty Name=""Category"" Type=""NorthwindModel.Category"" Partner=""Products"" />
        <NavigationProperty Name=""Supplier"" Type=""NorthwindModel.Supplier"" Partner=""Products"" />
      </EntityType>
      <EntityType Name=""Supplier"">
        <Key>
          <PropertyRef Name=""SupplierID"" />
        </Key>
        <Property Name=""SupplierID"" Type=""Edm.Int32"" Nullable=""false"" />
        <Property Name=""CompanyName"" Type=""Edm.String"" MaxLength=""40"" />
        <Property Name=""ContactName"" Type=""Edm.String"" MaxLength=""30"" />
        <Property Name=""ContactTitle"" Type=""Edm.String"" MaxLength=""30"" />
        <Property Name=""Address"" Type=""Edm.String"" MaxLength=""60"" />
        <Property Name=""City"" Type=""Edm.String"" MaxLength=""15"" />
        <Property Name=""Region"" Type=""Edm.String"" MaxLength=""15"" />
        <Property Name=""PostalCode"" Type=""Edm.String"" MaxLength=""10"" />
        <Property Name=""Country"" Type=""Edm.String"" MaxLength=""15"" />
        <Property Name=""Phone"" Type=""Edm.String"" MaxLength=""24"" />
        <Property Name=""Fax"" Type=""Edm.String"" MaxLength=""24"" />
        <Property Name=""HomePage"" Type=""Edm.String"" />
        <NavigationProperty Name=""Products"" Type=""Collection(NorthwindModel.Product)"" Partner=""Supplier"" />
      </EntityType>
      <EntityContainer Name=""NorthwindContext"">
        <EntitySet Name=""Categories"" EntityType=""NorthwindModel.Category"">
          <NavigationPropertyBinding Path=""Products"" Target=""Products"" />
        </EntitySet>
        <EntitySet Name=""Products"" EntityType=""NorthwindModel.Product"">
          <NavigationPropertyBinding Path=""Category"" Target=""Categories"" />
          <NavigationPropertyBinding Path=""Supplier"" Target=""Suppliers"" />
        </EntitySet>
        <EntitySet Name=""Suppliers"" EntityType=""NorthwindModel.Supplier"">
          <NavigationPropertyBinding Path=""Products"" Target=""Products"" />
        </EntitySet>
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
            // Setup HttpClientFactory
            var services = new ServiceCollection();
            services.AddHttpClient();
            var serviceProvider = services.BuildServiceProvider();
            _httpClientFactory = serviceProvider.GetRequiredService<IHttpClientFactory>();

            // Create tool factory
            _toolFactory = new McpToolFactory(
                logger: new NullLogger<McpToolFactory>(),
                httpClientFactory: _httpClientFactory
            );

            // Parse metadata
            var parser = new CsdlParser(new NullLogger<CsdlParser>());
            _edmModel = parser.ParseFromString(NorthwindMetadata);
        }

        [TestMethod]
        public async Task GenerateToolsAsync_WithDefaultOptions_GeneratesExpectedTools()
        {
            // Arrange
            var options = new McpToolGenerationOptions();

            // Act
            var tools = await _toolFactory.GenerateToolsAsync(_edmModel, options);

            // Assert
            tools.Should().NotBeNull();
            tools.Should().NotBeEmpty();

            // Verify we have tools for each entity set
            tools.Should().Contain(t => t.Name == "list_categories");
            tools.Should().Contain(t => t.Name == "list_products");
            tools.Should().Contain(t => t.Name == "list_suppliers");

            // Verify CRUD tools are generated
            tools.Should().Contain(t => t.Name == "get_category");
            tools.Should().Contain(t => t.Name == "create_category");
            tools.Should().Contain(t => t.Name == "update_category");
            tools.Should().Contain(t => t.Name == "delete_category");
        }

        [TestMethod]
        public async Task GenerateToolsAsync_WithBinaryFieldExclusion_ExcludesBinaryFields()
        {
            // Arrange
            var options = new McpToolGenerationOptions
            {
                ExcludeBinaryFieldsByDefault = true
            };

            // Act
            var tools = await _toolFactory.GenerateToolsAsync(_edmModel, options);

            // Assert
            var listCategoriesToolDef = tools.FirstOrDefault(t => t.Name == "list_categories");
            listCategoriesToolDef.Should().NotBeNull();

            // The tool should have metadata indicating binary field exclusion
            listCategoriesToolDef!.Metadata.Should().NotBeNull();
            listCategoriesToolDef.Metadata.Should().ContainKey("DefaultSelect");
            
            var defaultSelect = listCategoriesToolDef.Metadata["DefaultSelect"]?.ToString();
            defaultSelect.Should().NotBeNullOrEmpty();
            defaultSelect.Should().Contain("CategoryID");
            defaultSelect.Should().Contain("CategoryName");
            defaultSelect.Should().Contain("Description");
            defaultSelect.Should().NotContain("Picture"); // Binary field should be excluded
        }

        [TestMethod]
        public async Task GenerateToolsAsync_WithNavigationTools_GeneratesNavigationTools()
        {
            // Arrange
            var options = new McpToolGenerationOptions
            {
                GenerateNavigationTools = true
            };

            // Act
            var tools = await _toolFactory.GenerateToolsAsync(_edmModel, options);

            // Assert
            tools.Should().Contain(t => t.Name == "get_category_products");
            tools.Should().Contain(t => t.Name == "get_product_category");
            tools.Should().Contain(t => t.Name == "get_product_supplier");
        }

        [TestMethod]
        public async Task GenerateToolsAsync_WithMaxToolCount_LimitsToolGeneration()
        {
            // Arrange
            var options = new McpToolGenerationOptions
            {
                MaxToolCount = 5
            };

            // Act
            var tools = await _toolFactory.GenerateToolsAsync(_edmModel, options);

            // Assert
            tools.Should().HaveCount(5);
        }

        [TestMethod]
        public void IsBinaryOrStreamField_IdentifiesBinaryFields()
        {
            // Arrange
            var binaryProperty = new EdmProperty { Name = "Picture", Type = "Edm.Binary" };
            var streamProperty = new EdmProperty { Name = "File", Type = "Edm.Stream" };
            var stringProperty = new EdmProperty { Name = "Name", Type = "Edm.String" };

            // Act & Assert
            McpToolFactory.IsBinaryOrStreamField(binaryProperty).Should().BeTrue();
            McpToolFactory.IsBinaryOrStreamField(streamProperty).Should().BeTrue();
            McpToolFactory.IsBinaryOrStreamField(stringProperty).Should().BeFalse();
        }

        [TestMethod]
        public void BuildDefaultSelectForEntityType_ExcludesBinaryFields()
        {
            // Arrange
            var categoryType = _edmModel.EntityTypes.First(e => e.Name == "Category");
            var options = new McpToolGenerationOptions
            {
                ExcludeBinaryFieldsByDefault = true
            };

            // Act
            var selectClause = McpToolFactory.BuildDefaultSelectForEntityType(categoryType, options);

            // Assert
            selectClause.Should().NotBeNull();
            selectClause.Should().Contain("CategoryID");
            selectClause.Should().Contain("CategoryName");
            selectClause.Should().Contain("Description");
            selectClause.Should().NotContain("Picture");
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
            selectClause.Should().BeNull(); // No binary fields to exclude
        }

        [TestMethod]
        public async Task GenerateToolsAsync_ToolDefinitionsHaveCorrectMetadata()
        {
            // Arrange
            var options = new McpToolGenerationOptions
            {
                ExcludeBinaryFieldsByDefault = true,
                IncludeExamples = true
            };

            // Act
            var tools = await _toolFactory.GenerateToolsAsync(_edmModel, options);

            // Assert
            var getCategoryTool = tools.FirstOrDefault(t => t.Name == "get_category");
            getCategoryTool.Should().NotBeNull();
            
            getCategoryTool!.TargetEntitySet.Should().Be("Categories");
            getCategoryTool.TargetEntityType.Should().Be("Category");
            getCategoryTool.OperationType.Should().Be(McpToolOperationType.Read);
            getCategoryTool.Metadata.Should().ContainKey("EntityType");
            getCategoryTool.Metadata["EntityType"].Should().Be("Category");
        }

        [TestMethod]
        public async Task GenerateToolsAsync_HandlersAreNotNull()
        {
            // Arrange
            var options = new McpToolGenerationOptions();

            // Act
            var tools = await _toolFactory.GenerateToolsAsync(_edmModel, options);

            // Assert
            foreach (var tool in tools)
            {
                tool.Handler.Should().NotBeNull($"Tool {tool.Name} should have a handler");
            }
        }

        [TestMethod]
        public async Task GenerateToolsAsync_WithDifferentOperationTypes_SetsCorrectTypes()
        {
            // Arrange
            var options = new McpToolGenerationOptions
            {
                GenerateCrudTools = true,
                GenerateQueryTools = true
            };

            // Act
            var tools = await _toolFactory.GenerateToolsAsync(_edmModel, options);

            // Assert
            var listTool = tools.First(t => t.Name == "list_categories");
            listTool.OperationType.Should().Be(McpToolOperationType.Read);

            var getTool = tools.First(t => t.Name == "get_category");
            getTool.OperationType.Should().Be(McpToolOperationType.Read);

            var createTool = tools.First(t => t.Name == "create_category");
            createTool.OperationType.Should().Be(McpToolOperationType.Create);

            var updateTool = tools.First(t => t.Name == "update_category");
            updateTool.OperationType.Should().Be(McpToolOperationType.Update);

            var deleteTool = tools.First(t => t.Name == "delete_category");
            deleteTool.OperationType.Should().Be(McpToolOperationType.Delete);
        }

        #endregion

    }

}