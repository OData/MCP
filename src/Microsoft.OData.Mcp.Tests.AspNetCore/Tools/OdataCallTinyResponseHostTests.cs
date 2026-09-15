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
    /// <c>odata_call</c> with a tiny response cap.
    /// </summary>
    [TestClass]
    public class OdataCallTinyResponseHostTests : TinyResponseRichHost
    {

        #region Public Methods

        /// <summary>
        /// An oversized function payload is <c>IsError</c>.
        /// </summary>
        [TestMethod]
        public async Task OdataCall_MaxResponseBytesTiny()
        {
            var result = await InvokeAsync("odata_call", ToolArguments.Of("name", "MostValuable"));
            result.IsError.Should().BeTrue(result.Text);
        }

        #endregion

    }

}
