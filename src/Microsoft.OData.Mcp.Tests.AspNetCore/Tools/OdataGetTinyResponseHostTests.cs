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
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.OData;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OData.Mcp.Core.Catalog;
using Microsoft.OData.Mcp.Tests.AspNetCore.Fixtures;
using Microsoft.OData.Mcp.Tests.AspNetCore.RateLimit;
using Microsoft.OData.Mcp.Tests.Shared;
using Microsoft.OData.Mcp.Tests.Shared.Entities;
using Microsoft.OData.Mcp.Tests.Shared.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.OData.Mcp.Tests.AspNetCore.Tools
{


    /// <summary>
    /// <c>odata_get</c> cases that require a tiny <c>MaxResponseBytes</c>.
    /// </summary>
    [TestClass]
    public class OdataGetTinyResponseHostTests : TinyResponseRichHost
    {

        #region Public Methods

        /// <summary>
        /// An oversized get payload is <c>IsError</c> suggesting select and top.
        /// </summary>
        [TestMethod]
        public async Task OdataGet_MaxResponseBytesTiny_IsError()
        {
            var result = await InvokeAsync("odata_get", ToolArguments.Of("entitySet", "Customers", "key", "1"));
            result.IsError.Should().BeTrue(result.Text);
            result.Text.Should().Contain("select");
            result.Text.Should().Contain("top");
        }

        #endregion

    }

}
