// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

namespace Microsoft.OData.Mcp.Core.Configuration
{
    /// <summary>
    /// Defines the cache provider types.
    /// </summary>
    /// <remarks>
    /// Cache provider types determine the underlying storage mechanism
    /// and distribution characteristics of the caching system. Each type
    /// offers different trade-offs between performance, persistence,
    /// and scalability.
    /// </remarks>
    public enum CacheProviderType
    {
        /// <summary>
        /// In-memory cache within the application process.
        /// </summary>
        /// <remarks>
        /// Memory caching provides the fastest access times but is limited
        /// to a single application instance and is not persistent across
        /// application restarts. Best suited for single-instance deployments.
        /// </remarks>
        Memory,

        /// <summary>
        /// Distributed cache shared across multiple application instances.
        /// </summary>
        /// <remarks>
        /// Distributed caching enables cache sharing between multiple server
        /// instances, providing consistency and improved cache hit rates in
        /// multi-instance deployments. Requires additional infrastructure.
        /// </remarks>
        Distributed,

        /// <summary>
        /// Redis-based distributed cache.
        /// </summary>
        /// <remarks>
        /// Redis provides high-performance distributed caching with advanced
        /// features like persistence, clustering, and pub/sub capabilities.
        /// Ideal for high-scale production environments.
        /// </remarks>
        Redis,

        /// <summary>
        /// SQL Server-based distributed cache.
        /// </summary>
        /// <remarks>
        /// SQL Server caching leverages existing database infrastructure
        /// for distributed caching. Provides persistence and consistency
        /// but may have higher latency than in-memory alternatives.
        /// </remarks>
        SqlServer,

        /// <summary>
        /// Custom cache provider implementation.
        /// </summary>
        /// <remarks>
        /// Custom providers allow integration with proprietary or specialized
        /// caching systems. Requires implementing the appropriate cache
        /// provider interfaces.
        /// </remarks>
        Custom
    }
}