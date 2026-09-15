// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.OData;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OData.Mcp.AspNetCore.Hosting;
using Microsoft.OData.Mcp.Tests.AspNetCore.Fixtures;
using Microsoft.OData.Mcp.Tests.Shared.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.OData.Mcp.Tests.AspNetCore.Tools
{

    /// <summary>
    /// Isolated catalogs on two OData prefixes.
    /// </summary>
    [TestClass]
    public class ListEntitySetsTwoPrefixHostTests : ConventionRichHost
    {

        #region Public Methods

        /// <summary>
        /// Each prefix lists only its own exclusive sets.
        /// </summary>
        [TestMethod]
        public async Task ListEntitySets_TwoPrefixes_IsolatedCatalogs()
        {
            var factory = TestServer.Services.GetRequiredService<ODataMcpSessionFactory>();
            var odata = await factory.Sessions["odata"].Runtime.InvokeAsync("odata_list_entity_sets", null, CancellationToken.None);
            var shop = await factory.Sessions["shop"].Runtime.InvokeAsync("odata_list_entity_sets", null, CancellationToken.None);

            odata.IsError.Should().BeFalse(odata.Text);
            shop.IsError.Should().BeFalse(shop.Text);
            var odataNames = OdataListEntitySetsHostTests.ReadEntitySetNames(odata.StructuredContent);
            var shopNames = OdataListEntitySetsHostTests.ReadEntitySetNames(shop.StructuredContent);
            odataNames.Should().Contain("Customers");
            odataNames.Should().NotContain("Products");
            shopNames.Should().Contain("Products");
            shopNames.Should().NotContain("Customers");
            shopNames.Should().NotContain("Orders");
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
                    options.AddRouteComponents("odata", TestModels.GetMinimalModel());
                    options.AddRouteComponents("shop", TestModels.GetNoAuthModel());
                });
            services.AddODataMcp();
        }

        #endregion

    }

}
