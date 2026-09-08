// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using CloudNimble.Breakdance.AspNetCore;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.OData;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.OData.Mcp.AspNetCore.Hosting;
using Microsoft.OData.Mcp.Core.Catalog;
using Microsoft.OData.Mcp.Tests.Shared;
using Microsoft.OData.Mcp.Tests.Shared.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.OData.Mcp.Tests.AspNetCore.Paging
{

    /// <summary>
    /// Client-driven and server-driven paging on convention OData 8 routes and MCP, alone and together.
    /// </summary>
    [TestClass]
    public class ConventionPagingTests : AspNetCoreBreakdanceTestBase
    {

        #region Test Lifecycle

        /// <summary>
        /// Builds a paging host. MCP does not inject query options.
        /// </summary>
        [TestInitialize]
        public void Setup()
        {
            TestHostBuilder.ConfigureServices((_, services) =>
            {
                services
                    .AddControllers()
                    .AddApplicationPart(typeof(ClientCustomersController).Assembly)
                    .AddOData(options =>
                    {
                        options.EnableQueryFeatures(100);
                        options.AddRouteComponents("odata", TestModels.GetPagingModel());
                    });
                services.AddODataMcp();
            });
            AddMinimalMvc();
            TestHostBuilder.ConfigureWebHost(web =>
            {
                web.Configure(app =>
                {
                    app.UseRouting();
                    app.UseEndpoints(endpoints => endpoints.MapControllers());
                    app.UseODataMcp();
                });
            });
            TestSetup();
        }

        /// <summary>
        /// Tears down the host.
        /// </summary>
        [TestCleanup]
        public void TearDown()
        {
            TestTearDown();
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// MCP count+top reports the full set size and a two-row page.
        /// </summary>
        [TestMethod]
        public async Task Client_Mcp_CountWithTop_ReturnsTotal()
        {
            var json = await QueryMcpAsync("ClientCustomers", new Dictionary<string, JsonElement>
            {
                ["count"] = JsonSerializer.SerializeToElement(true),
                ["orderby"] = JsonSerializer.SerializeToElement("CustomerId"),
                ["top"] = JsonSerializer.SerializeToElement(2)
            });

            ODataFeedReader.ReadCount(json).Should().Be(5);
            ODataFeedReader.ReadCompanyNames(json).Should().Equal("Contoso", "Fabrikam");
        }

        /// <summary>
        /// MCP without top does not invent paging; the client-driven set returns all rows.
        /// </summary>
        [TestMethod]
        public async Task Client_Mcp_WithoutTop_ReturnsAllFive()
        {
            var json = await QueryMcpAsync("ClientCustomers", new Dictionary<string, JsonElement>
            {
                ["orderby"] = JsonSerializer.SerializeToElement("CustomerId")
            });

            ODataFeedReader.ReadCompanyNames(json).Should().Equal(PagingCustomerSeed.CompanyNames);
        }

        /// <summary>
        /// MCP filter+skip+top returns the third matching row.
        /// </summary>
        [TestMethod]
        public async Task Client_Mcp_FilterSkipTop_ReturnsAdventureWorks()
        {
            var json = await QueryMcpAsync("ClientCustomers", new Dictionary<string, JsonElement>
            {
                ["filter"] = JsonSerializer.SerializeToElement("CustomerId gt 1"),
                ["orderby"] = JsonSerializer.SerializeToElement("CustomerId"),
                ["skip"] = JsonSerializer.SerializeToElement(2),
                ["top"] = JsonSerializer.SerializeToElement(1)
            });

            ODataFeedReader.ReadCompanyNames(json).Should().Equal("AdventureWorks");
        }

        /// <summary>
        /// Named list tool skip+top matches generic odata_query.
        /// </summary>
        [TestMethod]
        public async Task Client_Mcp_ListTool_SkipAndTop_MatchGeneric()
        {
            var generic = await QueryMcpAsync("ClientCustomers", new Dictionary<string, JsonElement>
            {
                ["orderby"] = JsonSerializer.SerializeToElement("CustomerId"),
                ["skip"] = JsonSerializer.SerializeToElement(1),
                ["top"] = JsonSerializer.SerializeToElement(2)
            });
            var named = await InvokeAsync("list_client_customers", new Dictionary<string, JsonElement>
            {
                ["orderby"] = JsonSerializer.SerializeToElement("CustomerId"),
                ["skip"] = JsonSerializer.SerializeToElement(1),
                ["top"] = JsonSerializer.SerializeToElement(2)
            });

            ODataFeedReader.ReadCompanyNames(named).Should().Equal(ODataFeedReader.ReadCompanyNames(generic));
            ODataFeedReader.ReadCompanyNames(named).Should().Equal("Fabrikam", "Northwind");
        }

        /// <summary>
        /// MCP forwards a large specified top; the five-row set returns all rows.
        /// </summary>
        [TestMethod]
        public async Task Client_Mcp_LargeTop_ReturnsAllFive()
        {
            var json = await QueryMcpAsync("ClientCustomers", new Dictionary<string, JsonElement>
            {
                ["orderby"] = JsonSerializer.SerializeToElement("CustomerId"),
                ["top"] = JsonSerializer.SerializeToElement(50)
            });

            ODataFeedReader.ReadCompanyNames(json).Should().Equal(PagingCustomerSeed.CompanyNames);
        }

        /// <summary>
        /// MCP skip past the last row is an empty page.
        /// </summary>
        [TestMethod]
        public async Task Client_Mcp_SkipBeyondEnd_IsEmpty()
        {
            var json = await QueryMcpAsync("ClientCustomers", new Dictionary<string, JsonElement>
            {
                ["skip"] = JsonSerializer.SerializeToElement(10),
                ["top"] = JsonSerializer.SerializeToElement(2)
            });

            ODataFeedReader.ReadValueCount(json).Should().Be(0);
        }

        /// <summary>
        /// MCP skip+top returns the second page.
        /// </summary>
        [TestMethod]
        public async Task Client_Mcp_SkipTop_ReturnsSecondPage()
        {
            var json = await QueryMcpAsync("ClientCustomers", new Dictionary<string, JsonElement>
            {
                ["orderby"] = JsonSerializer.SerializeToElement("CustomerId"),
                ["skip"] = JsonSerializer.SerializeToElement(2),
                ["top"] = JsonSerializer.SerializeToElement(2)
            });

            ODataFeedReader.ReadCompanyNames(json).Should().Equal("Northwind", "AdventureWorks");
        }

        /// <summary>
        /// MCP top=1 returns the first company.
        /// </summary>
        [TestMethod]
        public async Task Client_Mcp_Top_ReturnsFirstPage()
        {
            var json = await QueryMcpAsync("ClientCustomers", new Dictionary<string, JsonElement>
            {
                ["orderby"] = JsonSerializer.SerializeToElement("CustomerId"),
                ["top"] = JsonSerializer.SerializeToElement(1)
            });

            ODataFeedReader.ReadCompanyNames(json).Should().Equal("Contoso");
        }

        /// <summary>
        /// OData $count=true with $top returns the collection size and a page.
        /// </summary>
        [TestMethod]
        public async Task Client_OData_CountWithTop_ReturnsTotal()
        {
            var json = await GetODataAsync("/odata/ClientCustomers?$count=true&$top=2&$orderby=CustomerId");

            ODataFeedReader.ReadCount(json).Should().Be(5);
            ODataFeedReader.ReadCompanyNames(json).Should().Equal("Contoso", "Fabrikam");
        }

        /// <summary>
        /// OData $filter+$skip+$top returns AdventureWorks.
        /// </summary>
        [TestMethod]
        public async Task Client_OData_FilterSkipTop_ReturnsAdventureWorks()
        {
            var json = await GetODataAsync("/odata/ClientCustomers?$filter=CustomerId gt 1&$orderby=CustomerId&$skip=2&$top=1");

            ODataFeedReader.ReadCompanyNames(json).Should().Equal("AdventureWorks");
        }

        /// <summary>
        /// OData $skip past the last row is an empty page.
        /// </summary>
        [TestMethod]
        public async Task Client_OData_SkipBeyondEnd_IsEmpty()
        {
            var json = await GetODataAsync("/odata/ClientCustomers?$skip=10&$top=2");

            ODataFeedReader.ReadValueCount(json).Should().Be(0);
        }

        /// <summary>
        /// OData $skip+$top returns the second page.
        /// </summary>
        [TestMethod]
        public async Task Client_OData_SkipTop_ReturnsSecondPage()
        {
            var json = await GetODataAsync("/odata/ClientCustomers?$orderby=CustomerId&$skip=2&$top=2");

            ODataFeedReader.ReadCompanyNames(json).Should().Equal("Northwind", "AdventureWorks");
        }

        /// <summary>
        /// OData $top=1 returns Contoso.
        /// </summary>
        [TestMethod]
        public async Task Client_OData_Top_ReturnsFirstPage()
        {
            var json = await GetODataAsync("/odata/ClientCustomers?$orderby=CustomerId&$top=1");

            ODataFeedReader.ReadCompanyNames(json).Should().Equal("Contoso");
        }

        /// <summary>
        /// Unpaged OData and MCP without top return the same five rows.
        /// </summary>
        [TestMethod]
        public async Task Client_ODataAndMcp_Unpaged_BothReturnAllFive()
        {
            var odata = await GetODataAsync("/odata/ClientCustomers?$orderby=CustomerId");
            var mcp = await QueryMcpAsync("ClientCustomers", new Dictionary<string, JsonElement>
            {
                ["orderby"] = JsonSerializer.SerializeToElement("CustomerId")
            });

            ODataFeedReader.ReadCompanyNames(odata).Should().Equal(PagingCustomerSeed.CompanyNames);
            ODataFeedReader.ReadCompanyNames(mcp).Should().Equal(ODataFeedReader.ReadCompanyNames(odata));
        }

        /// <summary>
        /// The same $count+$skip+$top+$orderby on OData and MCP produce the same page and count.
        /// </summary>
        [TestMethod]
        public async Task Client_ODataAndMcp_CountSkipTop_Match()
        {
            var odata = await GetODataAsync("/odata/ClientCustomers?$count=true&$orderby=CustomerId&$skip=1&$top=2");
            var mcp = await QueryMcpAsync("ClientCustomers", new Dictionary<string, JsonElement>
            {
                ["count"] = JsonSerializer.SerializeToElement(true),
                ["orderby"] = JsonSerializer.SerializeToElement("CustomerId"),
                ["skip"] = JsonSerializer.SerializeToElement(1),
                ["top"] = JsonSerializer.SerializeToElement(2)
            });

            ODataFeedReader.ReadCount(mcp).Should().Be(ODataFeedReader.ReadCount(odata)).And.Be(5);
            ODataFeedReader.ReadCompanyNames(mcp).Should().Equal(ODataFeedReader.ReadCompanyNames(odata));
            ODataFeedReader.ReadCompanyNames(mcp).Should().Equal("Fabrikam", "Northwind");
        }

        /// <summary>
        /// Filter+skip+top+orderby match on both sides.
        /// </summary>
        [TestMethod]
        public async Task Client_ODataAndMcp_FilterSkipTop_Match()
        {
            var odata = await GetODataAsync("/odata/ClientCustomers?$filter=CustomerId gt 1&$orderby=CustomerId&$skip=1&$top=2&$count=true");
            var mcp = await QueryMcpAsync("ClientCustomers", new Dictionary<string, JsonElement>
            {
                ["count"] = JsonSerializer.SerializeToElement(true),
                ["filter"] = JsonSerializer.SerializeToElement("CustomerId gt 1"),
                ["orderby"] = JsonSerializer.SerializeToElement("CustomerId"),
                ["skip"] = JsonSerializer.SerializeToElement(1),
                ["top"] = JsonSerializer.SerializeToElement(2)
            });

            ODataFeedReader.ReadCount(mcp).Should().Be(ODataFeedReader.ReadCount(odata)).And.Be(4);
            ODataFeedReader.ReadCompanyNames(mcp).Should().Equal(ODataFeedReader.ReadCompanyNames(odata));
            ODataFeedReader.ReadCompanyNames(mcp).Should().Equal("Northwind", "AdventureWorks");
        }

        /// <summary>
        /// Skip+top pages match on both sides.
        /// </summary>
        [TestMethod]
        public async Task Client_ODataAndMcp_SkipTop_Match()
        {
            var odata = await GetODataAsync("/odata/ClientCustomers?$orderby=CustomerId&$skip=2&$top=2");
            var mcp = await QueryMcpAsync("ClientCustomers", new Dictionary<string, JsonElement>
            {
                ["orderby"] = JsonSerializer.SerializeToElement("CustomerId"),
                ["skip"] = JsonSerializer.SerializeToElement(2),
                ["top"] = JsonSerializer.SerializeToElement(2)
            });

            ODataFeedReader.ReadKeys(mcp).Should().Equal(ODataFeedReader.ReadKeys(odata));
            ODataFeedReader.ReadCompanyNames(mcp).Should().Equal("Northwind", "AdventureWorks");
        }

        /// <summary>
        /// $top+$orderby match on both sides.
        /// </summary>
        [TestMethod]
        public async Task Client_ODataAndMcp_TopOrderBy_Match()
        {
            var odata = await GetODataAsync("/odata/ClientCustomers?$orderby=CustomerId&$top=1");
            var mcp = await QueryMcpAsync("ClientCustomers", new Dictionary<string, JsonElement>
            {
                ["orderby"] = JsonSerializer.SerializeToElement("CustomerId"),
                ["top"] = JsonSerializer.SerializeToElement(1)
            });

            ODataFeedReader.ReadCompanyNames(mcp).Should().Equal(ODataFeedReader.ReadCompanyNames(odata));
            ODataFeedReader.ReadCompanyNames(mcp).Should().Equal("Contoso");
        }

        /// <summary>
        /// MCP without top is a passthrough; server PageSize pages the feed and returns a next link.
        /// </summary>
        [TestMethod]
        public async Task Server_Mcp_WithoutTop_ReturnsFirstPageAndNextLink()
        {
            var json = await QueryMcpAsync("ServerCustomers", new Dictionary<string, JsonElement>
            {
                ["orderby"] = JsonSerializer.SerializeToElement("CustomerId")
            });

            ODataFeedReader.ReadCompanyNames(json).Should().Equal("Contoso", "Fabrikam");
            ODataFeedReader.ReadNextLink(json).Should().NotBeNullOrWhiteSpace();
        }

        /// <summary>
        /// OData PageSize=2 returns two rows and a next link.
        /// </summary>
        [TestMethod]
        public async Task Server_OData_PageSize_ReturnsNextLink()
        {
            var json = await GetODataAsync("/odata/ServerCustomers?$orderby=CustomerId");

            ODataFeedReader.ReadCompanyNames(json).Should().Equal("Contoso", "Fabrikam");
            var next = ODataFeedReader.ReadNextLink(json);
            next.Should().NotBeNullOrWhiteSpace();
            next.Should().Contain("ServerCustomers");
        }

        /// <summary>
        /// First server page matches on OData and MCP, including a next link.
        /// </summary>
        [TestMethod]
        public async Task Server_ODataAndMcp_FirstPage_Match()
        {
            var odata = await GetODataAsync("/odata/ServerCustomers?$orderby=CustomerId");
            var mcp = await QueryMcpAsync("ServerCustomers", new Dictionary<string, JsonElement>
            {
                ["orderby"] = JsonSerializer.SerializeToElement("CustomerId")
            });

            ODataFeedReader.ReadCompanyNames(mcp).Should().Equal(ODataFeedReader.ReadCompanyNames(odata));
            ODataFeedReader.ReadNextLink(odata).Should().NotBeNullOrWhiteSpace();
            ODataFeedReader.ReadNextLink(mcp).Should().NotBeNullOrWhiteSpace();
        }

        /// <summary>
        /// Second page via OData nextLink and MCP skip+top agree.
        /// </summary>
        [TestMethod]
        public async Task Server_ODataAndMcp_SecondPageViaSkip_Match()
        {
            var first = await GetODataAsync("/odata/ServerCustomers?$orderby=CustomerId");
            var next = ODataFeedReader.ReadNextLink(first);
            next.Should().NotBeNullOrWhiteSpace();

            using var client = TestServer.CreateClient();
            var nextResponse = await client.GetAsync(next);
            var nextBody = await nextResponse.Content.ReadAsStringAsync();
            nextResponse.IsSuccessStatusCode.Should().BeTrue("status {0} body {1}", (int)nextResponse.StatusCode, nextBody);

            var mcp = await QueryMcpAsync("ServerCustomers", new Dictionary<string, JsonElement>
            {
                ["orderby"] = JsonSerializer.SerializeToElement("CustomerId"),
                ["skip"] = JsonSerializer.SerializeToElement(2),
                ["top"] = JsonSerializer.SerializeToElement(2)
            });
            var odataSkip = await GetODataAsync("/odata/ServerCustomers?$orderby=CustomerId&$skip=2&$top=2");

            ODataFeedReader.ReadCompanyNames(mcp).Should().Equal(ODataFeedReader.ReadCompanyNames(odataSkip));
            ODataFeedReader.ReadCompanyNames(nextBody).Should().Equal(ODataFeedReader.ReadCompanyNames(mcp));
            ODataFeedReader.ReadCompanyNames(mcp).Should().Equal("Northwind", "AdventureWorks");
        }

        #endregion

        #region Internal Methods

        /// <summary>
        /// GETs an OData URL on the test server and returns the JSON body.
        /// </summary>
        /// <param name="path">The request path and query.</param>
        /// <returns>
        /// The response body.
        /// </returns>
        internal async Task<string> GetODataAsync(string path)
        {
            using var client = TestServer.CreateClient();
            var response = await client.GetAsync(path);
            var body = await response.Content.ReadAsStringAsync();
            response.IsSuccessStatusCode.Should().BeTrue("status {0} body {1}", (int)response.StatusCode, body);

            return body;
        }

        /// <summary>
        /// Invokes a catalog tool and returns structured content.
        /// </summary>
        /// <param name="name">The tool name.</param>
        /// <param name="arguments">Tool arguments.</param>
        /// <returns>
        /// Structured JSON.
        /// </returns>
        internal async Task<string> InvokeAsync(string name, Dictionary<string, JsonElement> arguments)
        {
            var result = await Runtime().InvokeAsync(name, arguments, CancellationToken.None);
            result.IsError.Should().BeFalse(result.Text);
            result.StructuredContent.Should().NotBeNullOrWhiteSpace();

            return result.StructuredContent!;
        }

        /// <summary>
        /// Queries an entity set through <c>odata_query</c>.
        /// </summary>
        /// <param name="entitySet">The entity set name.</param>
        /// <param name="arguments">Additional query arguments.</param>
        /// <returns>
        /// Structured JSON.
        /// </returns>
        internal async Task<string> QueryMcpAsync(string entitySet, Dictionary<string, JsonElement> arguments)
        {
            arguments["entitySet"] = JsonSerializer.SerializeToElement(entitySet);

            return await InvokeAsync("odata_query", arguments);
        }

        /// <summary>
        /// Gets the odata session runtime.
        /// </summary>
        /// <returns>
        /// The runtime.
        /// </returns>
        internal ODataToolRuntime Runtime()
        {
            return TestServer.Services.GetRequiredService<ODataMcpSessionFactory>().Sessions["odata"].Runtime;
        }

        #endregion

    }

}
