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
    /// Lists all 200 sets of a wide model even when named tools and resources are capped.
    /// </summary>
    [TestClass]
    public class ListEntitySetsWideModelHostTests : ConventionRichHost
    {

        #region Public Methods

        /// <summary>
        /// The list returns every declared set, not only named-tool sets.
        /// </summary>
        [TestMethod]
        public async Task ListEntitySets_WideModel200_ReturnsAllDeclaredSetsNotJustNamed()
        {
            var result = await InvokeAsync("odata_list_entity_sets");
            result.IsError.Should().BeFalse(result.Text);
            var names = OdataListEntitySetsHostTests.ReadEntitySetNames(result.StructuredContent);
            names.Should().HaveCount(200);
            names.Should().Contain("Rows000");
            names.Should().Contain("Rows199");
            result.Text.Should().Be("Declared entity sets: 200.");
            Session().Catalog.Tools.Count.Should().BeLessThanOrEqualTo(150);
            Session().Catalog.Resources.Count.Should().BeLessThanOrEqualTo(50);
        }

        #endregion

        #region Internal Methods

        /// <inheritdoc />
        internal override void ConfigureServices(IServiceCollection services)
        {
            services
                .AddControllers()
                .AddOData(options =>
                {
                    options.AddRouteComponents("odata", TestModels.GetWideModel(200));
                });
            services.AddODataMcp(options =>
            {
                options.Catalog.MaxNamedTools = 150;
                options.Catalog.MaxResources = 50;
            });
        }

        #endregion

    }

}
