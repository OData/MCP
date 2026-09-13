// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OData.Mcp.Core.Catalog;
using Microsoft.OData.Mcp.Core.Execution;
using Microsoft.OData.Mcp.Core.Parsing;
using Microsoft.OData.Mcp.Tests.Shared;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.OData.Mcp.Tests.Integration
{

    /// <summary>
    /// Live generic query tests against Northwind.
    /// </summary>
    [TestClass]
    public class NorthwindGenericQueryTests
    {

        #region Public Methods

        /// <summary>
        /// Catalog handler for odata_query entitySet=Products top=1 returns a product.
        /// </summary>
        [TestMethod]
        public async Task OdataQuery_NorthwindProductsTop1_ReturnsProduct()
        {
            using var http = new HttpClient();
            var xml = await http.GetStringAsync($"{LiveOData.Northwind.TrimEnd('/')}/$metadata");
            var catalog = new ODataMcpCatalog(new CsdlParser().ParseFromString(xml), new ODataMcpCatalogOptions());
            var services = new ServiceCollection();

            services.AddHttpClient("OData", client =>
            {
                client.BaseAddress = new Uri(LiveOData.Northwind.TrimEnd('/') + "/");
                client.DefaultRequestHeaders.Accept.ParseAdd("application/json");
            });

            var runtime = new ODataToolRuntime(catalog, new RemoteODataExecutor(services.BuildServiceProvider().GetRequiredService<IHttpClientFactory>()));
            var result = await runtime.InvokeAsync(
                "odata_query",
                new Dictionary<string, JsonElement>
                {
                    ["entitySet"] = JsonSerializer.SerializeToElement("Products"),
                    ["top"] = JsonSerializer.SerializeToElement(1)
                },
                CancellationToken.None);

            result.IsError.Should().BeFalse();
            result.StructuredContent.Should().NotBeNullOrWhiteSpace();
            result.StructuredContent.Should().Contain("Product");
        }

        #endregion

    }

}
