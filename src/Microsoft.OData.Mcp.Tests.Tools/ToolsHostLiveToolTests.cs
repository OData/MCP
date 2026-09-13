// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.OData.Mcp.Authentication.Outbound;
using Microsoft.OData.Mcp.Core.Catalog;
using Microsoft.OData.Mcp.Tests.Shared;
using Microsoft.OData.Mcp.Tools.Hosting;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ModelContextProtocol.Server;

namespace Microsoft.OData.Mcp.Tests.Tools
{

    /// <summary>
    /// Live Tools-host catalog and runtime tests against Northwind and TripPin.
    /// </summary>
    [TestClass]
    public class ToolsHostLiveToolTests
    {

        #region Public Methods

        /// <summary>
        /// The Northwind catalog lists generic OData tools and does not contain <c>shutdown_server</c>.
        /// </summary>
        [TestMethod]
        public async Task ToolsMcpHost_Northwind_CatalogOmitsShutdownServer()
        {
            using var host = await ToolsMcpHost.CreateAsync(LiveOData.Northwind, new OutboundOAuthOptions(), includeStdioMcp: false, verbose: false, lifetime: null, CancellationToken.None);
            var names = host.Catalog.Tools.Select(tool => tool.Name).ToList();

            names.Should().Contain("odata_query");
            names.Should().Contain("odata_list_entity_sets");
            names.Should().Contain("odata_describe_type");
            names.Should().Contain("odata_get");
            names.Should().Contain("odata_navigate");
            names.Should().NotContain("shutdown_server");
        }

        /// <summary>
        /// The Tools host advertises the shared default server instructions, configured before the host is built.
        /// </summary>
        [TestMethod]
        public async Task ToolsMcpHost_ServerInstructions_AreTheSharedDefault()
        {
            using var host = await ToolsMcpHost.CreateAsync(LiveOData.Northwind, new OutboundOAuthOptions(), includeStdioMcp: false, verbose: false, lifetime: null, CancellationToken.None);
            var mcp = host.Host.Services.GetRequiredService<IOptions<McpServerOptions>>().Value;

            mcp.ServerInstructions.Should().Be(ODataMcpInstructions.Default);
            mcp.ServerInstructions.Should().Contain("Do not read the $metadata resource to explore");
            mcp.ServerInstructions.Should().Contain("parameters");
        }

        /// <summary>
        /// <c>shutdown_server</c> is advertised only through the extra-tool list.
        /// </summary>
        [TestMethod]
        public void ToolsMcpHost_Northwind_CreateShutdownTool_IsExtra()
        {
            var extra = ToolsMcpHost.CreateShutdownTool();

            extra.Name.Should().Be("shutdown_server");
            extra.Title.Should().Be("Shut down MCP server");
            extra.Annotations.Should().NotBeNull();
            extra.Annotations!.DestructiveHint.Should().BeTrue();
            extra.Annotations.IdempotentHint.Should().BeFalse();
            extra.Annotations.OpenWorldHint.Should().BeFalse();
            extra.Annotations.ReadOnlyHint.Should().BeFalse();
            var schema = extra.InputSchema.GetRawText();
            schema.Should().Contain("delay_seconds");
            schema.Should().Contain("reason");
            schema.Should().NotContain("$");
        }

        /// <summary>
        /// <c>odata_describe_type</c> describes Northwind Customer through the tools host runtime.
        /// </summary>
        [TestMethod]
        public async Task ToolsMcpHost_Northwind_OdataDescribeType_Customer()
        {
            using var host = await ToolsMcpHost.CreateAsync(LiveOData.Northwind, new OutboundOAuthOptions(), includeStdioMcp: false, verbose: false, lifetime: null, CancellationToken.None);
            var result = await host.Session.Runtime.InvokeAsync(
                "odata_describe_type",
                new Dictionary<string, JsonElement>
                {
                    ["name"] = JsonSerializer.SerializeToElement("Customer")
                },
                CancellationToken.None);

            result.IsError.Should().BeFalse(result.Text);
            result.StructuredContent.Should().BeNull("text is the default describe representation");
            result.Text.Should().StartWith("Customer  (set: Customers, key: CustomerID)");
            result.Text.Should().Contain("CompanyName?: string");
        }

        /// <summary>
        /// <c>odata_get</c> returns Northwind customer ALFKI through the tools host runtime.
        /// </summary>
        [TestMethod]
        public async Task ToolsMcpHost_Northwind_OdataGet_Alfki()
        {
            using var client = new HttpClient();
            client.DefaultRequestHeaders.Accept.ParseAdd("application/json");
            var odata = await client.GetStringAsync($"{LiveOData.Northwind}/Customers('ALFKI')");
            odata.Should().Contain("Alfreds Futterkiste");

            using var host = await ToolsMcpHost.CreateAsync(LiveOData.Northwind, new OutboundOAuthOptions(), includeStdioMcp: false, verbose: false, lifetime: null, CancellationToken.None);
            var result = await host.Session.Runtime.InvokeAsync(
                "odata_get",
                new Dictionary<string, JsonElement>
                {
                    ["entitySet"] = JsonSerializer.SerializeToElement("Customers"),
                    ["key"] = JsonSerializer.SerializeToElement("ALFKI")
                },
                CancellationToken.None);

            result.IsError.Should().BeFalse(result.Text);
            result.StructuredContent.Should().NotBeNullOrWhiteSpace();
            result.StructuredContent.Should().Contain("ALFKI");
            result.StructuredContent.Should().Contain("Alfreds Futterkiste");
        }

        /// <summary>
        /// <c>odata_list_entity_sets</c> lists Northwind sets through the tools host runtime.
        /// </summary>
        [TestMethod]
        public async Task ToolsMcpHost_Northwind_OdataListEntitySets()
        {
            using var host = await ToolsMcpHost.CreateAsync(LiveOData.Northwind, new OutboundOAuthOptions(), includeStdioMcp: false, verbose: false, lifetime: null, CancellationToken.None);
            var result = await host.Session.Runtime.InvokeAsync("odata_list_entity_sets", null, CancellationToken.None);

            result.IsError.Should().BeFalse(result.Text);
            result.StructuredContent.Should().NotBeNullOrWhiteSpace();
            result.StructuredContent.Should().Contain("Customers");
            result.StructuredContent.Should().Contain("Products");
            result.StructuredContent.Should().Contain("Categories");
        }

        /// <summary>
        /// <c>odata_navigate</c> follows Products(1)/Category through the tools host runtime.
        /// </summary>
        [TestMethod]
        public async Task ToolsMcpHost_Northwind_OdataNavigate_ProductCategory()
        {
            using var client = new HttpClient();
            client.DefaultRequestHeaders.Accept.ParseAdd("application/json");
            var odata = await client.GetStringAsync($"{LiveOData.Northwind}/Products(1)/Category");
            odata.Should().Contain("Beverages");

            using var host = await ToolsMcpHost.CreateAsync(LiveOData.Northwind, new OutboundOAuthOptions(), includeStdioMcp: false, verbose: false, lifetime: null, CancellationToken.None);
            var result = await host.Session.Runtime.InvokeAsync(
                "odata_navigate",
                new Dictionary<string, JsonElement>
                {
                    ["entitySet"] = JsonSerializer.SerializeToElement("Products"),
                    ["key"] = JsonSerializer.SerializeToElement("1"),
                    ["navigation"] = JsonSerializer.SerializeToElement("Category")
                },
                CancellationToken.None);

            result.IsError.Should().BeFalse(result.Text);
            result.StructuredContent.Should().NotBeNullOrWhiteSpace();
            result.StructuredContent.Should().Contain("Beverages");
        }

        /// <summary>
        /// <c>odata_query</c> against live Northwind Products returns a product through the tools host runtime.
        /// </summary>
        [TestMethod]
        public async Task ToolsMcpHost_Northwind_OdataQuery_Products()
        {
            using var host = await ToolsMcpHost.CreateAsync(LiveOData.Northwind, new OutboundOAuthOptions(), includeStdioMcp: false, verbose: false, lifetime: null, CancellationToken.None);
            var result = await host.Session.Runtime.InvokeAsync(
                "odata_query",
                new Dictionary<string, JsonElement>
                {
                    ["entitySet"] = JsonSerializer.SerializeToElement("Products"),
                    ["top"] = JsonSerializer.SerializeToElement(1)
                },
                CancellationToken.None);

            result.IsError.Should().BeFalse(result.Text);
            result.StructuredContent.Should().NotBeNullOrWhiteSpace();
            result.StructuredContent.Should().Contain("Product");
        }

        /// <summary>
        /// <see cref="ToolsMcpHost.CreateAsync"/> loads TripPin metadata from the published URL as-is.
        /// </summary>
        [TestMethod]
        public async Task ToolsMcpHost_TripPin_CreateAsync_LoadsCatalog()
        {
            using var host = await ToolsMcpHost.CreateAsync(LiveOData.TripPin, new OutboundOAuthOptions(), includeStdioMcp: false, verbose: false, lifetime: null, CancellationToken.None);
            var names = host.Catalog.Tools.Select(tool => tool.Name).ToList();

            names.Should().Contain("odata_query");
            names.Should().NotContain("shutdown_server");

            var listed = await host.Session.Runtime.InvokeAsync("odata_list_entity_sets", null, CancellationToken.None);
            listed.IsError.Should().BeFalse(listed.Text);
            listed.StructuredContent.Should().NotBeNullOrWhiteSpace();
            listed.StructuredContent.Should().Contain("People");
        }

        #endregion

    }

}
