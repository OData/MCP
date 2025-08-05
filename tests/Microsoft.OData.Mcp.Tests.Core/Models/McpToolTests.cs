using FluentAssertions;
using Microsoft.OData.Mcp.Core.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;

namespace Microsoft.OData.Mcp.Tests.Core.Models
{
    /// <summary>
    /// Comprehensive tests for the McpTool class.
    /// </summary>
    [TestClass]
    public class McpToolTests
    {

        #region Constructor Tests

        /// <summary>
        /// Tests that default constructor initializes properties correctly.
        /// </summary>
        [TestMethod]
        public void Constructor_Default_InitializesProperties()
        {
            var tool = new McpTool();

            tool.Name.Should().BeNull();
            tool.Description.Should().BeNull();
            tool.InputSchema.Should().BeNull();
            tool.Handler.Should().BeNull();
        }

        #endregion

        #region Property Tests

        /// <summary>
        /// Tests that all properties can be set and retrieved correctly.
        /// </summary>
        [TestMethod]
        public void Properties_CanBeSetAndRetrieved()
        {
            var inputSchema = new Dictionary<string, object>
            {
                ["type"] = "object",
                ["properties"] = new Dictionary<string, object>
                {
                    ["customerId"] = new Dictionary<string, object>
                    {
                        ["type"] = "integer",
                        ["description"] = "The customer ID"
                    }
                }
            };

            Func<Dictionary<string, object>, object> handler = (args) => "result";

            var tool = new McpTool
            {
                Name = "GetCustomer",
                Description = "Retrieve a specific customer by ID",
                InputSchema = inputSchema,
                Handler = handler
            };

            tool.Name.Should().Be("GetCustomer");
            tool.Description.Should().Be("Retrieve a specific customer by ID");
            tool.InputSchema.Should().BeSameAs(inputSchema);
            tool.Handler.Should().BeSameAs(handler);
        }

        #endregion

        #region InputSchema Tests

        /// <summary>
        /// Tests that InputSchema can be set to complex schema.
        /// </summary>
        [TestMethod]
        public void InputSchema_CanBeSetToComplexSchema()
        {
            var tool = new McpTool();

            var complexSchema = new Dictionary<string, object>
            {
                ["type"] = "object",
                ["properties"] = new Dictionary<string, object>
                {
                    ["filter"] = new Dictionary<string, object>
                    {
                        ["type"] = "object",
                        ["properties"] = new Dictionary<string, object>
                        {
                            ["city"] = new Dictionary<string, object> { ["type"] = "string" },
                            ["minAge"] = new Dictionary<string, object> { ["type"] = "integer" }
                        }
                    },
                    ["orderBy"] = new Dictionary<string, object>
                    {
                        ["type"] = "array",
                        ["items"] = new Dictionary<string, object> { ["type"] = "string" }
                    },
                    ["limit"] = new Dictionary<string, object>
                    {
                        ["type"] = "integer",
                        ["minimum"] = 1,
                        ["maximum"] = 1000,
                        ["default"] = 100
                    }
                },
                ["required"] = new[] { "filter" }
            };

            tool.InputSchema = complexSchema;

            tool.InputSchema.Should().BeSameAs(complexSchema);
            tool.InputSchema.Should().ContainKey("type");
            tool.InputSchema.Should().ContainKey("properties");
            tool.InputSchema.Should().ContainKey("required");
        }

        #endregion

        #region Handler Tests

        /// <summary>
        /// Tests that Handler can be set to simple function.
        /// </summary>
        [TestMethod]
        public void Handler_CanBeSetToSimpleFunction()
        {
            var tool = new McpTool();

            Func<Dictionary<string, object>, object> simpleHandler = (args) =>
            {
                return new { success = true, data = "test result" };
            };

            tool.Handler = simpleHandler;

            tool.Handler.Should().BeSameAs(simpleHandler);

            // Test that handler can be invoked
            var testArgs = new Dictionary<string, object> { ["test"] = "value" };
            var result = tool.Handler(testArgs);
            result.Should().NotBeNull();
        }

        /// <summary>
        /// Tests that Handler can be set to complex function.
        /// </summary>
        [TestMethod]
        public void Handler_CanBeSetToComplexFunction()
        {
            var tool = new McpTool();

            Func<Dictionary<string, object>, object> complexHandler = (args) =>
            {
                if (args.ContainsKey("customerId") && args["customerId"] is int id)
                {
                    return new
                    {
                        customerId = id,
                        name = $"Customer {id}",
                        email = $"customer{id}@example.com",
                        created = DateTime.UtcNow
                    };
                }
                throw new ArgumentException("customerId is required");
            };

            tool.Handler = complexHandler;

            tool.Handler.Should().BeSameAs(complexHandler);

            // Test that handler works correctly
            var args = new Dictionary<string, object> { ["customerId"] = 123 };
            var result = tool.Handler(args);
            result.Should().NotBeNull();

            // Test that handler throws for invalid args
            var invalidArgs = new Dictionary<string, object> { ["wrongArg"] = "value" };
            var act = () => tool.Handler(invalidArgs);
            act.Should().Throw<ArgumentException>();
        }

        #endregion

        #region Integration Tests

        /// <summary>
        /// Tests creating a complete tool with all components.
        /// </summary>
        [TestMethod]
        public void Integration_CompleteToolCreation()
        {
            var tool = new McpTool
            {
                Name = "SearchCustomers",
                Description = "Search for customers using various criteria",
                InputSchema = new Dictionary<string, object>
                {
                    ["type"] = "object",
                    ["properties"] = new Dictionary<string, object>
                    {
                        ["query"] = new Dictionary<string, object>
                        {
                            ["type"] = "string",
                            ["description"] = "Search query string",
                            ["minLength"] = 1
                        },
                        ["city"] = new Dictionary<string, object>
                        {
                            ["type"] = "string",
                            ["description"] = "Filter by city"
                        },
                        ["active"] = new Dictionary<string, object>
                        {
                            ["type"] = "boolean",
                            ["description"] = "Filter by active status",
                            ["default"] = true
                        },
                        ["limit"] = new Dictionary<string, object>
                        {
                            ["type"] = "integer",
                            ["minimum"] = 1,
                            ["maximum"] = 100,
                            ["default"] = 10
                        }
                    },
                    ["required"] = new[] { "query" }
                },
                Handler = (args) =>
                {
                    var query = args.GetValueOrDefault("query", "").ToString();
                    var city = args.GetValueOrDefault("city", "").ToString();
                    var active = args.GetValueOrDefault("active", true);
                    var limit = Convert.ToInt32(args.GetValueOrDefault("limit", 10));

                    return new
                    {
                        results = new[]
                        {
                            new { id = 1, name = "John Doe", city = city ?? "Unknown", active = active },
                            new { id = 2, name = "Jane Smith", city = city ?? "Unknown", active = active }
                        }.Take(limit),
                        query = query,
                        totalCount = 2
                    };
                }
            };

            // Verify all properties are set
            tool.Name.Should().Be("SearchCustomers");
            tool.Description.Should().NotBeNullOrEmpty();
            tool.InputSchema.Should().NotBeNull();
            tool.Handler.Should().NotBeNull();

            // Test the complete tool functionality
            var searchArgs = new Dictionary<string, object>
            {
                ["query"] = "customer",
                ["city"] = "Seattle",
                ["active"] = true,
                ["limit"] = 5
            };

            var result = tool.Handler(searchArgs);
            result.Should().NotBeNull();

            // Verify schema structure
            var schema = tool.InputSchema;
            schema.Should().ContainKey("type");
            schema.Should().ContainKey("properties");
            schema.Should().ContainKey("required");

            var properties = schema["properties"] as Dictionary<string, object>;
            properties.Should().ContainKey("query");
            properties.Should().ContainKey("city");
            properties.Should().ContainKey("active");
            properties.Should().ContainKey("limit");
        }

        /// <summary>
        /// Tests creating multiple tools with different configurations.
        /// </summary>
        [TestMethod]
        public void Integration_MultipleToolsWithDifferentConfigurations()
        {
            var tools = new[]
            {
                new McpTool
                {
                    Name = "CreateCustomer",
                    Description = "Create a new customer",
                    InputSchema = new Dictionary<string, object>
                    {
                        ["type"] = "object",
                        ["properties"] = new Dictionary<string, object>
                        {
                            ["name"] = new Dictionary<string, object> { ["type"] = "string" },
                            ["email"] = new Dictionary<string, object> { ["type"] = "string" }
                        },
                        ["required"] = new[] { "name", "email" }
                    },
                    Handler = (args) => new { id = 123, status = "created" }
                },
                new McpTool
                {
                    Name = "UpdateCustomer",
                    Description = "Update an existing customer",
                    InputSchema = new Dictionary<string, object>
                    {
                        ["type"] = "object",
                        ["properties"] = new Dictionary<string, object>
                        {
                            ["id"] = new Dictionary<string, object> { ["type"] = "integer" },
                            ["name"] = new Dictionary<string, object> { ["type"] = "string" },
                            ["email"] = new Dictionary<string, object> { ["type"] = "string" }
                        },
                        ["required"] = new[] { "id" }
                    },
                    Handler = (args) => new { status = "updated" }
                },
                new McpTool
                {
                    Name = "DeleteCustomer",
                    Description = "Delete a customer",
                    InputSchema = new Dictionary<string, object>
                    {
                        ["type"] = "object",
                        ["properties"] = new Dictionary<string, object>
                        {
                            ["id"] = new Dictionary<string, object> { ["type"] = "integer" }
                        },
                        ["required"] = new[] { "id" }
                    },
                    Handler = (args) => new { status = "deleted" }
                }
            };

            tools.Should().HaveCount(3);
            tools.Should().OnlyContain(t => !string.IsNullOrEmpty(t.Name));
            tools.Should().OnlyContain(t => !string.IsNullOrEmpty(t.Description));
            tools.Should().OnlyContain(t => t.InputSchema != null);
            tools.Should().OnlyContain(t => t.Handler != null);

            // Each tool should have different names
            var names = tools.Select(t => t.Name).ToArray();
            names.Should().OnlyHaveUniqueItems();

            // Each tool should be functional
            foreach (var tool in tools)
            {
                var testArgs = new Dictionary<string, object> { ["id"] = 1 };
                if (tool.Name == "CreateCustomer")
                {
                    testArgs["name"] = "Test";
                    testArgs["email"] = "test@example.com";
                }

                var result = tool.Handler(testArgs);
                result.Should().NotBeNull();
            }
        }

        #endregion
    }
}