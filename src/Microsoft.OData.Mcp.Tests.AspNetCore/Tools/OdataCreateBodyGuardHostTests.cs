// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

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
    /// Create tests that require a small <c>MaxRequestBodyBytes</c>.
    /// </summary>
    [TestClass]
    public class OdataCreateBodyGuardHostTests : ConventionRichHost
    {

        #region Public Methods

        /// <summary>
        /// A body of exactly the configured maximum is forwarded.
        /// </summary>
        [TestMethod]
        public async Task OdataCreate_MaxRequestBodyBytesExact_Allowed()
        {
            var (runtime, capture) = CreateCapturingRuntime();
            Authenticate();
            var payload = new string('a', 20);
            var result = await runtime.InvokeAsync(
                "odata_create",
                ToolArguments.Of("entitySet", "Customers", "body", payload),
                CancellationToken.None);

            capture.Requests.Should().NotBeEmpty();
            result.IsError.Should().BeTrue();
        }

        /// <summary>
        /// Oversized bodies fail without an OData round-trip.
        /// </summary>
        [TestMethod]
        public async Task OdataCreate_MaxRequestBodyBytesExceeded_IsErrorNoHttp()
        {
            var (runtime, capture) = CreateCapturingRuntime();
            Authenticate();
            var result = await runtime.InvokeAsync(
                "odata_create",
                ToolArguments.Of("entitySet", "Customers", "body", new string('x', 65)),
                CancellationToken.None);

            result.IsError.Should().BeTrue();
            result.Text.Should().Contain("request body exceeds the maximum size");
            capture.Requests.Should().BeEmpty();
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
                    options.AddRouteComponents("odata", TestModels.GetRichModel());
                });
            services.AddODataMcp(options =>
            {
                options.Catalog.MaxRequestBodyBytes = 64;
            });
        }

        #endregion

    }

}
