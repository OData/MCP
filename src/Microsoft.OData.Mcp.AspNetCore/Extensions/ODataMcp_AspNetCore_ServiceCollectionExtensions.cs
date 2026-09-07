// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using System.Net.Http;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.OData.Mcp.AspNetCore.Execution;
using Microsoft.OData.Mcp.AspNetCore.Hosting;
using Microsoft.OData.Mcp.Core.Catalog;
using ModelContextProtocol.AspNetCore;

namespace Microsoft.Extensions.DependencyInjection
{

    /// <summary>
    /// Registers OData MCP hosting for ASP.NET Core.
    /// </summary>
    public static class ODataMcp_AspNetCore_ServiceCollectionExtensions
    {

        #region Public Methods

        /// <summary>
        /// Registers OData MCP services. Call <c>UseODataMcp</c> after mapping OData routes
        /// to turn MCP on at <c>{prefix}/mcp</c>. Discovers prefixes from ASP.NET Core endpoint
        /// routing (OData 7 <c>MapODataRoute</c> / Restier <c>MapApiRoute</c> and OData 8
        /// conventional endpoints), plus any routes registered with
        /// <see cref="ODataMcpHostOptions.AddRoute"/>. Prefer this unless a prefix must stay
        /// hidden from agents; then set <see cref="ODataMcpHostOptions.IncludePrefixes"/> or
        /// <see cref="ODataMcpHostOptions.ExcludeRoutes"/>.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <returns>
        /// The service collection.
        /// </returns>
        /// <example>
        /// <code>
        /// builder.Services
        ///     .AddControllers()
        ///     .AddOData(options =>
        ///     {
        ///         options.AddRouteComponents("odata", GetPublicModel());
        ///         options.AddRouteComponents("reporting", GetReportingModel());
        ///     });
        ///
        /// builder.Services.AddODataMcp();
        ///
        /// var app = builder.Build();
        /// app.MapControllers();
        /// app.UseODataMcp();
        /// </code>
        /// </example>
        /// <remarks>
        /// This package does not reference Microsoft.AspNetCore.OData. The app brings OData 7 or 8.
        /// This method does not map endpoints. Call <c>UseODataMcp</c> to map MCP. Do not call
        /// the SDK <c>MapMcp</c> yourself.
        /// </remarks>
        public static IServiceCollection AddODataMcp(this IServiceCollection services)
        {
            return services.AddODataMcp(_ => { });
        }

        /// <summary>
        /// Registers OData MCP services. Call <c>UseODataMcp</c> after mapping OData routes
        /// to turn MCP on at <c>{prefix}/mcp</c>. Discovers prefixes from ASP.NET Core endpoint
        /// routing (OData 7 <c>MapODataRoute</c> / Restier <c>MapApiRoute</c> and OData 8
        /// conventional endpoints), plus any routes registered with
        /// <see cref="ODataMcpHostOptions.AddRoute"/>. Prefer this unless a prefix must stay
        /// hidden from agents; then set <see cref="ODataMcpHostOptions.IncludePrefixes"/> or
        /// <see cref="ODataMcpHostOptions.ExcludeRoutes"/>.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <param name="configure">Configures host options.</param>
        /// <returns>
        /// The service collection.
        /// </returns>
        /// <example>
        /// <code>
        /// builder.Services.AddODataMcp(options =>
        /// {
        ///     options.IncludePrefixes.Add("odata");
        ///     options.Catalog.MaxNamedTools = 80;
        ///     options.RateLimitingPolicyName = "mcp";
        /// });
        ///
        /// var app = builder.Build();
        /// app.MapControllers();
        /// app.UseODataMcp();
        /// </code>
        /// </example>
        /// <remarks>
        /// This package does not reference Microsoft.AspNetCore.OData. The app brings OData 7 or 8.
        /// This method does not map endpoints. Call <c>UseODataMcp</c> to map MCP. Do not call
        /// the SDK <c>MapMcp</c> yourself.
        /// </remarks>
        public static IServiceCollection AddODataMcp(this IServiceCollection services, Action<ODataMcpHostOptions> configure)
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(configure);

            services.AddOptions<ODataMcpHostOptions>().Configure(configure);
            services.AddHttpContextAccessor();
            services.AddHttpClient(InProcessODataExecutor.HttpClientName, client =>
                {
                    client.BaseAddress = new Uri("http://localhost/");
                    client.DefaultRequestHeaders.Accept.ParseAdd("application/json");
                })
                .ConfigurePrimaryHttpMessageHandler(sp =>
                    InProcessODataExecutor.TryCreateServerHandler(sp.GetService<IServer>()) ?? new SocketsHttpHandler());

            services.TryAddSingleton<ODataMcpSessionFactory>();

            services.AddMcpServer()
                .WithHttpTransport(options =>
                {
                    options.Stateless = true;
                })
                .WithODataCatalogHandlers(
                    sp =>
                    {
                        var factory = sp.GetRequiredService<ODataMcpSessionFactory>();
                        var http = sp.GetService<IHttpContextAccessor>()?.HttpContext;

                        return factory.Resolve(http);
                    });

            return services;
        }

        #endregion

    }

}
