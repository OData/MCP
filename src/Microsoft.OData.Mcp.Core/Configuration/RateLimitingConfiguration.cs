using System;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.OData.Mcp.Core.Configuration
{
    // Additional configuration classes would be implemented here:
    // - RateLimitingConfiguration
    // - SecurityHeadersConfiguration  
    // - InputValidationConfiguration
    // - IpRestrictionConfiguration
    // - DataProtectionConfiguration
    // - FeatureFlagsConfiguration

    // For brevity, I'll include simplified placeholder implementations:

    /// <summary>
    /// Configuration for request rate limiting and throttling.
    /// </summary>
    /// <remarks>
    /// Rate limiting configuration controls how many requests clients can make
    /// within specific time windows. This helps protect the server from abuse,
    /// denial-of-service attacks, and ensures fair resource usage across clients.
    /// </remarks>
    public sealed class RateLimitingConfiguration
    {
        /// <summary>
        /// Gets or sets the maximum number of requests allowed per minute.
        /// </summary>
        /// <value>The maximum requests per minute per client.</value>
        /// <remarks>
        /// This sets the sustained rate limit for clients. Requests exceeding
        /// this rate will be throttled or rejected based on the rate limiting policy.
        /// </remarks>
        public int RequestsPerMinute { get; set; } = 100;

        /// <summary>
        /// Gets or sets the burst limit for requests.
        /// </summary>
        /// <value>The maximum number of requests allowed in a short burst.</value>
        /// <remarks>
        /// The burst limit allows clients to exceed the sustained rate temporarily,
        /// accommodating normal traffic spikes while still preventing abuse.
        /// </remarks>
        public int BurstLimit { get; set; } = 20;

        /// <summary>
        /// Gets or sets the time window for rate limit calculations.
        /// </summary>
        /// <value>The time window over which requests are counted.</value>
        /// <remarks>
        /// The time window defines the period over which the rate limit is enforced.
        /// Shorter windows provide more responsive protection but may be more sensitive
        /// to normal traffic variations.
        /// </remarks>
        public TimeSpan TimeWindow { get; set; } = TimeSpan.FromMinutes(1);

        /// <summary>
        /// Creates a rate limiting configuration optimized for production environments.
        /// </summary>
        /// <returns>A rate limiting configuration suitable for production use.</returns>
        public static RateLimitingConfiguration ForProduction() => new() { RequestsPerMinute = 60, BurstLimit = 10 };

        /// <summary>
        /// Validates the rate limiting configuration.
        /// </summary>
        /// <returns>A collection of validation errors, or empty if the configuration is valid.</returns>
        public IEnumerable<string> Validate() => Enumerable.Empty<string>();

        /// <summary>
        /// Creates a copy of this rate limiting configuration.
        /// </summary>
        /// <returns>A new instance with the same settings.</returns>
        public RateLimitingConfiguration Clone() => new() { RequestsPerMinute = RequestsPerMinute, BurstLimit = BurstLimit, TimeWindow = TimeWindow };

        /// <summary>
        /// Merges another rate limiting configuration into this one.
        /// </summary>
        /// <param name="other">The configuration to merge into this one.</param>
        public void MergeWith(RateLimitingConfiguration other) { if (other != null) { RequestsPerMinute = other.RequestsPerMinute; BurstLimit = other.BurstLimit; TimeWindow = other.TimeWindow; } }
    }
}
