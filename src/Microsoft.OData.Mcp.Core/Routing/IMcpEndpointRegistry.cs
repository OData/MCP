// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace Microsoft.OData.Mcp.Core.Routing
{

    /// <summary>
    /// Manages the registration and discovery of MCP endpoints.
    /// </summary>
    public interface IMcpEndpointRegistry
    {

        /// <summary>
        /// Registers an MCP endpoint.
        /// </summary>
        /// <param name="route">The route entry to register.</param>
        void Register(McpRouteEntry route);

        /// <summary>
        /// Attempts to get an endpoint by path.
        /// </summary>
        /// <param name="path">The request path.</param>
        /// <param name="route">The matched route entry.</param>
        /// <param name="command">The MCP command.</param>
        /// <returns>True if an endpoint was found; otherwise, false.</returns>
        bool TryGetEndpoint(string path, [NotNullWhen(true)] out McpRouteEntry? route, out McpCommand command);

        /// <summary>
        /// Gets all registered endpoints.
        /// </summary>
        /// <returns>A collection of all registered endpoints.</returns>
        IEnumerable<McpRouteEntry> GetAllEndpoints();

        /// <summary>
        /// Checks if a route has an MCP endpoint registered.
        /// </summary>
        /// <param name="routeName">The OData route name.</param>
        /// <returns>True if the route has an MCP endpoint; otherwise, false.</returns>
        bool HasEndpoint(string routeName);

        /// <summary>
        /// Gets the MCP URL for a given OData route.
        /// </summary>
        /// <param name="routeName">The OData route name.</param>
        /// <param name="command">The MCP command (optional).</param>
        /// <returns>The MCP URL, or null if not found.</returns>
        string? GetMcpUrl(string routeName, McpCommand? command = null);

    }

}
