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
    /// Update tests that require a small request-body guard.
    /// </summary>
    [TestClass]
    public class OdataUpdateBodyGuardHostTests : ConventionRichHost
    {

        #region Public Methods

        /// <summary>
        /// Oversized PATCH bodies fail without an OData round-trip.
        /// </summary>
        [TestMethod]
        public async Task OdataUpdate_MaxRequestBodyBytesExceeded_NoHttp()
        {
            var (runtime, capture) = CreateCapturingRuntime();
            Authenticate();
            var result = await runtime.InvokeAsync(
                "odata_update",
                ToolArguments.Of("entitySet", "Customers", "key", "1", "body", new string('x', 65)),
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
