// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System.Net;
using System.Net.Http;
using System.Text;
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
