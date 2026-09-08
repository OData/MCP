// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.OData;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OData.Mcp.Tests.AspNetCore.Fixtures;
using Microsoft.OData.Mcp.Tests.Shared;
using Microsoft.OData.Mcp.Tests.Shared.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.OData.Mcp.Tests.AspNetCore.Tools
{

    /// <summary>
    /// Hop-1 resource-enforcement tests: a request with no <c>Authorization</c> header is 401. These tests are
    /// not linked into <c>Microsoft.OData.Mcp.Tests.Authentication</c> because that suite's outbound OAuth path
    /// attaches a Bearer token to every request, making "no Authorization header" unreachable there.
    /// </summary>
    [TestClass]
    public class AuthEnforcementHostTests : ConventionRichHost
    {

        #region Public Methods

        /// <summary>
        /// Unauthenticated create is tool <c>IsError</c> with status 401.
        /// </summary>
        [TestMethod]
        public async Task OdataCreate_OData8_WithoutAuthorization_IsError401()
        {
            using var client = CreateClient();
            using var twinContent = new StringContent("""{"CompanyName":"NoAuth"}""", Encoding.UTF8, "application/json");
            using var twin = await client.PostAsync("/odata/Customers", twinContent);
            twin.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

            var (runtime, capture) = CreateCapturingRuntime();
            var result = await runtime.InvokeAsync(
                "odata_create",
                ToolArguments.Of("entitySet", "Customers", "body", """{"CompanyName":"NoAuth"}"""),
                CancellationToken.None);

            result.IsError.Should().BeTrue();
            result.Text.Should().Contain("status 401");
            capture.Requests.Should().NotBeEmpty();
        }

        /// <summary>
        /// Unauthenticated create 401s; authenticated create then succeeds.
        /// </summary>
        [TestMethod]
        public async Task OdataCreate_UnauthenticatedThenAuthenticated_OData8()
        {
            var (runtime, capture) = CreateCapturingRuntime();
            var denied = await runtime.InvokeAsync(
                "odata_create",
                ToolArguments.Of("entitySet", "Customers", "body", """{"CompanyName":"NeedAuth"}"""),
                CancellationToken.None);
            denied.IsError.Should().BeTrue();
            denied.Text.Should().Contain("status 401");

            TestServer.Services.GetRequiredService<IHttpContextAccessor>().HttpContext!.Request.Headers.Authorization = "Bearer test";
            var allowed = await runtime.InvokeAsync(
                "odata_create",
                ToolArguments.Of("entitySet", "Customers", "body", """{"CompanyName":"NeedAuth"}"""),
                CancellationToken.None);
            allowed.IsError.Should().BeFalse(allowed.Text);
            capture.Requests.Should().HaveCountGreaterThanOrEqualTo(2);
        }

        /// <summary>
        /// Unauthenticated create is 401.
        /// </summary>
        [TestMethod]
        public async Task OdataCreate_WithoutAuthorization_IsError401()
        {
            var created = await InvokeAsync(
                "odata_create",
                ToolArguments.Of("entitySet", "Customers", "body", """{"CompanyName":"NoAuth"}"""));

            created.IsError.Should().BeTrue();
            created.Text.Should().Contain("status 401");
        }

        /// <summary>
        /// Unauthenticated PATCH is 401.
        /// </summary>
        [TestMethod]
        public async Task OdataUpdate_OData8_WithoutAuthorization_401()
        {
            using var client = CreateClient();
            using var twinContent = new StringContent("""{"CompanyName":"Updated"}""", Encoding.UTF8, "application/json");
            using var twin = await client.PatchAsync("/odata/Customers(1)", twinContent);
            twin.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

            var (runtime, _) = CreateCapturingRuntime();
            var result = await runtime.InvokeAsync(
                "odata_update",
                ToolArguments.Of("entitySet", "Customers", "key", "1", "body", """{"CompanyName":"Updated"}"""),
                CancellationToken.None);

            result.IsError.Should().BeTrue();
            result.Text.Should().Contain("status 401");
        }

        /// <summary>
        /// Update without auth is 401.
        /// </summary>
        [TestMethod]
        public async Task OdataUpdate_WithoutAuthorization_IsError401()
        {
            var updated = await InvokeAsync(
                "odata_update",
                ToolArguments.Of("entitySet", "Customers", "key", "1", "body", """{"CompanyName":"Nope"}"""));

            updated.IsError.Should().BeTrue();
            updated.Text.Should().Contain("status 401");
        }

        #endregion

    }

    /// <summary>
    /// Hop-1 resource-enforcement tests for the named <c>create_customer</c>/<c>update_customer</c> tools on the
    /// convention simple model. Not linked into <c>Microsoft.OData.Mcp.Tests.Authentication</c>; see
    /// <see cref="AuthEnforcementHostTests"/>.
    /// </summary>
    [TestClass]
    public class NamedCrudAuthEnforcementHostTests : ConventionRichHost
    {

        #region Public Methods

        /// <summary>
        /// Unauthenticated named create is 401.
        /// </summary>
        [TestMethod]
        public async Task CreateCustomer_OData8_NoAuth_401()
        {
            var result = await InvokeAsync("create_customer", ToolArguments.Of("CompanyName", "NoAuthNamed"));

            result.IsError.Should().BeTrue();
            result.Text.Should().Contain("status 401");
        }

        /// <summary>
        /// Unauthenticated named update is 401.
        /// </summary>
        [TestMethod]
        public async Task UpdateCustomer_OData8_NoAuth_401()
        {
            var result = await InvokeAsync(
                "update_customer",
                ToolArguments.Of("key", "1", "body", """{"CompanyName":"X"}"""));

            result.IsError.Should().BeTrue();
            result.Text.Should().Contain("status 401");
        }

        #endregion

        #region Internal Methods

        /// <inheritdoc />
        internal override void ConfigureServices(IServiceCollection services)
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
            services.AddODataMcp();
        }

        #endregion

    }

}
