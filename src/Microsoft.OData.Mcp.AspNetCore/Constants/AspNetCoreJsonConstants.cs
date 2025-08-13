// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System.Text.Json;
using Microsoft.OData.Mcp.Core.Constants;

namespace Microsoft.OData.Mcp.AspNetCore.Constants
{

    /// <summary>
    /// Provides centralized JsonSerializerOptions instances specific to ASP.NET Core scenarios.
    /// </summary>
    /// <remarks>
    /// These options extend the core JsonConstants with ASP.NET Core specific configurations.
    /// All instances are thread-safe and designed for reuse to minimize memory allocations.
    /// </remarks>
    public static class AspNetCoreJsonConstants
    {

        #region Fields

        /// <summary>
        /// JSON serialization options for standard API responses.
        /// </summary>
        /// <remarks>
        /// Delegates to the Core ApiResponse options for consistency.
        /// Use this for all HTTP API responses in ASP.NET Core middleware and controllers.
        /// </remarks>
        public static readonly JsonSerializerOptions ApiResponse = JsonConstants.ApiResponse;

        /// <summary>
        /// JSON serialization options for error responses with camelCase naming.
        /// </summary>
        /// <remarks>
        /// Use this specifically for error response formatting in middleware.
        /// Maintains consistency with standard API responses while being explicit about error handling.
        /// </remarks>
        public static readonly JsonSerializerOptions ErrorResponse = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        };

        /// <summary>
        /// JSON serialization options for health check responses.
        /// </summary>
        /// <remarks>
        /// Optimized for health check endpoints that need to be fast and lightweight.
        /// Uses compact formatting to minimize response size.
        /// </remarks>
        public static readonly JsonSerializerOptions HealthCheck = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        };

        /// <summary>
        /// JSON serialization options for MCP protocol responses.
        /// </summary>
        /// <remarks>
        /// Specifically configured for Model Context Protocol responses.
        /// Ensures compatibility with MCP clients expecting camelCase properties.
        /// </remarks>
        public static readonly JsonSerializerOptions McpResponse = ApiResponse;

        #endregion

    }

}