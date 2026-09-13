// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.OData.Mcp.Tests.AspNetCore.Fixtures;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.OData.Mcp.Tests.AspNetCore.Tools
{

    /// <summary>
    /// AspNetCore hosts must not advertise or execute <c>shutdown_server</c>.
    /// </summary>
    [TestClass]
    public class ShutdownAbsentHostTests : ConventionRichHost
    {

        #region Public Methods

        /// <summary>
        /// Calling <c>shutdown_server</c> on the AspNetCore runtime is an unknown tool.
        /// </summary>
        [TestMethod]
        public async Task InvokeAsync_ShutdownServer_IsUnknownTool()
        {
            var result = await InvokeAsync("shutdown_server");

            result.IsError.Should().BeTrue();
            result.Text.Should().Be("Unknown tool 'shutdown_server'.");
        }

        /// <summary>
        /// <c>tools/list</c> names omit <c>shutdown_server</c>.
        /// </summary>
        [TestMethod]
        public void ToolsList_OmitsShutdownServer()
        {
            var names = Session().Catalog.Tools.Select(tool => tool.Name).ToList();

            names.Should().Contain("odata_query");
            names.Should().NotContain("shutdown_server");
        }

        #endregion

    }

}
