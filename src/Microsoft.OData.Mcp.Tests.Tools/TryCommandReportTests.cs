// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using System.IO;
using System.Threading.Tasks;
using FluentAssertions;
using McMaster.Extensions.CommandLineUtils;
using Microsoft.OData.Mcp.Authentication.Outbound;
using Microsoft.OData.Mcp.Tests.Shared;
using Microsoft.OData.Mcp.Tools.Commands;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.OData.Mcp.Tests.Tools
{

    /// <summary>
    /// <c>odata-mcp try</c> end to end against the live services, plus its flag binding.
    /// </summary>
    [TestClass]
    public class TryCommandReportTests
    {

        #region Public Methods

        /// <summary>
        /// An API key without a header name is rejected before any request is made.
        /// </summary>
        [TestMethod]
        public async Task TryCommand_ApiKeyWithoutHeader_ReturnsOne()
        {
            var exit = await new TryCommand
            {
                ApiKey = "key",
                Output = new StringWriter(),
                Url = LiveOData.Northwind
            }.OnExecuteAsync();

            exit.Should().Be(1);
        }

        /// <summary>
        /// A pasted $metadata URL is trimmed and the report shows the service root.
        /// </summary>
        [TestMethod]
        public async Task TryCommand_MetadataUrl_ProbesServiceRoot()
        {
            var output = new StringWriter();

            var exit = await new TryCommand
            {
                Output = output,
                Url = $"{LiveOData.Northwind}/$metadata"
            }.OnExecuteAsync();

            exit.Should().Be(0);
            output.ToString().Should().StartWith($"Probing {LiveOData.Northwind}/ anonymously");
        }

        /// <summary>
        /// Northwind is open, so the report shows four passing steps and exit code 0.
        /// </summary>
        [TestMethod]
        public async Task TryCommand_Northwind_ReportsReadyAndReturnsZero()
        {
            var output = new StringWriter();

            var exit = await new TryCommand
            {
                Output = output,
                Url = LiveOData.Northwind
            }.OnExecuteAsync();

            exit.Should().Be(0);
            var report = output.ToString();
            report.Should().Contain("[1/4] Metadata  ok    $metadata -> 200; 26 entity sets");
            report.Should().Contain("[2/4] Data      ok    Categories?$top=1 -> 200 application/json");
            report.Should().Contain("[3/4] Security  none  metadata and data both answered");
            report.Should().Contain("[4/4] Results   ok    1 Category row; all 4 properties match the model");
            report.Should().Contain($"Verdict: ready. Next: odata-mcp start {LiveOData.Northwind}/");
            report.Should().NotContain("credentials you passed", "an open service never needs the second pass");
        }

        /// <summary>
        /// Flags bind the same way they do on <c>start</c>.
        /// </summary>
        [TestMethod]
        public void TryCommand_ParsesFlags_BuildsOptions()
        {
            var app = new CommandLineApplication<TryCommand>();
            app.Conventions.UseDefaultConventions();
            app.Parse(
                "https://example.com/odata",
                "--client-id", "cid",
                "--scopes", "read write",
                "--grant", "device_code",
                "--auth-timeout", "45");

            var options = app.Model.BuildOptions();

            options.ClientId.Should().Be("cid");
            options.Scopes.Should().BeEquivalentTo(["read", "write"], o => o.WithStrictOrdering());
            options.Grant.Should().Be(OutboundGrantKind.DeviceCode);
            options.AuthTimeout.Should().Be(TimeSpan.FromSeconds(45));
            TryCommand.HasAnyCredential(options).Should().BeTrue();
            TryCommand.HasAnyCredential(new OutboundOAuthOptions()).Should().BeFalse();
        }

        /// <summary>
        /// With a token supplied, an open service still reports ready from the anonymous pass alone.
        /// </summary>
        [TestMethod]
        public async Task TryCommand_AuthTokenOnOpenService_ReturnsZero()
        {
            var output = new StringWriter();

            var exit = await new TryCommand
            {
                AuthToken = "token",
                Output = output,
                Url = LiveOData.TripPin
            }.OnExecuteAsync();

            exit.Should().Be(0);
            output.ToString().Should().Contain("1 Person row");
        }

        /// <summary>
        /// A root that is not OData exits 1 with an unreachable or unreadable verdict, never an exception.
        /// </summary>
        [TestMethod]
        public async Task TryCommand_NotOData_ReturnsOne()
        {
            var output = new StringWriter();

            var exit = await new TryCommand
            {
                Output = output,
                Url = "https://www.example.com/"
            }.OnExecuteAsync();

            exit.Should().Be(1);
            output.ToString().Should().MatchRegex("Verdict: (unreachable|unreadable)");
        }

        #endregion

    }

}
