// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System.Threading.Tasks;
using CloudNimble.Breakdance.AspNetCore;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.OData;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.OData.Mcp.Core.Catalog;
using Microsoft.OData.Mcp.Tests.AspNetCore.Fixtures;
using Microsoft.OData.Mcp.Tests.Shared.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ModelContextProtocol.Server;

namespace Microsoft.OData.Mcp.Tests.AspNetCore.Hosting
{

    /// <summary>
    /// <c>initialize.instructions</c> on the ASP.NET Core host: the developer preface first, then the shared default.
    /// </summary>
    [TestClass]
    public class ServerInstructionsHostTests : AspNetCoreBreakdanceTestBase
    {

        #region Test Lifecycle

        /// <summary>
        /// Builds a host with an instructions preface.
        /// </summary>
        [TestInitialize]
        public void Setup()
        {
            TestHostBuilder.ConfigureServices((_, services) =>
            {
                services
                    .AddControllers()
                    .AddOData(options => options.AddRouteComponents("odata", TestModels.GetSimpleModel()));
                services.AddODataMcp(options => options.Catalog.InstructionsPreface = "  Contoso sales data. Amounts are USD.  ");
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
        /// Tears down the test host.
        /// </summary>
        [TestCleanup]
        public void TearDown()
        {
            TestTearDown();
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// The MCP server options carry the composed instructions: trimmed preface, blank line, default.
        /// </summary>
        [TestMethod]
        public void ServerInstructions_PrefaceThenDefault()
        {
            var mcp = TestServer.Services.GetRequiredService<IOptions<McpServerOptions>>().Value;

            mcp.ServerInstructions.Should().Be($"Contoso sales data. Amounts are USD.\n\n{ODataMcpInstructions.Default}");
        }

        /// <summary>
        /// The JSON-RPC <c>initialize</c> response advertises the composed instructions.
        /// </summary>
        [TestMethod]
        public async Task Initialize_ResponseCarriesInstructions()
        {
            using var client = TestServer.CreateClient();
            McpJsonRpc.AcceptMcp(client);
            using var response = await client.PostAsync("/odata/mcp", McpJsonRpc.Content(McpJsonRpc.InitializePayload()));
            var body = await McpJsonRpc.ReadBodyAsync(response);

            response.IsSuccessStatusCode.Should().BeTrue(body);
            body.Should().Contain("\"instructions\"");
            body.Should().Contain("Contoso sales data. Amounts are USD.");
            body.Should().Contain("Do not read $metadata to explore.");
            body.IndexOf("Contoso sales data", System.StringComparison.Ordinal).Should().BeLessThan(body.IndexOf("Query options have no $ prefix", System.StringComparison.Ordinal));
        }

        #endregion

    }

}
