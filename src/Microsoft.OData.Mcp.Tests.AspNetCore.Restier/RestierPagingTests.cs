// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OData.Mcp.AspNetCore.Hosting;
using Microsoft.OData.Mcp.Core.Catalog;
using Microsoft.OData.Mcp.Tests.Shared;
using Microsoft.Restier.Breakdance;
using Microsoft.Restier.Core;
using Microsoft.Restier.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.OData.Mcp.Tests.AspNetCore.Restier
{

    /// <summary>
    /// Client-driven paging on Restier OData 7 routes and MCP, alone and together.
    /// </summary>
    [TestClass]
    public class RestierPagingTests : RestierBreakdanceTestBase<McpCustomerApi>
    {

        #region Fields

        internal readonly string _databaseName = "RestierPaging-" + Guid.NewGuid().ToString("N");

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="RestierPagingTests"/> class using endpoint routing.
        /// </summary>
        public RestierPagingTests()
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
                    RestierTestSeed.EnsureFiveCustomers(restierServices);
                });
            };

            MapRestierAction = routeBuilder =>
            {
                routeBuilder.MapApiRoute<McpCustomerApi>("odata", "odata");
            };
        }

        #endregion

        #region Test Lifecycle

        /// <summary>
        /// Enables MCP. Query options are passed through unchanged.
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
        /// MCP count+top reports five customers and a two-row page.
        /// </summary>
        [TestMethod]
        public async Task Mcp_CountWithTop_ReturnsTotal()
        {
            var json = await QueryMcpAsync(new Dictionary<string, JsonElement>
            {
                ["count"] = JsonSerializer.SerializeToElement(true),
                ["orderby"] = JsonSerializer.SerializeToElement("Id"),
                ["top"] = JsonSerializer.SerializeToElement(2)
            });

            ODataFeedReader.ReadCount(json).Should().Be(5);
            ODataFeedReader.ReadCompanyNames(json).Should().Equal("Contoso", "Fabrikam");
        }

        /// <summary>
        /// MCP without top does not invent paging; Restier returns all five rows.
        /// </summary>
        [TestMethod]
        public async Task Mcp_WithoutTop_ReturnsAllFive()
        {
            var json = await QueryMcpAsync(new Dictionary<string, JsonElement>
            {
                ["orderby"] = JsonSerializer.SerializeToElement("Id")
            });

            ODataFeedReader.ReadCompanyNames(json).Should().Equal("Contoso", "Fabrikam", "Northwind", "AdventureWorks", "Wide World");
        }

        /// <summary>
        /// MCP filter+skip+top returns AdventureWorks.
        /// </summary>
        [TestMethod]
        public async Task Mcp_FilterSkipTop_ReturnsAdventureWorks()
        {
            var json = await QueryMcpAsync(new Dictionary<string, JsonElement>
            {
                ["filter"] = JsonSerializer.SerializeToElement("Id gt 1"),
                ["orderby"] = JsonSerializer.SerializeToElement("Id"),
                ["skip"] = JsonSerializer.SerializeToElement(2),
                ["top"] = JsonSerializer.SerializeToElement(1)
            });

            ODataFeedReader.ReadCompanyNames(json).Should().Equal("AdventureWorks");
        }

        /// <summary>
        /// Named list_customers skip+top matches odata_query.
        /// </summary>
        [TestMethod]
        public async Task Mcp_ListTool_SkipAndTop_MatchGeneric()
        {
            var generic = await QueryMcpAsync(new Dictionary<string, JsonElement>
            {
                ["orderby"] = JsonSerializer.SerializeToElement("Id"),
                ["skip"] = JsonSerializer.SerializeToElement(1),
                ["top"] = JsonSerializer.SerializeToElement(2)
            });
            var named = await InvokeAsync("list_customers", new Dictionary<string, JsonElement>
            {
                ["orderby"] = JsonSerializer.SerializeToElement("Id"),
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
        public async Task Mcp_LargeTop_ReturnsAllFive()
        {
            var json = await QueryMcpAsync(new Dictionary<string, JsonElement>
            {
                ["orderby"] = JsonSerializer.SerializeToElement("Id"),
                ["top"] = JsonSerializer.SerializeToElement(50)
            });

            ODataFeedReader.ReadCompanyNames(json).Should().Equal("Contoso", "Fabrikam", "Northwind", "AdventureWorks", "Wide World");
        }

        /// <summary>
        /// MCP skip past the last row is empty.
        /// </summary>
        [TestMethod]
        public async Task Mcp_SkipBeyondEnd_IsEmpty()
        {
            var json = await QueryMcpAsync(new Dictionary<string, JsonElement>
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
        public async Task Mcp_SkipTop_ReturnsSecondPage()
        {
            var json = await QueryMcpAsync(new Dictionary<string, JsonElement>
            {
                ["orderby"] = JsonSerializer.SerializeToElement("Id"),
                ["skip"] = JsonSerializer.SerializeToElement(2),
                ["top"] = JsonSerializer.SerializeToElement(2)
            });

            ODataFeedReader.ReadCompanyNames(json).Should().Equal("Northwind", "AdventureWorks");
        }

        /// <summary>
        /// MCP top=1 returns Contoso.
        /// </summary>
        [TestMethod]
        public async Task Mcp_Top_ReturnsFirstPage()
        {
            var json = await QueryMcpAsync(new Dictionary<string, JsonElement>
            {
                ["orderby"] = JsonSerializer.SerializeToElement("Id"),
                ["top"] = JsonSerializer.SerializeToElement(1)
            });

            ODataFeedReader.ReadCompanyNames(json).Should().Equal("Contoso");
        }

        /// <summary>
        /// Restier $count+$top returns the collection size and a page.
        /// </summary>
        [TestMethod]
        public async Task OData_CountWithTop_ReturnsTotal()
        {
            var json = await GetODataAsync("odata/Customers?$count=true&$top=2&$orderby=Id");

            ODataFeedReader.ReadCount(json).Should().Be(5);
            ODataFeedReader.ReadCompanyNames(json).Should().Equal("Contoso", "Fabrikam");
        }

        /// <summary>
        /// Restier $filter+$skip+$top returns AdventureWorks.
        /// </summary>
        [TestMethod]
        public async Task OData_FilterSkipTop_ReturnsAdventureWorks()
        {
            var json = await GetODataAsync("odata/Customers?$filter=Id gt 1&$orderby=Id&$skip=2&$top=1");

            ODataFeedReader.ReadCompanyNames(json).Should().Equal("AdventureWorks");
        }

        /// <summary>
        /// Restier $skip past the last row is empty.
        /// </summary>
        [TestMethod]
        public async Task OData_SkipBeyondEnd_IsEmpty()
        {
            var json = await GetODataAsync("odata/Customers?$skip=10&$top=2");

            ODataFeedReader.ReadValueCount(json).Should().Be(0);
        }

        /// <summary>
        /// Restier $skip+$top returns the second page.
        /// </summary>
        [TestMethod]
        public async Task OData_SkipTop_ReturnsSecondPage()
        {
            var json = await GetODataAsync("odata/Customers?$orderby=Id&$skip=2&$top=2");

            ODataFeedReader.ReadCompanyNames(json).Should().Equal("Northwind", "AdventureWorks");
        }

        /// <summary>
        /// Restier $top=1 returns Contoso.
        /// </summary>
        [TestMethod]
        public async Task OData_Top_ReturnsFirstPage()
        {
            var json = await GetODataAsync("odata/Customers?$orderby=Id&$top=1");

            ODataFeedReader.ReadCompanyNames(json).Should().Equal("Contoso");
        }

        /// <summary>
        /// Unpaged Restier and MCP without top return the same five rows.
        /// </summary>
        [TestMethod]
        public async Task ODataAndMcp_Unpaged_BothReturnAllFive()
        {
            var odata = await GetODataAsync("odata/Customers?$orderby=Id");
            var mcp = await QueryMcpAsync(new Dictionary<string, JsonElement>
            {
                ["orderby"] = JsonSerializer.SerializeToElement("Id")
            });

            ODataFeedReader.ReadCompanyNames(odata).Should().Equal("Contoso", "Fabrikam", "Northwind", "AdventureWorks", "Wide World");
            ODataFeedReader.ReadCompanyNames(mcp).Should().Equal(ODataFeedReader.ReadCompanyNames(odata));
        }

        /// <summary>
        /// The same count+skip+top+orderby on Restier and MCP produce the same page.
        /// </summary>
        [TestMethod]
        public async Task ODataAndMcp_CountSkipTop_Match()
        {
            var odata = await GetODataAsync("odata/Customers?$count=true&$orderby=Id&$skip=1&$top=2");
            var mcp = await QueryMcpAsync(new Dictionary<string, JsonElement>
            {
                ["count"] = JsonSerializer.SerializeToElement(true),
                ["orderby"] = JsonSerializer.SerializeToElement("Id"),
                ["skip"] = JsonSerializer.SerializeToElement(1),
                ["top"] = JsonSerializer.SerializeToElement(2)
            });

            ODataFeedReader.ReadCount(mcp).Should().Be(ODataFeedReader.ReadCount(odata)).And.Be(5);
            ODataFeedReader.ReadCompanyNames(mcp).Should().Equal(ODataFeedReader.ReadCompanyNames(odata));
            ODataFeedReader.ReadCompanyNames(mcp).Should().Equal("Fabrikam", "Northwind");
        }

        /// <summary>
        /// Filter+skip+top+count match on Restier and MCP.
        /// </summary>
        [TestMethod]
        public async Task ODataAndMcp_FilterSkipTop_Match()
        {
            var odata = await GetODataAsync("odata/Customers?$filter=Id gt 1&$orderby=Id&$skip=1&$top=2&$count=true");
            var mcp = await QueryMcpAsync(new Dictionary<string, JsonElement>
            {
                ["count"] = JsonSerializer.SerializeToElement(true),
                ["filter"] = JsonSerializer.SerializeToElement("Id gt 1"),
                ["orderby"] = JsonSerializer.SerializeToElement("Id"),
                ["skip"] = JsonSerializer.SerializeToElement(1),
                ["top"] = JsonSerializer.SerializeToElement(2)
            });

            ODataFeedReader.ReadCount(mcp).Should().Be(ODataFeedReader.ReadCount(odata)).And.Be(4);
            ODataFeedReader.ReadCompanyNames(mcp).Should().Equal(ODataFeedReader.ReadCompanyNames(odata));
            ODataFeedReader.ReadCompanyNames(mcp).Should().Equal("Northwind", "AdventureWorks");
        }

        /// <summary>
        /// Skip+top pages match on Restier and MCP.
        /// </summary>
        [TestMethod]
        public async Task ODataAndMcp_SkipTop_Match()
        {
            var odata = await GetODataAsync("odata/Customers?$orderby=Id&$skip=2&$top=2");
            var mcp = await QueryMcpAsync(new Dictionary<string, JsonElement>
            {
                ["orderby"] = JsonSerializer.SerializeToElement("Id"),
                ["skip"] = JsonSerializer.SerializeToElement(2),
                ["top"] = JsonSerializer.SerializeToElement(2)
            });

            ODataFeedReader.ReadKeys(mcp).Should().Equal(ODataFeedReader.ReadKeys(odata));
            ODataFeedReader.ReadCompanyNames(mcp).Should().Equal("Northwind", "AdventureWorks");
        }

        /// <summary>
        /// $top+$orderby match on Restier and MCP.
        /// </summary>
        [TestMethod]
        public async Task ODataAndMcp_TopOrderBy_Match()
        {
            var odata = await GetODataAsync("odata/Customers?$orderby=Id&$top=1");
            var mcp = await QueryMcpAsync(new Dictionary<string, JsonElement>
            {
                ["orderby"] = JsonSerializer.SerializeToElement("Id"),
                ["top"] = JsonSerializer.SerializeToElement(1)
            });

            ODataFeedReader.ReadCompanyNames(mcp).Should().Equal(ODataFeedReader.ReadCompanyNames(odata));
            ODataFeedReader.ReadCompanyNames(mcp).Should().Equal("Contoso");
        }

        #endregion

        #region Internal Methods

        /// <summary>
        /// GETs a Restier URL and returns the JSON body.
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
        /// Queries Customers through <c>odata_query</c>.
        /// </summary>
        /// <param name="arguments">Query arguments.</param>
        /// <returns>
        /// Structured JSON.
        /// </returns>
        internal async Task<string> QueryMcpAsync(Dictionary<string, JsonElement> arguments)
        {
            arguments["entitySet"] = JsonSerializer.SerializeToElement("Customers");

            return await InvokeAsync("odata_query", arguments);
        }

        /// <summary>
        /// Gets the MCP runtime for the Restier odata prefix.
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
