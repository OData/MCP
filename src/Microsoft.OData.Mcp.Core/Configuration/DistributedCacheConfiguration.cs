// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;

namespace Microsoft.OData.Mcp.Core.Configuration
{

    /// <summary>
    /// Configuration for distributed caching.
    /// </summary>
    /// <remarks>
    /// Distributed cache configuration specifies how the MCP server
    /// connects to and uses distributed caching services like Redis
    /// or SQL Server for sharing cache data across multiple instances.
    /// </remarks>
    public sealed class DistributedCacheConfiguration
    {

        #region Properties

        /// <summary>
        /// Gets or sets the connection string for the distributed cache.
        /// </summary>
        /// <value>The connection string used to connect to the distributed cache service.</value>
        /// <remarks>
        /// The format of the connection string depends on the cache provider type.
        /// For Redis, this would be a Redis connection string. For SQL Server,
        /// this would be a SQL Server connection string.
        /// </remarks>
        /// <example>
        /// For Redis: "localhost:6379"
        /// For SQL Server: "Server=(localdb)\\mssqllocaldb;Database=DistCache;Trusted_Connection=true;"
        /// </example>
        public string? ConnectionString { get; set; }

        /// <summary>
        /// Gets or sets the instance name for the distributed cache.
        /// </summary>
        /// <value>A unique name identifying this cache instance.</value>
        /// <remarks>
        /// The instance name is used to create unique cache keys and separate
        /// cache data between different applications or environments sharing
        /// the same distributed cache infrastructure.
        /// </remarks>
        public string? InstanceName { get; set; }

        /// <summary>
        /// Gets or sets the default sliding expiration for distributed cache entries.
        /// </summary>
        /// <value>The time span that cache entries remain valid after their last access.</value>
        /// <remarks>
        /// Sliding expiration resets the expiration time each time an entry is accessed,
        /// keeping frequently used items in the cache longer. If not specified,
        /// entries will use absolute expiration only.
        /// </remarks>
        public TimeSpan? DefaultSlidingExpiration { get; set; }

        /// <summary>
        /// Gets or sets the default absolute expiration for distributed cache entries.
        /// </summary>
        /// <value>The maximum time span that cache entries remain valid regardless of access.</value>
        /// <remarks>
        /// Absolute expiration ensures that entries are removed from the cache
        /// after a fixed period, regardless of how frequently they are accessed.
        /// This is useful for ensuring data freshness.
        /// </remarks>
        public TimeSpan? DefaultAbsoluteExpiration { get; set; }

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="DistributedCacheConfiguration"/> class.
        /// </summary>
        public DistributedCacheConfiguration()
        {
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Validates the distributed cache configuration.
        /// </summary>
        /// <returns>A collection of validation errors, or empty if the configuration is valid.</returns>
        public IEnumerable<string> Validate()
        {
            var errors = new List<string>();

            if (DefaultSlidingExpiration.HasValue && DefaultSlidingExpiration.Value < TimeSpan.Zero)
            {
                errors.Add("DefaultSlidingExpiration cannot be negative");
            }

            if (DefaultAbsoluteExpiration.HasValue && DefaultAbsoluteExpiration.Value < TimeSpan.Zero)
            {
                errors.Add("DefaultAbsoluteExpiration cannot be negative");
            }

            if (DefaultSlidingExpiration.HasValue && DefaultAbsoluteExpiration.HasValue &&
                DefaultSlidingExpiration.Value >= DefaultAbsoluteExpiration.Value)
            {
                errors.Add("DefaultSlidingExpiration must be less than DefaultAbsoluteExpiration");
            }

            return errors;
        }

        /// <summary>
        /// Creates a copy of this configuration.
        /// </summary>
        /// <returns>A new instance with the same settings.</returns>
        public DistributedCacheConfiguration Clone()
        {
            return new DistributedCacheConfiguration
            {
                ConnectionString = ConnectionString,
                InstanceName = InstanceName,
                DefaultSlidingExpiration = DefaultSlidingExpiration,
                DefaultAbsoluteExpiration = DefaultAbsoluteExpiration
            };
        }

        /// <summary>
        /// Merges another configuration into this one, with the other configuration taking precedence.
        /// </summary>
        /// <param name="other">The configuration to merge into this one.</param>
        /// <remarks>
        /// Only non-null and non-empty values from the other configuration will
        /// override values in this configuration. This allows for partial updates
        /// without losing existing settings.
        /// </remarks>
        public void MergeWith(DistributedCacheConfiguration other)
        {
            if (other is null) return;

            if (!string.IsNullOrWhiteSpace(other.ConnectionString)) 
                ConnectionString = other.ConnectionString;
            
            if (!string.IsNullOrWhiteSpace(other.InstanceName)) 
                InstanceName = other.InstanceName;
            
            DefaultSlidingExpiration = other.DefaultSlidingExpiration ?? DefaultSlidingExpiration;
            DefaultAbsoluteExpiration = other.DefaultAbsoluteExpiration ?? DefaultAbsoluteExpiration;
        }

        /// <summary>
        /// Creates a configuration for Redis distributed caching.
        /// </summary>
        /// <param name="connectionString">The Redis connection string.</param>
        /// <param name="instanceName">The cache instance name.</param>
        /// <returns>A distributed cache configuration for Redis.</returns>
        public static DistributedCacheConfiguration ForRedis(string connectionString, string instanceName)
        {
            return new DistributedCacheConfiguration
            {
                ConnectionString = connectionString,
                InstanceName = instanceName,
                DefaultSlidingExpiration = TimeSpan.FromMinutes(20),
                DefaultAbsoluteExpiration = TimeSpan.FromHours(2)
            };
        }

        /// <summary>
        /// Creates a configuration for SQL Server distributed caching.
        /// </summary>
        /// <param name="connectionString">The SQL Server connection string.</param>
        /// <param name="instanceName">The cache instance name.</param>
        /// <returns>A distributed cache configuration for SQL Server.</returns>
        public static DistributedCacheConfiguration ForSqlServer(string connectionString, string instanceName)
        {
            return new DistributedCacheConfiguration
            {
                ConnectionString = connectionString,
                InstanceName = instanceName,
                DefaultSlidingExpiration = TimeSpan.FromMinutes(30),
                DefaultAbsoluteExpiration = TimeSpan.FromHours(4)
            };
        }

        #endregion

    }

}
