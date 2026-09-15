// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.OData.Mcp.AspNetCore.Hosting;

namespace Microsoft.AspNetCore.Builder
{

    /// <summary>
    /// Maps OData MCP endpoints onto the ASP.NET Core pipeline.
    /// </summary>
    public static class ODataMcp_AspNetCore_ApplicationBuilderExtensions
    {

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
        /// </remarks>
        public static IApplicationBuilder UseODataMcp(this IApplicationBuilder app)
        {
            ArgumentNullException.ThrowIfNull(app);

            var pipeline = app.ApplicationServices.GetRequiredService<ODataMcpPipeline>();
            pipeline.IsEnabled = true;

            var factory = app.ApplicationServices.GetRequiredService<ODataMcpSessionFactory>();
            factory.Rebuild();
            var options = app.ApplicationServices.GetRequiredService<IOptions<ODataMcpHostOptions>>().Value;

            app.UseEndpoints(endpoints =>
            {
                foreach (var prefix in factory.Sessions.Keys)
                {
                    var pattern = string.IsNullOrEmpty(prefix) ? "/mcp" : $"/{prefix.Trim('/')}/mcp";
                    var group = endpoints.MapGroup(pattern);
                    ApplyHostConventions(group, options);
                    group.MapMcp(string.Empty);
                }
            });

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

        #endregion

    }

}
