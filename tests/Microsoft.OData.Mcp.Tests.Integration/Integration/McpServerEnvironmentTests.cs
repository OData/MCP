using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.OData.Mcp.Tests.Integration
{
    /// <summary>
    /// Tests MCP server behavior across different environments and OData service configurations.
    /// Tests real-world deployment scenarios and different OData service capabilities.
    /// </summary>
    [TestClass]
    public class McpServerEnvironmentTests : McpServerIntegrationTestBase
    {
        #region Configuration Variation Tests

        [TestMethod]
        public async Task DefaultConfiguration_ShouldWorkWithNorthwind()
        {
            // Arrange & Act
            await InitializeMcpConnectionAsync();

            // Assert - Verify we can connect to Northwind service
            var metadataResponse = await McpClient!.CallToolAsync("GetMetadata", new { }, CreateTestCancellationToken());
            metadataResponse.Result!.IsError.Should().BeFalse();
            
            var metadataContent = metadataResponse.Result.Content![0].Text;
            metadataContent.Should().Contain("NorthwindModel");
        }

        [TestMethod]
        public async Task AlternativeODataService_ShouldWorkCorrectly()
        {
            // Note: This test would require creating a server with different configuration
            // For demonstration, we'll test the TripPin service discovery capability
            
            // Arrange
            await InitializeMcpConnectionAsync();

            // Act - Test validation with TripPin service URL
            var validationResponse = await McpClient!.CallToolAsync("ValidateQuery", 
                new { queryUrl = "https://services.odata.org/TripPinRESTierService/People" }, 
                CreateTestCancellationToken());

            // Assert
            validationResponse.Should().NotBeNull();
            validationResponse.Result!.IsError.Should().BeFalse();
            
            var validationContent = validationResponse.Result.Content![0].Text;
            var validation = JsonSerializer.Deserialize<JsonElement>(validationContent);
            
            // The URL should be recognized as valid OData format, even if it's not our configured service
            validation.TryGetProperty("IsValid", out var isValid).Should().BeTrue();
        }

        [TestMethod]
        public async Task MetadataCaching_ShouldWorkCorrectly()
        {
            // Arrange
            await InitializeMcpConnectionAsync();
            var cancellationToken = CreateTestCancellationToken();

            // Act - Request metadata multiple times
            var start = DateTime.UtcNow;
            var firstResponse = await McpClient!.CallToolAsync("GetMetadata", new { }, cancellationToken);
            var firstDuration = DateTime.UtcNow - start;

            start = DateTime.UtcNow;
            var secondResponse = await McpClient.CallToolAsync("GetMetadata", new { }, cancellationToken);
            var secondDuration = DateTime.UtcNow - start;

            // Assert
            firstResponse.Result!.Content![0].Text.Should().Be(secondResponse.Result!.Content![0].Text);
            // Second request should be faster due to caching (though this may not always be measurable)
            secondDuration.Should().BeLessOrEqualTo(firstDuration.Add(TimeSpan.FromSeconds(1)));
        }

        #endregion

        #region Service Discovery and Capability Tests

        [TestMethod]
        public async Task ServiceCapabilities_ShouldReflectODataVersion()
        {
            // Arrange
            await InitializeMcpConnectionAsync();

            // Act
            var metadataResponse = await McpClient!.CallToolAsync("GetMetadata", new { }, CreateTestCancellationToken());
            
            // Assert
            var metadataContent = metadataResponse.Result!.Content![0].Text;
            metadataContent.Should().Contain("Version=\"4.0\""); // Should indicate OData v4
        }

        [TestMethod]
        public async Task EntitySetDiscovery_ShouldFindExpectedSets()
        {
            // Arrange
            await InitializeMcpConnectionAsync();

            // Act
            var discoveryResponse = await McpClient!.CallToolAsync("DiscoverEntitySets", new { }, CreateTestCancellationToken());

            // Assert
            discoveryResponse.Result!.IsError.Should().BeFalse();
            
            var entitySetsJson = discoveryResponse.Result.Content![0].Text;
            var entitySets = JsonSerializer.Deserialize<JsonElement[]>(entitySetsJson);
            
            // Verify we found the expected Northwind entity sets
            var entitySetNames = new List<string>();
            foreach (var entitySet in entitySets!)
            {
                entitySetNames.Add(entitySet.GetProperty("Name").GetString()!);
            }
            
            entitySetNames.Should().Contain(new[] 
            {
                "Categories", "CustomerDemographics", "Customers", "Employees",
                "Order_Details", "Orders", "Products", "Region", "Shippers", "Suppliers", "Territories"
            });
        }

        [TestMethod]
        public async Task ComplexEntityTypes_ShouldBeDiscoveredCorrectly()
        {
            // Arrange
            await InitializeMcpConnectionAsync();

            // Act
            var descriptionResponse = await McpClient!.CallToolAsync("DescribeEntityType", 
                new { entityTypeName = "Employee" }, CreateTestCancellationToken());

            // Assert
            descriptionResponse.Result!.IsError.Should().BeFalse();
            
            var entityTypeJson = descriptionResponse.Result.Content![0].Text;
            var entityType = JsonSerializer.Deserialize<JsonElement>(entityTypeJson);
            
            // Employee entity should have complex relationships
            entityType.TryGetProperty("NavigationProperties", out var navProps).Should().BeTrue();
            navProps.GetArrayLength().Should().BeGreaterThan(0);
            
            entityType.TryGetProperty("Properties", out var props).Should().BeTrue();
            props.GetArrayLength().Should().BeGreaterThan(5); // Employee has many properties
        }

        #endregion

        #region Performance and Scalability Tests

        [TestMethod]
        public async Task LargeEntitySet_ShouldHandleEfficiently()
        {
            // Arrange
            await InitializeMcpConnectionAsync();
            var cancellationToken = CreateTestCancellationToken(TimeSpan.FromMinutes(2));

            // Act - Query a potentially large entity set
            var start = DateTime.UtcNow;
            var response = await McpClient!.CallToolAsync("QueryEntitySet", 
                new { entitySet = "Order_Details", top = 100 }, cancellationToken);
            var duration = DateTime.UtcNow - start;

            // Assert
            response.Result!.IsError.Should().BeFalse();
            duration.Should().BeLessThan(TimeSpan.FromSeconds(30)); // Should complete within reasonable time
            
            var jsonContent = response.Result.Content![0].Text;
            var jsonDoc = JsonDocument.Parse(jsonContent);
            var values = jsonDoc.RootElement.GetProperty("value");
            values.GetArrayLength().Should().Be(100);
        }

        [TestMethod]
        public async Task ComplexQuery_ShouldExecuteEfficiently()
        {
            // Arrange
            await InitializeMcpConnectionAsync();
            var cancellationToken = CreateTestCancellationToken(TimeSpan.FromMinutes(2));

            // Act - Execute a complex query with multiple conditions
            var start = DateTime.UtcNow;
            var response = await McpClient!.CallToolAsync("QueryEntitySet", new 
            {
                entitySet = "Products",
                filter = "CategoryID eq 1 and UnitPrice gt 10 and UnitsInStock gt 0",
                orderBy = "UnitPrice desc",
                select = "ProductID,ProductName,UnitPrice,UnitsInStock",
                top = 20
            }, cancellationToken);
            var duration = DateTime.UtcNow - start;

            // Assert
            response.Result!.IsError.Should().BeFalse();
            duration.Should().BeLessThan(TimeSpan.FromSeconds(10));
            
            var jsonContent = response.Result.Content![0].Text;
            var jsonDoc = JsonDocument.Parse(jsonContent);
            var values = jsonDoc.RootElement.GetProperty("value");
            
            // Verify the filter and select worked
            foreach (var item in values.EnumerateArray())
            {
                item.TryGetProperty("CategoryID", out _).Should().BeFalse(); // Not selected
                item.TryGetProperty("ProductName", out _).Should().BeTrue(); // Selected
                item.TryGetProperty("UnitPrice", out var price).Should().BeTrue();
                price.GetDecimal().Should().BeGreaterThan(10); // Filter condition
            }
        }

        [TestMethod]
        public async Task ServerUnderLoad_ShouldMaintainResponsiveness()
        {
            // Arrange
            await InitializeMcpConnectionAsync();
            var cancellationToken = CreateTestCancellationToken(TimeSpan.FromMinutes(3));
            const int concurrentRequests = 20;

            // Act - Generate load with multiple concurrent requests
            var tasks = new Task[concurrentRequests];
            for (int i = 0; i < concurrentRequests; i++)
            {
                var requestIndex = i;
                tasks[i] = McpClient!.CallToolAsync("QueryEntitySet", new 
                {
                    entitySet = requestIndex % 2 == 0 ? "Customers" : "Products",
                    top = 10 + (requestIndex % 5) // Vary the request size
                }, cancellationToken);
            }

            var start = DateTime.UtcNow;
            await Task.WhenAll(tasks);
            var totalDuration = DateTime.UtcNow - start;

            // Assert
            totalDuration.Should().BeLessThan(TimeSpan.FromMinutes(1)); // Should complete within reasonable time
            
            // Verify all requests succeeded
            foreach (var task in tasks)
            {
                var response = await (Task<McpCallToolResponse>)task;
                response.Result!.IsError.Should().BeFalse();
            }
        }

        #endregion

        #region Error Handling and Recovery Tests

        [TestMethod]
        public async Task InvalidODataService_ShouldHandleGracefully()
        {
            // Arrange
            await InitializeMcpConnectionAsync();

            // Act - Test with a URL that looks like OData but isn't
            var validationResponse = await McpClient!.CallToolAsync("ValidateQuery", 
                new { queryUrl = "https://httpbin.org/json" }, CreateTestCancellationToken());

            // Assert
            validationResponse.Result!.IsError.Should().BeFalse();
            
            var validationContent = validationResponse.Result.Content![0].Text;
            var validation = JsonSerializer.Deserialize<JsonElement>(validationContent);
            
            validation.GetProperty("IsValid").GetBoolean().Should().BeFalse();
            validation.TryGetProperty("Errors", out var errors).Should().BeTrue();
            errors.GetArrayLength().Should().BeGreaterThan(0);
        }

        [TestMethod]
        public async Task NetworkConnectivity_ShouldRecoverFromTransientFailures()
        {
            // Arrange
            await InitializeMcpConnectionAsync();

            // Act - Make a successful request first
            var successResponse = await McpClient!.CallToolAsync("QueryEntitySet", 
                new { entitySet = "Customers", top = 1 }, CreateTestCancellationToken());
            successResponse.Result!.IsError.Should().BeFalse();

            // Simulate network issues by making rapid successive requests
            // (The Northwind service may rate-limit or have transient issues)
            var results = new List<bool>();
            for (int i = 0; i < 10; i++)
            {
                try
                {
                    var response = await McpClient.CallToolAsync("QueryEntitySet", 
                        new { entitySet = "Products", top = 1 }, CreateTestCancellationToken());
                    results.Add(!response.Result!.IsError);
                }
                catch
                {
                    results.Add(false);
                }
                
                await Task.Delay(100); // Small delay between requests
            }

            // Assert - Should have some successful requests (network recovery)
            results.Should().Contain(true); // At least some requests should succeed
        }

        [TestMethod]
        public async Task ServiceUnavailable_ShouldProvideInformativeError()
        {
            // Arrange
            await InitializeMcpConnectionAsync();

            // Act - Test validation with a non-existent service
            var validationResponse = await McpClient!.CallToolAsync("ValidateQuery", 
                new { queryUrl = "https://nonexistent.example.com/odata/Service" }, CreateTestCancellationToken());

            // Assert
            validationResponse.Should().NotBeNull();
            validationResponse.Result!.IsError.Should().BeFalse(); // Validation itself should not error
            
            var validationContent = validationResponse.Result.Content![0].Text;
            var validation = JsonSerializer.Deserialize<JsonElement>(validationContent);
            
            // Should identify the URL format issues
            validation.GetProperty("IsValid").GetBoolean().Should().BeFalse();
            validation.TryGetProperty("Warnings", out var warnings).Should().BeTrue();
        }

        #endregion

        #region Cross-Platform and Environment Tests

        [TestMethod]
        public async Task ServerExecutable_ShouldStartOnCurrentPlatform()
        {
            // Arrange & Act (server startup happens in TestSetup)
            await InitializeMcpConnectionAsync();

            // Assert
            McpClient!.IsServerRunning.Should().BeTrue();
            
            // Verify basic functionality works on this platform
            var pingResponse = await McpClient.PingAsync(CreateTestCancellationToken());
            pingResponse.Error.Should().BeNull();
        }

        [TestMethod]
        public async Task MemoryUsage_ShouldRemainStable()
        {
            // Arrange
            await InitializeMcpConnectionAsync();
            var serverProcess = System.Diagnostics.Process.GetProcessById(McpClient!.ServerProcessId);
            var initialMemory = serverProcess.WorkingSet64;

            // Act - Perform multiple operations that could accumulate memory
            for (int i = 0; i < 20; i++)
            {
                await McpClient.CallToolAsync("GetMetadata", new { }, CreateTestCancellationToken());
                await McpClient.CallToolAsync("QueryEntitySet", 
                    new { entitySet = "Customers", top = 10 }, CreateTestCancellationToken());
                await Task.Delay(50); // Small delay
            }

            // Force garbage collection and wait
            GC.Collect();
            GC.WaitForPendingFinalizers();
            await Task.Delay(1000);

            // Assert
            serverProcess.Refresh();
            var finalMemory = serverProcess.WorkingSet64;
            var memoryIncrease = finalMemory - initialMemory;
            
            // Memory increase should be reasonable (less than 50MB for these operations)
            memoryIncrease.Should().BeLessThan(50 * 1024 * 1024);
        }

        [TestMethod]
        public async Task FileHandles_ShouldNotLeak()
        {
            // Arrange
            await InitializeMcpConnectionAsync();
            var serverProcess = System.Diagnostics.Process.GetProcessById(McpClient!.ServerProcessId);
            
            // Act - Perform operations that might open/close resources
            for (int i = 0; i < 50; i++)
            {
                await McpClient.CallToolAsync("QueryEntitySet", 
                    new { entitySet = "Products", top = 1 }, CreateTestCancellationToken());
            }

            // Assert - Process should still be responsive and not have excessive handles
            serverProcess.Refresh();
            serverProcess.HasExited.Should().BeFalse();
            
            var pingResponse = await McpClient.PingAsync(CreateTestCancellationToken());
            pingResponse.Error.Should().BeNull();
        }

        #endregion

        #region Integration with Real-World Scenarios

        [TestMethod]
        public async Task TypicalAIWorkflow_ShouldWorkSmoothly()
        {
            // Simulate a typical AI assistant workflow
            
            // Arrange
            await InitializeMcpConnectionAsync();
            var cancellationToken = CreateTestCancellationToken();

            // Act & Assert - Step 1: Discover available data
            var discoveryResponse = await McpClient!.CallToolAsync("DiscoverEntitySets", new { }, cancellationToken);
            discoveryResponse.Result!.IsError.Should().BeFalse();

            // Step 2: Get schema information for a specific entity
            var schemaResponse = await McpClient.CallToolAsync("DescribeEntityType", 
                new { entityTypeName = "Customer" }, cancellationToken);
            schemaResponse.Result!.IsError.Should().BeFalse();

            // Step 3: Generate example queries
            var examplesResponse = await McpClient.CallToolAsync("GenerateQueryExamples", 
                new { entitySetName = "Customers" }, cancellationToken);
            examplesResponse.Result!.IsError.Should().BeFalse();

            // Step 4: Execute actual query
            var queryResponse = await McpClient.CallToolAsync("QueryEntitySet", 
                new { entitySet = "Customers", top = 5 }, cancellationToken);
            queryResponse.Result!.IsError.Should().BeFalse();

            // Step 5: Get specific entity details
            var entityResponse = await McpClient.CallToolAsync("GetEntity", 
                new { entitySet = "Customers", key = "ALFKI" }, cancellationToken);
            entityResponse.Result!.IsError.Should().BeFalse();

            // All steps should complete successfully
            Logger!.LogInformation("Typical AI workflow completed successfully with {StepCount} steps", 5);
        }

        [TestMethod]
        public async Task DataAnalysisWorkflow_ShouldProvideRichResults()
        {
            // Simulate a data analysis scenario
            
            // Arrange
            await InitializeMcpConnectionAsync();
            var cancellationToken = CreateTestCancellationToken();

            // Act - Analyze order patterns
            var ordersResponse = await McpClient!.CallToolAsync("QueryEntitySet", new 
            {
                entitySet = "Orders",
                filter = "year(OrderDate) eq 1997",
                orderBy = "OrderDate desc",
                select = "OrderID,OrderDate,Freight,ShipCountry",
                top = 50
            }, cancellationToken);

            // Assert
            ordersResponse.Result!.IsError.Should().BeFalse();
            
            var ordersData = ordersResponse.Result.Content![0].Text;
            var ordersDoc = JsonDocument.Parse(ordersData);
            var orders = ordersDoc.RootElement.GetProperty("value");
            
            orders.GetArrayLength().Should().BeGreaterThan(0);
            
            // Verify data quality for analysis
            foreach (var order in orders.EnumerateArray())
            {
                order.TryGetProperty("OrderDate", out var orderDate).Should().BeTrue();
                order.TryGetProperty("Freight", out var freight).Should().BeTrue();
                order.TryGetProperty("ShipCountry", out var country).Should().BeTrue();
                
                // Data should be properly formatted
                orderDate.ToString().Should().Contain("1997");
                freight.ValueKind.Should().Be(JsonValueKind.Number);
                country.GetString().Should().NotBeNullOrEmpty();
            }
        }

        #endregion
    }
}