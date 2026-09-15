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
    /// <c>odata_list_operations</c> on a few-tables model with no operations.
    /// </summary>
    [TestClass]
    public class OdataListOperationsFewTablesHostTests : FewTablesToolHost
    {

        #region Public Methods

        /// <summary>
        /// Few-tables catalogs declare zero operations.
        /// </summary>
        [TestMethod]
        public async Task ListOperations_FewTablesNoOps_DeclaredOperations0()
        {
            var result = await InvokeAsync("odata_list_operations");
            result.IsError.Should().BeFalse(result.Text);
            result.Text.Should().Be("Declared operations: 0.");
            result.StructuredContent.Should().Be("{}", "an empty operations section is omitted, never an empty array");
        }

        #endregion

    }

}
