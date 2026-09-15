// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using CloudNimble.Breakdance.AspNetCore;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.OData;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.OData.Mcp.AspNetCore.Execution;
using Microsoft.OData.Mcp.AspNetCore.Hosting;
using Microsoft.OData.Mcp.Core.Catalog;
using Microsoft.OData.Mcp.Tests.AspNetCore.Fixtures;
using Microsoft.OData.Mcp.Tests.Shared;
using Microsoft.OData.Mcp.Tests.Shared.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.OData.Mcp.Tests.AspNetCore.Tools
{


    /// <summary>
    /// <c>odata_list_operations</c> with a tiny response cap.
    /// </summary>
    [TestClass]
    public class OdataListOperationsTinyResponseHostTests : TinyResponseRichHost
    {

        #region Public Methods

        /// <summary>
        /// An oversized operations list is <c>IsError</c>.
        /// </summary>
        [TestMethod]
        public async Task ListOperations_MaxResponseBytesTiny_IsError()
        {
            var result = await InvokeAsync("odata_list_operations");
            result.IsError.Should().BeTrue(result.Text);
        }

        #endregion

    }

}
