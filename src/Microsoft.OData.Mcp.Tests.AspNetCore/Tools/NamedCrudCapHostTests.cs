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
    /// Named family cap: no split families.
    /// </summary>
    [TestClass]
    public class NamedCrudCapHostTests : AspNetCoreBreakdanceTestBase
    {

        #region Test Lifecycle

        /// <summary>
        /// Builds a simple-model host with a tight cap.
        /// </summary>
        [TestInitialize]
        public void Setup()
        {
            TestHostBuilder.ConfigureServices((_, services) =>
            {
                services.AddSingleton<CustomerStore>();
                services
                    .AddControllers()
                    .AddApplicationPart(typeof(CustomersController).Assembly)
                    .AddOData(options =>
                    {
                        options.EnableQueryFeatures();
                        options.AddRouteComponents("odata", TestModels.GetSimpleModel());
                    });
                services.AddODataMcp(options =>
                {
                    options.Catalog.MaxNamedTools = Cap;
                    options.Catalog.IncludeCreate = IncludeCreate;
                    options.Catalog.IncludeUpdate = IncludeUpdate;
                    options.Catalog.IncludeDelete = IncludeDelete;
                    if (!string.IsNullOrWhiteSpace(IncludeFirst))
                    {
                        options.Catalog.IncludeEntitySets.Add(IncludeFirst);
                    }
                });
            });
            AddMinimalMvc();
            TestHostBuilder.ConfigureWebHost(web =>
            {
                web.Configure(app =>
                {
                    app.UseRouting();
                    app.UseEndpoints(endpoints => endpoints.MapControllers());
                    app.UseODataMcp();
                });
            });
            TestSetup();
        }

        /// <summary>
        /// Tears down the host.
        /// </summary>
        [TestCleanup]
        public void TearDown()
        {
            TestTearDown();
        }

        #endregion

        #region Properties

        /// <summary>
        /// Gets or sets the named-tool cap for this run. MSTest creates a new instance per method.
        /// </summary>
        public int Cap { get; set; } = 10;

        /// <summary>
        /// Gets or sets whether named create is included.
        /// </summary>
        public bool IncludeCreate { get; set; } = true;

        /// <summary>
        /// Gets or sets whether named delete is included.
        /// </summary>
        public bool IncludeDelete { get; set; } = true;

        /// <summary>
        /// Gets or sets an IncludeEntitySets pin.
        /// </summary>
        public string? IncludeFirst { get; set; }

        /// <summary>
        /// Gets or sets whether named update is included.
        /// </summary>
        public bool IncludeUpdate { get; set; } = true;

        #endregion

        #region Public Methods

        /// <summary>
        /// Remaining 0 emits only generics.
        /// </summary>
        [TestMethod]
        public void Cap_MaxNamedTools11_OnlyGenerics()
        {
            Cap = 11;
            var names = Tools();
            names.Should().OnlyContain(name => name.StartsWith("odata_", StringComparison.Ordinal));
            names.Should().NotContain("list_customers");
        }

        /// <summary>
        /// Remaining 4 cannot fit a family of 5.
        /// </summary>
        [TestMethod]
        public void Cap_MaxNamedTools15_NoFamilyOf5()
        {
            Cap = 15;
            Tools().Should().NotContain("list_customers");
        }

        /// <summary>
        /// Remaining 5 fits exactly one family of 5.
        /// </summary>
        [TestMethod]
        public void Cap_MaxNamedTools16_ExactlyOneFamilyOf5()
        {
            Cap = 16;
            var names = Tools();
            names.Count(name => !name.StartsWith("odata_", StringComparison.Ordinal)).Should().Be(5);
            names.Should().Contain("list_customers");
            names.Should().Contain("get_customer");
        }

        /// <summary>
        /// IncludeCreate false reduces the family to 4, which fits remaining 4.
        /// </summary>
        [TestMethod]
        public void Cap_IncludeCreateFalse_Family4_FitsRemaining4()
        {
            Cap = 15;
            IncludeCreate = false;
            var names = Tools();
            names.Should().Contain("list_customers");
            names.Should().Contain("get_customer");
            names.Should().NotContain("create_customer");
            names.Should().Contain("update_customer");
            names.Should().Contain("delete_customer");
        }

        /// <summary>
        /// IncludeEntitySets pins Products first.
        /// </summary>
        [TestMethod]
        public void Cap_IncludeEntitySetsProducts_FirstFamilyIsProducts()
        {
            Cap = 16;
            IncludeFirst = "Products";
            var names = Tools();
            names.Should().Contain("list_products");
            names.Should().Contain("get_product");
            names.Should().NotContain("list_customers");
        }

        /// <summary>
        /// Generics remain when the cap is below the generic count.
        /// </summary>
        [TestMethod]
        public void Cap_GenericAlwaysPresentEvenWhenCap0ClampedToGenerics()
        {
            Cap = 3;
            var names = Tools();
            names.Should().HaveCount(11);
            names.Should().OnlyContain(name => name.StartsWith("odata_", StringComparison.Ordinal));
        }

        /// <summary>
        /// Named create is omitted when IncludeCreate is false; generic create remains.
        /// </summary>
        [TestMethod]
        public async Task CreateCustomer_OmittedWhenIncludeCreateFalse_UnknownTool_GenericCreateStillWorks()
        {
            Cap = 150;
            IncludeCreate = false;
            var names = Tools();
            names.Should().NotContain("create_customer");
            names.Should().Contain("odata_create");
            var accessor = TestServer.Services.GetRequiredService<McpHttpContextAccessor>();
            accessor.HttpContext = new DefaultHttpContext { RequestServices = TestServer.Services };
            accessor.HttpContext.User = new ClaimsPrincipal(new ClaimsIdentity("test"));
            var result = await TestServer.Services.GetRequiredService<ODataMcpSessionFactory>().Sessions["odata"].Runtime.InvokeAsync(
                "odata_create",
                ToolArguments.Of("entitySet", "Customers", "body", """{"CompanyName":"GenericStill"}"""),
                CancellationToken.None);
            result.IsError.Should().BeFalse(result.Text);
        }

        /// <summary>
        /// Named update omitted; generic update remains.
        /// </summary>
        [TestMethod]
        public async Task UpdateCustomer_OmittedWhenIncludeUpdateFalse_GenericUpdateStillWorks()
        {
            Cap = 150;
            IncludeUpdate = false;
            Tools().Should().NotContain("update_customer");
            Tools().Should().Contain("odata_update");
            var accessor = TestServer.Services.GetRequiredService<McpHttpContextAccessor>();
            accessor.HttpContext = new DefaultHttpContext { RequestServices = TestServer.Services };
            accessor.HttpContext.User = new ClaimsPrincipal(new ClaimsIdentity("test"));
            var result = await TestServer.Services.GetRequiredService<ODataMcpSessionFactory>().Sessions["odata"].Runtime.InvokeAsync(
                "odata_update",
                ToolArguments.Of("entitySet", "Customers", "key", "1", "body", """{"CompanyName":"GenericUp"}"""),
                CancellationToken.None);
            result.IsError.Should().BeFalse(result.Text);
        }

        /// <summary>
        /// Named delete omitted; generic delete remains.
        /// </summary>
        [TestMethod]
        public async Task DeleteCustomer_OmittedWhenIncludeDeleteFalse_GenericDeleteStillWorks()
        {
            Cap = 150;
            IncludeDelete = false;
            Tools().Should().NotContain("delete_customer");
            Tools().Should().Contain("odata_delete");
        }

        /// <summary>
        /// All write flags false: family is list and get only.
        /// </summary>
        [TestMethod]
        public void Flags_AllWriteFlagsFalse_FamilyIsListAndGetOnly_CapUses2()
        {
            Cap = 13;
            IncludeCreate = false;
            IncludeUpdate = false;
            IncludeDelete = false;
            var names = Tools();
            names.Should().Contain("list_customers");
            names.Should().Contain("get_customer");
            names.Should().NotContain("create_customer");
            names.Should().NotContain("update_customer");
            names.Should().NotContain("delete_customer");
        }

        /// <summary>
        /// List is absent when remaining 4 cannot fit family 5.
        /// </summary>
        [TestMethod]
        public void ListCustomers_AbsentWhenRemaining4AndFamily5()
        {
            Cap = 15;
            Tools().Should().NotContain("list_customers");
            Tools().Should().NotContain("get_customer");
        }

        /// <summary>
        /// List is absent when cap equals generic count.
        /// </summary>
        [TestMethod]
        public void ListCustomers_AbsentWhenCapTooSmallForFamily()
        {
            Cap = 11;
            Tools().Should().NotContain("list_customers");
        }

        /// <summary>
        /// IncludeCreate false reduces family to fit remaining 4.
        /// </summary>
        [TestMethod]
        public void ListCustomers_PresentWhenIncludeCreateFalseReducesFamilyToFit()
        {
            Cap = 15;
            IncludeCreate = false;
            Tools().Should().Contain("list_customers");
            Tools().Should().Contain("get_customer");
            Tools().Should().NotContain("create_customer");
        }

        #endregion

        #region Internal Methods

        /// <summary>
        /// Creates an HTTP client for direct OData calls against the host under test.
        /// </summary>
        /// <returns>
        /// The client.
        /// </returns>
        internal HttpClient CreateClient()
        {
            return TestServer.CreateClient();
        }

        /// <summary>
        /// Catalog tool names.
        /// </summary>
        /// <returns>
        /// Names.
        /// </returns>
        internal List<string> Tools()
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddHttpContextAccessor();
            services
                .AddControllers()
                .AddOData(options =>
                {
                    options.AddRouteComponents("odata", TestModels.GetSimpleModel());
                });
            services.AddODataMcp(options =>
            {
                options.Catalog.MaxNamedTools = Cap;
                options.Catalog.IncludeCreate = IncludeCreate;
                options.Catalog.IncludeUpdate = IncludeUpdate;
                options.Catalog.IncludeDelete = IncludeDelete;
                if (!string.IsNullOrWhiteSpace(IncludeFirst))
                {
                    options.Catalog.IncludeEntitySets.Add(IncludeFirst);
                }
            });
            using var provider = services.BuildServiceProvider();

            return [.. provider.GetRequiredService<ODataMcpSessionFactory>().Sessions["odata"].Catalog.Tools.Select(tool => tool.Name)];
        }

        #endregion

    }

}
