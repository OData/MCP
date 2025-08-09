using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace Microsoft.OData.Mcp.Core.Routing
{

    /// <summary>
    /// Efficiently matches MCP routes to their corresponding OData endpoints.
    /// </summary>
    /// <remarks>
    /// This matcher is optimized for startup-time registration and runtime lookups
    /// using frozen collections for maximum performance.
    /// </remarks>
    public sealed class McpRouteMatcher
    {

        #region Fields

        internal readonly FrozenDictionary<string, McpRouteEntry> _routesByODataPrefix;
        internal readonly FrozenDictionary<string, McpRouteEntry> _routesByMcpPath;
        internal readonly McpRouteEntry? _rootRoute;

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="McpRouteMatcher"/> class.
        /// </summary>
        /// <param name="routes">The routes to register.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="routes"/> is null.</exception>
        public McpRouteMatcher(IEnumerable<McpRouteEntry> routes)
        {
            ArgumentNullException.ThrowIfNull(routes);

            var routeList = routes.ToList();
            
            // Build lookup dictionaries
            var odataLookup = new Dictionary<string, McpRouteEntry>(StringComparer.OrdinalIgnoreCase);
            var mcpLookup = new Dictionary<string, McpRouteEntry>(StringComparer.OrdinalIgnoreCase);

            foreach (var route in routeList)
            {
                // Normalize OData prefix (remove leading/trailing slashes)
                var normalizedODataPrefix = NormalizeRoutePath(route.ODataRoutePrefix);
                
                if (string.IsNullOrEmpty(normalizedODataPrefix))
                {
                    _rootRoute = route;
                }
                else
                {
                    odataLookup[normalizedODataPrefix] = route;
                }

                // Normalize MCP path
                var normalizedMcpPath = NormalizeRoutePath(route.McpBasePath);
                mcpLookup[normalizedMcpPath] = route;
            }

            _routesByODataPrefix = odataLookup.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);
            _routesByMcpPath = mcpLookup.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Attempts to match a request path to an MCP route.
        /// </summary>
        /// <param name="path">The request path.</param>
        /// <param name="route">The matched route entry.</param>
        /// <param name="mcpCommand">The MCP command extracted from the path.</param>
        /// <returns>True if a route was matched; otherwise, false.</returns>
        public bool TryMatch(string path, [NotNullWhen(true)] out McpRouteEntry? route, out McpCommand mcpCommand)
        {
            route = null;
            mcpCommand = McpCommand.Unknown;

            if (string.IsNullOrEmpty(path))
            {
                return false;
            }

            var parser = new SpanRouteParser(path.AsSpan());
            
            // Check if this is an MCP route
            if (!parser.TryParseMcpRoute(out var odataRoute, out _))
            {
                return false;
            }

            // Get the MCP command
            parser.TryGetMcpCommand(out mcpCommand);

            // Find the matching route entry
            if (odataRoute.IsEmpty)
            {
                // Root route
                route = _rootRoute;
            }
            else
            {
                // Look up by OData prefix
                var odataPrefix = odataRoute.ToString();
                _routesByODataPrefix.TryGetValue(odataPrefix, out route);
            }

            return route != null;
        }

        /// <summary>
        /// Gets a route entry by its OData prefix.
        /// </summary>
        /// <param name="odataPrefix">The OData route prefix.</param>
        /// <param name="route">The route entry if found.</param>
        /// <returns>True if the route was found; otherwise, false.</returns>
        public bool TryGetRouteByODataPrefix(string odataPrefix, [NotNullWhen(true)] out McpRouteEntry? route)
        {
            var normalized = NormalizeRoutePath(odataPrefix);
            
            if (string.IsNullOrEmpty(normalized))
            {
                route = _rootRoute;
                return route != null;
            }

            return _routesByODataPrefix.TryGetValue(normalized, out route);
        }

        /// <summary>
        /// Gets all registered routes.
        /// </summary>
        /// <returns>A collection of all registered route entries.</returns>
        public IEnumerable<McpRouteEntry> GetAllRoutes()
        {
            if (_rootRoute != null)
            {
                yield return _rootRoute;
            }

            foreach (var route in _routesByODataPrefix.Values)
            {
                yield return route;
            }
        }

        /// <summary>
        /// Builds the MCP URL for a given OData route.
        /// </summary>
        /// <param name="odataPrefix">The OData route prefix.</param>
        /// <param name="command">The MCP command (optional).</param>
        /// <returns>The MCP URL, or null if the route is not found.</returns>
        public string? BuildMcpUrl(string odataPrefix, McpCommand? command = null)
        {
            if (!TryGetRouteByODataPrefix(odataPrefix, out var route))
            {
                return null;
            }

            var basePath = route.McpBasePath.TrimEnd('/');
            
            return command switch
            {
                McpCommand.Info or null => basePath,
                McpCommand.Tools => $"{basePath}/tools",
                McpCommand.ToolsExecute => $"{basePath}/tools/execute",
                _ => basePath
            };
        }

        #endregion

        #region Internal Methods

        /// <summary>
        /// Normalizes a route path by removing leading and trailing slashes.
        /// </summary>
        /// <param name="path">The path to normalize.</param>
        /// <returns>The normalized path.</returns>
        internal static string NormalizeRoutePath(string? path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return string.Empty;
            }

            return path.Trim('/');
        }

        #endregion

    }

}
