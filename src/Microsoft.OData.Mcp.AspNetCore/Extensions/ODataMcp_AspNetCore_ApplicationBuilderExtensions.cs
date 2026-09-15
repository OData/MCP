// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using System.Diagnostics;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.OData.Mcp.AspNetCore.Hosting;

namespace Microsoft.AspNetCore.Builder
{

    /// <summary>
    /// Maps OData MCP endpoints onto the ASP.NET Core pipeline.
    /// </summary>
    public static class ODataMcp_AspNetCore_ApplicationBuilderExtensions
    {

        #region Fields

        /// <summary>
        /// Message written when discovery found no OData prefixes after reading the application,
        /// DI, and a last-resort endpoint flush.
        /// </summary>
        internal const string NoODataRoutesWarning = "UseODataMcp found no OData routes on the WebApplication or in DI. Map OData or Restier before UseODataMcp, or register ExplicitRoutes.";

        #endregion

        #region Public Methods

        /// <summary>
        /// Maps official MCP HTTP endpoints at <c>{prefix}/mcp</c> for each OData prefix
        /// enabled by <c>AddODataMcp</c>. Call this after OData routes are mapped
        /// (<c>MapControllers</c>, <c>MapODataRoute</c>, or Restier <c>MapApiRoute</c>).
        /// </summary>
        /// <param name="app">The application builder.</param>
        /// <returns>
        /// The application builder.
        /// </returns>
        /// <example>
        /// <code>
        /// builder.Services.AddControllers()
        ///     .AddOData(options => options.AddRouteComponents("odata", GetEdmModel()));
        /// builder.Services.AddODataMcp();
        ///
        /// var app = builder.Build();
        /// app.UseRouting();
        /// app.MapControllers();
        /// app.UseODataMcp();
        /// </code>
        /// </example>
        /// <remarks>
        /// <c>AddODataMcp</c> registers services only. This method turns MCP on. Do not call
        /// the MCP SDK <c>MapMcp</c> yourself; this method invokes it per enabled prefix.
        /// Discovery reads the <c>WebApplication</c> data sources, then DI, each source once.
        /// An empty <c>UseEndpoints</c> flush runs only when those collections are empty.
        /// </remarks>
        public static IApplicationBuilder UseODataMcp(this IApplicationBuilder app)
        {
            ArgumentNullException.ThrowIfNull(app);

            var pipeline = app.ApplicationServices.GetRequiredService<ODataMcpPipeline>();
            pipeline.IsEnabled = true;

            var factory = app.ApplicationServices.GetRequiredService<ODataMcpSessionFactory>();
            var routeBuilder = app as IEndpointRouteBuilder;
            var appSources = routeBuilder?.DataSources;

            factory.Rebuild(appSources);
            if (factory.Sessions.Count == 0 && (appSources is null || appSources.Count == 0))
            {
                app.UseEndpoints(_ => { });
                factory.Rebuild(routeBuilder?.DataSources);
            }

            if (factory.Sessions.Count == 0)
            {
                WarnNoODataRoutes(app);

                return app;
            }

            var options = app.ApplicationServices.GetRequiredService<IOptions<ODataMcpHostOptions>>().Value;
            app.UseEndpoints(endpoints => MapODataMcpEndpoints(endpoints, factory, options));

            return app;
        }

        #endregion

        #region Internal Methods

        /// <summary>
        /// Attaches optional authorization and named rate-limiting policies. A global
        /// <c>UseRateLimiter</c> limiter still applies when no policy name is set.
        /// </summary>
        /// <param name="builder">The MCP endpoint builder.</param>
        /// <param name="options">Host options.</param>
        /// <returns>
        /// The same builder.
        /// </returns>
        internal static IEndpointConventionBuilder ApplyHostConventions(IEndpointConventionBuilder builder, ODataMcpHostOptions options)
        {
            ArgumentNullException.ThrowIfNull(builder);
            ArgumentNullException.ThrowIfNull(options);

            if (!string.IsNullOrWhiteSpace(options.RateLimitingPolicyName))
            {
                builder.RequireRateLimiting(options.RateLimitingPolicyName);
            }

            if (options.RequireAuthorization)
            {
                builder.RequireAuthorization();
            }

            return builder;
        }

        /// <summary>
        /// Maps <c>{prefix}/mcp</c> for each discovered session.
        /// </summary>
        /// <param name="endpoints">The endpoint route builder.</param>
        /// <param name="factory">The session factory whose keys are the OData prefixes.</param>
        /// <param name="options">Host conventions.</param>
        internal static void MapODataMcpEndpoints(IEndpointRouteBuilder endpoints, ODataMcpSessionFactory factory, ODataMcpHostOptions options)
        {
            ArgumentNullException.ThrowIfNull(endpoints);
            ArgumentNullException.ThrowIfNull(factory);
            ArgumentNullException.ThrowIfNull(options);

            foreach (var prefix in factory.Sessions.Keys)
            {
                var pattern = string.IsNullOrEmpty(prefix) ? "/mcp" : $"/{prefix.Trim('/')}/mcp";
                var group = endpoints.MapGroup(pattern);
                ApplyHostConventions(group, options);
                group.MapMcp(string.Empty);
            }
        }

        /// <summary>
        /// Writes a debug warning when no OData prefixes could be discovered.
        /// </summary>
        /// <param name="app">The application builder, used to resolve an optional logger.</param>
        internal static void WarnNoODataRoutes(IApplicationBuilder app)
        {
            ArgumentNullException.ThrowIfNull(app);

            Debug.WriteLine(NoODataRoutesWarning);
            app.ApplicationServices.GetService<ILoggerFactory>()
                ?.CreateLogger("Microsoft.OData.Mcp.AspNetCore")
                .LogWarning(NoODataRoutesWarning);
        }

        #endregion

    }

}
