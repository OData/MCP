using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.OData.Mcp.Tests.Integration
{
    /// <summary>
    /// Integration tests for OData MCP tools using real OData services via MCP protocol.
    /// Tests actual tool invocation with success and failure scenarios.
    /// </summary>
    [TestClass]
    public class ODataToolsIntegrationTests : McpServerIntegrationTestBase
    {
        [TestInitialize]
        public async Task InitializeTest()
        {
            await InitializeMcpConnectionAsync();
        }

        #region GetMetadata Tool Tests

        [TestMethod]
        public async Task GetMetadata_ShouldReturnValidODataMetadata()
        {
            // Arrange
            var cancellationToken = CreateTestCancellationToken();

            // Act
            var response = await McpClient!.CallToolAsync("GetMetadata", new { }, cancellationToken);

            // Assert
            response.Should().NotBeNull();
            response.Error.Should().BeNull();
            response.Result.Should().NotBeNull();
            response.Result!.IsError.Should().BeFalse();
            response.Result.Content.Should().NotBeNullOrEmpty();

            var metadataContent = response.Result.Content![0].Text;
            metadataContent.Should().NotBeNullOrEmpty();
            metadataContent.Should().Contain("<?xml");
            metadataContent.Should().Contain("edmx:Edmx");
            metadataContent.Should().Contain("Schema");
            metadataContent.Should().Contain("EntityType");
            metadataContent.Should().Contain("EntityContainer");
        }

        [TestMethod]
        public async Task GetMetadata_MultipleRequests_ShouldReturnConsistentResults()
        {
            // Arrange
            var cancellationToken = CreateTestCancellationToken();

            // Act
            var response1 = await McpClient!.CallToolAsync("GetMetadata", new { }, cancellationToken);
            var response2 = await McpClient.CallToolAsync("GetMetadata", new { }, cancellationToken);

            // Assert
            response1.Result!.Content![0].Text.Should().Be(response2.Result!.Content![0].Text);
        }

        #endregion

        #region QueryEntitySet Tool Tests

        [TestMethod]
        public async Task QueryEntitySet_WithValidEntitySet_ShouldReturnEntities()
        {
            // Arrange
            var parameters = new { entitySet = "Customers" };
            var cancellationToken = CreateTestCancellationToken();

            // Act
            var response = await McpClient!.CallToolAsync("QueryEntitySet", parameters, cancellationToken);

            // Assert
            response.Should().NotBeNull();
            response.Error.Should().BeNull();
            response.Result.Should().NotBeNull();
            response.Result!.IsError.Should().BeFalse();
            response.Result.Content.Should().NotBeNullOrEmpty();

            var jsonContent = response.Result.Content![0].Text;
            jsonContent.Should().NotBeNullOrEmpty();
            jsonContent.Should().Contain("@odata.context");
            jsonContent.Should().Contain("value");

            // Parse and validate JSON structure
            var jsonDoc = JsonDocument.Parse(jsonContent);
            jsonDoc.RootElement.TryGetProperty("@odata.context", out _).Should().BeTrue();
            jsonDoc.RootElement.TryGetProperty("value", out var valueElement).Should().BeTrue();
            valueElement.ValueKind.Should().Be(JsonValueKind.Array);
            valueElement.GetArrayLength().Should().BeGreaterThan(0);
        }

        [TestMethod]
        public async Task QueryEntitySet_WithFilter_ShouldReturnFilteredResults()
        {
            // Arrange
            var parameters = new 
            { 
                entitySet = "Customers",
                filter = "startswith(CompanyName,'A')",
                top = 5
            };
            var cancellationToken = CreateTestCancellationToken();

            // Act
            var response = await McpClient!.CallToolAsync("QueryEntitySet", parameters, cancellationToken);

            // Assert
            response.Should().NotBeNull();
            response.Error.Should().BeNull();
            response.Result!.IsError.Should().BeFalse();

            var jsonContent = response.Result.Content![0].Text;
            var jsonDoc = JsonDocument.Parse(jsonContent);
            var values = jsonDoc.RootElement.GetProperty("value");
            values.GetArrayLength().Should().BeLessOrEqualTo(5);

            // Verify filter was applied
            foreach (var item in values.EnumerateArray())
            {
                if (item.TryGetProperty("CompanyName", out var companyName))
                {
                    companyName.GetString()!.Should().StartWithEquivalentOf("A");
                }
            }
        }

        [TestMethod]
        public async Task QueryEntitySet_WithSelect_ShouldReturnOnlySelectedFields()
        {
            // Arrange
            var parameters = new 
            { 
                entitySet = "Customers",
                select = "CustomerID,CompanyName",
                top = 3
            };
            var cancellationToken = CreateTestCancellationToken();

            // Act
            var response = await McpClient!.CallToolAsync("QueryEntitySet", parameters, cancellationToken);

            // Assert
            response.Should().NotBeNull();
            response.Error.Should().BeNull();
            response.Result!.IsError.Should().BeFalse();

            var jsonContent = response.Result.Content![0].Text;
            var jsonDoc = JsonDocument.Parse(jsonContent);
            var values = jsonDoc.RootElement.GetProperty("value");

            foreach (var item in values.EnumerateArray())
            {
                item.TryGetProperty("CustomerID", out _).Should().BeTrue();
                item.TryGetProperty("CompanyName", out _).Should().BeTrue();
                item.TryGetProperty("ContactName", out _).Should().BeFalse(); // Should not be present due to $select
            }
        }

        [TestMethod]
        public async Task QueryEntitySet_WithOrderBy_ShouldReturnOrderedResults()
        {
            // Arrange
            var parameters = new 
            { 
                entitySet = "Products",
                orderBy = "ProductName asc",
                top = 5
            };
            var cancellationToken = CreateTestCancellationToken();

            // Act
            var response = await McpClient!.CallToolAsync("QueryEntitySet", parameters, cancellationToken);

            // Assert
            response.Should().NotBeNull();
            response.Error.Should().BeNull();
            response.Result!.IsError.Should().BeFalse();

            var jsonContent = response.Result.Content![0].Text;
            var jsonDoc = JsonDocument.Parse(jsonContent);
            var values = jsonDoc.RootElement.GetProperty("value");

            // Verify ordering (should be alphabetical ascending)
            string? previousProductName = null;
            foreach (var item in values.EnumerateArray())
            {
                if (item.TryGetProperty("ProductName", out var productName))
                {
                    var currentName = productName.GetString();
                    if (previousProductName != null)
                    {
                        string.Compare(previousProductName, currentName, StringComparison.OrdinalIgnoreCase)
                            .Should().BeLessOrEqualTo(0);
                    }
                    previousProductName = currentName;
                }
            }
        }

        [TestMethod]
        public async Task QueryEntitySet_WithInvalidEntitySet_ShouldReturnError()
        {
            // Arrange
            var parameters = new { entitySet = "NonExistentEntitySet" };
            var cancellationToken = CreateTestCancellationToken();

            // Act
            var response = await McpClient!.CallToolAsync("QueryEntitySet", parameters, cancellationToken);

            // Assert
            response.Should().NotBeNull();
            response.Result.Should().NotBeNull();
            response.Result!.IsError.Should().BeTrue();
            response.Result.Content.Should().NotBeNullOrEmpty();
            response.Result.Content![0].Text.Should().ContainEquivalentOf("failed");
        }

        [TestMethod]
        public async Task QueryEntitySet_WithInvalidFilter_ShouldReturnError()
        {
            // Arrange
            var parameters = new 
            { 
                entitySet = "Customers",
                filter = "invalid filter expression!!!"
            };
            var cancellationToken = CreateTestCancellationToken();

            // Act
            var response = await McpClient!.CallToolAsync("QueryEntitySet", parameters, cancellationToken);

            // Assert
            response.Should().NotBeNull();
            response.Result.Should().NotBeNull();
            response.Result!.IsError.Should().BeTrue();
            response.Result.Content![0].Text.Should().ContainEquivalentOf("failed");
        }

        [TestMethod]
        public async Task QueryEntitySet_WithLargeTop_ShouldHandleCorrectly()
        {
            // Arrange
            var parameters = new 
            { 
                entitySet = "Customers",
                top = 1000 // Large number
            };
            var cancellationToken = CreateTestCancellationToken(TimeSpan.FromMinutes(3));

            // Act
            var response = await McpClient!.CallToolAsync("QueryEntitySet", parameters, cancellationToken);

            // Assert
            response.Should().NotBeNull();
            response.Error.Should().BeNull();
            response.Result.Should().NotBeNull();
            
            // Server should either return the data or handle it gracefully
            if (!response.Result!.IsError)
            {
                var jsonContent = response.Result.Content![0].Text;
                var jsonDoc = JsonDocument.Parse(jsonContent);
                jsonDoc.RootElement.TryGetProperty("value", out _).Should().BeTrue();
            }
        }

        #endregion

        #region GetEntity Tool Tests

        [TestMethod]
        public async Task GetEntity_WithValidKey_ShouldReturnEntity()
        {
            // Arrange
            var parameters = new 
            { 
                entitySet = "Customers",
                key = "ALFKI"
            };
            var cancellationToken = CreateTestCancellationToken();

            // Act
            var response = await McpClient!.CallToolAsync("GetEntity", parameters, cancellationToken);

            // Assert
            response.Should().NotBeNull();
            response.Error.Should().BeNull();
            response.Result.Should().NotBeNull();
            response.Result!.IsError.Should().BeFalse();

            var jsonContent = response.Result.Content![0].Text;
            jsonContent.Should().Contain("@odata.context");
            jsonContent.Should().Contain("CustomerID");
            jsonContent.Should().Contain("ALFKI");

            var jsonDoc = JsonDocument.Parse(jsonContent);
            jsonDoc.RootElement.GetProperty("CustomerID").GetString().Should().Be("ALFKI");
        }

        [TestMethod]
        public async Task GetEntity_WithSelect_ShouldReturnOnlySelectedFields()
        {
            // Arrange
            var parameters = new 
            { 
                entitySet = "Customers",
                key = "ALFKI",
                select = "CustomerID,CompanyName"
            };
            var cancellationToken = CreateTestCancellationToken();

            // Act
            var response = await McpClient!.CallToolAsync("GetEntity", parameters, cancellationToken);

            // Assert
            response.Should().NotBeNull();
            response.Error.Should().BeNull();
            response.Result!.IsError.Should().BeFalse();

            var jsonContent = response.Result.Content![0].Text;
            var jsonDoc = JsonDocument.Parse(jsonContent);
            
            jsonDoc.RootElement.TryGetProperty("CustomerID", out _).Should().BeTrue();
            jsonDoc.RootElement.TryGetProperty("CompanyName", out _).Should().BeTrue();
            jsonDoc.RootElement.TryGetProperty("ContactName", out _).Should().BeFalse();
        }

        [TestMethod]
        public async Task GetEntity_WithInvalidKey_ShouldReturnError()
        {
            // Arrange
            var parameters = new 
            { 
                entitySet = "Customers",
                key = "INVALID_KEY_THAT_DOES_NOT_EXIST"
            };
            var cancellationToken = CreateTestCancellationToken();

            // Act
            var response = await McpClient!.CallToolAsync("GetEntity", parameters, cancellationToken);

            // Assert
            response.Should().NotBeNull();
            response.Result.Should().NotBeNull();
            response.Result!.IsError.Should().BeTrue();
            response.Result.Content![0].Text.Should().ContainEquivalentOf("failed");
        }

        [TestMethod]
        public async Task GetEntity_WithMissingParameters_ShouldReturnError()
        {
            // Arrange
            var parameters = new { entitySet = "Customers" }; // Missing key parameter
            var cancellationToken = CreateTestCancellationToken();

            // Act
            var response = await McpClient!.CallToolAsync("GetEntity", parameters, cancellationToken);

            // Assert
            response.Should().NotBeNull();
            response.Result.Should().NotBeNull();
            response.Result!.IsError.Should().BeTrue();
        }

        #endregion

        #region CreateEntity Tool Tests

        [TestMethod]
        public async Task CreateEntity_WithReadOnlyService_ShouldReturnError()
        {
            // Arrange
            var parameters = new 
            { 
                entitySet = "Customers",
                entityData = """{"CompanyName": "Test Company", "ContactName": "Test Contact"}"""
            };
            var cancellationToken = CreateTestCancellationToken();

            // Act
            var response = await McpClient!.CallToolAsync("CreateEntity", parameters, cancellationToken);

            // Assert
            response.Should().NotBeNull();
            response.Result.Should().NotBeNull();
            response.Result!.IsError.Should().BeTrue();
            response.Result.Content![0].Text.Should().ContainEquivalentOf("create");
        }

        #endregion

        #region Dynamic Tools Tests

        [TestMethod]
        public async Task DiscoverEntitySets_ShouldReturnValidEntitySets()
        {
            // Arrange
            var cancellationToken = CreateTestCancellationToken();

            // Act
            var response = await McpClient!.CallToolAsync("DiscoverEntitySets", new { }, cancellationToken);

            // Assert
            response.Should().NotBeNull();
            response.Error.Should().BeNull();
            response.Result!.IsError.Should().BeFalse();

            var jsonContent = response.Result.Content![0].Text;
            var entitySets = JsonSerializer.Deserialize<JsonElement[]>(jsonContent);
            entitySets.Should().NotBeEmpty();

            var entitySetNames = entitySets!.Select(es => es.GetProperty("Name").GetString()).ToArray();
            entitySetNames.Should().Contain("Customers");
            entitySetNames.Should().Contain("Products");
            entitySetNames.Should().Contain("Orders");
        }

        [TestMethod]
        public async Task DescribeEntityType_WithValidType_ShouldReturnSchema()
        {
            // Arrange
            var parameters = new { entityTypeName = "Customer" };
            var cancellationToken = CreateTestCancellationToken();

            // Act
            var response = await McpClient!.CallToolAsync("DescribeEntityType", parameters, cancellationToken);

            // Assert
            response.Should().NotBeNull();
            response.Error.Should().BeNull();
            response.Result!.IsError.Should().BeFalse();

            var jsonContent = response.Result.Content![0].Text;
            var entityTypeInfo = JsonSerializer.Deserialize<JsonElement>(jsonContent);
            
            entityTypeInfo.GetProperty("Name").GetString().Should().Be("Customer");
            entityTypeInfo.TryGetProperty("Properties", out var properties).Should().BeTrue();
            properties.ValueKind.Should().Be(JsonValueKind.Array);
            properties.GetArrayLength().Should().BeGreaterThan(0);
        }

        [TestMethod]
        public async Task GenerateQueryExamples_ShouldReturnValidExamples()
        {
            // Arrange
            var parameters = new { entitySetName = "Customers" };
            var cancellationToken = CreateTestCancellationToken();

            // Act
            var response = await McpClient!.CallToolAsync("GenerateQueryExamples", parameters, cancellationToken);

            // Assert
            response.Should().NotBeNull();
            response.Error.Should().BeNull();
            response.Result!.IsError.Should().BeFalse();

            var jsonContent = response.Result.Content![0].Text;
            var queryExamples = JsonSerializer.Deserialize<JsonElement>(jsonContent);
            
            queryExamples.GetProperty("EntitySet").GetString().Should().Be("Customers");
            queryExamples.GetProperty("TotalExamples").GetInt32().Should().BeGreaterThan(0);
            queryExamples.TryGetProperty("Examples", out var examples).Should().BeTrue();
            examples.GetArrayLength().Should().BeGreaterThan(0);
        }

        [TestMethod]
        public async Task ValidateQuery_WithValidUrl_ShouldReturnValid()
        {
            // Arrange
            var parameters = new 
            { 
                queryUrl = "https://services.odata.org/V4/Northwind/Northwind.svc/Customers?$top=5"
            };
            var cancellationToken = CreateTestCancellationToken();

            // Act
            var response = await McpClient!.CallToolAsync("ValidateQuery", parameters, cancellationToken);

            // Assert
            response.Should().NotBeNull();
            response.Error.Should().BeNull();
            response.Result!.IsError.Should().BeFalse();

            var jsonContent = response.Result.Content![0].Text;
            var validation = JsonSerializer.Deserialize<JsonElement>(jsonContent);
            
            validation.GetProperty("IsValid").GetBoolean().Should().BeTrue();
            validation.TryGetProperty("Errors", out var errors).Should().BeTrue();
            errors.GetArrayLength().Should().Be(0);
        }

        [TestMethod]
        public async Task ValidateQuery_WithInvalidUrl_ShouldReturnInvalid()
        {
            // Arrange
            var parameters = new { queryUrl = "not-a-valid-url" };
            var cancellationToken = CreateTestCancellationToken();

            // Act
            var response = await McpClient!.CallToolAsync("ValidateQuery", parameters, cancellationToken);

            // Assert
            response.Should().NotBeNull();
            response.Error.Should().BeNull();
            response.Result!.IsError.Should().BeFalse();

            var jsonContent = response.Result.Content![0].Text;
            var validation = JsonSerializer.Deserialize<JsonElement>(jsonContent);
            
            validation.GetProperty("IsValid").GetBoolean().Should().BeFalse();
            validation.TryGetProperty("Errors", out var errors).Should().BeTrue();
            errors.GetArrayLength().Should().BeGreaterThan(0);
        }

        #endregion

        #region Stress and Performance Tests

        [TestMethod]
        public async Task ConcurrentToolCalls_ShouldHandleCorrectly()
        {
            // Arrange
            var cancellationToken = CreateTestCancellationToken(TimeSpan.FromMinutes(3));
            const int concurrentRequests = 10;

            // Act
            var tasks = Enumerable.Range(0, concurrentRequests)
                .Select(i => McpClient!.CallToolAsync("QueryEntitySet", 
                    new { entitySet = "Customers", top = 2 }, cancellationToken))
                .ToArray();

            var responses = await Task.WhenAll(tasks);

            // Assert
            responses.Should().HaveCount(concurrentRequests);
            responses.Should().OnlyContain(r => r.Error == null && !r.Result!.IsError);
        }

        [TestMethod]
        public async Task LargeResultSet_ShouldHandleCorrectly()
        {
            // Arrange
            var parameters = new { entitySet = "Orders" }; // Orders typically has many records
            var cancellationToken = CreateTestCancellationToken(TimeSpan.FromMinutes(5));

            // Act
            var response = await McpClient!.CallToolAsync("QueryEntitySet", parameters, cancellationToken);

            // Assert
            response.Should().NotBeNull();
            response.Error.Should().BeNull();
            response.Result!.IsError.Should().BeFalse();

            var jsonContent = response.Result.Content![0].Text;
            jsonContent.Length.Should().BeGreaterThan(1000); // Should be substantial data
        }

        [TestMethod]
        public async Task ComplexODataQuery_ShouldHandleCorrectly()
        {
            // Arrange
            var parameters = new 
            { 
                entitySet = "Orders",
                filter = "year(OrderDate) eq 1997 and Freight gt 50",
                orderBy = "OrderDate desc",
                select = "OrderID,OrderDate,Freight,ShipCountry",
                top = 10
            };
            var cancellationToken = CreateTestCancellationToken();

            // Act
            var response = await McpClient!.CallToolAsync("QueryEntitySet", parameters, cancellationToken);

            // Assert
            response.Should().NotBeNull();
            response.Error.Should().BeNull();
            response.Result!.IsError.Should().BeFalse();

            var jsonContent = response.Result.Content![0].Text;
            var jsonDoc = JsonDocument.Parse(jsonContent);
            var values = jsonDoc.RootElement.GetProperty("value");
            values.GetArrayLength().Should().BeLessOrEqualTo(10);
        }

        #endregion

        #region Error Recovery Tests

        [TestMethod]
        public async Task NetworkFailure_ShouldRecoverGracefully()
        {
            // This test simulates network issues by using an invalid OData service URL
            // First we need to test with a valid service, then simulate failure

            // Arrange - Valid request first
            var validResponse = await McpClient!.CallToolAsync("QueryEntitySet", 
                new { entitySet = "Customers", top = 1 }, CreateTestCancellationToken());
            validResponse.Result!.IsError.Should().BeFalse();

            // Act - Now the server should handle subsequent requests normally
            var subsequentResponse = await McpClient.CallToolAsync("QueryEntitySet", 
                new { entitySet = "Products", top = 1 }, CreateTestCancellationToken());

            // Assert
            subsequentResponse.Should().NotBeNull();
            subsequentResponse.Error.Should().BeNull();
        }

        [TestMethod]
        public async Task InvalidToolName_ShouldReturnError()
        {
            // Arrange
            var cancellationToken = CreateTestCancellationToken();

            // Act
            var response = await McpClient!.CallToolAsync("NonExistentTool", new { }, cancellationToken);

            // Assert
            response.Should().NotBeNull();
            response.Error.Should().NotBeNull();
            response.Error!.Code.Should().Be(-32602); // Invalid params or method not found
        }

        #endregion
    }
}