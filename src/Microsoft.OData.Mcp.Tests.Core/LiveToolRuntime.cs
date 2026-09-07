// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OData.Mcp.Core.Catalog;
using Microsoft.OData.Mcp.Core.Execution;
using Microsoft.OData.Mcp.Tests.Shared;

namespace Microsoft.OData.Mcp.Tests.Core
{

    /// <summary>
    /// Builds catalog runtimes against live Northwind and TripPin.
    /// </summary>
    public static class LiveToolRuntime
    {

        #region Public Methods

        /// <summary>
        /// Creates a runtime bound to live Northwind.
        /// </summary>
        /// <param name="configure">Optional catalog configuration.</param>
        /// <returns>
        /// Runtime and optional URI capture.
        /// </returns>
        public static async Task<(ODataToolRuntime Runtime, CapturingODataExecutor Capture)> CreateNorthwindAsync(Action<ODataMcpCatalogOptions>? configure = null)
        {
            return await CreateAsync(LiveOData.Northwind, configure);
        }

        /// <summary>
        /// Creates a runtime bound to live TripPin. Follows the service-root redirect.
        /// </summary>
        /// <param name="configure">Optional catalog configuration.</param>
        /// <returns>
        /// Runtime and capture wrapper.
        /// </returns>
        public static async Task<(ODataToolRuntime Runtime, CapturingODataExecutor Capture)> CreateTripPinAsync(Action<ODataMcpCatalogOptions>? configure = null)
        {
            using var probe = new HttpClient();
            using var response = await probe.GetAsync(LiveOData.TripPin.TrimEnd('/') + "/");
            var root = response.RequestMessage?.RequestUri?.ToString() ?? LiveOData.TripPin;

            return await CreateAsync(root, configure);
        }

        #endregion

        #region Internal Methods

        /// <summary>
        /// Fetches <c>$metadata</c> and binds a capturing remote executor.
        /// </summary>
        /// <param name="serviceRoot">The OData service root.</param>
        /// <param name="configure">Optional catalog configuration.</param>
        /// <returns>
        /// Runtime and capture handler.
        /// </returns>
        internal static async Task<(ODataToolRuntime Runtime, CapturingODataExecutor Capture)> CreateAsync(string serviceRoot, Action<ODataMcpCatalogOptions>? configure)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(serviceRoot);

            var root = serviceRoot.TrimEnd('/') + "/";
            using var http = new HttpClient();
            var xml = await http.GetStringAsync(root + "$metadata");
            var options = new ODataMcpCatalogOptions
            {
                RouteName = "remote"
            };
            configure?.Invoke(options);
            var catalog = new ODataMcpCatalog(new Microsoft.OData.Mcp.Core.Parsing.CsdlParser().ParseFromString(xml), options);
            var services = new ServiceCollection();
            services.AddHttpClient("OData", client =>
            {
                client.BaseAddress = new Uri(root);
                client.DefaultRequestHeaders.Accept.ParseAdd("application/json");
            });

            var inner = new RemoteODataExecutor(services.BuildServiceProvider().GetRequiredService<IHttpClientFactory>());
            var capture = new CapturingODataExecutor(inner);

            return (new ODataToolRuntime(catalog, capture), capture);
        }

        #endregion

    }

}
