// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OData.Mcp.Core.Execution;
using Microsoft.OData.Mcp.Tests.Shared;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.OData.Mcp.Tests.Core.Execution
{

    /// <summary>
    /// Live tests for <see cref="RemoteODataExecutor"/> against Northwind.
    /// </summary>
    [TestClass]
    public class RemoteODataExecutorLiveTests
    {

        #region Public Methods

        /// <summary>
        /// GET Products?$top=1 against the official Northwind service succeeds.
        /// </summary>
        [TestMethod]
        public async Task ExecuteAsync_NorthwindProductsTop1_ReturnsJson()
        {
            var services = new ServiceCollection();

            services.AddHttpClient("OData", client =>
            {
                client.BaseAddress = new Uri(LiveOData.Northwind.TrimEnd('/') + "/");
                client.DefaultRequestHeaders.Accept.ParseAdd("application/json");
            });

            var executor = new RemoteODataExecutor(services.BuildServiceProvider().GetRequiredService<IHttpClientFactory>());
            var result = await executor.ExecuteAsync(
                new ODataExecuteRequest
                {
                    QueryOptions =
                    {
                        ["top"] = "1"
                    },
                    RelativePath = "Products"
                },
                CancellationToken.None);

            result.IsSuccess.Should().BeTrue();
            result.Body.Should().NotBeNullOrWhiteSpace();
            result.Body.Should().Contain("Product");
        }

        /// <summary>
        /// Query option keys must not be double-prefixed with '$'.
        /// </summary>
        [TestMethod]
        public void BuildRelativeUri_AddsSingleDollarPrefix()
        {
            var uri = RemoteODataExecutor.BuildRelativeUri(new ODataExecuteRequest
            {
                QueryOptions =
                {
                    ["filter"] = "ProductID eq 1",
                    ["top"] = "1"
                },
                RelativePath = "Products"
            });

            uri.Should().Contain("$filter");
            uri.Should().Contain("$top");
            uri.Should().NotContain("$$");
        }

        #endregion

    }

}
