// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.OData.Mcp.Core.Catalog;
using Microsoft.OData.Mcp.Tests.AspNetCore.Fixtures;
using Microsoft.OData.Mcp.Tests.Shared;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.OData.Mcp.Tests.AspNetCore.Tools
{


    /// <summary>
    /// <c>odata_call</c> on the operations-only model.
    /// </summary>
    [TestClass]
    public class OdataCallOperationsOnlyHostTests : OperationsOnlyToolHost
    {

        #region Public Methods

        /// <summary>
        /// Operations-only hosts support MostValuable, GetStatus, and Reset.
        /// </summary>
        [TestMethod]
        public async Task OdataCall_OData8_OperationsOnly_SameThreeCalls()
        {
            var most = await InvokeAsync("odata_call", ToolArguments.Of("name", "MostValuable"));
            most.IsError.Should().BeFalse(most.Text);
            most.StructuredContent.Should().Contain("42");
            var status = await InvokeAsync("odata_call", ToolArguments.Of("name", "GetStatus", "parameters", new { code = "open" }));
            status.IsError.Should().BeFalse(status.Text);
            var reset = await InvokeAsync("odata_call", ToolArguments.Of("name", "Reset"));
            reset.IsError.Should().BeFalse(reset.Text);
        }

        /// <summary>
        /// Operations-only hosts can call MostValuable with no entity sets.
        /// </summary>
        [TestMethod]
        public async Task OdataCall_OperationsOnly_CallWhenNoSets()
        {
            Session().Catalog.Tools.Where(tool => tool.EntitySetName is not null).Should().BeEmpty();
            var result = await InvokeAsync("odata_call", ToolArguments.Of("name", "MostValuable"));
            result.IsError.Should().BeFalse(result.Text);
            result.StructuredContent.Should().Contain("42");
        }

        #endregion

    }

}
