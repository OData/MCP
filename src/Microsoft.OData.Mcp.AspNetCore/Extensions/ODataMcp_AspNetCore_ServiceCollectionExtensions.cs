// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using System.Net.Http;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Microsoft.OData.Mcp.AspNetCore.Authentication;
using Microsoft.OData.Mcp.AspNetCore.Execution;
using Microsoft.OData.Mcp.AspNetCore.Hosting;
using Microsoft.OData.Mcp.Core.Catalog;
using ModelContextProtocol.Server;

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

            // initialize.instructions: the shared default, with the developer's preface first. The preface is a catalog
            // option known before any metadata is read, so this is configured once and never assigned after build.
            services.AddOptions<McpServerOptions>()
                .Configure<IOptions<ODataMcpHostOptions>>((mcp, host) => mcp.ServerInstructions = ODataMcpInstructions.Compose(host.Value.Catalog.InstructionsPreface));

            return services;
        }

        /// <summary>
        /// Publishes RFC 9728 protected resource metadata for this host, and annotates the
        /// <c>WWW-Authenticate</c> challenge on a <c>401</c> beneath a covered route base with the URL of
        /// that document. This is how a client such as <c>dotnet odata-mcp start</c> discovers how to sign in
        /// without being told.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <param name="configure">Configures what the API publishes about itself.</param>
        /// <returns>
        /// The service collection.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="services"/> or <paramref name="configure"/> is <see langword="null"/>.</exception>
        /// <example>
        /// <code>
        /// builder.Services.AddProtectedResourceMetadata(options =&gt;
        /// {
        ///     options.AuthorizationServers.Add(new Uri("https://login.microsoftonline.com/contoso.com/v2.0"));
        ///     options.ScopesSupported.Add("api://contoso-odata/Data.Read");
        /// });
        ///
        /// // GET /.well-known/oauth-protected-resource now answers 200, anonymously.
        /// // The protected resource is the application root.
        /// </code>
        /// </example>
        /// <remarks>
        /// Independent of <see cref="AddODataMcp(IServiceCollection)"/>: an API that hosts no MCP server at all
        /// still benefits, because the discovery this feeds is the client's, not ours. The default route base
        /// is the application root. Set <see cref="ProtectedResourceMetadataOptions.Prefixes"/> to any route
        /// base Endpoint Routing or Minimal APIs accept, including an empty string, when the resource is not
        /// the whole host.
        /// <para>
        /// The middleware is inserted at the front of the pipeline through an <c>IStartupFilter</c>, which is
        /// what keeps the metadata document anonymously readable. There is no <c>Use</c> call to add. Calling
        /// this more than once adds the extra configuration and registers the filter once.
        /// </para>
        /// </remarks>
        public static IServiceCollection AddProtectedResourceMetadata(this IServiceCollection services, Action<ProtectedResourceMetadataOptions> configure)
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(configure);

            services.AddOptions<ProtectedResourceMetadataOptions>().Configure(configure);
            services.TryAddEnumerable(ServiceDescriptor.Transient<IStartupFilter, ProtectedResourceMetadataStartupFilter>());

            return services;
        }

        #endregion

    }

}
