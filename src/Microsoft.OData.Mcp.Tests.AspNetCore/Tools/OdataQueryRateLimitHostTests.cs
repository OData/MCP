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
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.OData;
using Microsoft.Extensions.DependencyInjection;
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
    /// Per-set OData rate limits around <c>odata_query</c>.
    /// </summary>
    [TestClass]
    public class OdataQueryRateLimitHostTests : ConventionRichHost
    {

        #region Public Methods

        /// <summary>
        /// Exhausting Customers leaves Products unlimited on its own budget.
        /// </summary>
        [TestMethod]
        public async Task OdataQuery_CustomersLimited_ProductsUnlimited_Independent()
        {
            var first = await InvokeAsync("odata_query", ToolArguments.Of("entitySet", "Customers"));
            first.IsError.Should().BeFalse(first.Text);
            first.StructuredContent.Should().Contain("Contoso");

            var second = await InvokeAsync("odata_query", ToolArguments.Of("entitySet", "Customers"));
            second.IsError.Should().BeTrue();
            second.Text.Should().Contain("429");

            var products = await InvokeAsync("odata_query", ToolArguments.Of("entitySet", "Products"));
            products.IsError.Should().BeFalse(products.Text);
            products.StructuredContent.Should().Contain("Widget");
        }

        #endregion

        #region Internal Methods

        /// <inheritdoc />
        internal override void ConfigureApp(IApplicationBuilder app)
        {
            app.UseRouting();
            app.UseRateLimiter();
            app.UseEndpoints(endpoints => endpoints.MapControllers());
            app.UseODataMcp();
        }

        /// <inheritdoc />
        internal override void ConfigureServices(IServiceCollection services)
        {
            services.AddRateLimiter(options => ODataPartitionedLimiter.Apply(options, new RateLimitBudget
            {
                Customers = 1,
                Function = 1,
                Mcp = 1,
                Products = 2
            }));
            services
                .AddControllers()
                .AddApplicationPart(typeof(CustomersController).Assembly)
                .AddOData(options =>
                {
                    options.EnableQueryFeatures();
                    options.AddRouteComponents("odata", TestModels.GetRateLimitModel());
                });
            services.AddODataMcp();
        }

        #endregion

    }

}
