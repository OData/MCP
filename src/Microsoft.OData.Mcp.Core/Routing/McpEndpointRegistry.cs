using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace Microsoft.OData.Mcp.Core.Routing
{

    /// <summary>
    /// Default implementation of the MCP endpoint registry.
    /// </summary>
    /// <remarks>
    /// This implementation is thread-safe and optimized for concurrent access during
    /// application startup when multiple OData routes may be registered simultaneously.
    /// </remarks>
    public class McpEndpointRegistry : IMcpEndpointRegistry
    {

        #region Fields

        internal readonly ConcurrentDictionary<string, McpRouteEntry> _routesByName;
        internal McpRouteMatcher? _routeMatcher;
        internal readonly object _matcherLock = new();

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="McpEndpointRegistry"/> class.
        /// </summary>
        public McpEndpointRegistry()
        {
            _routesByName = new ConcurrentDictionary<string, McpRouteEntry>(StringComparer.OrdinalIgnoreCase);
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Registers an MCP endpoint.
        /// </summary>
        /// <param name="route">The route entry to register.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="route"/> is null.</exception>
        public void Register(McpRouteEntry route)
        {
            ArgumentNullException.ThrowIfNull(route);

            _routesByName.AddOrUpdate(route.RouteName, route, (key, existing) =>
            {
                // If explicitly registering over an automatic registration, prefer the explicit one
                if (route.IsExplicit && !existing.IsExplicit)
                {
                    return route;
                }
                return existing;
            });

            // Invalidate the matcher cache
            lock (_matcherLock)
            {
                _routeMatcher = null;
            }
        }

        /// <summary>
        /// Attempts to get an endpoint by path.
        /// </summary>
        /// <param name="path">The request path.</param>
        /// <param name="route">The matched route entry.</param>
        /// <param name="command">The MCP command.</param>
        /// <returns>True if an endpoint was found; otherwise, false.</returns>
        public bool TryGetEndpoint(string path, [NotNullWhen(true)] out McpRouteEntry? route, out McpCommand command)
        {
            var matcher = GetOrCreateMatcher();
            return matcher.TryMatch(path, out route, out command);
        }

        /// <summary>
        /// Gets all registered endpoints.
        /// </summary>
        /// <returns>A collection of all registered endpoints.</returns>
        public IEnumerable<McpRouteEntry> GetAllEndpoints()
        {
            return _routesByName.Values.ToList();
        }

        /// <summary>
        /// Checks if a route has an MCP endpoint registered.
        /// </summary>
        /// <param name="routeName">The OData route name.</param>
        /// <returns>True if the route has an MCP endpoint; otherwise, false.</returns>
        public bool HasEndpoint(string routeName)
        {
            if (string.IsNullOrWhiteSpace(routeName))
            {
                return false;
            }

            return _routesByName.ContainsKey(routeName);
        }

        /// <summary>
        /// Gets the MCP URL for a given OData route.
        /// </summary>
        /// <param name="routeName">The OData route name.</param>
        /// <param name="command">The MCP command (optional).</param>
        /// <returns>The MCP URL, or null if not found.</returns>
        public string? GetMcpUrl(string routeName, McpCommand? command = null)
        {
            if (string.IsNullOrWhiteSpace(routeName))
            {
                return null;
            }

            if (!_routesByName.TryGetValue(routeName, out var route))
            {
                return null;
            }

            var matcher = GetOrCreateMatcher();
            return matcher.BuildMcpUrl(route.ODataRoutePrefix, command);
        }

        #endregion

        #region Internal Methods

        /// <summary>
        /// Gets or creates the route matcher.
        /// </summary>
        /// <returns>The route matcher.</returns>
        internal McpRouteMatcher GetOrCreateMatcher()
        {
            lock (_matcherLock)
            {
                if (_routeMatcher == null)
                {
                    var routes = _routesByName.Values.ToList();
                    _routeMatcher = new McpRouteMatcher(routes);
                }
                return _routeMatcher;
            }
        }

        #endregion

    }

}
