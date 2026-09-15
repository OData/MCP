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
    /// Named list/get vs Customers/Products rate-limit budgets.
    /// </summary>
    [TestClass]
    public class NamedCrudRateLimitHostTests : ConventionRichHost
    {

        #region Public Methods

        /// <summary>
        /// Named list_customers spends the same Customers budget as odata_query.
        /// </summary>
        [TestMethod]
        public async Task Rate_NamedListCustomers_SameBudgetAsOdataQueryCustomers()
        {
            var runtime = Session().Runtime;
            var first = await runtime.InvokeAsync("list_customers", null, CancellationToken.None);
            first.IsError.Should().BeFalse(first.Text);
            var second = await runtime.InvokeAsync("odata_query", ToolArguments.Of("entitySet", "Customers"), CancellationToken.None);
            second.IsError.Should().BeTrue();
            second.Text.Should().Contain("429");
        }

        /// <summary>
        /// Named list Customers 429 does not spend Products.
        /// </summary>
        [TestMethod]
        public async Task ListCustomers_VsListProducts_IndependentSetLimits()
        {
            var runtime = Session().Runtime;
            (await runtime.InvokeAsync("list_customers", null, CancellationToken.None)).IsError.Should().BeFalse();
            (await runtime.InvokeAsync("list_customers", null, CancellationToken.None)).IsError.Should().BeTrue();
            var products = await runtime.InvokeAsync("list_products", null, CancellationToken.None);
            products.IsError.Should().BeFalse(products.Text);
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
                Mcp = 0,
                Products = 2
            }));
            services.AddSingleton<CustomerStore>();
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
