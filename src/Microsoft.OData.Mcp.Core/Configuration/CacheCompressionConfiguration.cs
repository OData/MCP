// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System.Collections.Generic;

namespace Microsoft.OData.Mcp.Core.Configuration
{
    /// <summary>
    /// Configuration for cache compression.
    /// </summary>
    /// <remarks>
    /// Cache compression configuration controls how cached data is compressed
    /// to reduce memory usage and storage requirements. Compression can
    /// significantly reduce cache size at the cost of additional CPU overhead
    /// during cache operations.
    /// </remarks>
    public sealed class CacheCompressionConfiguration
    {
        #region Properties

        /// <summary>
        /// Gets or sets a value indicating whether cache compression is enabled.
        /// </summary>
        /// <value><c>true</c> to enable compression; otherwise, <c>false</c>.</value>
        /// <remarks>
        /// When compression is enabled, cached data will be compressed before
        /// storage and decompressed when retrieved. This can significantly
        /// reduce memory usage for large cached objects.
        /// </remarks>
        public bool Enabled { get; set; } = false;

        /// <summary>
        /// Gets or sets the compression algorithm to use.
        /// </summary>
        /// <value>The name of the compression algorithm.</value>
        /// <remarks>
        /// Supported algorithms typically include "gzip", "deflate", and "brotli".
        /// Different algorithms offer different trade-offs between compression
        /// ratio, speed, and CPU usage.
        /// </remarks>
        /// <example>
        /// Common values:
        /// - "gzip": Good balance of compression and speed
        /// - "deflate": Similar to gzip but with less overhead
        /// - "brotli": Better compression ratio but slower
        /// </example>
        public string Algorithm { get; set; } = "gzip";

        /// <summary>
        /// Gets or sets the minimum size in bytes before compression is applied.
        /// </summary>
        /// <value>The minimum size threshold for compression.</value>
        /// <remarks>
        /// Small objects may not benefit from compression due to the overhead
        /// of the compression algorithm. Objects smaller than this threshold
        /// will be stored uncompressed.
        /// </remarks>
        public int MinimumSize { get; set; } = 1024; // 1KB

        /// <summary>
        /// Gets or sets the compression level.
        /// </summary>
        /// <value>The compression level from 0 (no compression) to 9 (maximum compression).</value>
        /// <remarks>
        /// Higher compression levels provide better compression ratios but
        /// require more CPU time. Level 6 typically provides a good balance
        /// between compression ratio and performance.
        /// </remarks>
        public int CompressionLevel { get; set; } = 6; // Balanced compression

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="CacheCompressionConfiguration"/> class.
        /// </summary>
        public CacheCompressionConfiguration()
        {
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Validates the compression configuration.
        /// </summary>
        /// <returns>A collection of validation errors, or empty if the configuration is valid.</returns>
        public IEnumerable<string> Validate()
        {
            var errors = new List<string>();

            if (MinimumSize < 0)
            {
                errors.Add("MinimumSize cannot be negative");
            }

            if (CompressionLevel < 0 || CompressionLevel > 9)
            {
                errors.Add("CompressionLevel must be between 0 and 9");
            }

            if (string.IsNullOrWhiteSpace(Algorithm))
            {
                errors.Add("Algorithm cannot be null or whitespace");
            }

            // Validate supported algorithms
            var supportedAlgorithms = new[] { "gzip", "deflate", "brotli" };
            if (!string.IsNullOrWhiteSpace(Algorithm) && 
                System.Array.IndexOf(supportedAlgorithms, Algorithm.ToLowerInvariant()) == -1)
            {
                errors.Add($"Unsupported compression algorithm: {Algorithm}. Supported algorithms: {string.Join(", ", supportedAlgorithms)}");
            }

            return errors;
        }

        /// <summary>
        /// Creates a copy of this configuration.
        /// </summary>
        /// <returns>A new instance with the same settings.</returns>
        public CacheCompressionConfiguration Clone()
        {
            return new CacheCompressionConfiguration
            {
                Enabled = Enabled,
                Algorithm = Algorithm,
                MinimumSize = MinimumSize,
                CompressionLevel = CompressionLevel
            };
        }

        /// <summary>
        /// Merges another configuration into this one, with the other configuration taking precedence.
        /// </summary>
        /// <param name="other">The configuration to merge into this one.</param>
        /// <remarks>
        /// All values from the other configuration will override values in this
        /// configuration. This allows for complete updates of compression settings.
        /// </remarks>
        public void MergeWith(CacheCompressionConfiguration other)
        {
            if (other is null) return;

            Enabled = other.Enabled;
            Algorithm = other.Algorithm;
            MinimumSize = other.MinimumSize;
            CompressionLevel = other.CompressionLevel;
        }

        /// <summary>
        /// Creates a configuration optimized for fast compression.
        /// </summary>
        /// <returns>A compression configuration optimized for speed.</returns>
        public static CacheCompressionConfiguration FastCompression()
        {
            return new CacheCompressionConfiguration
            {
                Enabled = true,
                Algorithm = "gzip",
                MinimumSize = 2048, // 2KB
                CompressionLevel = 3 // Fast compression
            };
        }

        /// <summary>
        /// Creates a configuration optimized for maximum compression.
        /// </summary>
        /// <returns>A compression configuration optimized for compression ratio.</returns>
        public static CacheCompressionConfiguration MaximumCompression()
        {
            return new CacheCompressionConfiguration
            {
                Enabled = true,
                Algorithm = "brotli",
                MinimumSize = 512, // 512 bytes
                CompressionLevel = 9 // Maximum compression
            };
        }

        /// <summary>
        /// Creates a configuration with compression disabled.
        /// </summary>
        /// <returns>A compression configuration with compression disabled.</returns>
        public static CacheCompressionConfiguration Disabled()
        {
            return new CacheCompressionConfiguration
            {
                Enabled = false
            };
        }

        #endregion
    }
}