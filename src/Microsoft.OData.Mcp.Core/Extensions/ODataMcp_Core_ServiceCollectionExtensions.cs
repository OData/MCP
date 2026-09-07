// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using System.Net.Http;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.OData.Mcp.Core.Execution;
using Microsoft.OData.Mcp.Core.Parsing;

namespace Microsoft.Extensions.DependencyInjection
{

    /// <summary>
    /// Registers Core MCP services that do not depend on ASP.NET or Authentication.
    /// </summary>
    public static class ODataMcp_Core_ServiceCollectionExtensions
    {

        #region Public Methods

        /// <summary>
        /// Adds the CSDL parser and a named OData <see cref="HttpClient"/>.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <param name="configureClient">Optional named-client configuration.</param>
        /// <returns>
        /// The service collection.
        /// </returns>
        public static IServiceCollection AddODataMcpCore(this IServiceCollection services, Action<HttpClient>? configureClient = null)
        {
            ArgumentNullException.ThrowIfNull(services);

            services.TryAddSingleton<CsdlParser>();
            services.AddHttpClient(RemoteODataExecutor.HttpClientName, client =>
            {
                configureClient?.Invoke(client);
            });
            services.TryAddTransient<IODataExecutor, RemoteODataExecutor>();

            return services;
        }

        #endregion

    }

}
