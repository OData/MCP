// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

namespace Microsoft.OData.Mcp.Core.Tools
{

    /// <summary>
    /// Defines the operation types for MCP tools.
    /// </summary>
    public enum McpToolOperationType
    {

        /// <summary>
        /// Read operation that retrieves data without modification.
        /// </summary>
        Read,

        /// <summary>
        /// Create operation that adds new data.
        /// </summary>
        Create,

        /// <summary>
        /// Update operation that modifies existing data.
        /// </summary>
        Update,

        /// <summary>
        /// Delete operation that removes data.
        /// </summary>
        Delete,

        /// <summary>
        /// Query operation that searches and filters data.
        /// </summary>
        Query,

        /// <summary>
        /// Navigate operation that traverses relationships.
        /// </summary>
        Navigate,

        /// <summary>
        /// Batch operation that processes multiple items.
        /// </summary>
        Batch,

        /// <summary>
        /// Custom operation with specific business logic.
        /// </summary>
        Custom

    }

}
