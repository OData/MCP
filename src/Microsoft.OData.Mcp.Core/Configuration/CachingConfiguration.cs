using System;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.OData.Mcp.Core.Configuration
{
    /// <summary>
    /// Configuration for metadata and tool caching behavior.
    /// </summary>
    /// <remarks>
    /// Caching configuration controls how long metadata and generated tools are cached
    /// to improve performance and reduce load on the underlying OData service.
    /// This class provides comprehensive caching options including provider selection,
    /// TTL settings, size limits, and advanced features like compression and warming.
    /// </remarks>
    public sealed class CachingConfiguration
    {
        #region Properties

        /// <summary>
        /// Gets or sets a value indicating whether caching is enabled.
        /// </summary>
        /// <value><c>true</c> to enable caching; otherwise, <c>false</c>.</value>
        /// <remarks>
        /// When caching is disabled, metadata and tools will be regenerated for every request,
        /// which can impact performance but ensures the latest data is always used.
        /// </remarks>
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// Gets or sets the cache provider type.
        /// </summary>
        /// <value>The type of cache provider to use.</value>
        /// <remarks>
        /// Different cache providers offer different characteristics in terms of
        /// persistence, performance, and distributed caching capabilities.
        /// </remarks>
        public CacheProviderType ProviderType { get; set; } = CacheProviderType.Memory;

        /// <summary>
        /// Gets or sets the Time-To-Live (TTL) for metadata cache entries.
        /// </summary>
        /// <value>The duration to cache metadata before it expires.</value>
        /// <remarks>
        /// Metadata is typically stable and can be cached for longer periods.
        /// A shorter TTL ensures faster detection of schema changes.
        /// </remarks>
        public TimeSpan MetadataTtl { get; set; } = TimeSpan.FromHours(1);

        /// <summary>
        /// Gets or sets the Time-To-Live (TTL) for generated tools cache entries.
        /// </summary>
        /// <value>The duration to cache generated tools before they expire.</value>
        /// <remarks>
        /// Generated tools are derived from metadata and can be cached separately
        /// with different TTL values for performance optimization.
        /// </remarks>
        public TimeSpan ToolsTtl { get; set; } = TimeSpan.FromMinutes(30);

        /// <summary>
        /// Gets or sets the Time-To-Live (TTL) for query result cache entries.
        /// </summary>
        /// <value>The duration to cache query results before they expire.</value>
        /// <remarks>
        /// Query results represent actual data and should typically have shorter
        /// TTL values to ensure data freshness.
        /// </remarks>
        public TimeSpan QueryResultsTtl { get; set; } = TimeSpan.FromMinutes(5);

        /// <summary>
        /// Gets or sets the maximum size of the cache in megabytes.
        /// </summary>
        /// <value>The maximum cache size in MB, or null for no limit.</value>
        /// <remarks>
        /// Cache size limits prevent excessive memory usage. When the limit is reached,
        /// older entries will be evicted using the configured eviction policy.
        /// </remarks>
        public int? MaxSizeMb { get; set; } = 100;

        /// <summary>
        /// Gets or sets the maximum number of cache entries.
        /// </summary>
        /// <value>The maximum number of entries in the cache, or null for no limit.</value>
        /// <remarks>
        /// Entry count limits provide an alternative way to control cache size
        /// when individual entry sizes vary significantly.
        /// </remarks>
        public int? MaxEntries { get; set; } = 1000;

        /// <summary>
        /// Gets or sets the cache eviction policy.
        /// </summary>
        /// <value>The policy used to determine which entries to evict when cache limits are reached.</value>
        /// <remarks>
        /// Different eviction policies optimize for different access patterns
        /// and performance characteristics.
        /// </remarks>
        public CacheEvictionPolicy EvictionPolicy { get; set; } = CacheEvictionPolicy.LeastRecentlyUsed;

        /// <summary>
        /// Gets or sets the cache key prefix.
        /// </summary>
        /// <value>A prefix added to all cache keys to avoid collisions.</value>
        /// <remarks>
        /// Key prefixes are useful when multiple MCP server instances share
        /// the same cache infrastructure.
        /// </remarks>
        public string KeyPrefix { get; set; } = "mcp:";

        /// <summary>
        /// Gets or sets a value indicating whether to enable cache statistics collection.
        /// </summary>
        /// <value><c>true</c> to collect cache statistics; otherwise, <c>false</c>.</value>
        /// <remarks>
        /// Cache statistics provide insights into cache hit rates, evictions,
        /// and performance metrics for monitoring and optimization.
        /// </remarks>
        public bool EnableStatistics { get; set; } = true;

        /// <summary>
        /// Gets or sets a value indicating whether to enable cache warming.
        /// </summary>
        /// <value><c>true</c> to pre-populate the cache on startup; otherwise, <c>false</c>.</value>
        /// <remarks>
        /// Cache warming pre-populates frequently accessed data to improve
        /// initial response times after server startup.
        /// </remarks>
        public bool EnableWarming { get; set; } = false;

        /// <summary>
        /// Gets or sets the cache warming delay after startup.
        /// </summary>
        /// <value>The delay before starting cache warming operations.</value>
        /// <remarks>
        /// A startup delay allows the server to fully initialize before
        /// beginning resource-intensive cache warming operations.
        /// </remarks>
        public TimeSpan WarmingDelay { get; set; } = TimeSpan.FromSeconds(30);

        /// <summary>
        /// Gets or sets the distributed cache configuration.
        /// </summary>
        /// <value>Configuration for distributed caching across multiple server instances.</value>
        /// <remarks>
        /// Distributed caching enables cache sharing between multiple MCP server
        /// instances for improved consistency and performance.
        /// </remarks>
        public DistributedCacheConfiguration DistributedCache { get; set; } = new();

        /// <summary>
        /// Gets or sets the compression configuration for cached data.
        /// </summary>
        /// <value>Configuration for compressing cached data to save memory/storage.</value>
        /// <remarks>
        /// Cache compression can significantly reduce memory usage for large
        /// cached objects at the cost of additional CPU overhead.
        /// </remarks>
        public CacheCompressionConfiguration Compression { get; set; } = new();

        /// <summary>
        /// Gets or sets custom caching properties.
        /// </summary>
        /// <value>A dictionary of custom caching configuration values.</value>
        /// <remarks>
        /// Custom properties allow extending the configuration with cache provider-specific
        /// settings that don't fit into the standard configuration properties.
        /// </remarks>
        public Dictionary<string, object> CustomProperties { get; set; } = [];

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="CachingConfiguration"/> class.
        /// </summary>
        public CachingConfiguration()
        {
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Validates the caching configuration.
        /// </summary>
        /// <returns>A collection of validation errors, or empty if the configuration is valid.</returns>
        public IEnumerable<string> Validate()
        {
            var errors = new List<string>();

            if (MetadataTtl < TimeSpan.Zero)
            {
                errors.Add("MetadataTtl cannot be negative");
            }

            if (ToolsTtl < TimeSpan.Zero)
            {
                errors.Add("ToolsTtl cannot be negative");
            }

            if (QueryResultsTtl < TimeSpan.Zero)
            {
                errors.Add("QueryResultsTtl cannot be negative");
            }

            if (MaxSizeMb.HasValue && MaxSizeMb.Value <= 0)
            {
                errors.Add("MaxSizeMb must be greater than zero when specified");
            }

            if (MaxEntries.HasValue && MaxEntries.Value <= 0)
            {
                errors.Add("MaxEntries must be greater than zero when specified");
            }

            if (string.IsNullOrWhiteSpace(KeyPrefix))
            {
                errors.Add("KeyPrefix cannot be null or whitespace");
            }

            if (WarmingDelay < TimeSpan.Zero)
            {
                errors.Add("WarmingDelay cannot be negative");
            }

            // Validate distributed cache configuration
            var distributedErrors = DistributedCache.Validate();
            errors.AddRange(distributedErrors.Select(e => $"DistributedCache: {e}"));

            // Validate compression configuration
            var compressionErrors = Compression.Validate();
            errors.AddRange(compressionErrors.Select(e => $"Compression: {e}"));

            return errors;
        }

        /// <summary>
        /// Creates a configuration optimized for development environments.
        /// </summary>
        /// <returns>A caching configuration suitable for development.</returns>
        /// <remarks>
        /// Development configurations use shorter TTLs and smaller cache sizes
        /// to ensure faster detection of changes during development cycles.
        /// </remarks>
        public static CachingConfiguration ForDevelopment()
        {
            return new CachingConfiguration
            {
                Enabled = true,
                ProviderType = CacheProviderType.Memory,
                MetadataTtl = TimeSpan.FromMinutes(5),
                ToolsTtl = TimeSpan.FromMinutes(2),
                QueryResultsTtl = TimeSpan.FromMinutes(1),
                MaxSizeMb = 50,
                MaxEntries = 500,
                EnableStatistics = true,
                EnableWarming = false
            };
        }

        /// <summary>
        /// Creates a configuration optimized for production environments.
        /// </summary>
        /// <returns>A caching configuration suitable for production.</returns>
        /// <remarks>
        /// Production configurations use longer TTLs, larger cache sizes,
        /// and enable advanced features like compression and warming for
        /// optimal performance.
        /// </remarks>
        public static CachingConfiguration ForProduction()
        {
            return new CachingConfiguration
            {
                Enabled = true,
                ProviderType = CacheProviderType.Distributed,
                MetadataTtl = TimeSpan.FromHours(4),
                ToolsTtl = TimeSpan.FromHours(2),
                QueryResultsTtl = TimeSpan.FromMinutes(15),
                MaxSizeMb = 500,
                MaxEntries = 10000,
                EnableStatistics = true,
                EnableWarming = true,
                WarmingDelay = TimeSpan.FromMinutes(1),
                Compression = new CacheCompressionConfiguration { Enabled = true }
            };
        }

        /// <summary>
        /// Creates a configuration with caching disabled.
        /// </summary>
        /// <returns>A caching configuration with all caching disabled.</returns>
        /// <remarks>
        /// Disabled caching ensures the freshest data is always retrieved
        /// at the cost of performance. Useful for debugging or when
        /// data consistency is more important than performance.
        /// </remarks>
        public static CachingConfiguration Disabled()
        {
            return new CachingConfiguration
            {
                Enabled = false,
                EnableStatistics = false,
                EnableWarming = false
            };
        }

        /// <summary>
        /// Gets the cache key for a metadata entry.
        /// </summary>
        /// <param name="serviceUrl">The OData service URL.</param>
        /// <returns>The cache key for the metadata.</returns>
        /// <exception cref="ArgumentException">Thrown when <paramref name="serviceUrl"/> is null or whitespace.</exception>
        public string GetMetadataKey(string serviceUrl)
        {
            if (string.IsNullOrWhiteSpace(serviceUrl))
            {
                throw new ArgumentException("Service URL cannot be null or whitespace.", nameof(serviceUrl));
            }

            var normalizedUrl = serviceUrl.TrimEnd('/');
            return $"{KeyPrefix}metadata:{normalizedUrl.GetHashCode():X8}";
        }

        /// <summary>
        /// Gets the cache key for a tools entry.
        /// </summary>
        /// <param name="serviceUrl">The OData service URL.</param>
        /// <param name="optionsHash">The hash of the tool generation options.</param>
        /// <returns>The cache key for the tools.</returns>
        /// <exception cref="ArgumentException">Thrown when <paramref name="serviceUrl"/> is null or whitespace.</exception>
        public string GetToolsKey(string serviceUrl, string optionsHash)
        {
            if (string.IsNullOrWhiteSpace(serviceUrl))
            {
                throw new ArgumentException("Service URL cannot be null or whitespace.", nameof(serviceUrl));
            }

            var normalizedUrl = serviceUrl.TrimEnd('/');
            return $"{KeyPrefix}tools:{normalizedUrl.GetHashCode():X8}:{optionsHash}";
        }

        /// <summary>
        /// Gets the cache key for a query result entry.
        /// </summary>
        /// <param name="serviceUrl">The OData service URL.</param>
        /// <param name="queryHash">The hash of the query parameters.</param>
        /// <param name="userContext">Optional user context for user-specific caching.</param>
        /// <returns>The cache key for the query result.</returns>
        /// <exception cref="ArgumentException">Thrown when <paramref name="serviceUrl"/> is null or whitespace.</exception>
        public string GetQueryResultKey(string serviceUrl, string queryHash, string? userContext = null)
        {
            if (string.IsNullOrWhiteSpace(serviceUrl))
            {
                throw new ArgumentException("Service URL cannot be null or whitespace.", nameof(serviceUrl));
            }

            var normalizedUrl = serviceUrl.TrimEnd('/');
            var userSuffix = !string.IsNullOrWhiteSpace(userContext) ? $":{userContext}" : string.Empty;
            return $"{KeyPrefix}query:{normalizedUrl.GetHashCode():X8}:{queryHash}{userSuffix}";
        }

        /// <summary>
        /// Creates a copy of this configuration.
        /// </summary>
        /// <returns>A new instance with the same settings.</returns>
        public CachingConfiguration Clone()
        {
            return new CachingConfiguration
            {
                Enabled = Enabled,
                ProviderType = ProviderType,
                MetadataTtl = MetadataTtl,
                ToolsTtl = ToolsTtl,
                QueryResultsTtl = QueryResultsTtl,
                MaxSizeMb = MaxSizeMb,
                MaxEntries = MaxEntries,
                EvictionPolicy = EvictionPolicy,
                KeyPrefix = KeyPrefix,
                EnableStatistics = EnableStatistics,
                EnableWarming = EnableWarming,
                WarmingDelay = WarmingDelay,
                DistributedCache = DistributedCache.Clone(),
                Compression = Compression.Clone(),
                CustomProperties = new Dictionary<string, object>(CustomProperties)
            };
        }

        /// <summary>
        /// Merges another configuration into this one, with the other configuration taking precedence.
        /// </summary>
        /// <param name="other">The configuration to merge into this one.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="other"/> is null.</exception>
        public void MergeWith(CachingConfiguration other)
        {
#if NET8_0_OR_GREATER
            ArgumentNullException.ThrowIfNull(other);
#else
            if (other is null)
            {
                throw new ArgumentNullException(nameof(other));
            }
#endif

            Enabled = other.Enabled;
            ProviderType = other.ProviderType;
            MetadataTtl = other.MetadataTtl;
            ToolsTtl = other.ToolsTtl;
            QueryResultsTtl = other.QueryResultsTtl;
            MaxSizeMb = other.MaxSizeMb ?? MaxSizeMb;
            MaxEntries = other.MaxEntries ?? MaxEntries;
            EvictionPolicy = other.EvictionPolicy;
            EnableStatistics = other.EnableStatistics;
            EnableWarming = other.EnableWarming;
            WarmingDelay = other.WarmingDelay;

            if (!string.IsNullOrWhiteSpace(other.KeyPrefix)) KeyPrefix = other.KeyPrefix;

            DistributedCache.MergeWith(other.DistributedCache);
            Compression.MergeWith(other.Compression);

            // Merge custom properties
            foreach (var kvp in other.CustomProperties)
            {
                CustomProperties[kvp.Key] = kvp.Value;
            }
        }

        #endregion
    }
}