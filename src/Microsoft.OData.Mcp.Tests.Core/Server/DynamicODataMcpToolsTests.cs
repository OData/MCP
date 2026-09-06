// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System.Net;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.OData.Mcp.Core.Configuration;
using Microsoft.OData.Mcp.Core.Models;
using Microsoft.OData.Mcp.Core.Parsing;
using Microsoft.OData.Mcp.Core.Server;

namespace Microsoft.OData.Mcp.Tests.Core.Server
{

    /// <summary>
    /// Unit tests for the DynamicODataMcpTools class.
    /// </summary>
    [TestClass]
    public class DynamicODataMcpToolsTests
    {

        #region Fields

        private const string ValidCsdlXml = """
            <?xml version="1.0" encoding="utf-8"?>
            <edmx:Edmx Version="4.0" xmlns:edmx="http://docs.oasis-open.org/odata/ns/edmx">
              <edmx:DataServices>
                <Schema Namespace="TestService" xmlns="http://docs.oasis-open.org/odata/ns/edm">
                  <EntityType Name="Product">
                    <Key>
                      <PropertyRef Name="Id" />
                    </Key>
                    <Property Name="Id" Type="Edm.Int32" Nullable="false" />
                    <Property Name="Name" Type="Edm.String" MaxLength="100" />
                    <Property Name="Price" Type="Edm.Decimal" Precision="18" Scale="2" />
                    <Property Name="InStock" Type="Edm.Boolean" Nullable="false" DefaultValue="true" />
                    <NavigationProperty Name="Category" Type="TestService.Category" />
                  </EntityType>
                  <EntityType Name="Category">
                    <Key>
                      <PropertyRef Name="Id" />
                    </Key>
                    <Property Name="Id" Type="Edm.Int32" Nullable="false" />
                    <Property Name="Name" Type="Edm.String" Nullable="false" />
                    <NavigationProperty Name="Products" Type="Collection(TestService.Product)" />
                  </EntityType>
                  <EntityContainer Name="Container">
                    <EntitySet Name="Products" EntityType="TestService.Product">
                      <NavigationPropertyBinding Path="Category" Target="Categories" />
                    </EntitySet>
                    <EntitySet Name="Categories" EntityType="TestService.Category">
                      <NavigationPropertyBinding Path="Products" Target="Products" />
                    </EntitySet>
                  </EntityContainer>
                </Schema>
              </edmx:DataServices>
            </edmx:Edmx>
            """;

        private const string EmptyCsdlXml = """
            <?xml version="1.0" encoding="utf-8"?>
            <edmx:Edmx Version="4.0" xmlns:edmx="http://docs.oasis-open.org/odata/ns/edmx">
              <edmx:DataServices>
                <Schema Namespace="TestService" xmlns="http://docs.oasis-open.org/odata/ns/edm">
                  <EntityContainer Name="Container">
                  </EntityContainer>
                </Schema>
              </edmx:DataServices>
            </edmx:Edmx>
            """;

        private const string ComplexCsdlXml = """
            <?xml version="1.0" encoding="utf-8"?>
            <edmx:Edmx Version="4.0" xmlns:edmx="http://docs.oasis-open.org/odata/ns/edmx">
              <edmx:DataServices>
                <Schema Namespace="TestService" xmlns="http://docs.oasis-open.org/odata/ns/edm">
                  <EntityType Name="Customer">
                    <Key>
                      <PropertyRef Name="Id" />
                    </Key>
                    <Property Name="Id" Type="Edm.Guid" Nullable="false" />
                    <Property Name="Name" Type="Edm.String" Nullable="false" MaxLength="255" />
                    <Property Name="Email" Type="Edm.String" />
                    <Property Name="CreatedDate" Type="Edm.DateTimeOffset" Nullable="false" />
                    <Property Name="CreditLimit" Type="Edm.Decimal" Precision="18" Scale="2" />
                    <Property Name="Picture" Type="Edm.Stream" />
                    <NavigationProperty Name="Orders" Type="Collection(TestService.Order)" />
                  </EntityType>
                  <EntityType Name="Order">
                    <Key>
                      <PropertyRef Name="OrderId" />
                    </Key>
                    <Property Name="OrderId" Type="Edm.Int64" Nullable="false" />
                    <Property Name="CustomerId" Type="Edm.Guid" Nullable="false" />
                    <Property Name="OrderDate" Type="Edm.DateTimeOffset" Nullable="false" />
                    <Property Name="Total" Type="Edm.Double" Nullable="false" />
                    <NavigationProperty Name="Customer" Type="TestService.Customer">
                      <ReferentialConstraint Property="CustomerId" ReferencedProperty="Id" />
                    </NavigationProperty>
                    <NavigationProperty Name="OrderLines" Type="Collection(TestService.OrderLine)" />
                  </EntityType>
                  <EntityType Name="OrderLine">
                    <Key>
                      <PropertyRef Name="OrderId" />
                      <PropertyRef Name="LineNumber" />
                    </Key>
                    <Property Name="OrderId" Type="Edm.Int64" Nullable="false" />
                    <Property Name="LineNumber" Type="Edm.Int32" Nullable="false" />
                    <Property Name="ProductId" Type="Edm.Int32" Nullable="false" />
                    <Property Name="Quantity" Type="Edm.Int32" Nullable="false" />
                    <Property Name="Price" Type="Edm.Decimal" Precision="18" Scale="2" Nullable="false" />
                    <NavigationProperty Name="Order" Type="TestService.Order">
                      <ReferentialConstraint Property="OrderId" ReferencedProperty="OrderId" />
                    </NavigationProperty>
                  </EntityType>
                  <EntityType Name="VipCustomer" BaseType="TestService.Customer">
                    <Property Name="VipLevel" Type="Edm.String" />
                    <Property Name="DiscountPercentage" Type="Edm.Decimal" Precision="5" Scale="2" />
                  </EntityType>
                  <EntityContainer Name="Container">
                    <EntitySet Name="Customers" EntityType="TestService.Customer">
                      <NavigationPropertyBinding Path="Orders" Target="Orders" />
                    </EntitySet>
                    <EntitySet Name="Orders" EntityType="TestService.Order">
                      <NavigationPropertyBinding Path="Customer" Target="Customers" />
                      <NavigationPropertyBinding Path="OrderLines" Target="OrderLines" />
                    </EntitySet>
                    <EntitySet Name="OrderLines" EntityType="TestService.OrderLine">
                      <NavigationPropertyBinding Path="Order" Target="Orders" />
                    </EntitySet>
                  </EntityContainer>
                </Schema>
              </edmx:DataServices>
            </edmx:Edmx>
            """;

        private const string MalformedCsdlXml = """
            <?xml version="1.0" encoding="utf-8"?>
            <edmx:Edmx Version="4.0" xmlns:edmx="http://docs.oasis-open.org/odata/ns/edmx">
              <edmx:DataServices>
                <Schema Namespace="TestService" xmlns="http://docs.oasis-open.org/odata/ns/edm">
                  <EntityType Name="BrokenEntity">
                    <!-- Missing Key element -->
                    <Property Name="Id" Type="Edm.Int32" Nullable="false" />
                  </EntityType>
            """;

        private const string InvalidTypeCsdlXml = """
            <?xml version="1.0" encoding="utf-8"?>
            <edmx:Edmx Version="4.0" xmlns:edmx="http://docs.oasis-open.org/odata/ns/edmx">
              <edmx:DataServices>
                <Schema Namespace="TestService" xmlns="http://docs.oasis-open.org/odata/ns/edm">
                  <EntityType Name="InvalidEntity">
                    <Key>
                      <PropertyRef Name="Id" />
                    </Key>
                    <Property Name="Id" Type="Edm.InvalidType" Nullable="false" />
                  </EntityType>
                  <EntityContainer Name="Container">
                    <EntitySet Name="InvalidEntities" EntityType="TestService.InvalidEntity" />
                  </EntityContainer>
                </Schema>
              </edmx:DataServices>
            </edmx:Edmx>
            """;

        #endregion

        #region Fields

        private IOptions<McpServerConfiguration> _configuration = null!;
        private TestHttpMessageHandler _httpHandler = null!;
        private IHttpClientFactory _httpClientFactory = null!;
        private ICsdlMetadataParser _metadataParser = null!;
        private IEnumerable<EdmModel> _edmModels = null!;

        #endregion

        #region Public Methods

        [TestInitialize]
        public void TestInitialize()
        {
            _configuration = Options.Create(new McpServerConfiguration
            {
                ODataService = new ODataServiceConfiguration
                {
                    BaseUrl = "https://services.odata.org/V4/TripPinService/",
                    MetadataPath = "/$metadata"
                },
                Caching = new CachingConfiguration
                {
                    MetadataTtl = TimeSpan.FromMinutes(10)
                }
            });

            _httpHandler = new TestHttpMessageHandler(ValidCsdlXml);
            _httpClientFactory = new TestHttpClientFactory(_httpHandler);
            _metadataParser = new CsdlParser();

            // Parse the valid CSDL to create EdmModel for tests
            var edmModel = _metadataParser.ParseFromString(ValidCsdlXml);
            _edmModels = new[] { edmModel };
        }

        [TestMethod]
        public void Constructor_WithNullHttpClientFactory_ShouldThrow()
        {
            Action act = () => new DynamicODataMcpTools(null!, _configuration, _metadataParser, NullLogger<DynamicODataMcpTools>.Instance, _edmModels);

            act.Should().ThrowExactly<ArgumentNullException>()
                .WithParameterName("httpClientFactory");
        }

        [TestMethod]
        public void Constructor_WithNullConfiguration_ShouldThrow()
        {
            Action act = () => new DynamicODataMcpTools(_httpClientFactory, null!, _metadataParser, NullLogger<DynamicODataMcpTools>.Instance, _edmModels);

            act.Should().ThrowExactly<ArgumentNullException>()
                .WithParameterName("configuration");
        }

        [TestMethod]
        public void Constructor_WithNullMetadataParser_ShouldThrow()
        {
            Action act = () => new DynamicODataMcpTools(_httpClientFactory, _configuration, null!, NullLogger<DynamicODataMcpTools>.Instance, _edmModels);

            act.Should().ThrowExactly<ArgumentNullException>()
                .WithParameterName("metadataParser");
        }

        [TestMethod]
        public void Constructor_WithNullLogger_ShouldThrow()
        {
            Action act = () => new DynamicODataMcpTools(_httpClientFactory, _configuration, _metadataParser, null!, _edmModels);

            act.Should().ThrowExactly<ArgumentNullException>()
                .WithParameterName("logger");
        }

        [TestMethod]
        public void Constructor_WithNullEdmModels_ShouldThrow()
        {
            Action act = () => new DynamicODataMcpTools(_httpClientFactory, _configuration, _metadataParser, NullLogger<DynamicODataMcpTools>.Instance, null!);

            act.Should().ThrowExactly<ArgumentNullException>()
                .WithParameterName("edmModels");
        }

        [TestMethod]
        public void Constructor_WithEmptyEdmModels_ShouldThrow()
        {
            Action act = () => new DynamicODataMcpTools(_httpClientFactory, _configuration, _metadataParser, NullLogger<DynamicODataMcpTools>.Instance, Array.Empty<EdmModel>());

            act.Should().ThrowExactly<ArgumentException>()
                .WithParameterName("edmModels");
        }

        [TestMethod]
        public void Constructor_WithValidParameters_ShouldSucceed()
        {
            var tools = new DynamicODataMcpTools(_httpClientFactory, _configuration, _metadataParser, NullLogger<DynamicODataMcpTools>.Instance, _edmModels);

            tools.Should().NotBeNull();
        }

        [TestMethod]
        public void DiscoverEntitySets_WithValidSchema_ShouldReturnEntitySets()
        {
            var tools = new DynamicODataMcpTools(_httpClientFactory, _configuration, _metadataParser, NullLogger<DynamicODataMcpTools>.Instance, _edmModels);

            var result = tools.DiscoverEntitySets();

            result.Should().NotBeNullOrWhiteSpace();
            var entitySets = JsonSerializer.Deserialize<List<Dictionary<string, object>>>(result);
            entitySets.Should().NotBeNull();
            entitySets.Should().HaveCount(2);
        }

        [TestMethod]
        public void DiscoverEntitySets_WithEmptySchema_ShouldReturnEmptyArray()
        {
            var emptyModel = _metadataParser.ParseFromString(EmptyCsdlXml);
            var tools = new DynamicODataMcpTools(_httpClientFactory, _configuration, _metadataParser, NullLogger<DynamicODataMcpTools>.Instance, new[] { emptyModel });

            var result = tools.DiscoverEntitySets();

            result.Should().NotBeNullOrWhiteSpace();
            var entitySets = JsonSerializer.Deserialize<List<Dictionary<string, object>>>(result);
            entitySets.Should().NotBeNull();
            entitySets.Should().BeEmpty();
        }

        [TestMethod]
        public void DiscoverEntitySets_WithComplexSchema_ShouldReturnAllEntitySets()
        {
            var complexModel = _metadataParser.ParseFromString(ComplexCsdlXml);
            var tools = new DynamicODataMcpTools(_httpClientFactory, _configuration, _metadataParser, NullLogger<DynamicODataMcpTools>.Instance, new[] { complexModel });

            var result = tools.DiscoverEntitySets();

            result.Should().NotBeNullOrWhiteSpace();
            var entitySets = JsonSerializer.Deserialize<List<Dictionary<string, object>>>(result);
            entitySets.Should().NotBeNull();
            entitySets.Should().HaveCount(3);
        }

        [TestMethod]
        public void DescribeEntityType_WithValidEntityType_ShouldReturnDescription()
        {
            var tools = new DynamicODataMcpTools(_httpClientFactory, _configuration, _metadataParser, NullLogger<DynamicODataMcpTools>.Instance, _edmModels);

            var result = tools.DescribeEntityType("Product");

            result.Should().NotBeNullOrWhiteSpace();
            var description = JsonSerializer.Deserialize<Dictionary<string, object>>(result);
            description.Should().NotBeNull();
            description.Should().ContainKey("Name");
            description.Should().ContainKey("Properties");
            description.Should().ContainKey("NavigationProperties");
        }

        [TestMethod]
        public void DescribeEntityType_WithCaseInsensitiveName_ShouldReturnDescription()
        {
            var tools = new DynamicODataMcpTools(_httpClientFactory, _configuration, _metadataParser, NullLogger<DynamicODataMcpTools>.Instance, _edmModels);

            var result = tools.DescribeEntityType("PRODUCT");

            result.Should().NotBeNullOrWhiteSpace();
        }

        [TestMethod]
        public void DescribeEntityType_WithFullyQualifiedName_ShouldReturnDescription()
        {
            var tools = new DynamicODataMcpTools(_httpClientFactory, _configuration, _metadataParser, NullLogger<DynamicODataMcpTools>.Instance, _edmModels);

            var result = tools.DescribeEntityType("TestService.Product");

            result.Should().NotBeNullOrWhiteSpace();
        }

        [TestMethod]
        public void DescribeEntityType_WithNonExistentEntityType_ShouldThrow()
        {
            var tools = new DynamicODataMcpTools(_httpClientFactory, _configuration, _metadataParser, NullLogger<DynamicODataMcpTools>.Instance, _edmModels);

            Action act = () => tools.DescribeEntityType("NonExistent");

            act.Should().ThrowExactly<InvalidOperationException>()
                .WithMessage("Failed to describe entity type*");
        }

        [TestMethod]
        public void DescribeEntityType_WithNullEntityTypeName_ShouldThrow()
        {
            var tools = new DynamicODataMcpTools(_httpClientFactory, _configuration, _metadataParser, NullLogger<DynamicODataMcpTools>.Instance, _edmModels);

            Action act = () => tools.DescribeEntityType(null!);

            act.Should().ThrowExactly<InvalidOperationException>();
        }

        [TestMethod]
        public void DescribeEntityType_WithEmptyEntityTypeName_ShouldThrow()
        {
            var tools = new DynamicODataMcpTools(_httpClientFactory, _configuration, _metadataParser, NullLogger<DynamicODataMcpTools>.Instance, _edmModels);

            Action act = () => tools.DescribeEntityType(string.Empty);

            act.Should().ThrowExactly<InvalidOperationException>();
        }

        [TestMethod]
        public void DescribeEntityType_WithInheritance_ShouldIncludeBaseType()
        {
            var complexModel = _metadataParser.ParseFromString(ComplexCsdlXml);
            var tools = new DynamicODataMcpTools(_httpClientFactory, _configuration, _metadataParser, NullLogger<DynamicODataMcpTools>.Instance, new[] { complexModel });

            var result = tools.DescribeEntityType("VipCustomer");

            result.Should().NotBeNullOrWhiteSpace();
            var description = JsonSerializer.Deserialize<Dictionary<string, object>>(result);
            description.Should().ContainKey("BaseType");
        }

        [TestMethod]
        public void GenerateQueryExamples_WithValidEntitySet_ShouldReturnExamples()
        {
            var tools = new DynamicODataMcpTools(_httpClientFactory, _configuration, _metadataParser, NullLogger<DynamicODataMcpTools>.Instance, _edmModels);

            var result = tools.GenerateQueryExamples("Products", false);

            result.Should().NotBeNullOrWhiteSpace();
            var examples = JsonSerializer.Deserialize<Dictionary<string, object>>(result);
            examples.Should().NotBeNull();
            examples.Should().ContainKey("Examples");
        }

        [TestMethod]
        public void GenerateQueryExamples_WithAdvancedFlag_ShouldReturnMoreExamples()
        {
            var tools = new DynamicODataMcpTools(_httpClientFactory, _configuration, _metadataParser, NullLogger<DynamicODataMcpTools>.Instance, _edmModels);

            var basicResult = tools.GenerateQueryExamples("Products", false);
            var advancedResult = tools.GenerateQueryExamples("Products", true);

            var basicExamples = JsonSerializer.Deserialize<Dictionary<string, object>>(basicResult);
            var advancedExamples = JsonSerializer.Deserialize<Dictionary<string, object>>(advancedResult);

            var basicCount = ((JsonElement)basicExamples!["TotalExamples"]).GetInt32();
            var advancedCount = ((JsonElement)advancedExamples!["TotalExamples"]).GetInt32();

            advancedCount.Should().BeGreaterThan(basicCount);
        }

        [TestMethod]
        public void GenerateQueryExamples_WithNonExistentEntitySet_ShouldThrow()
        {
            var tools = new DynamicODataMcpTools(_httpClientFactory, _configuration, _metadataParser, NullLogger<DynamicODataMcpTools>.Instance, _edmModels);

            Action act = () => tools.GenerateQueryExamples("NonExistent", false);

            act.Should().ThrowExactly<InvalidOperationException>()
                .WithMessage("Failed to generate query examples*");
        }

        [TestMethod]
        public void GenerateQueryExamples_WithNullEntitySetName_ShouldThrow()
        {
            var tools = new DynamicODataMcpTools(_httpClientFactory, _configuration, _metadataParser, NullLogger<DynamicODataMcpTools>.Instance, _edmModels);

            Action act = () => tools.GenerateQueryExamples(null!, false);

            act.Should().ThrowExactly<InvalidOperationException>();
        }

        [TestMethod]
        public void GenerateQueryExamples_WithEmptyEntitySetName_ShouldThrow()
        {
            var tools = new DynamicODataMcpTools(_httpClientFactory, _configuration, _metadataParser, NullLogger<DynamicODataMcpTools>.Instance, _edmModels);

            Action act = () => tools.GenerateQueryExamples(string.Empty, false);

            act.Should().ThrowExactly<InvalidOperationException>();
        }

        [TestMethod]
        public void ValidateQuery_WithValidUrl_ShouldReturnValid()
        {
            var tools = new DynamicODataMcpTools(_httpClientFactory, _configuration, _metadataParser, NullLogger<DynamicODataMcpTools>.Instance, _edmModels);

            var result = tools.ValidateQuery("https://services.odata.org/V4/TripPinService/Products");

            result.Should().NotBeNullOrWhiteSpace();
            var validation = JsonSerializer.Deserialize<Dictionary<string, object>>(result);
            validation.Should().NotBeNull();
            validation.Should().ContainKey("IsValid");
            var isValid = ((JsonElement)validation!["IsValid"]).GetBoolean();
            isValid.Should().BeTrue();
        }

        [TestMethod]
        public void ValidateQuery_WithInvalidUrl_ShouldReturnInvalid()
        {
            var tools = new DynamicODataMcpTools(_httpClientFactory, _configuration, _metadataParser, NullLogger<DynamicODataMcpTools>.Instance, _edmModels);

            var result = tools.ValidateQuery("not a url");

            result.Should().NotBeNullOrWhiteSpace();
            var validation = JsonSerializer.Deserialize<Dictionary<string, object>>(result);
            validation.Should().NotBeNull();
            validation.Should().ContainKey("IsValid");
            var isValid = ((JsonElement)validation!["IsValid"]).GetBoolean();
            isValid.Should().BeFalse();
        }

        [TestMethod]
        public void ValidateQuery_WithNonExistentEntitySet_ShouldReturnErrors()
        {
            var tools = new DynamicODataMcpTools(_httpClientFactory, _configuration, _metadataParser, NullLogger<DynamicODataMcpTools>.Instance, _edmModels);

            var result = tools.ValidateQuery("https://services.odata.org/V4/TripPinService/NonExistent");

            result.Should().NotBeNullOrWhiteSpace();
            var validation = JsonSerializer.Deserialize<Dictionary<string, object>>(result);
            validation.Should().NotBeNull();
            var isValid = ((JsonElement)validation!["IsValid"]).GetBoolean();
            isValid.Should().BeFalse();
        }

        [TestMethod]
        public void ValidateQuery_WithValidODataParameters_ShouldReturnValid()
        {
            var tools = new DynamicODataMcpTools(_httpClientFactory, _configuration, _metadataParser, NullLogger<DynamicODataMcpTools>.Instance, _edmModels);

            var result = tools.ValidateQuery("https://services.odata.org/V4/TripPinService/Products?$filter=Price gt 10&$top=5");

            result.Should().NotBeNullOrWhiteSpace();
        }

        [TestMethod]
        public void ValidateQuery_WithInvalidODataParameters_ShouldReturnWarnings()
        {
            var tools = new DynamicODataMcpTools(_httpClientFactory, _configuration, _metadataParser, NullLogger<DynamicODataMcpTools>.Instance, _edmModels);

            var result = tools.ValidateQuery("https://services.odata.org/V4/TripPinService/Products?$invalid=something");

            result.Should().NotBeNullOrWhiteSpace();
            var validation = JsonSerializer.Deserialize<Dictionary<string, object>>(result);
            validation.Should().ContainKey("Warnings");
        }

        [TestMethod]
        public void ValidateQuery_WithNullQueryUrl_ShouldReturnInvalid()
        {
            var tools = new DynamicODataMcpTools(_httpClientFactory, _configuration, _metadataParser, NullLogger<DynamicODataMcpTools>.Instance, _edmModels);

            var result = tools.ValidateQuery(null!);

            result.Should().NotBeNullOrWhiteSpace();
            var validation = JsonSerializer.Deserialize<Dictionary<string, object>>(result);
            validation.Should().NotBeNull();
            var isValid = ((JsonElement)validation!["IsValid"]).GetBoolean();
            isValid.Should().BeFalse();
        }

        [TestMethod]
        public void GetSampleKeyValue_WithIntType_ShouldReturn1()
        {
            var result = DynamicODataMcpTools.GetSampleKeyValue("Edm.Int32");

            result.Should().Be("1");
        }

        [TestMethod]
        public void GetSampleKeyValue_WithGuidType_ShouldReturnGuid()
        {
            var result = DynamicODataMcpTools.GetSampleKeyValue("Edm.Guid");

            result.Should().Contain("guid");
        }

        [TestMethod]
        public void GetSampleKeyValue_WithStringType_ShouldReturnSample()
        {
            var result = DynamicODataMcpTools.GetSampleKeyValue("Edm.String");

            result.Should().Be("'sample'");
        }

        [TestMethod]
        public void GetSampleKeyValue_WithDecimalType_ShouldReturn1Point0()
        {
            var result = DynamicODataMcpTools.GetSampleKeyValue("Edm.Decimal");

            result.Should().Be("1.0");
        }

        [TestMethod]
        public void GetSampleKeyValue_WithDoubleType_ShouldReturn1Point0()
        {
            var result = DynamicODataMcpTools.GetSampleKeyValue("Edm.Double");

            result.Should().Be("1.0");
        }

        [TestMethod]
        public void GetSampleKeyValue_WithDateTimeType_ShouldReturnDateTime()
        {
            var result = DynamicODataMcpTools.GetSampleKeyValue("Edm.DateTimeOffset");

            result.Should().Contain("datetime");
        }

        [TestMethod]
        public void GetSampleKeyValue_WithNullType_ShouldReturnDefaultSample()
        {
            var result = DynamicODataMcpTools.GetSampleKeyValue(null);

            result.Should().Be("'sample'");
        }

        [TestMethod]
        public void GetSampleKeyValue_WithUnknownType_ShouldReturnDefaultSample()
        {
            var result = DynamicODataMcpTools.GetSampleKeyValue("Edm.UnknownType");

            result.Should().Be("'sample'");
        }

        [TestMethod]
        public void ParseQueryString_WithValidQueryString_ShouldParsePairs()
        {
            var result = DynamicODataMcpTools.ParseQueryString("?key1=value1&key2=value2");

            result.Should().HaveCount(2);
            result["key1"].Should().Be("value1");
            result["key2"].Should().Be("value2");
        }

        [TestMethod]
        public void ParseQueryString_WithoutQuestionMark_ShouldParsePairs()
        {
            var result = DynamicODataMcpTools.ParseQueryString("key1=value1&key2=value2");

            result.Should().HaveCount(2);
        }

        [TestMethod]
        public void ParseQueryString_WithEncodedValues_ShouldDecodeValues()
        {
            var result = DynamicODataMcpTools.ParseQueryString("key=hello%20world");

            result["key"].Should().Be("hello world");
        }

        [TestMethod]
        public void ParseQueryString_WithEmptyString_ShouldReturnEmpty()
        {
            var result = DynamicODataMcpTools.ParseQueryString(string.Empty);

            result.Should().BeEmpty();
        }

        [TestMethod]
        public void ParseQueryString_WithNull_ShouldReturnEmpty()
        {
            var result = DynamicODataMcpTools.ParseQueryString(null!);

            result.Should().BeEmpty();
        }

        [TestMethod]
        public void ParseQueryString_WithValuelessParameter_ShouldHandleGracefully()
        {
            var result = DynamicODataMcpTools.ParseQueryString("key1&key2=value2");

            result.Should().HaveCount(2);
            result["key1"].Should().Be(string.Empty);
            result["key2"].Should().Be("value2");
        }


        #endregion

    }

}
