// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OData.Mcp.Core.Catalog;
using Microsoft.OData.Mcp.Core.Execution;
using Microsoft.OData.Mcp.Tests.Core.Parsing;
using Microsoft.OData.Mcp.Tests.Shared;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.OData.Mcp.Tests.Core.Catalog
{

    /// <summary>
    /// Live runtime tests for catalog tools.
    /// </summary>
    [TestClass]
    public class ODataToolRuntimeLiveTests
    {

        #region Public Methods

        /// <summary>
        /// Generic query against live Northwind Products returns a product.
        /// </summary>
        [TestMethod]
        public async Task InvokeAsync_OdataQueryProductsTop1_ReturnsProduct()
        {
            var runtime = await CreateNorthwindRuntimeAsync();
            var result = await runtime.InvokeAsync(
                "odata_query",
                new Dictionary<string, JsonElement>
                {
                    ["entitySet"] = JsonSerializer.SerializeToElement("Products"),
                    ["top"] = JsonSerializer.SerializeToElement(1)
                },
                CancellationToken.None);

            result.IsError.Should().BeFalse();
            result.StructuredContent.Should().NotBeNullOrWhiteSpace();
            result.StructuredContent.Should().Contain("Product");
        }

        /// <summary>
        /// Live Northwind $skip+$top and MCP skip+top return the same product page.
        /// </summary>
        [TestMethod]
        public async Task InvokeAsync_OdataQueryProducts_SkipAndTop_MatchHttp()
        {
            using var http = new HttpClient();
            http.DefaultRequestHeaders.Accept.ParseAdd("application/json");
            var odata = await http.GetStringAsync($"{LiveOData.Northwind}/Products?$orderby=ProductID&$skip=1&$top=1");
            var runtime = await CreateNorthwindRuntimeAsync();
            var result = await runtime.InvokeAsync(
                "odata_query",
                new Dictionary<string, JsonElement>
                {
                    ["entitySet"] = JsonSerializer.SerializeToElement("Products"),
                    ["orderby"] = JsonSerializer.SerializeToElement("ProductID"),
                    ["skip"] = JsonSerializer.SerializeToElement(1),
                    ["top"] = JsonSerializer.SerializeToElement(1)
                },
                CancellationToken.None);

            result.IsError.Should().BeFalse(result.Text);
            var mcpNames = ODataFeedReader.ReadStrings(result.StructuredContent!, "ProductName", "productName");
            var odataNames = ODataFeedReader.ReadStrings(odata, "ProductName", "productName");
            mcpNames.Should().Equal(odataNames);
            mcpNames.Should().HaveCount(1);
        }

        /// <summary>
        /// Live Northwind skip=0 and skip=1 pages are different.
        /// </summary>
        [TestMethod]
        public async Task InvokeAsync_OdataQueryProducts_SkipPagesDiffer()
        {
            var runtime = await CreateNorthwindRuntimeAsync();
            var first = await runtime.InvokeAsync(
                "odata_query",
                new Dictionary<string, JsonElement>
                {
                    ["entitySet"] = JsonSerializer.SerializeToElement("Products"),
                    ["orderby"] = JsonSerializer.SerializeToElement("ProductID"),
                    ["skip"] = JsonSerializer.SerializeToElement(0),
                    ["top"] = JsonSerializer.SerializeToElement(1)
                },
                CancellationToken.None);
            var second = await runtime.InvokeAsync(
                "odata_query",
                new Dictionary<string, JsonElement>
                {
                    ["entitySet"] = JsonSerializer.SerializeToElement("Products"),
                    ["orderby"] = JsonSerializer.SerializeToElement("ProductID"),
                    ["skip"] = JsonSerializer.SerializeToElement(1),
                    ["top"] = JsonSerializer.SerializeToElement(1)
                },
                CancellationToken.None);

            first.IsError.Should().BeFalse(first.Text);
            second.IsError.Should().BeFalse(second.Text);
            var firstName = ODataFeedReader.ReadStrings(first.StructuredContent!, "ProductName", "productName").Single();
            var secondName = ODataFeedReader.ReadStrings(second.StructuredContent!, "ProductName", "productName").Single();
            firstName.Should().NotBe(secondName);
        }

        /// <summary>
        /// List entity sets includes CSDL documentation when present.
        /// </summary>
        [TestMethod]
        public async Task InvokeAsync_ListEntitySets_IncludesDocumentation()
        {
            var model = new Microsoft.OData.Mcp.Core.Parsing.CsdlParser().ParseFromString(CsdlParserDocumentationTests.DocumentedCsdl);
            var catalog = new ODataMcpCatalog(model, new ODataMcpCatalogOptions());
            var runtime = new ODataToolRuntime(catalog, new UnusedODataExecutor());
            var result = await runtime.InvokeAsync("odata_list_entity_sets", null, CancellationToken.None);

            result.IsError.Should().BeFalse();
            result.StructuredContent.Should().Contain("People who travel.");
        }

        /// <summary>
        /// Describe type returns property documentation from CSDL.
        /// </summary>
        [TestMethod]
        public async Task InvokeAsync_DescribeType_ReturnsCsdlDocs()
        {
            var model = new Microsoft.OData.Mcp.Core.Parsing.CsdlParser().ParseFromString(CsdlParserDocumentationTests.DocumentedCsdl);
            var catalog = new ODataMcpCatalog(model, new ODataMcpCatalogOptions());
            var runtime = new ODataToolRuntime(catalog, new UnusedODataExecutor());
            var result = await runtime.InvokeAsync(
                "odata_describe_type",
                new Dictionary<string, JsonElement>
                {
                    ["name"] = JsonSerializer.SerializeToElement("People")
                },
                CancellationToken.None);

            result.IsError.Should().BeFalse();
            result.StructuredContent.Should().Contain("People who travel.");
            result.StructuredContent.Should().Contain("A person who travels.");
            result.StructuredContent.Should().Contain("Unique person name.");
            result.StructuredContent.Should().Contain("Other people this person knows.");
        }

        #endregion

        #region Internal Methods

        /// <summary>
        /// Creates a runtime bound to live Northwind.
        /// </summary>
        /// <returns>
        /// The runtime.
        /// </returns>
        internal static async Task<ODataToolRuntime> CreateNorthwindRuntimeAsync()
        {
            var model = await LiveMetadata.LoadNorthwindModelAsync();
            var catalog = new ODataMcpCatalog(model, new ODataMcpCatalogOptions());
            var services = new ServiceCollection();

            services.AddHttpClient("OData", client =>
            {
                client.BaseAddress = new Uri(LiveOData.Northwind.TrimEnd('/') + "/");
                client.DefaultRequestHeaders.Accept.ParseAdd("application/json");
            });

            var executor = new RemoteODataExecutor(services.BuildServiceProvider().GetRequiredService<IHttpClientFactory>());

            return new ODataToolRuntime(catalog, executor);
        }

        #endregion

    }

}
