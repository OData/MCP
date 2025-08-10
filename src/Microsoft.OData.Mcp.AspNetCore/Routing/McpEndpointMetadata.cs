// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using Microsoft.OData.Mcp.Core.Routing;

namespace Microsoft.OData.Mcp.AspNetCore.Routing
{
    /// <summary>
    /// Metadata for MCP endpoints.
    /// </summary>
    public class McpEndpointMetadata
    {
        /// <summary>
        /// Gets or sets the MCP command type.
        /// </summary>
        public McpCommand Command { get; set; }

        /// <summary>
        /// Gets or sets the OData route name.
        /// </summary>
        public string? RouteName { get; set; }
    }
}
