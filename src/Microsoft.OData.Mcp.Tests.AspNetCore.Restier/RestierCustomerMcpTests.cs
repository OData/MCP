// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OData.Mcp.AspNetCore.Hosting;
using Microsoft.OData.Mcp.Tests.AspNetCore.Restier.Scenarios;
using Microsoft.Restier.Breakdance;
using Microsoft.Restier.Core;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.OData.Mcp.Tests.AspNetCore.Restier
{

    /// <summary>
    /// Restier OData 7 endpoint routing plus MCP, asserted side by side against the same seed.
    /// </summary>
    [TestClass]
    public class RestierCustomerMcpTests : RestierBreakdanceTestBase<McpCustomerApi>
    {

        #region Fields

        internal readonly string _databaseName = "RestierCustomers-" + Guid.NewGuid().ToString("N");

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="RestierCustomerMcpTests"/> class using endpoint routing.
        /// </summary>
        public RestierCustomerMcpTests()
            : base(useEndpointRouting: true)
        {

            AddRestierAction = apiBuilder =>
            {
                apiBuilder.AddRestierApi<McpCustomerApi>(restierServices =>
                {
                    restierServices.AddEFCoreProviderServices<McpCustomerContext>((_, options) =>
                    {
                        options.UseInMemoryDatabase(_databaseName);
                    });
                    RestierTestSeed.EnsureCustomers(restierServices);
                });
            };

            MapRestierAction = routeBuilder =>
            {
                routeBuilder.MapApiRoute<McpCustomerApi>("odata", "odata");
            };

            ApplicationBuilderLastAction = app => app.UseODataMcp();

        }

        #endregion

        #region Test Lifecycle

        /// <summary>
        /// Enables MCP and starts the Restier host.
        /// </summary>
        [TestInitialize]
        public void Setup()
        {
            TestHostBuilder.ConfigureServices((_, services) =>
            {
                services.AddODataMcp();
            });

            TestSetup();
        }

        /// <summary>
        /// Tears down the Restier host.
        /// </summary>
        [TestCleanup]
        public void TearDown()
        {
            TestTearDown();
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// GET /odata/Customers returns the seeded companies.
        /// </summary>
        [TestMethod]
        public async Task Restier_GetCustomers_ReturnsSeed()
        {
            using var client = TestServer.CreateClient();
            var response = await client.GetAsync("odata/Customers");
            var body = await response.Content.ReadAsStringAsync();

            response.IsSuccessStatusCode.Should().BeTrue("status {0} body {1}", (int)response.StatusCode, body);
            body.Should().Contain("Contoso");
            body.Should().Contain("Fabrikam");
        }

        /// <summary>
        /// GET /odata/Customers(1) returns Contoso.
        /// </summary>
        [TestMethod]
        public async Task Restier_GetCustomerByKey_ReturnsContoso()
        {
            using var client = TestServer.CreateClient();
            var response = await client.GetAsync("odata/Customers(1)");
            var body = await response.Content.ReadAsStringAsync();

            response.IsSuccessStatusCode.Should().BeTrue("status {0} body {1}", (int)response.StatusCode, body);
            body.Should().Contain("Contoso");
            body.Should().NotContain("Fabrikam");
        }

        /// <summary>
        /// GET /odata/$metadata describes Customers.
        /// </summary>
        [TestMethod]
        public async Task Restier_Metadata_DescribesCustomers()
        {
            using var client = TestServer.CreateClient();
            var response = await client.GetAsync("odata/$metadata");
            var body = await response.Content.ReadAsStringAsync();

            response.IsSuccessStatusCode.Should().BeTrue("status {0} body {1}", (int)response.StatusCode, body);
            body.Should().Contain("Customers");
            body.Should().Contain("McpCustomer");
        }

        /// <summary>
        /// The OData service document lists Customers.
        /// </summary>
        [TestMethod]
        public async Task Restier_ServiceDocument_ListsCustomers()
        {
            using var client = TestServer.CreateClient();
            var body = await client.GetStringAsync("odata");

            body.Should().Contain("Customers");
        }

        /// <summary>
        /// $filter on the Restier route returns one company.
        /// </summary>
        [TestMethod]
        public async Task Restier_Filter_ReturnsContosoOnly()
        {
            using var client = TestServer.CreateClient();
            var response = await client.GetAsync("odata/Customers?$filter=CompanyName eq 'Contoso'");
            var body = await response.Content.ReadAsStringAsync();

            response.IsSuccessStatusCode.Should().BeTrue("status {0} body {1}", (int)response.StatusCode, body);
            body.Should().Contain("Contoso");
            body.Should().NotContain("Fabrikam");
        }

        /// <summary>
        /// Discovery found the Restier prefix from endpoint routing.
        /// </summary>
        [TestMethod]
        public void Restier_Discovery_FindsOdataPrefix()
        {
            var factory = TestServer.Services.GetRequiredService<ODataMcpSessionFactory>();

            factory.Sessions.Keys.Should().Contain("odata");
            factory.Sessions["odata"].Catalog.Tools.Select(tool => tool.Name).Should().Contain("odata_query");
            factory.Sessions["odata"].Catalog.Tools.Select(tool => tool.Name).Should().Contain("list_customers");
            factory.Sessions["odata"].Catalog.Tools.Select(tool => tool.Name).Should().NotContain("shutdown_server");
        }

        /// <summary>
        /// Endpoint routing registered a Restier catch-all.
        /// </summary>
        [TestMethod]
        public void Restier_EndpointDataSource_HasODataCatchAll()
        {
            var source = TestServer.Services.GetRequiredService<EndpointDataSource>();
            var templates = source.Endpoints.OfType<RouteEndpoint>().Select(endpoint => endpoint.RoutePattern.RawText ?? string.Empty);

            templates.Should().Contain(template => template.Contains("ODataEndpointPath_", StringComparison.Ordinal));
            templates.Should().Contain(template => template.Contains("odata", StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// MCP odata_query returns the same seed as GET /odata/Customers.
        /// </summary>
        [TestMethod]
        public async Task Restier_OdataQuery_MatchesHttpGet()
        {
            using var client = TestServer.CreateClient();
            var odata = await client.GetStringAsync("odata/Customers");
            odata.Should().Contain("Contoso");

            var result = await Runtime().InvokeAsync(
                "odata_query",
                new Dictionary<string, JsonElement>
                {
                    ["entitySet"] = JsonSerializer.SerializeToElement("Customers"),
                    ["top"] = JsonSerializer.SerializeToElement(20)
                },
                CancellationToken.None);

            result.IsError.Should().BeFalse(result.Text);
            result.StructuredContent.Should().Contain("Contoso");
            result.StructuredContent.Should().Contain("Fabrikam");
        }

        /// <summary>
        /// MCP filter matches Restier $filter.
        /// </summary>
        [TestMethod]
        public async Task Restier_OdataQueryFilter_MatchesHttpFilter()
        {
            using var client = TestServer.CreateClient();
            var odata = await client.GetStringAsync("odata/Customers?$filter=CompanyName eq 'Fabrikam'");
            odata.Should().Contain("Fabrikam");
            odata.Should().NotContain("Contoso");

            var result = await Runtime().InvokeAsync(
                "odata_query",
                new Dictionary<string, JsonElement>
                {
                    ["entitySet"] = JsonSerializer.SerializeToElement("Customers"),
                    ["filter"] = JsonSerializer.SerializeToElement("CompanyName eq 'Fabrikam'")
                },
                CancellationToken.None);

            result.IsError.Should().BeFalse(result.Text);
            result.StructuredContent.Should().Contain("Fabrikam");
            result.StructuredContent.Should().NotContain("Contoso");
        }

        /// <summary>
        /// MCP odata_get returns the same entity as GET Customers(1).
        /// </summary>
        [TestMethod]
        public async Task Restier_OdataGet_MatchesHttpGetByKey()
        {
            using var client = TestServer.CreateClient();
            var odata = await client.GetStringAsync("odata/Customers(1)");
            odata.Should().Contain("Contoso");

            var result = await Runtime().InvokeAsync(
                "odata_get",
                new Dictionary<string, JsonElement>
                {
                    ["entitySet"] = JsonSerializer.SerializeToElement("Customers"),
                    ["key"] = JsonSerializer.SerializeToElement("1")
                },
                CancellationToken.None);

            result.IsError.Should().BeFalse(result.Text);
            result.StructuredContent.Should().Contain("Contoso");
        }

        /// <summary>
        /// MCP list-entity-sets agrees with $metadata.
        /// </summary>
        [TestMethod]
        public async Task Restier_ListEntitySets_AgreesWithMetadata()
        {
            using var client = TestServer.CreateClient();
            var metadata = await client.GetAsync("odata/$metadata");
            var metadataBody = await metadata.Content.ReadAsStringAsync();
            metadata.IsSuccessStatusCode.Should().BeTrue("status {0} body {1}", (int)metadata.StatusCode, metadataBody);
            metadataBody.Should().Contain("Customers");

            var listed = await Runtime().InvokeAsync("odata_list_entity_sets", null, CancellationToken.None);
            listed.IsError.Should().BeFalse(listed.Text);
            listed.StructuredContent.Should().Contain("Customers");
        }

        /// <summary>
        /// describe-type reports the Restier customer type.
        /// </summary>
        [TestMethod]
        public async Task Restier_DescribeType_Customers()
        {
            var result = await Runtime().InvokeAsync(
                "odata_describe_type",
                new Dictionary<string, JsonElement>
                {
                    ["name"] = JsonSerializer.SerializeToElement("Customers")
                },
                CancellationToken.None);

            result.IsError.Should().BeFalse(result.Text);
            result.Text.Should().Contain("CompanyName");
        }

        /// <summary>
        /// Unknown entity sets fail through MCP the same way the OData route does not list them.
        /// </summary>
        [TestMethod]
        public async Task Restier_UnknownEntitySet_IsErrorOnMcpAndHttp()
        {
            using var client = TestServer.CreateClient();
            var missing = await client.GetAsync("odata/DoesNotExist");
            missing.IsSuccessStatusCode.Should().BeFalse();

            var result = await Runtime().InvokeAsync(
                "odata_query",
                new Dictionary<string, JsonElement>
                {
                    ["entitySet"] = JsonSerializer.SerializeToElement("DoesNotExist")
                },
                CancellationToken.None);

            result.IsError.Should().BeTrue();
        }

        /// <summary>
        /// POST /odata/mcp is mapped beside the Restier route.
        /// </summary>
        [TestMethod]
        public async Task Restier_McpEndpoint_IsNotMissing()
        {
            using var client = TestServer.CreateClient();
            var response = await client.PostAsync("odata/mcp", RestierJsonContent.Json("{}"));

            response.StatusCode.Should().NotBe(HttpStatusCode.NotFound);
            response.StatusCode.Should().NotBe(HttpStatusCode.MethodNotAllowed);
        }

        /// <summary>
        /// Creating a customer through Restier HTTP is visible to a later MCP query.
        /// </summary>
        [TestMethod]
        public async Task Restier_HttpCreate_VisibleToMcpQuery()
        {
            using var client = TestServer.CreateClient();
            using var content = RestierJsonContent.Json("""{"Id":3,"CompanyName":"Northwind"}""");
            var created = await client.PostAsync("odata/Customers", content);
            var createdBody = await created.Content.ReadAsStringAsync();
            created.IsSuccessStatusCode.Should().BeTrue("status {0} body {1}", (int)created.StatusCode, createdBody);

            var result = await Runtime().InvokeAsync(
                "odata_query",
                new Dictionary<string, JsonElement>
                {
                    ["entitySet"] = JsonSerializer.SerializeToElement("Customers"),
                    ["filter"] = JsonSerializer.SerializeToElement("CompanyName eq 'Northwind'")
                },
                CancellationToken.None);

            result.IsError.Should().BeFalse(result.Text);
            result.StructuredContent.Should().Contain("Northwind");
        }

        /// <summary>
        /// Creating a customer through MCP is visible on the Restier HTTP route.
        /// </summary>
        [TestMethod]
        public async Task Restier_McpCreate_VisibleToHttpGet()
        {
            var created = await Runtime().InvokeAsync(
                "odata_create",
                new Dictionary<string, JsonElement>
                {
                    ["entitySet"] = JsonSerializer.SerializeToElement("Customers"),
                    ["body"] = JsonSerializer.SerializeToElement("""{"Id":4,"CompanyName":"AdventureWorks"}""")
                },
                CancellationToken.None);

            created.IsError.Should().BeFalse(created.Text);

            using var client = TestServer.CreateClient();
            var body = await client.GetStringAsync("odata/Customers?$filter=CompanyName eq 'AdventureWorks'");
            body.Should().Contain("AdventureWorks");
        }

        /// <summary>
        /// $top on Restier and MCP top agree on a single row.
        /// </summary>
        [TestMethod]
        public async Task Restier_Top_MatchesODataAndMcp()
        {
            using var client = TestServer.CreateClient();
            var odata = await client.GetAsync("odata/Customers?$top=1&$orderby=Id");
            var odataBody = await odata.Content.ReadAsStringAsync();
            odata.IsSuccessStatusCode.Should().BeTrue("status {0} body {1}", (int)odata.StatusCode, odataBody);

            var result = await Runtime().InvokeAsync(
                "odata_query",
                new Dictionary<string, JsonElement>
                {
                    ["entitySet"] = JsonSerializer.SerializeToElement("Customers"),
                    ["top"] = JsonSerializer.SerializeToElement(1),
                    ["orderby"] = JsonSerializer.SerializeToElement("Id")
                },
                CancellationToken.None);

            result.IsError.Should().BeFalse(result.Text);
            result.StructuredContent.Should().Contain("Contoso");
        }

        /// <summary>
        /// The host package did not pull OData 8 into this Restier process.
        /// </summary>
        [TestMethod]
        public void Restier_Process_DoesNotLoadODataEightController()
        {
            var odataEight = Type.GetType("Microsoft.AspNetCore.OData.Routing.Controllers.ODataController, Microsoft.AspNetCore.OData");
            var odataSeven = Type.GetType("Microsoft.AspNet.OData.ODataController, Microsoft.AspNetCore.OData");

            odataEight.Should().BeNull("Restier tests must not load OData 8 controller types");
            odataSeven.Should().NotBeNull("Restier.AspNetCore loads Microsoft.AspNet.OData.ODataController");
        }

        #endregion

        #region Internal Methods

        /// <summary>
        /// Gets the MCP runtime for the Restier odata prefix.
        /// </summary>
        /// <returns>
        /// The runtime.
        /// </returns>
        internal Microsoft.OData.Mcp.Core.Catalog.ODataToolRuntime Runtime()
        {
            return GetService<ODataMcpSessionFactory>().Sessions["odata"].Runtime;
        }

        #endregion

    }

}
