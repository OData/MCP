// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
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
    /// Tests for <see cref="TestCommand"/>'s outbound OAuth flag parsing and <c>BuildOptions</c>.
    /// </summary>
    [TestClass]
    public class TestCommandOptionsTests
    {

        #region Public Methods

        /// <summary>
        /// <c>--api-key</c> without <c>--api-key-header</c> fails validation before any network call.
        /// </summary>
        [TestMethod]
        public async Task TestCommand_ApiKeyWithoutHeader_ReturnsOne()
        {
            var exit = await new TestCommand
            {
                ApiKey = "key",
                Url = LiveOData.Northwind
            }.OnExecuteAsync();

            exit.Should().Be(1);
        }

        /// <summary>
        /// Parsing a representative subset of outbound OAuth flags populates <c>BuildOptions</c>.
        /// </summary>
        [TestMethod]
        public void TestCommand_ParsesFlags_BuildsOptions()
        {
            var app = new CommandLineApplication<TestCommand>();
            app.Conventions.UseDefaultConventions();
            app.Parse(
                "https://example.com/odata",
                "--client-id", "cid",
                "--scopes", "read write",
                "--grant", "device_code",
                "--auth-timeout", "45");
            var command = app.Model;

            var options = command.BuildOptions();

            options.ClientId.Should().Be("cid");
            options.Scopes.Should().BeEquivalentTo(["read", "write"], o => o.WithStrictOrdering());
            options.Grant.Should().Be(OutboundGrantKind.DeviceCode);
            options.AuthTimeout.Should().Be(TimeSpan.FromSeconds(45));
        }

        #endregion

    }

}
