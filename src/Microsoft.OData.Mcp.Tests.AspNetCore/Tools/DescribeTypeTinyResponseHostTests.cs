// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.OData;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OData.Mcp.Tests.AspNetCore.Fixtures;
using Microsoft.OData.Mcp.Tests.Shared;
using Microsoft.OData.Mcp.Tests.Shared.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.OData.Mcp.Tests.AspNetCore.Tools
{


    /// <summary>
    /// Describes a type when the response size guard is tiny.
    /// </summary>
    [TestClass]
    public class DescribeTypeTinyResponseHostTests : ConventionRichHost
    {

        #region Public Methods

        /// <summary>
        /// A tiny <c>MaxResponseBytes</c> rejects the type card.
        /// </summary>
        [TestMethod]
        public async Task DescribeType_MaxResponseBytesTiny_IsError()
        {
            var result = await InvokeAsync("odata_describe_type", ToolArguments.Of("name", "Customers"));

            result.IsError.Should().BeTrue();
            result.Text.Should().Contain("select").And.Contain("top");
        }

        #endregion

        #region Internal Methods

        /// <inheritdoc />
        internal override void ConfigureServices(IServiceCollection services)
        {
            services
                .AddControllers()
                .AddApplicationPart(typeof(CustomersController).Assembly)
                .AddOData(options =>
                {
                    options.EnableQueryFeatures();
                    options.AddRouteComponents("odata", TestModels.GetRichModel());
                });
            services.AddODataMcp(options =>
            {
                options.Catalog.MaxResponseBytes = 10;
            });
        }

        #endregion

    }

}
