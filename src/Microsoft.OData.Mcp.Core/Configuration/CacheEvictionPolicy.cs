namespace Microsoft.OData.Mcp.Core.Configuration
{
    /// <summary>
    /// Defines the cache eviction policies.
    /// </summary>
    /// <remarks>
    /// Cache eviction policies determine which entries are removed from
    /// the cache when size or entry limits are reached. Different policies
    /// optimize for different access patterns and performance characteristics.
    /// </remarks>
    public enum CacheEvictionPolicy
    {
        /// <summary>
        /// Least Recently Used (LRU) eviction policy.
        /// </summary>
        /// <remarks>
        /// LRU removes the entries that have been accessed least recently,
        /// assuming that recently accessed items are more likely to be
        /// accessed again. This is the most commonly used eviction policy
        /// and works well for most scenarios.
        /// </remarks>
        LeastRecentlyUsed,

        /// <summary>
        /// Least Frequently Used (LFU) eviction policy.
        /// </summary>
        /// <remarks>
        /// LFU removes the entries that have been accessed least frequently,
        /// keeping popular items in the cache longer. This policy works well
        /// when access patterns are relatively stable over time.
        /// </remarks>
        LeastFrequentlyUsed,

        /// <summary>
        /// First In, First Out (FIFO) eviction policy.
        /// </summary>
        /// <remarks>
        /// FIFO removes the oldest entries first, regardless of access patterns.
        /// This is the simplest eviction policy to implement and has predictable
        /// behavior, but may not optimize for access patterns.
        /// </remarks>
        FirstInFirstOut,

        /// <summary>
        /// Random eviction policy.
        /// </summary>
        /// <remarks>
        /// Random eviction removes entries randomly when the cache is full.
        /// This policy has minimal overhead but provides no optimization
        /// for access patterns. Useful when cache access is truly random.
        /// </remarks>
        Random,

        /// <summary>
        /// Time-based eviction (shortest TTL first).
        /// </summary>
        /// <remarks>
        /// Time-based eviction removes entries with the shortest remaining
        /// time-to-live first. This policy respects the intended lifetime
        /// of cached entries and works well with time-sensitive data.
        /// </remarks>
        TimeToLive
    }
}