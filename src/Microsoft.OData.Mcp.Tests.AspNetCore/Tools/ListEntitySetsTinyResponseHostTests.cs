// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.OData;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OData.Mcp.Tests.AspNetCore.Fixtures;
using Microsoft.OData.Mcp.Tests.Shared.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.OData.Mcp.Tests.AspNetCore.Tools
{

    /// <summary>
    /// Lists entity sets when the response size guard is tiny.
    /// </summary>
    [TestClass]
    public class ListEntitySetsTinyResponseHostTests : ConventionRichHost
    {

        #region Public Methods

        /// <summary>
        /// A tiny <c>MaxResponseBytes</c> rejects the list and suggests select and top.
        /// </summary>
        [TestMethod]
        public async Task ListEntitySets_MaxResponseBytesTiny_IsErrorSuggestingSelectTop()
        {
            var result = await InvokeAsync("odata_list_entity_sets");

            result.IsError.Should().BeTrue();
            result.Text.Should().Contain("select");
            result.Text.Should().Contain("top");
        }

        #endregion

        #region Internal Methods

        /// <inheritdoc />
        internal override void ConfigureServices(IServiceCollection services)
        {
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
                options.Catalog.MaxResponseBytes = 10;
            });
        }

        #endregion

    }

}
