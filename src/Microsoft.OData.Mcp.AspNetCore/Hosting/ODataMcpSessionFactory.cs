// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.OData.Mcp.AspNetCore.Adaptation;
using Microsoft.OData.Mcp.AspNetCore.Execution;
using Microsoft.OData.Mcp.Core.Catalog;

namespace Microsoft.OData.Mcp.AspNetCore.Hosting
{

    /// <summary>
    /// Builds one MCP session per enabled OData route prefix discovered from endpoint routing.
    /// </summary>
    public sealed class ODataMcpSessionFactory
    {

        #region Fields

        internal bool _discovered;

        internal readonly object _gate = new();

        internal readonly IOptions<ODataMcpHostOptions> _hostOptions;

        internal readonly McpHttpContextAccessor _httpContextAccessor;

        internal readonly ODataMcpPipeline _pipeline;

        internal readonly IServiceProvider _services;

        internal IReadOnlyDictionary<string, ODataMcpSession> _sessions =
            new Dictionary<string, ODataMcpSession>(StringComparer.OrdinalIgnoreCase);

        #endregion

        #region Properties

        /// <summary>
        /// Gets sessions keyed by OData route prefix. Discovery runs on first access when
        /// <c>UseODataMcp</c> has not already rebuilt after endpoints exist.
        /// </summary>
        public IReadOnlyDictionary<string, ODataMcpSession> Sessions
        {
            get
            {
                if (!_discovered)
                {
                    Rebuild();
                }

                return _sessions;
            }
        }

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="ODataMcpSessionFactory"/> class.
        /// </summary>
        /// <param name="services">The application service provider used to read <c>EndpointDataSource</c>.</param>
        /// <param name="hostOptions">Host catalog, include/exclude, and explicit routes.</param>
        /// <param name="pipeline">The captured application pipeline.</param>
        /// <param name="httpContextAccessor">The wrapping accessor that can Start/End the inner context.</param>
        public ODataMcpSessionFactory(
            IServiceProvider services,
            IOptions<ODataMcpHostOptions> hostOptions,
            ODataMcpPipeline pipeline,
            McpHttpContextAccessor httpContextAccessor)
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(hostOptions);
            ArgumentNullException.ThrowIfNull(pipeline);
            ArgumentNullException.ThrowIfNull(httpContextAccessor);

            _services = services;
            _hostOptions = hostOptions;
            _pipeline = pipeline;
            _httpContextAccessor = httpContextAccessor;
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Discovers routes from DI and optional application data sources, then rebuilds sessions.
        /// </summary>
        /// <param name="additionalSources">
        /// <c>WebApplication</c> data sources that may not be exported to DI yet. Pass <see langword="null"/>
        /// to use DI only.
        /// </param>
        public void Rebuild(IEnumerable<EndpointDataSource>? additionalSources = null)
        {
            lock (_gate)
            {
                var sessions = new Dictionary<string, ODataMcpSession>(StringComparer.OrdinalIgnoreCase);
                foreach (var binding in ODataMcpRouteDiscovery.Discover(_services, _hostOptions.Value, additionalSources))
                {
                    var catalogOptions = CopyCatalogOptions(_hostOptions.Value.Catalog, binding.Prefix);
                    var catalog = new ODataMcpCatalog(EdmModelAdapter.ToCoreModel(binding.Model), catalogOptions);
                    var executor = new InProcessODataExecutor(
                        _pipeline,
                        _httpContextAccessor,
                        _services.GetRequiredService<IServiceScopeFactory>(),
                        binding.Prefix);
                    sessions[binding.Prefix] = new ODataMcpSession(catalog, new ODataToolRuntime(catalog, executor), EdmModelAdapter.ToCsdlXml(binding.Model));
                }

                _sessions = sessions;
                _discovered = true;
            }
        }

        /// <summary>
        /// Resolves the session for the current HTTP path.
        /// </summary>
        /// <param name="http">The current HTTP context.</param>
        /// <returns>
        /// The matching session.
        /// </returns>
        public ODataMcpSession Resolve(HttpContext? http)
        {
            var sessions = Sessions;
            if (sessions.Count == 1)
            {
                return sessions.Values.First();
            }

            var path = http?.Request.Path.Value ?? string.Empty;
            foreach (var (prefix, session) in sessions)
            {
                var mcp = string.IsNullOrEmpty(prefix) ? "/mcp" : $"/{prefix.Trim('/')}/mcp";
                if (path.StartsWith(mcp, StringComparison.OrdinalIgnoreCase))
                {
                    return session;
                }
            }

            throw new InvalidOperationException($"No MCP catalog is registered for path '{path}'.");
        }

        #endregion

        #region Internal Methods

        /// <summary>
        /// Copies catalog options and sets the route name to the OData prefix.
        /// </summary>
        /// <param name="source">The host catalog options.</param>
        /// <param name="prefix">The route prefix.</param>
        /// <returns>
        /// A per-route copy.
        /// </returns>
        internal static ODataMcpCatalogOptions CopyCatalogOptions(ODataMcpCatalogOptions source, string prefix)
        {
            ArgumentNullException.ThrowIfNull(source);

            return new ODataMcpCatalogOptions
            {
                EnforceRequiredOnCreate = source.EnforceRequiredOnCreate,
                EnumJsonFormat = source.EnumJsonFormat,
                ExcludeEntitySets = [.. source.ExcludeEntitySets],
                IncludeCreate = source.IncludeCreate,
                IncludeDelete = source.IncludeDelete,
                IncludeEntitySets = [.. source.IncludeEntitySets],
                IncludeUpdate = source.IncludeUpdate,
                InstructionsPreface = source.InstructionsPreface,
                IsDynamicModel = source.IsDynamicModel,
                MaxCompletionValues = source.MaxCompletionValues,
                MaxExpandLength = source.MaxExpandLength,
                MaxFilterLength = source.MaxFilterLength,
                MaxNamedTools = source.MaxNamedTools,
                MaxRequestBodyBytes = source.MaxRequestBodyBytes,
                MaxResources = source.MaxResources,
                MaxResponseBytes = source.MaxResponseBytes,
                MaxSelectLength = source.MaxSelectLength,
                RouteName = string.IsNullOrWhiteSpace(prefix) ? "odata" : prefix.Trim('/')
            };
        }

        #endregion

    }

}
