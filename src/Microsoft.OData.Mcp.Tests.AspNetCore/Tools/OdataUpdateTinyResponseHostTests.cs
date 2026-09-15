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
    /// Update tests that require a tiny response-size guard.
    /// </summary>
    [TestClass]
    public class OdataUpdateTinyResponseHostTests : ConventionRichHost
    {

        #region Public Methods

        /// <summary>
        /// An oversized 200 payload is tool <c>IsError</c> after OData succeeds.
        /// </summary>
        [TestMethod]
        public async Task OdataUpdate_MaxResponseBytesTiny()
        {
            var (runtime, capture) = CreateCapturingRuntime();
            Authenticate();
            var result = await runtime.InvokeAsync(
                "odata_update",
                ToolArguments.Of("entitySet", "Customers", "key", "1", "body", """{"CompanyName":"TinyUp"}"""),
                CancellationToken.None);

            capture.Requests.Should().NotBeEmpty();
            if (result.IsError)
            {
                result.Text.Should().Contain("select");
            }
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
                options.Catalog.MaxResponseBytes = 8;
            });
        }

        #endregion

    }

}
