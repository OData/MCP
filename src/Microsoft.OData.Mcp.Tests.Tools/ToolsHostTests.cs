// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.OData.Mcp.Authentication.Outbound;
using Microsoft.OData.Mcp.Tests.Shared;
using Microsoft.OData.Mcp.Tools.Commands;
using Microsoft.OData.Mcp.Tools.Hosting;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.OData.Mcp.Tests.Tools
{

    /// <summary>
    /// Tests for the local Tools host, shutdown tool, and test command.
    /// </summary>
    [TestClass]
    public class ToolsHostTests
    {

        #region Public Methods

        /// <summary>
        /// <c>shutdown_server</c> with delay 0 cancels the host immediately.
        /// </summary>
        [TestMethod]
        public async Task ShutdownServerTool_DelayZero_CancelsHost()
        {
            using var cts = new CancellationTokenSource();
            var tool = new ShutdownServerTool(cts);

            await tool.InvokeAsync("test", 0, CancellationToken.None);

            cts.IsCancellationRequested.Should().BeTrue();
        }

        /// <summary>
        /// A Northwind tools host advertises odata_query and shutdown_server.
        /// </summary>
        [TestMethod]
        public async Task ToolsMcpHost_Northwind_IncludesQueryAndShutdown()
        {
            using var host = await ToolsMcpHost.CreateAsync(LiveOData.Northwind, new OutboundOAuthOptions(), includeStdioMcp: false, verbose: false, lifetime: null, CancellationToken.None);
            var names = host.Catalog.Tools.Select(tool => tool.Name).ToList();

            names.Should().Contain("odata_query");
            names.Should().NotContain("shutdown_server");
            ToolsMcpHost.CreateShutdownTool().Name.Should().Be("shutdown_server");
        }

        /// <summary>
        /// The test command prints the entity set count for live Northwind.
        /// </summary>
        [TestMethod]
        public async Task TestCommand_Northwind_ReturnsZero()
        {
            var command = new TestCommand
            {
                Url = LiveOData.Northwind
            };

            var exit = await command.OnExecuteAsync();

            exit.Should().Be(0);
        }


        /// <summary>
        /// A service root is kept as-is, with exactly one trailing slash.
        /// </summary>
        [TestMethod]
        public void NormalizeServiceRoot_ServiceRoot_AddsTrailingSlash()
        {
            ToolsMcpHost.NormalizeServiceRoot("https://services.odata.org/V4/Northwind/Northwind.svc").ToString().Should().Be("https://services.odata.org/V4/Northwind/Northwind.svc/");
            ToolsMcpHost.NormalizeServiceRoot("https://services.odata.org/V4/Northwind/Northwind.svc/").ToString().Should().Be("https://services.odata.org/V4/Northwind/Northwind.svc/");
        }

        /// <summary>
        /// A pasted $metadata URL is trimmed to its service root, including any query or fragment after it.
        /// </summary>
        [TestMethod]
        public void NormalizeServiceRoot_MetadataUrl_TrimsToServiceRoot()
        {
            ToolsMcpHost.NormalizeServiceRoot("https://services.odata.org/V4/Northwind/Northwind.svc/$metadata").ToString().Should().Be("https://services.odata.org/V4/Northwind/Northwind.svc/");
            ToolsMcpHost.NormalizeServiceRoot("https://services.odata.org/V4/Northwind/Northwind.svc/$metadata/").ToString().Should().Be("https://services.odata.org/V4/Northwind/Northwind.svc/");
            ToolsMcpHost.NormalizeServiceRoot("https://api.example.com/odata/$METADATA?$format=xml#x").ToString().Should().Be("https://api.example.com/odata/");
            ToolsMcpHost.NormalizeServiceRoot("https://api.example.com/$metadata").ToString().Should().Be("https://api.example.com/");
        }

        /// <summary>
        /// $metadata in the middle of a path is part of the service root and is left alone.
        /// </summary>
        [TestMethod]
        public void NormalizeServiceRoot_MetadataNotLastSegment_IsKept()
        {
            ToolsMcpHost.NormalizeServiceRoot("https://api.example.com/$metadata/v2").ToString().Should().Be("https://api.example.com/$metadata/v2/");
        }
        #endregion

    }

}
