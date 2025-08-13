// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System.Text.Json;
using System.Text.Json.Serialization;

namespace Microsoft.OData.Mcp.Core.Constants
{

    /// <summary>
    /// Provides centralized, reusable JsonSerializerOptions instances to improve memory efficiency and performance.
    /// </summary>
    /// <remarks>
    /// Creating new JsonSerializerOptions instances is expensive as each instance creates internal caches and converters.
    /// By reusing these static instances, we significantly reduce memory allocations and improve performance.
    /// These options are thread-safe and can be used concurrently across the application.
    /// </remarks>
    public static class JsonConstants
    {

        #region Fields

        /// <summary>
        /// Default JSON serialization options for general use.
        /// </summary>
        /// <remarks>
        /// Use this for standard serialization/deserialization without special formatting requirements.
        /// This is the most memory-efficient option as it uses default settings.
        /// </remarks>
        public static readonly JsonSerializerOptions Default = new();

        /// <summary>
        /// JSON serialization options with indented formatting for human-readable output.
        /// </summary>
        /// <remarks>
        /// Use this when the JSON output needs to be human-readable (e.g., for debugging, logging, or API responses).
        /// This is the most commonly used pattern in the codebase.
        /// </remarks>
        public static readonly JsonSerializerOptions PrettyPrint = new()
        {
            WriteIndented = true
        };

        /// <summary>
        /// JSON serialization options for API responses with camelCase property naming and indentation.
        /// </summary>
        /// <remarks>
        /// Use this for REST API responses that follow JavaScript naming conventions.
        /// Commonly used in ASP.NET Core middleware and API endpoints.
        /// </remarks>
        public static readonly JsonSerializerOptions ApiResponse = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        };

        /// <summary>
        /// JSON deserialization options with case-insensitive property matching and indented output.
        /// </summary>
        /// <remarks>
        /// Use this when deserializing JSON from external sources where property casing may vary.
        /// The case-insensitive matching helps with compatibility across different systems.
        /// </remarks>
        public static readonly JsonSerializerOptions CaseInsensitive = new()
        {
            PropertyNameCaseInsensitive = true,
            WriteIndented = true
        };

        /// <summary>
        /// JSON serialization options optimized for minimal size (no indentation).
        /// </summary>
        /// <remarks>
        /// Use this when bandwidth or storage efficiency is critical.
        /// Produces compact JSON without unnecessary whitespace.
        /// </remarks>
        public static readonly JsonSerializerOptions Compact = new()
        {
            WriteIndented = false
        };

        /// <summary>
        /// JSON serialization options with support for reference handling to prevent circular references.
        /// </summary>
        /// <remarks>
        /// Use this when serializing object graphs that may contain circular references.
        /// Particularly useful for Entity Framework models or complex object hierarchies.
        /// </remarks>
        public static readonly JsonSerializerOptions WithReferenceHandling = new()
        {
            ReferenceHandler = ReferenceHandler.IgnoreCycles,
            WriteIndented = true
        };

        #endregion

    }

}