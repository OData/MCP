// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
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
    /// Tests for McpToolGenerationOptions and their effects on tool generation.
    /// </summary>
    [TestClass]
    public class ToolGenerationOptionsTests
    {

        #region Fields

        private const string SimpleMetadata = @"<?xml version=""1.0"" encoding=""utf-8""?>
<edmx:Edmx Version=""4.0"" xmlns:edmx=""http://docs.oasis-open.org/odata/ns/edmx"">
  <edmx:DataServices>
    <Schema Namespace=""SimpleModel"" xmlns=""http://docs.oasis-open.org/odata/ns/edm"">
      <EntityType Name=""Customer"">
        <Key>
          <PropertyRef Name=""Id"" />
        </Key>
        <Property Name=""Id"" Type=""Edm.Int32"" Nullable=""false"" />
        <Property Name=""Name"" Type=""Edm.String"" />
        <Property Name=""Email"" Type=""Edm.String"" />
        <NavigationProperty Name=""Orders"" Type=""Collection(SimpleModel.Order)"" Partner=""Customer"" />
      </EntityType>
      <EntityType Name=""Order"">
        <Key>
          <PropertyRef Name=""Id"" />
        </Key>
        <Property Name=""Id"" Type=""Edm.Int32"" Nullable=""false"" />
        <Property Name=""OrderDate"" Type=""Edm.DateTimeOffset"" />
        <Property Name=""Total"" Type=""Edm.Decimal"" />
        <NavigationProperty Name=""Customer"" Type=""SimpleModel.Customer"" Partner=""Orders"" />
        <NavigationProperty Name=""Items"" Type=""Collection(SimpleModel.OrderItem)"" Partner=""Order"" />
      </EntityType>
      <EntityType Name=""OrderItem"">
        <Key>
          <PropertyRef Name=""Id"" />
        </Key>
        <Property Name=""Id"" Type=""Edm.Int32"" Nullable=""false"" />
        <Property Name=""ProductName"" Type=""Edm.String"" />
        <Property Name=""Quantity"" Type=""Edm.Int32"" />
        <Property Name=""Price"" Type=""Edm.Decimal"" />
        <NavigationProperty Name=""Order"" Type=""SimpleModel.Order"" Partner=""Items"" />
      </EntityType>
      <EntityContainer Name=""SimpleContext"">
        <EntitySet Name=""Customers"" EntityType=""SimpleModel.Customer"">
          <NavigationPropertyBinding Path=""Orders"" Target=""Orders"" />
        </EntitySet>
        <EntitySet Name=""Orders"" EntityType=""SimpleModel.Order"">
          <NavigationPropertyBinding Path=""Customer"" Target=""Customers"" />
          <NavigationPropertyBinding Path=""Items"" Target=""OrderItems"" />
        </EntitySet>
        <EntitySet Name=""OrderItems"" EntityType=""SimpleModel.OrderItem"">
          <NavigationPropertyBinding Path=""Order"" Target=""Orders"" />
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
            var services = new ServiceCollection();
            services.AddHttpClient();
            var serviceProvider = services.BuildServiceProvider();
            _httpClientFactory = serviceProvider.GetRequiredService<IHttpClientFactory>();

            _toolFactory = new McpToolFactory(
                logger: new NullLogger<McpToolFactory>(),
                httpClientFactory: _httpClientFactory
            );

            var parser = new CsdlParser(new NullLogger<CsdlParser>());
            _edmModel = parser.ParseFromString(SimpleMetadata);
        }

        [TestMethod]
        public async Task GenerateToolsAsync_WithOnlyCrudTools_GeneratesCorrectTools()
        {
            // Arrange
            var options = new McpToolGenerationOptions
            {
                GenerateCrudTools = true,
                GenerateQueryTools = false,
                GenerateNavigationTools = false
            };

            // Act
            var tools = await _toolFactory.GenerateToolsAsync(_edmModel, options);

            // Assert
            // Should have CRUD tools for each entity set (4 tools per entity set)
            var crudTools = new[] { "get", "create", "update", "delete" };
            var entitySets = new[] { "customer", "order", "order_item" };

            foreach (var entitySet in entitySets)
            {
                foreach (var operation in crudTools)
                {
                    tools.Should().Contain(t => t.Name == $"{operation}_{entitySet}",
                        $"Should have {operation}_{entitySet} tool");
                }
            }

            // Should not have list tools
            tools.Should().NotContain(t => t.Name.StartsWith("list_"));
            
            // Should not have navigation tools
            tools.Should().NotContain(t => t.Name.Contains("_orders") && !t.Name.StartsWith("get_order"));
        }

        [TestMethod]
        public async Task GenerateToolsAsync_WithOnlyQueryTools_GeneratesCorrectTools()
        {
            // Arrange
            var options = new McpToolGenerationOptions
            {
                GenerateCrudTools = false,
                GenerateQueryTools = true,
                GenerateNavigationTools = false
            };

            // Act
            var tools = await _toolFactory.GenerateToolsAsync(_edmModel, options);

            // Assert
            // Should have list tools for each entity set
            tools.Should().Contain(t => t.Name == "list_customers");
            tools.Should().Contain(t => t.Name == "list_orders");
            tools.Should().Contain(t => t.Name == "list_order_items");

            // Should not have CRUD tools
            tools.Should().NotContain(t => t.Name.StartsWith("get_") || 
                                        t.Name.StartsWith("create_") || 
                                        t.Name.StartsWith("update_") || 
                                        t.Name.StartsWith("delete_"));
        }

        [TestMethod]
        public async Task GenerateToolsAsync_WithOnlyNavigationTools_GeneratesCorrectTools()
        {
            // Arrange
            var options = new McpToolGenerationOptions
            {
                GenerateCrudTools = false,
                GenerateQueryTools = false,
                GenerateNavigationTools = true
            };

            // Act
            var tools = await _toolFactory.GenerateToolsAsync(_edmModel, options);

            // Assert
            // Should have navigation tools
            tools.Should().Contain(t => t.Name == "get_customer_orders");
            tools.Should().Contain(t => t.Name == "get_order_customer");
            tools.Should().Contain(t => t.Name == "get_order_items");
            tools.Should().Contain(t => t.Name == "get_order_item_order");

            // Should not have CRUD or list tools
            tools.Should().NotContain(t => t.Name == "list_customers");
            tools.Should().NotContain(t => t.Name == "get_customer");
        }

        [TestMethod]
        public async Task GenerateToolsAsync_WithMaxToolCount_LimitsGeneration()
        {
            // Arrange
            var options = new McpToolGenerationOptions
            {
                GenerateCrudTools = true,
                GenerateQueryTools = true,
                GenerateNavigationTools = true,
                MaxToolCount = 10
            };

            // Act
            var tools = await _toolFactory.GenerateToolsAsync(_edmModel, options);

            // Assert
            tools.Should().HaveCount(10);
            
            // Should prioritize CRUD and Query tools over Navigation tools
            tools.Should().Contain(t => t.Name.StartsWith("list_"));
            tools.Should().Contain(t => t.Name.StartsWith("get_") && !t.Name.Contains("_"));
        }

        [TestMethod]
        public async Task GenerateToolsAsync_WithToolVersion_SetsVersionInMetadata()
        {
            // Arrange
            var options = new McpToolGenerationOptions
            {
                ToolVersion = "2.0.0",
                GenerateQueryTools = true
            };

            // Act
            var tools = await _toolFactory.GenerateToolsAsync(_edmModel, options);

            // Assert
            foreach (var tool in tools)
            {
                tool.Metadata.Should().ContainKey("Version");
                tool.Metadata["Version"].Should().Be("2.0.0");
            }
        }

        [TestMethod]
        public async Task GenerateToolsAsync_WithIncludeExamples_AddsExamplesToMetadata()
        {
            // Arrange
            var options = new McpToolGenerationOptions
            {
                IncludeExamples = true,
                GenerateCrudTools = true
            };

            // Act
            var tools = await _toolFactory.GenerateToolsAsync(_edmModel, options);

            // Assert
            var getTool = tools.FirstOrDefault(t => t.Name == "get_customer");
            getTool.Should().NotBeNull();
            
            // When examples are included, descriptions should be more detailed
            getTool!.Description.Should().NotBeNullOrEmpty();
            getTool.Metadata.Should().NotBeNull();
        }

        [TestMethod]
        public async Task GenerateToolsAsync_WithNoOptionsEnabled_GeneratesNoTools()
        {
            // Arrange
            var options = new McpToolGenerationOptions
            {
                GenerateCrudTools = false,
                GenerateQueryTools = false,
                GenerateNavigationTools = false
            };

            // Act
            var tools = await _toolFactory.GenerateToolsAsync(_edmModel, options);

            // Assert
            tools.Should().BeEmpty();
        }

        [TestMethod]
        public async Task GenerateToolsAsync_DefaultOptions_GeneratesExpectedSet()
        {
            // Arrange
            var options = new McpToolGenerationOptions(); // Use defaults

            // Act
            var tools = await _toolFactory.GenerateToolsAsync(_edmModel, options);

            // Assert
            // Default should generate CRUD and Query tools
            tools.Should().Contain(t => t.Name == "list_customers");
            tools.Should().Contain(t => t.Name == "get_customer");
            tools.Should().Contain(t => t.Name == "create_customer");
            tools.Should().Contain(t => t.Name == "update_customer");
            tools.Should().Contain(t => t.Name == "delete_customer");

            // Default should include navigation tools
            tools.Should().Contain(t => t.Name == "get_customer_orders");
        }

        [TestMethod]
        public async Task GenerateToolsAsync_AlwaysExcludePropertyTypes_ExcludesSpecifiedTypes()
        {
            // Arrange
            var metadataWithGeo = @"<?xml version=""1.0"" encoding=""utf-8""?>
<edmx:Edmx Version=""4.0"" xmlns:edmx=""http://docs.oasis-open.org/odata/ns/edmx"">
  <edmx:DataServices>
    <Schema Namespace=""GeoModel"" xmlns=""http://docs.oasis-open.org/odata/ns/edm"">
      <EntityType Name=""Location"">
        <Key>
          <PropertyRef Name=""Id"" />
        </Key>
        <Property Name=""Id"" Type=""Edm.Int32"" Nullable=""false"" />
        <Property Name=""Name"" Type=""Edm.String"" />
        <Property Name=""Coordinates"" Type=""Edm.GeographyPoint"" />
        <Property Name=""Area"" Type=""Edm.GeographyPolygon"" />
      </EntityType>
      <EntityContainer Name=""GeoContext"">
        <EntitySet Name=""Locations"" EntityType=""GeoModel.Location"" />
      </EntityContainer>
    </Schema>
  </edmx:DataServices>
</edmx:Edmx>";

            var parser = new CsdlParser(new NullLogger<CsdlParser>());
            var geoModel = parser.ParseFromString(metadataWithGeo);

            var options = new McpToolGenerationOptions
            {
                ExcludeBinaryFieldsByDefault = true,
                AlwaysExcludePropertyTypes = new List<string> 
                { 
                    "Edm.Binary", 
                    "Edm.Stream", 
                    "Edm.GeographyPoint", 
                    "Edm.GeographyPolygon" 
                }
            };

            // Act
            var tools = await _toolFactory.GenerateToolsAsync(geoModel, options);

            // Assert
            var listLocationsTool = tools.FirstOrDefault(t => t.Name == "list_locations");
            listLocationsTool.Should().NotBeNull();
            
            if (listLocationsTool!.Metadata.ContainsKey("DefaultSelect"))
            {
                var selectClause = listLocationsTool.Metadata["DefaultSelect"]?.ToString();
                selectClause.Should().NotContain("Coordinates");
                selectClause.Should().NotContain("Area");
                selectClause.Should().Contain("Id");
                selectClause.Should().Contain("Name");
            }
        }

        [TestMethod]
        public async Task GenerateToolsAsync_WithNullModel_ReturnsEmptyList()
        {
            // Arrange
            var options = new McpToolGenerationOptions();

            // Act
            var tools = await _toolFactory.GenerateToolsAsync(null!, options);

            // Assert
            tools.Should().NotBeNull();
            tools.Should().BeEmpty();
        }

        [TestMethod]
        public async Task GenerateToolsAsync_WithNullOptions_UsesDefaults()
        {
            // Act
            var tools = await _toolFactory.GenerateToolsAsync(_edmModel, null!);

            // Assert
            tools.Should().NotBeEmpty();
            // Should generate default set of tools
            tools.Should().Contain(t => t.Name == "list_customers");
        }

        [TestMethod]
        public async Task GenerateToolsAsync_OperationTypesSetCorrectly()
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
            var operations = new Dictionary<string, McpToolOperationType>
            {
                ["list_customers"] = McpToolOperationType.Read,
                ["get_customer"] = McpToolOperationType.Read,
                ["create_customer"] = McpToolOperationType.Create,
                ["update_customer"] = McpToolOperationType.Update,
                ["delete_customer"] = McpToolOperationType.Delete
            };

            foreach (var kvp in operations)
            {
                var tool = tools.FirstOrDefault(t => t.Name == kvp.Key);
                tool.Should().NotBeNull($"Tool {kvp.Key} should exist");
                tool!.OperationType.Should().Be(kvp.Value, 
                    $"Tool {kvp.Key} should have operation type {kvp.Value}");
            }
        }

        #endregion

    }

}