// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using Microsoft.OData.Edm;
using Microsoft.OData.Mcp.Core.Catalog;

namespace Microsoft.OData.Mcp.AspNetCore.Hosting
{

    /// <summary>
    /// Host options for <c>AddODataMcp</c>.
    /// </summary>
    public sealed class ODataMcpHostOptions
    {

        #region Properties

        /// <summary>
        /// Gets the catalog options applied to every enabled OData route.
        /// </summary>
        public ODataMcpCatalogOptions Catalog { get; } = new();

        /// <summary>
        /// Gets prefixes that must not be exposed over MCP, matched without regard to case or leading slashes.
        /// Applied after <see cref="IncludePrefixes"/>.
        /// </summary>
        public List<string> ExcludeRoutes { get; } = [];

        /// <summary>
        /// Gets routes registered explicitly when endpoint discovery cannot see an <see cref="IEdmModel"/>.
        /// Discovered routes with the same prefix replace these entries.
        /// </summary>
        public List<ODataMcpRouteBinding> ExplicitRoutes { get; } = [];

        /// <summary>
        /// Gets prefixes that should be exposed over MCP. When empty, every discovered (and explicit) prefix is enabled.
        /// When non-empty, only matching prefixes are enabled. Prefer this over an OData-version-specific helper.
        /// </summary>
        /// <example>
        /// <code>
        /// builder.Services.AddODataMcp(options =>
        /// {
        ///     options.IncludePrefixes.Add("odata");
        /// });
        /// </code>
        /// </example>
        public List<string> IncludePrefixes { get; } = [];

        /// <summary>
        /// Gets or sets the ASP.NET Core rate-limiting policy name attached to each MCP endpoint.
        /// When null, the endpoint still participates in a global limiter configured with <c>UseRateLimiter</c>.
        /// </summary>
        /// <example>
        /// <code>
        /// builder.Services.AddRateLimiter(options =>
        /// {
        ///     options.AddFixedWindowLimiter("mcp", limiter =>
        ///     {
        ///         limiter.PermitLimit = 30;
        ///         limiter.Window = TimeSpan.FromMinutes(1);
        ///     });
        /// });
        /// builder.Services.AddODataMcp(options =>
        /// {
        ///     options.RateLimitingPolicyName = "mcp";
        /// });
        /// </code>
        /// </example>
        public string? RateLimitingPolicyName { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether MCP endpoints require an authenticated user.
        /// When <c>true</c>, the mapped MCP endpoints call <c>RequireAuthorization</c> and participate
        /// in the host's authentication and authorization pipeline.
        /// </summary>
        public bool RequireAuthorization { get; set; }

        #endregion

        #region Public Methods

        /// <summary>
        /// Registers an explicit <paramref name="prefix"/> and <paramref name="model"/> when the routing table
        /// does not carry an <see cref="IEdmModel"/> (for example a custom host that never wrote OData metadata).
        /// </summary>
        /// <param name="prefix">The OData route prefix, for example <c>odata</c>. Empty is the application root.</param>
        /// <param name="model">The EdmLib model for that prefix.</param>
        /// <returns>
        /// The same options instance.
        /// </returns>
        /// <example>
        /// <code>
        /// builder.Services.AddODataMcp(options =>
        /// {
        ///     options.AddRoute("odata", GetEdmModel());
        /// });
        /// </code>
        /// </example>
        /// <remarks>
        /// Discovery from <c>EndpointDataSource</c> still runs and wins when it finds the same prefix.
        /// </remarks>
        public ODataMcpHostOptions AddRoute(string prefix, IEdmModel model)
        {
            ArgumentNullException.ThrowIfNull(prefix);
            ArgumentNullException.ThrowIfNull(model);

            ExplicitRoutes.Add(new ODataMcpRouteBinding
            {
                Model = model,
                Prefix = prefix
            });

            return this;
        }

        #endregion

    }

}
