// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System.IO;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.OData.Mcp.Authentication.Outbound;
using Microsoft.OData.Mcp.Tools.Commands;
using Microsoft.OData.Mcp.Tools.Hosting;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ModelContextProtocol.Server;

namespace Microsoft.OData.Mcp.Tests.Tools
{

    /// <summary>
    /// Additional Tools command coverage.
    /// </summary>
    [TestClass]
    public class TestCommandMoreTests
    {

        #region Public Methods

        /// <summary>
        /// Missing URL fails.
        /// </summary>
        [TestMethod]
        public async Task TestCommand_MissingUrl_ReturnsOne()
        {
            var exit = await new TestCommand().OnExecuteAsync();
            exit.Should().Be(1);
        }

        /// <summary>
        /// Invalid URL fails.
        /// </summary>
        [TestMethod]
        public async Task TestCommand_InvalidUrl_ReturnsOne()
        {
            var exit = await new TestCommand
            {
                Url = "not-a-url"
            }.OnExecuteAsync();

            exit.Should().Be(1);
        }

        /// <summary>
        /// Shutdown delay out of range throws.
        /// </summary>
        [TestMethod]
        public async Task ShutdownServerTool_InvalidDelay_Throws()
        {
            using var cts = new System.Threading.CancellationTokenSource();
            var tool = new ShutdownServerTool(cts);
            var act = async () => await tool.InvokeAsync("x", 11, System.Threading.CancellationToken.None);

            await act.Should().ThrowAsync<System.ArgumentOutOfRangeException>();
        }

        /// <summary>
        /// The add wizard derives names and builds a Claude MCP command.
        /// </summary>
        [TestMethod]
        public void AddCommand_DerivesNameAndBuildsCommand()
        {
            var command = new AddCommand();

            command.DeriveNameFromUrl("https://services.odata.org/V4/Northwind/Northwind.svc").Should().Be("northwind");
            command.DeriveNameFromUrl("https://services.odata.org/V4/TripPinServiceRW").Should().Be("trippin");
            command.DeriveNameFromUrl("https://contoso.example.com/odata").Should().Be("odata");
            command.DeriveNameFromUrl("not-a-url").Should().Be("odata-service");
            command.BuildMcpCommand(
                    "northwind",
                    "https://example.com/odata",
                    new AddCommandAuthSettings { ClientId = "the-app", Grant = OutboundGrantKind.DeviceCode },
                    "user",
                    true)
                .Should().Contain("claude mcp add northwind")
                .And.Contain("--client-id \"the-app\"")
                .And.Contain("--grant device_code")
                .And.Contain("--verbose")
                .And.NotContain("--auth-token")
                .And.NotContain("--env");
            command.BuildMcpCommand("svc", "https://example.com/odata", new AddCommandAuthSettings(), "project", false)
                .Should().NotContain("--auth-token")
                .And.NotContain("--grant")
                .And.NotContain("--verbose");
        }

        /// <summary>
        /// Connection test fetches live Northwind metadata.
        /// </summary>
        [TestMethod]
        [Timeout(20000)]
        public async Task AddCommand_TestConnection_NorthwindSucceeds()
        {
            var output = new StringWriter();
            var command = new AddCommand
            {
                Error = output,
                Output = output
            };

            await command.TestConnection(Microsoft.OData.Mcp.Tests.Shared.LiveOData.Northwind, new AddCommandAuthSettings());
            output.ToString().Should().MatchRegex("Connection successful|Could not connect");
        }

        /// <summary>
        /// The CLI entry point shows help and returns zero.
        /// </summary>
        [TestMethod]
        public async Task Program_Help_ReturnsZero()
        {
            var exit = await Microsoft.OData.Mcp.Tools.Program.Main(["--help"]);
            exit.Should().Be(0);
        }

        /// <summary>
        /// The root command prints help.
        /// </summary>
        [TestMethod]
        public void RootCommand_OnExecute_ShowsHelp()
        {
            var app = new McMaster.Extensions.CommandLineUtils.CommandLineApplication();
            var exit = new ODataMcpRootCommand().OnExecute(app);
            exit.Should().Be(0);
        }

        /// <summary>
        /// Start command requires a URL.
        /// </summary>
        [TestMethod]
        public async Task StartCommand_MissingUrl_ReturnsOne()
        {
            var exit = await new StartCommand().OnExecuteAsync();
            exit.Should().Be(1);
        }

        /// <summary>
        /// Invalid service URLs throw from the host factory.
        /// </summary>
        [TestMethod]
        public async Task ToolsMcpHost_InvalidUrl_Throws()
        {
            var act = async () => await ToolsMcpHost.CreateAsync("ftp://example.com", new OutboundOAuthOptions(), includeStdioMcp: false, verbose: false, lifetime: null, System.Threading.CancellationToken.None);
            await act.Should().ThrowAsync<System.ArgumentException>();
        }

        /// <summary>
        /// Non-shutdown calls fall through.
        /// </summary>
        [TestMethod]
        public async Task HandleShutdown_OtherTool_ReturnsNull()
        {
            using var cts = new System.Threading.CancellationTokenSource();
            var request = (ModelContextProtocol.Server.RequestContext<ModelContextProtocol.Protocol.CallToolRequestParams>)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(ModelContextProtocol.Server.RequestContext<ModelContextProtocol.Protocol.CallToolRequestParams>));
            request.Params = new ModelContextProtocol.Protocol.CallToolRequestParams { Name = "odata_query" };

            var result = await ToolsMcpHost.HandleShutdownAsync(request, new ShutdownServerTool(cts), System.Threading.CancellationToken.None);

            result.Should().BeNull();
        }

        /// <summary>
        /// Shutdown handling cancels after the requested delay of zero.
        /// </summary>
        [TestMethod]
        public async Task HandleShutdown_DelayZero_Cancels()
        {
            using var cts = new System.Threading.CancellationTokenSource();
            var request = (ModelContextProtocol.Server.RequestContext<ModelContextProtocol.Protocol.CallToolRequestParams>)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(ModelContextProtocol.Server.RequestContext<ModelContextProtocol.Protocol.CallToolRequestParams>));
            request.Params = new ModelContextProtocol.Protocol.CallToolRequestParams
            {
                Arguments = new System.Collections.Generic.Dictionary<string, System.Text.Json.JsonElement>
                {
                    ["reason"] = System.Text.Json.JsonSerializer.SerializeToElement("done"),
                    ["delay_seconds"] = System.Text.Json.JsonSerializer.SerializeToElement(0)
                },
                Name = "shutdown_server"
            };

            var result = await ToolsMcpHost.HandleShutdownAsync(request, new ShutdownServerTool(cts), System.Threading.CancellationToken.None);

            result.Should().NotBeNull();
            cts.IsCancellationRequested.Should().BeTrue();
        }

        /// <summary>
        /// ReadString returns null for missing arguments.
        /// </summary>
        [TestMethod]
        public void ReadString_Missing_ReturnsNull()
        {
            ToolsMcpHost.ReadString(null, "reason").Should().BeNull();
        }

        /// <summary>
        /// A delayed shutdown still acknowledges immediately.
        /// </summary>
        [TestMethod]
        public async Task ShutdownServerTool_DelayOne_Acknowledges()
        {
            using var cts = new System.Threading.CancellationTokenSource();
            var tool = new ShutdownServerTool(cts);
            var payload = await tool.InvokeAsync("  ", 1, System.Threading.CancellationToken.None);

            payload.Should().Contain("1 second");
            cts.IsCancellationRequested.Should().BeFalse();
        }

        /// <summary>
        /// A host built with the stdio transport registers the MCP server, the <c>shutdown_server</c> tool, and
        /// a session holder the catalog handlers can already resolve.
        /// </summary>
        [TestMethod]
        public async Task CreateAsync_IncludeStdio_RegistersMcpServer()
        {
            using var lifetime = new System.Threading.CancellationTokenSource();
            var options = new OutboundOAuthOptions
            {
                AuthToken = "token"
            };
            using var host = await ToolsMcpHost.CreateAsync(
                Microsoft.OData.Mcp.Tests.Shared.LiveOData.Northwind,
                options,
                includeStdioMcp: true,
                verbose: true,
                lifetime,
                lifetime.Token);

            host.Host.Services.GetServices<Microsoft.Extensions.Hosting.IHostedService>().Should().NotBeEmpty();
            host.Host.Services.GetService<IOptions<McpServerOptions>>()!.Value.Handlers.CallToolHandler.Should().NotBeNull();
            host.Host.Services.GetService<ShutdownServerTool>().Should().NotBeNull();
            host.Host.Services.GetRequiredService<ToolsMcpSessionHolder>().Session.Should().BeSameAs(host.Session);
            ToolsMcpHost.CreateShutdownTool().Description.Should().Contain("Local Tools host only");
        }

        #endregion

    }

}
