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
    /// Describes a set on a 200-entity-set model.
    /// </summary>
    [TestClass]
    public class DescribeTypeWideModelHostTests : ConventionRichHost
    {

        #region Public Methods

        /// <summary>
        /// Describing <c>Rows000</c> succeeds.
        /// </summary>
        [TestMethod]
        public async Task DescribeType_WideModel_DescribeRows000_Succeeds()
        {
            var result = await InvokeAsync("odata_describe_type", ToolArguments.Of("name", "Rows000", "format", "json"));

            result.IsError.Should().BeFalse(result.Text);
            OdataDescribeTypeHostTests.ReadKeys(result.StructuredContent).Should().Contain("Id");
        }

        #endregion

        #region Internal Methods

        /// <inheritdoc />
        internal override void ConfigureServices(IServiceCollection services)
        {
            services
                .AddControllers()
                .AddOData(options =>
                {
                    options.AddRouteComponents("odata", TestModels.GetWideModel(200));
                });
            services.AddODataMcp(options =>
            {
                options.Catalog.MaxNamedTools = 150;
                options.Catalog.MaxResources = 50;
            });
        }

        #endregion

    }

}
