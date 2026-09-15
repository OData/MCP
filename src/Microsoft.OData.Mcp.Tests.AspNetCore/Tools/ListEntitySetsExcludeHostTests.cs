// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System.Linq;
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
    /// Lists entity sets when Orders is excluded from a simple model.
    /// </summary>
    [TestClass]
    public class ListEntitySetsExcludeHostTests : ConventionRichHost
    {

        #region Public Methods

        /// <summary>
        /// Excluded Orders is omitted from the list and from named tools.
        /// </summary>
        [TestMethod]
        public async Task ListEntitySets_ExcludeEntitySets_OmitsPeopleFromList()
        {
            var result = await InvokeAsync("odata_list_entity_sets");
            result.IsError.Should().BeFalse(result.Text);
            var names = OdataListEntitySetsHostTests.ReadEntitySetNames(result.StructuredContent);
            names.Should().Contain("Customers");
            names.Should().NotContain("Orders");
            Session().Catalog.Tools.Select(tool => tool.Name).Should().Contain("odata_list_entity_sets");
            Session().Catalog.Tools.Select(tool => tool.Name).Should().Contain("odata_query");
            Session().Catalog.Tools.Select(tool => tool.Name).Should().NotContain("list_orders");
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
                options.Catalog.ExcludeEntitySets.Add("Orders");
            });
        }

        #endregion

    }

}
