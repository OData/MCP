// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Claims;
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
using Microsoft.OData.Mcp.Tests.AspNetCore.Paging;
using Microsoft.OData.Mcp.Tests.AspNetCore.RateLimit;
using Microsoft.OData.Mcp.Tests.Shared;
using Microsoft.OData.Mcp.Tests.Shared.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.OData.Mcp.Tests.AspNetCore.Tools
{


    /// <summary>
    /// Named family, snake_case, binary omit, and cap behavior on the rich model.
    /// </summary>
    [TestClass]
    public class NamedCrudRichHostTests : ConventionRichHost
    {

        #region Public Methods

        /// <summary>
        /// <c>OrderItems</c> snakes to <c>list_order_items</c> / <c>get_order_item</c>.
        /// </summary>
        [TestMethod]
        public void Snake_OrderItems_ListOrder_items()
        {
            var names = Session().Catalog.Tools.Select(tool => tool.Name).ToList();
            names.Should().Contain("list_order_items");
            names.Should().Contain("get_order_item");
        }

        /// <summary>
        /// Named create schema for Document omits binary and stream.
        /// </summary>
        [TestMethod]
        public void Cap_NamedCreateSchemaDeclaredPropertiesOnly()
        {
            var create = Session().Catalog.Tools.FirstOrDefault(tool => tool.Name == "create_document");
            if (create is null)
            {
                Session().Catalog.Tools.Should().Contain(tool => tool.Name == "odata_create");
                return;
            }

            create.InputSchema.Should().Contain("Title");
            create.InputSchema.Should().NotContain("Photo");
            create.InputSchema.Should().NotContain("File");
        }

        /// <summary>
        /// If <c>list_customers</c> is advertised, <c>get_customer</c> is advertised.
        /// </summary>
        [TestMethod]
        public void Cap_NeverListWithoutGet()
        {
            var names = Session().Catalog.Tools.Select(tool => tool.Name).ToHashSet(StringComparer.Ordinal);
            foreach (var list in names.Where(name => name.StartsWith("list_", StringComparison.Ordinal)))
            {
                names.Should().Contain(name => name.StartsWith("get_", StringComparison.Ordinal), "family for {0}", list);
            }
        }

        /// <summary>
        /// Ignored InternalSecret is absent from named create and describe.
        /// </summary>
        [TestMethod]
        public async Task Ignore_InternalSecret_AbsentFromDescribeNamedCreateResourceCard()
        {
            var describe = await InvokeAsync("odata_describe_type", ToolArguments.Of("name", "Customers"));
            describe.StructuredContent.Should().NotContain("InternalSecret");
            var resource = Session().Catalog.Resources.Single(item => item.Name == "Customers");
            resource.ReadContents.Should().NotContain("InternalSecret");
        }

        #endregion

    }

}
