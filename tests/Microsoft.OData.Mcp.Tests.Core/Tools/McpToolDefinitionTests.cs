using FluentAssertions;
using Microsoft.OData.Mcp.Core.Tools;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.OData.Mcp.Tests.Core.Tools
{
    /// <summary>
    /// Comprehensive tests for the McpToolDefinition class.
    /// </summary>
    [TestClass]
    public class McpToolDefinitionTests
    {

        #region Constructor Tests

        /// <summary>
        /// Tests that default constructor initializes collections correctly.
        /// </summary>
        [TestMethod]
        public void Constructor_Default_InitializesCollections()
        {
            var toolDef = new McpToolDefinition();

            toolDef.Name.Should().BeNull();
            toolDef.Description.Should().BeNull();
            toolDef.Schema.Should().NotBeNull().And.BeEmpty();
            toolDef.RequiredScopes.Should().NotBeNull().And.BeEmpty();
            toolDef.RequiredRoles.Should().NotBeNull().And.BeEmpty();
            toolDef.Tags.Should().NotBeNull().And.BeEmpty();
            toolDef.Examples.Should().NotBeNull().And.BeEmpty();
            toolDef.Category.Should().BeNull();
        }

        #endregion

        #region Property Tests

        /// <summary>
        /// Tests that all properties can be set and retrieved correctly.
        /// </summary>
        [TestMethod]
        public void Properties_CanBeSetAndRetrieved()
        {
            var schema = new Dictionary<string, object>
            {
                ["type"] = "object",
                ["properties"] = new Dictionary<string, object>()
            };

            var toolDef = new McpToolDefinition
            {
                Name = "GetCustomers",
                Description = "Retrieve all customers from the system",
                Schema = schema,
                Category = "Query"
            };

            toolDef.Name.Should().Be("GetCustomers");
            toolDef.Description.Should().Be("Retrieve all customers from the system");
            toolDef.Schema.Should().BeSameAs(schema);
            toolDef.Category.Should().Be("Query");
        }

        /// <summary>
        /// Tests that RequiredScopes collection can be modified.
        /// </summary>
        [TestMethod]
        public void RequiredScopes_CanBeModified()
        {
            var toolDef = new McpToolDefinition();

            toolDef.RequiredScopes.Add("read");
            toolDef.RequiredScopes.Add("customers");

            toolDef.RequiredScopes.Should().HaveCount(2);
            toolDef.RequiredScopes.Should().Contain("read");
            toolDef.RequiredScopes.Should().Contain("customers");
        }

        /// <summary>
        /// Tests that RequiredRoles collection can be modified.
        /// </summary>
        [TestMethod]
        public void RequiredRoles_CanBeModified()
        {
            var toolDef = new McpToolDefinition();

            toolDef.RequiredRoles.Add("admin");
            toolDef.RequiredRoles.Add("user");

            toolDef.RequiredRoles.Should().HaveCount(2);
            toolDef.RequiredRoles.Should().Contain("admin");
            toolDef.RequiredRoles.Should().Contain("user");
        }

        /// <summary>
        /// Tests that Tags collection can be modified.
        /// </summary>
        [TestMethod]
        public void Tags_CanBeModified()
        {
            var toolDef = new McpToolDefinition();

            toolDef.Tags.Add("customer");
            toolDef.Tags.Add("query");
            toolDef.Tags.Add("odata");

            toolDef.Tags.Should().HaveCount(3);
            toolDef.Tags.Should().Contain("customer");
            toolDef.Tags.Should().Contain("query");
            toolDef.Tags.Should().Contain("odata");
        }

        /// <summary>
        /// Tests that Examples collection can be modified.
        /// </summary>
        [TestMethod]
        public void Examples_CanBeModified()
        {
            var toolDef = new McpToolDefinition();
            
            var example1 = new McpToolExample
            {
                Name = "Basic Query",
                Description = "Get all customers",
                Parameters = new Dictionary<string, object>()
            };
            
            var example2 = new McpToolExample
            {
                Name = "Filtered Query",
                Description = "Get customers by city",
                Parameters = new Dictionary<string, object> { ["city"] = "Seattle" }
            };

            toolDef.Examples.Add(example1);
            toolDef.Examples.Add(example2);

            toolDef.Examples.Should().HaveCount(2);
            toolDef.Examples.Should().Contain(example1);
            toolDef.Examples.Should().Contain(example2);
        }

        /// <summary>
        /// Tests that Schema dictionary can be modified.
        /// </summary>
        [TestMethod]
        public void Schema_CanBeModified()
        {
            var toolDef = new McpToolDefinition();

            toolDef.Schema["type"] = "object";
            toolDef.Schema["required"] = new[] { "id" };
            toolDef.Schema["properties"] = new Dictionary<string, object>
            {
                ["id"] = new { type = "integer" }
            };

            toolDef.Schema.Should().HaveCount(3);
            toolDef.Schema["type"].Should().Be("object");
            toolDef.Schema.Should().ContainKey("required");
            toolDef.Schema.Should().ContainKey("properties");
        }

        #endregion

        #region Validation Tests

        /// <summary>
        /// Tests that Validate returns no errors for valid tool definition.
        /// </summary>
        [TestMethod]
        public void Validate_ValidToolDefinition_ReturnsNoErrors()
        {
            var toolDef = new McpToolDefinition
            {
                Name = "GetCustomers",
                Description = "Get all customers",
                Schema = new Dictionary<string, object>
                {
                    ["type"] = "object"
                }
            };

            var errors = toolDef.Validate();

            errors.Should().BeEmpty();
        }

        /// <summary>
        /// Tests that Validate returns error for missing name.
        /// </summary>
        [TestMethod]
        public void Validate_MissingName_ReturnsError()
        {
            var toolDef = new McpToolDefinition
            {
                Description = "Get all customers",
                Schema = new Dictionary<string, object>()
            };

            var errors = toolDef.Validate().ToList();

            errors.Should().NotBeEmpty();
            errors.Should().Contain(e => e.Contains("Name"));
        }

        /// <summary>
        /// Tests that Validate returns error for empty name.
        /// </summary>
        [TestMethod]
        public void Validate_EmptyName_ReturnsError()
        {
            var toolDef = new McpToolDefinition
            {
                Name = "",
                Description = "Get all customers",
                Schema = new Dictionary<string, object>()
            };

            var errors = toolDef.Validate().ToList();

            errors.Should().NotBeEmpty();
            errors.Should().Contain(e => e.Contains("Name"));
        }

        /// <summary>
        /// Tests that Validate returns error for missing description.
        /// </summary>
        [TestMethod]
        public void Validate_MissingDescription_ReturnsError()
        {
            var toolDef = new McpToolDefinition
            {
                Name = "GetCustomers",
                Schema = new Dictionary<string, object>()
            };

            var errors = toolDef.Validate().ToList();

            errors.Should().NotBeEmpty();
            errors.Should().Contain(e => e.Contains("Description"));
        }

        /// <summary>
        /// Tests that Validate returns error for invalid name characters.
        /// </summary>
        [TestMethod]
        public void Validate_InvalidNameCharacters_ReturnsError()
        {
            var toolDef = new McpToolDefinition
            {
                Name = "Get Customers!",  // Invalid: spaces and special chars
                Description = "Get all customers",
                Schema = new Dictionary<string, object>()
            };

            var errors = toolDef.Validate().ToList();

            errors.Should().NotBeEmpty();
            errors.Should().Contain(e => e.Contains("Name") && e.Contains("alphanumeric"));
        }

        #endregion

        #region Integration Tests

        /// <summary>
        /// Tests creating a complete tool definition with all properties.
        /// </summary>
        [TestMethod]
        public void Integration_CompleteToolDefinition()
        {
            var toolDef = new McpToolDefinition
            {
                Name = "GetCustomersByCity",
                Description = "Retrieve customers filtered by city",
                Category = "Query",
                Schema = new Dictionary<string, object>
                {
                    ["type"] = "object",
                    ["properties"] = new Dictionary<string, object>
                    {
                        ["city"] = new Dictionary<string, object>
                        {
                            ["type"] = "string",
                            ["description"] = "The city to filter by"
                        },
                        ["limit"] = new Dictionary<string, object>
                        {
                            ["type"] = "integer",
                            ["description"] = "Maximum number of results",
                            ["default"] = 100
                        }
                    },
                    ["required"] = new[] { "city" }
                }
            };

            toolDef.RequiredScopes.Add("customers:read");
            toolDef.RequiredRoles.Add("User");
            toolDef.Tags.Add("customers");
            toolDef.Tags.Add("query");
            toolDef.Tags.Add("location");

            var example = new McpToolExample
            {
                Name = "Seattle Customers",
                Description = "Get all customers in Seattle",
                Parameters = new Dictionary<string, object>
                {
                    ["city"] = "Seattle",
                    ["limit"] = 50
                }
            };
            toolDef.Examples.Add(example);

            // Should be valid
            var errors = toolDef.Validate();
            errors.Should().BeEmpty();

            // Should have all properties set correctly
            toolDef.Name.Should().Be("GetCustomersByCity");
            toolDef.RequiredScopes.Should().HaveCount(1);
            toolDef.RequiredRoles.Should().HaveCount(1);
            toolDef.Tags.Should().HaveCount(3);
            toolDef.Examples.Should().HaveCount(1);
            toolDef.Schema.Should().ContainKey("type");
            toolDef.Schema.Should().ContainKey("properties");
            toolDef.Schema.Should().ContainKey("required");
        }

        #endregion
    }
}