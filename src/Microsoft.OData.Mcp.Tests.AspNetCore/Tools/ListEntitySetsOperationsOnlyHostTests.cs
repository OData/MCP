// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System.Threading;
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
    /// Lists entity sets on an operations-only model.
    /// </summary>
    [TestClass]
    public class ListEntitySetsOperationsOnlyHostTests : ConventionRichHost
    {

        #region Public Methods

        /// <summary>
        /// Operations-only catalogs return an empty entity-set array.
        /// </summary>
        [TestMethod]
        public async Task ListEntitySets_OperationsOnlyModel_ReturnsEmptyArray()
        {
            var (runtime, capture) = CreateCapturingRuntime();
            var result = await runtime.InvokeAsync("odata_list_entity_sets", null, CancellationToken.None);

            result.IsError.Should().BeFalse(result.Text);
            result.Text.Should().Be("Declared entity sets: 0.");
            OdataListEntitySetsHostTests.ReadEntitySetNames(result.StructuredContent).Should().BeEmpty();
            capture.Requests.Should().BeEmpty();
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
                    options.EnableQueryFeatures();
                    options.AddRouteComponents("odata", TestModels.GetOperationsOnlyModel());
                });
            services.AddODataMcp();
        }

        #endregion

    }

}
