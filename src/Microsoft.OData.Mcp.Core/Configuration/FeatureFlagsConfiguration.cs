using System;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.OData.Mcp.Core.Configuration
{
    /// <summary>
    /// Configuration for enabling/disabling specific features.
    /// </summary>
    /// <remarks>
    /// Feature flags allow selective enabling of functionality for gradual rollouts,
    /// A/B testing, or environment-specific configurations without code changes.
    /// </remarks>
    public sealed class FeatureFlagsConfiguration
    {
        #region Properties

        /// <summary>
        /// Gets or sets a value indicating whether development endpoints are enabled.
        /// </summary>
        /// <value><c>true</c> to enable development-specific endpoints; otherwise, <c>false</c>.</value>
        /// <remarks>
        /// Development endpoints include features like detailed diagnostics, configuration
        /// inspection, and testing utilities that should not be available in production.
        /// </remarks>
        public bool EnableDevelopmentEndpoints { get; set; } = false;

        /// <summary>
        /// Gets or sets a value indicating whether experimental features are enabled.
        /// </summary>
        /// <value><c>true</c> to enable experimental features; otherwise, <c>false</c>.</value>
        /// <remarks>
        /// Experimental features are new or unstable functionality that may change
        /// or be removed in future versions. Use with caution in production.
        /// </remarks>
        public bool EnableExperimentalFeatures { get; set; } = false;

        /// <summary>
        /// Gets or sets a value indicating whether batch operations are enabled.
        /// </summary>
        /// <value><c>true</c> to enable batch operations; otherwise, <c>false</c>.</value>
        /// <remarks>
        /// Batch operations allow multiple operations to be executed in a single request,
        /// improving performance but increasing complexity and resource usage.
        /// </remarks>
        public bool EnableBatchOperations { get; set; } = true;

        /// <summary>
        /// Gets or sets a value indicating whether advanced querying features are enabled.
        /// </summary>
        /// <value><c>true</c> to enable advanced querying; otherwise, <c>false</c>.</value>
        /// <remarks>
        /// Advanced querying includes features like complex $filter expressions,
        /// custom functions, and sophisticated $expand operations.
        /// </remarks>
        public bool EnableAdvancedQuerying { get; set; } = true;

        /// <summary>
        /// Gets or sets a value indicating whether caching optimizations are enabled.
        /// </summary>
        /// <value><c>true</c> to enable caching optimizations; otherwise, <c>false</c>.</value>
        /// <remarks>
        /// Caching optimizations include aggressive caching strategies that may
        /// improve performance at the cost of data freshness.
        /// </remarks>
        public bool EnableCachingOptimizations { get; set; } = false;

        /// <summary>
        /// Gets or sets a value indicating whether async streaming is enabled.
        /// </summary>
        /// <value><c>true</c> to enable async streaming; otherwise, <c>false</c>.</value>
        /// <remarks>
        /// Async streaming allows large result sets to be returned incrementally,
        /// improving perceived performance and reducing memory usage.
        /// </remarks>
        public bool EnableAsyncStreaming { get; set; } = false;

        /// <summary>
        /// Gets or sets a value indicating whether custom tool extensions are enabled.
        /// </summary>
        /// <value><c>true</c> to enable custom tool extensions; otherwise, <c>false</c>.</value>
        /// <remarks>
        /// Custom tool extensions allow loading additional MCP tools from external
        /// assemblies or configuration, extending the server's capabilities.
        /// </remarks>
        public bool EnableCustomToolExtensions { get; set; } = false;

        /// <summary>
        /// Gets or sets a value indicating whether schema validation is enforced.
        /// </summary>
        /// <value><c>true</c> to enforce strict schema validation; otherwise, <c>false</c>.</value>
        /// <remarks>
        /// Strict schema validation ensures all requests conform exactly to the
        /// OData schema but may reject valid requests with minor variations.
        /// </remarks>
        public bool EnforceStrictSchemaValidation { get; set; } = true;

        /// <summary>
        /// Gets or sets a value indicating whether legacy compatibility mode is enabled.
        /// </summary>
        /// <value><c>true</c> to enable legacy compatibility; otherwise, <c>false</c>.</value>
        /// <remarks>
        /// Legacy compatibility mode maintains backward compatibility with older
        /// OData versions or non-standard implementations at the cost of modern features.
        /// </remarks>
        public bool EnableLegacyCompatibility { get; set; } = false;

        /// <summary>
        /// Gets or sets a value indicating whether performance profiling is enabled.
        /// </summary>
        /// <value><c>true</c> to enable performance profiling; otherwise, <c>false</c>.</value>
        /// <remarks>
        /// Performance profiling collects detailed timing and resource usage information
        /// for optimization purposes but adds overhead to request processing.
        /// </remarks>
        public bool EnablePerformanceProfiling { get; set; } = false;

        /// <summary>
        /// Gets or sets a value indicating whether beta API versions are enabled.
        /// </summary>
        /// <value><c>true</c> to enable beta API versions; otherwise, <c>false</c>.</value>
        /// <remarks>
        /// Beta API versions provide access to new functionality before it becomes
        /// generally available, but may be unstable or subject to breaking changes.
        /// </remarks>
        public bool EnableBetaApiVersions { get; set; } = false;

        /// <summary>
        /// Gets or sets a value indicating whether enhanced security features are enabled.
        /// </summary>
        /// <value><c>true</c> to enable enhanced security; otherwise, <c>false</c>.</value>
        /// <remarks>
        /// Enhanced security features provide additional protection mechanisms
        /// that may impact performance or compatibility with some clients.
        /// </remarks>
        public bool EnableEnhancedSecurity { get; set; } = false;

        /// <summary>
        /// Gets or sets custom feature flag values.
        /// </summary>
        /// <value>A dictionary of custom feature flag names and their enabled status.</value>
        /// <remarks>
        /// Custom feature flags allow applications to define their own toggleable
        /// features beyond the predefined flags in this configuration.
        /// </remarks>
        public Dictionary<string, bool> CustomFlags { get; set; } = [];

        /// <summary>
        /// Gets or sets feature flag metadata.
        /// </summary>
        /// <value>A dictionary containing metadata about feature flags.</value>
        /// <remarks>
        /// Metadata can include information such as flag descriptions, deprecation
        /// notices, rollout percentages, or other contextual information.
        /// </remarks>
        public Dictionary<string, object> FlagMetadata { get; set; } = [];

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="FeatureFlagsConfiguration"/> class.
        /// </summary>
        public FeatureFlagsConfiguration()
        {
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Creates a configuration optimized for development environments.
        /// </summary>
        /// <returns>A feature flags configuration suitable for development.</returns>
        public static FeatureFlagsConfiguration ForDevelopment()
        {
            return new FeatureFlagsConfiguration
            {
                EnableDevelopmentEndpoints = true,
                EnableExperimentalFeatures = true,
                EnableBatchOperations = true,
                EnableAdvancedQuerying = true,
                EnableCachingOptimizations = false,
                EnableAsyncStreaming = true,
                EnableCustomToolExtensions = true,
                EnforceStrictSchemaValidation = false,
                EnableLegacyCompatibility = true,
                EnablePerformanceProfiling = true,
                EnableBetaApiVersions = true,
                EnableEnhancedSecurity = false
            };
        }

        /// <summary>
        /// Creates a configuration optimized for production environments.
        /// </summary>
        /// <returns>A feature flags configuration suitable for production.</returns>
        public static FeatureFlagsConfiguration ForProduction()
        {
            return new FeatureFlagsConfiguration
            {
                EnableDevelopmentEndpoints = false,
                EnableExperimentalFeatures = false,
                EnableBatchOperations = true,
                EnableAdvancedQuerying = true,
                EnableCachingOptimizations = true,
                EnableAsyncStreaming = false,
                EnableCustomToolExtensions = false,
                EnforceStrictSchemaValidation = true,
                EnableLegacyCompatibility = false,
                EnablePerformanceProfiling = false,
                EnableBetaApiVersions = false,
                EnableEnhancedSecurity = true
            };
        }

        /// <summary>
        /// Creates a minimal configuration with most features disabled.
        /// </summary>
        /// <returns>A feature flags configuration with minimal features enabled.</returns>
        public static FeatureFlagsConfiguration Minimal()
        {
            return new FeatureFlagsConfiguration
            {
                EnableDevelopmentEndpoints = false,
                EnableExperimentalFeatures = false,
                EnableBatchOperations = false,
                EnableAdvancedQuerying = false,
                EnableCachingOptimizations = false,
                EnableAsyncStreaming = false,
                EnableCustomToolExtensions = false,
                EnforceStrictSchemaValidation = true,
                EnableLegacyCompatibility = false,
                EnablePerformanceProfiling = false,
                EnableBetaApiVersions = false,
                EnableEnhancedSecurity = false
            };
        }

        /// <summary>
        /// Determines whether a specific feature flag is enabled.
        /// </summary>
        /// <param name="flagName">The name of the feature flag to check.</param>
        /// <returns><c>true</c> if the flag is enabled; otherwise, <c>false</c>.</returns>
        public bool IsEnabled(string flagName)
        {
            if (string.IsNullOrWhiteSpace(flagName))
            {
                return false;
            }

            // Check predefined flags first
            switch (flagName.ToLowerInvariant())
            {
                case "developmentendpoints":
                case "development-endpoints":
                    return EnableDevelopmentEndpoints;

                case "experimentalfeatures":
                case "experimental-features":
                    return EnableExperimentalFeatures;

                case "batchoperations":
                case "batch-operations":
                    return EnableBatchOperations;

                case "advancedquerying":
                case "advanced-querying":
                    return EnableAdvancedQuerying;

                case "cachingoptimizations":
                case "caching-optimizations":
                    return EnableCachingOptimizations;

                case "asyncstreaming":
                case "async-streaming":
                    return EnableAsyncStreaming;

                case "customtoolextensions":
                case "custom-tool-extensions":
                    return EnableCustomToolExtensions;

                case "strictschemavalidation":
                case "strict-schema-validation":
                    return EnforceStrictSchemaValidation;

                case "legacycompatibility":
                case "legacy-compatibility":
                    return EnableLegacyCompatibility;

                case "performanceprofiling":
                case "performance-profiling":
                    return EnablePerformanceProfiling;

                case "betaapiversions":
                case "beta-api-versions":
                    return EnableBetaApiVersions;

                case "enhancedsecurity":
                case "enhanced-security":
                    return EnableEnhancedSecurity;

                default:
                    // Check custom flags
                    return CustomFlags.TryGetValue(flagName, out var enabled) && enabled;
            }
        }

        /// <summary>
        /// Sets the value of a custom feature flag.
        /// </summary>
        /// <param name="flagName">The name of the feature flag.</param>
        /// <param name="enabled">Whether the flag should be enabled.</param>
        /// <exception cref="ArgumentException">Thrown when <paramref name="flagName"/> is null or whitespace.</exception>
        public void SetCustomFlag(string flagName, bool enabled)
        {
ArgumentException.ThrowIfNullOrWhiteSpace(flagName);

            CustomFlags[flagName] = enabled;
        }

        /// <summary>
        /// Removes a custom feature flag.
        /// </summary>
        /// <param name="flagName">The name of the feature flag to remove.</param>
        /// <returns><c>true</c> if the flag was removed; otherwise, <c>false</c>.</returns>
        public bool RemoveCustomFlag(string flagName)
        {
            return !string.IsNullOrWhiteSpace(flagName) && CustomFlags.Remove(flagName);
        }

        /// <summary>
        /// Adds metadata for a feature flag.
        /// </summary>
        /// <param name="flagName">The name of the feature flag.</param>
        /// <param name="metadata">The metadata object to associate with the flag.</param>
        /// <exception cref="ArgumentException">Thrown when <paramref name="flagName"/> is null or whitespace.</exception>
        public void AddFlagMetadata(string flagName, object metadata)
        {
ArgumentException.ThrowIfNullOrWhiteSpace(flagName);

            FlagMetadata[flagName] = metadata;
        }

        /// <summary>
        /// Gets metadata for a feature flag.
        /// </summary>
        /// <typeparam name="T">The type of the metadata object.</typeparam>
        /// <param name="flagName">The name of the feature flag.</param>
        /// <returns>The metadata object if found and of the correct type; otherwise, the default value.</returns>
        public T? GetFlagMetadata<T>(string flagName)
        {
            if (FlagMetadata.TryGetValue(flagName, out var metadata) && metadata is T typedMetadata)
            {
                return typedMetadata;
            }
            return default;
        }

        /// <summary>
        /// Gets all enabled feature flags.
        /// </summary>
        /// <returns>A collection of enabled feature flag names.</returns>
        public IEnumerable<string> GetEnabledFlags()
        {
            var enabled = new List<string>();

            if (EnableDevelopmentEndpoints) enabled.Add("DevelopmentEndpoints");
            if (EnableExperimentalFeatures) enabled.Add("ExperimentalFeatures");
            if (EnableBatchOperations) enabled.Add("BatchOperations");
            if (EnableAdvancedQuerying) enabled.Add("AdvancedQuerying");
            if (EnableCachingOptimizations) enabled.Add("CachingOptimizations");
            if (EnableAsyncStreaming) enabled.Add("AsyncStreaming");
            if (EnableCustomToolExtensions) enabled.Add("CustomToolExtensions");
            if (EnforceStrictSchemaValidation) enabled.Add("StrictSchemaValidation");
            if (EnableLegacyCompatibility) enabled.Add("LegacyCompatibility");
            if (EnablePerformanceProfiling) enabled.Add("PerformanceProfiling");
            if (EnableBetaApiVersions) enabled.Add("BetaApiVersions");
            if (EnableEnhancedSecurity) enabled.Add("EnhancedSecurity");

            // Add enabled custom flags
            enabled.AddRange(CustomFlags.Where(kvp => kvp.Value).Select(kvp => kvp.Key));

            return enabled;
        }

        /// <summary>
        /// Gets feature flag statistics for monitoring and diagnostics.
        /// </summary>
        /// <returns>A dictionary containing feature flag statistics.</returns>
        public Dictionary<string, object> GetStatistics()
        {
            var enabledFlags = GetEnabledFlags().ToList();
            
            return new Dictionary<string, object>
            {
                ["TotalFlags"] = 12 + CustomFlags.Count, // 12 predefined + custom
                ["EnabledFlags"] = enabledFlags.Count,
                ["EnabledFlagNames"] = enabledFlags,
                ["CustomFlagsCount"] = CustomFlags.Count,
                ["MetadataCount"] = FlagMetadata.Count,
                ["DevelopmentMode"] = EnableDevelopmentEndpoints,
                ["ProductionOptimized"] = EnableCachingOptimizations && EnableEnhancedSecurity && !EnableExperimentalFeatures
            };
        }

        /// <summary>
        /// Validates the feature flags configuration.
        /// </summary>
        /// <returns>A collection of validation warnings, or empty if the configuration is valid.</returns>
        /// <remarks>
        /// Feature flags validation focuses on warnings rather than errors, as most
        /// combinations are valid but some may indicate misconfigurations.
        /// </remarks>
        public IEnumerable<string> Validate()
        {
            var warnings = new List<string>();

            // Warn about potentially problematic combinations
            if (EnableDevelopmentEndpoints && EnableEnhancedSecurity)
            {
                warnings.Add("Development endpoints are enabled with enhanced security - this may cause conflicts");
            }

            if (EnableExperimentalFeatures && EnforceStrictSchemaValidation)
            {
                warnings.Add("Experimental features with strict schema validation may cause unexpected failures");
            }

            if (!EnableBatchOperations && EnableAdvancedQuerying)
            {
                warnings.Add("Advanced querying without batch operations may limit performance optimizations");
            }

            if (EnableLegacyCompatibility && EnableEnhancedSecurity)
            {
                warnings.Add("Legacy compatibility may conflict with enhanced security features");
            }

            if (EnablePerformanceProfiling && EnableCachingOptimizations)
            {
                warnings.Add("Performance profiling may interfere with caching behavior");
            }

            // Check for invalid custom flag names
            foreach (var flagName in CustomFlags.Keys)
            {
                if (string.IsNullOrWhiteSpace(flagName))
                {
                    warnings.Add("Custom flag with empty or whitespace name detected");
                }
                else if (flagName.Contains(' ') || flagName.Contains('\t'))
                {
                    warnings.Add($"Custom flag '{flagName}' contains whitespace characters");
                }
            }

            return warnings;
        }

        /// <summary>
        /// Creates a copy of this configuration.
        /// </summary>
        /// <returns>A new instance with the same settings.</returns>
        public FeatureFlagsConfiguration Clone()
        {
            return new FeatureFlagsConfiguration
            {
                EnableDevelopmentEndpoints = EnableDevelopmentEndpoints,
                EnableExperimentalFeatures = EnableExperimentalFeatures,
                EnableBatchOperations = EnableBatchOperations,
                EnableAdvancedQuerying = EnableAdvancedQuerying,
                EnableCachingOptimizations = EnableCachingOptimizations,
                EnableAsyncStreaming = EnableAsyncStreaming,
                EnableCustomToolExtensions = EnableCustomToolExtensions,
                EnforceStrictSchemaValidation = EnforceStrictSchemaValidation,
                EnableLegacyCompatibility = EnableLegacyCompatibility,
                EnablePerformanceProfiling = EnablePerformanceProfiling,
                EnableBetaApiVersions = EnableBetaApiVersions,
                EnableEnhancedSecurity = EnableEnhancedSecurity,
                CustomFlags = new Dictionary<string, bool>(CustomFlags),
                FlagMetadata = new Dictionary<string, object>(FlagMetadata)
            };
        }

        /// <summary>
        /// Merges another configuration into this one, with the other configuration taking precedence.
        /// </summary>
        /// <param name="other">The configuration to merge into this one.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="other"/> is null.</exception>
        public void MergeWith(FeatureFlagsConfiguration other)
        {
ArgumentNullException.ThrowIfNull(other);

            EnableDevelopmentEndpoints = other.EnableDevelopmentEndpoints;
            EnableExperimentalFeatures = other.EnableExperimentalFeatures;
            EnableBatchOperations = other.EnableBatchOperations;
            EnableAdvancedQuerying = other.EnableAdvancedQuerying;
            EnableCachingOptimizations = other.EnableCachingOptimizations;
            EnableAsyncStreaming = other.EnableAsyncStreaming;
            EnableCustomToolExtensions = other.EnableCustomToolExtensions;
            EnforceStrictSchemaValidation = other.EnforceStrictSchemaValidation;
            EnableLegacyCompatibility = other.EnableLegacyCompatibility;
            EnablePerformanceProfiling = other.EnablePerformanceProfiling;
            EnableBetaApiVersions = other.EnableBetaApiVersions;
            EnableEnhancedSecurity = other.EnableEnhancedSecurity;

            // Merge custom flags
            foreach (var kvp in other.CustomFlags)
            {
                CustomFlags[kvp.Key] = kvp.Value;
            }

            // Merge metadata
            foreach (var kvp in other.FlagMetadata)
            {
                FlagMetadata[kvp.Key] = kvp.Value;
            }
        }

        #endregion
    }
}