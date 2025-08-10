// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Microsoft.OData.Mcp.Sample.Examples
{
    /// <summary>
    /// Example demonstrating how to interact with MCP endpoints.
    /// </summary>
    public class McpClientExample
    {
        internal readonly HttpClient _httpClient;
        internal readonly JsonSerializerOptions _jsonOptions;


        /// <summary>
        /// 
        /// </summary>
        public McpClientExample()
        {
            _httpClient = new HttpClient { BaseAddress = new Uri("https://localhost:5001") };
            _jsonOptions = new JsonSerializerOptions { WriteIndented = true, PropertyNameCaseInsensitive = true };
        }

        /// <summary>
        /// Example 1: Get MCP server information.
        /// </summary>
        public async Task GetServerInfoExample()
        {
            Console.WriteLine("=== Example 1: Get MCP Server Info ===");
            
            // Try all three MCP endpoints
            var endpoints = new[] { "/api/v1/mcp", "/api/v2/mcp", "/odata/mcp" };
            
            foreach (var endpoint in endpoints)
            {
                Console.WriteLine($"\nGetting info from: {endpoint}");
                var response = await _httpClient.GetAsync(endpoint);
                
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    Console.WriteLine(content);
                }
            }
        }

        /// <summary>
        /// Example 2: List available tools.
        /// </summary>
        public async Task ListToolsExample()
        {
            Console.WriteLine("\n=== Example 2: List Available Tools ===");
            
            var response = await _httpClient.GetAsync("/odata/mcp/tools");
            
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                var tools = JsonSerializer.Deserialize<ToolsResponse>(content, _jsonOptions);
                
                Console.WriteLine($"Found {tools?.Tools?.Count ?? 0} tools:");
                foreach (var tool in tools?.Tools ?? [])
                {
                    Console.WriteLine($"  - {tool.Name}: {tool.Description}");
                }
            }
        }

        /// <summary>
        /// Example 3: Query customers using MCP tool.
        /// </summary>
        public async Task QueryCustomersExample()
        {
            Console.WriteLine("\n=== Example 3: Query Customers ===");
            
            var toolRequest = new
            {
                tool = "odata.Customer.query",
                parameters = new
                {
                    filter = "Country eq 'USA'",
                    orderby = "Name",
                    top = 5,
                    select = "Id,Name,Email,Country"
                }
            };
            
            var json = JsonSerializer.Serialize(toolRequest, _jsonOptions);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            
            var response = await _httpClient.PostAsync("/odata/mcp/tools/execute", content);
            
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadAsStringAsync();
                Console.WriteLine("Query Results:");
                Console.WriteLine(result);
            }
        }

        /// <summary>
        /// Example 4: Create a new customer.
        /// </summary>
        public async Task CreateCustomerExample()
        {
            Console.WriteLine("\n=== Example 4: Create New Customer ===");
            
            var newCustomer = new
            {
                Name = "Tech Innovations Inc.",
                Email = "contact@techinnovations.com",
                Phone = "+1-555-0199",
                Address = "789 Innovation Drive",
                City = "San Francisco",
                Country = "USA",
                CreditLimit = 50000
            };
            
            var toolRequest = new
            {
                tool = "odata.Customer.create",
                parameters = new
                {
                    entity = JsonSerializer.Serialize(newCustomer)
                }
            };
            
            var json = JsonSerializer.Serialize(toolRequest, _jsonOptions);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            
            var response = await _httpClient.PostAsync("/odata/mcp/tools/execute", content);
            
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadAsStringAsync();
                Console.WriteLine("Created Customer:");
                Console.WriteLine(result);
            }
        }

        /// <summary>
        /// Example 5: Get a specific customer by ID.
        /// </summary>
        public async Task GetCustomerByIdExample()
        {
            Console.WriteLine("\n=== Example 5: Get Customer by ID ===");
            
            var toolRequest = new
            {
                tool = "odata.Customer.get",
                parameters = new
                {
                    key = "1",
                    expand = "Orders"
                }
            };
            
            var json = JsonSerializer.Serialize(toolRequest, _jsonOptions);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            
            var response = await _httpClient.PostAsync("/odata/mcp/tools/execute", content);
            
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadAsStringAsync();
                Console.WriteLine("Customer Details:");
                Console.WriteLine(result);
            }
        }

        /// <summary>
        /// Example 6: Query products with complex filtering.
        /// </summary>
        public async Task QueryProductsExample()
        {
            Console.WriteLine("\n=== Example 6: Query Products ===");
            
            var toolRequest = new
            {
                tool = "odata.Product.query",
                parameters = new
                {
                    filter = "UnitPrice gt 50 and UnitsInStock lt 100",
                    orderby = "UnitPrice desc",
                    expand = "Category",
                    top = 10
                }
            };
            
            var json = JsonSerializer.Serialize(toolRequest, _jsonOptions);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            
            var response = await _httpClient.PostAsync("/odata/mcp/tools/execute", content);
            
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadAsStringAsync();
                Console.WriteLine("Product Query Results:");
                Console.WriteLine(result);
            }
        }

        /// <summary>
        /// Example 7: Execute an OData action.
        /// </summary>
        public async Task ExecuteActionExample()
        {
            Console.WriteLine("\n=== Example 7: Execute OData Action ===");
            
            // Example: Apply discount to a product
            var toolRequest = new
            {
                tool = "odata.Product.applyDiscount",
                parameters = new
                {
                    key = "1",
                    percentage = 15
                }
            };
            
            var json = JsonSerializer.Serialize(toolRequest, _jsonOptions);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            
            var response = await _httpClient.PostAsync("/odata/mcp/tools/execute", content);
            
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadAsStringAsync();
                Console.WriteLine("Action Result:");
                Console.WriteLine(result);
            }
        }

        /// <summary>
        /// Example 8: Navigate relationships.
        /// </summary>
        public async Task NavigateRelationshipsExample()
        {
            Console.WriteLine("\n=== Example 8: Navigate Relationships ===");
            
            // Get orders for a specific customer
            var toolRequest = new
            {
                tool = "odata.Customer.getOrders",
                parameters = new
                {
                    key = "1",
                    filter = "TotalAmount gt 1000",
                    orderby = "OrderDate desc"
                }
            };
            
            var json = JsonSerializer.Serialize(toolRequest, _jsonOptions);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            
            var response = await _httpClient.PostAsync("/odata/mcp/tools/execute", content);
            
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadAsStringAsync();
                Console.WriteLine("Customer Orders:");
                Console.WriteLine(result);
            }
        }

        /// <summary>
        /// Run all examples.
        /// </summary>
        public async Task RunAllExamples()
        {
            try
            {
                await GetServerInfoExample();
                await ListToolsExample();
                await QueryCustomersExample();
                await CreateCustomerExample();
                await GetCustomerByIdExample();
                await QueryProductsExample();
                await ExecuteActionExample();
                await NavigateRelationshipsExample();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }
        }

        // Helper classes for deserialization
        internal class ToolsResponse
        {
            public List<Tool> Tools { get; set; } = [];
        }

        internal class Tool
        {
            public string Name { get; set; } = string.Empty;
            public string Description { get; set; } = string.Empty;
            public Dictionary<string, object> Parameters { get; set; } = [];
        }
    }
}
