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
    /// Create tests that require a tiny response-size guard.
    /// </summary>
    [TestClass]
    public class OdataCreateTinyResponseHostTests : ConventionRichHost
    {

        #region Public Methods

        /// <summary>
        /// The entity is created even when the 201 payload exceeds the MCP response cap.
        /// </summary>
        [TestMethod]
        public async Task OdataCreate_MaxResponseBytesTinyOn201Payload_IsErrorAfterODataSucceeds()
        {
            var (runtime, capture) = CreateCapturingRuntime();
            Authenticate();
            var result = await runtime.InvokeAsync(
                "odata_create",
                ToolArguments.Of("entitySet", "Customers", "body", """{"CompanyName":"TinyResp"}"""),
                CancellationToken.None);

            result.IsError.Should().BeTrue();
            result.Text.Should().Contain("select");
            result.Text.Should().Contain("top");
            capture.Requests.Should().NotBeEmpty();

            using var client = CreateClient();
            var listed = await client.GetStringAsync("/odata/Customers?$filter=CompanyName eq 'TinyResp'");
            listed.Should().Contain("TinyResp");
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
