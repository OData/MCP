// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.OData.Mcp.Tests.AspNetCore.Fixtures;
using Microsoft.OData.Mcp.Tests.Shared;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.OData.Mcp.Tests.AspNetCore.Tools
{

    /// <summary>
    /// <c>odata_navigate</c> cases that require a tiny <c>MaxResponseBytes</c>.
    /// </summary>
    [TestClass]
    public class OdataNavigateTinyResponseHostTests : TinyResponseRichHost
    {

        #region Public Methods

        /// <summary>
        /// An oversized navigation payload is <c>IsError</c> when OData succeeds.
        /// </summary>
        [TestMethod]
        public async Task OdataNavigate_MaxResponseBytesTiny_Friends()
        {
            var result = await InvokeAsync("odata_navigate", ToolArguments.Of("entitySet", "Customers", "key", "1", "navigation", "Orders"));
            result.IsError.Should().BeTrue(result.Text);
        }

        #endregion

    }

}
