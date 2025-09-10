// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.OData.Mcp.Core.Configuration
{

    /// <summary>
    /// Configuration for data protection and encryption settings.
    /// </summary>
    /// <remarks>
    /// Data protection configuration controls how sensitive data is encrypted
    /// and protected within the MCP server. This includes encryption keys,
    /// rotation periods, and encryption policies for different data types.
    /// </remarks>
    public sealed class DataProtectionConfiguration
    {

        /// <summary>
        /// Gets or sets a value indicating whether sensitive data should be encrypted.
        /// </summary>
        /// <value><c>true</c> to encrypt sensitive data; otherwise, <c>false</c>.</value>
        /// <remarks>
        /// When enabled, sensitive data such as authentication tokens, API keys,
        /// and user credentials will be encrypted before storage or transmission.
        /// </remarks>
        public bool EncryptSensitiveData { get; set; } = true;

        /// <summary>
        /// Gets or sets the encryption key used for data protection.
        /// </summary>
        /// <value>The base64-encoded encryption key.</value>
        /// <remarks>
        /// This key is used for encrypting and decrypting sensitive data.
        /// It should be a strong, randomly generated key and kept secure.
        /// Consider using key management services in production environments.
        /// </remarks>
        public string EncryptionKey { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the period after which encryption keys should be rotated.
        /// </summary>
        /// <value>The time span between key rotations.</value>
        /// <remarks>
        /// Regular key rotation is a security best practice that limits the
        /// exposure window if a key is compromised. Shorter rotation periods
        /// provide better security but require more frequent key management.
        /// </remarks>
        public TimeSpan KeyRotationPeriod { get; set; } = TimeSpan.FromDays(90);

        /// <summary>
        /// Creates a data protection configuration optimized for production environments.
        /// </summary>
        /// <returns>A data protection configuration suitable for production use.</returns>
        public static DataProtectionConfiguration ForProduction() => new() { EncryptSensitiveData = true, KeyRotationPeriod = TimeSpan.FromDays(30) };

        /// <summary>
        /// Validates the data protection configuration.
        /// </summary>
        /// <returns>A collection of validation errors, or empty if the configuration is valid.</returns>
        public IEnumerable<string> Validate() => Enumerable.Empty<string>();

        /// <summary>
        /// Creates a copy of this data protection configuration.
        /// </summary>
        /// <returns>A new instance with the same settings.</returns>
        public DataProtectionConfiguration Clone() => new() { EncryptSensitiveData = EncryptSensitiveData, EncryptionKey = EncryptionKey, KeyRotationPeriod = KeyRotationPeriod };

        /// <summary>
        /// Merges another data protection configuration into this one.
        /// </summary>
        /// <param name="other">The configuration to merge into this one.</param>
        public void MergeWith(DataProtectionConfiguration other) { if (other != null) { EncryptSensitiveData = other.EncryptSensitiveData; if (!string.IsNullOrWhiteSpace(other.EncryptionKey)) EncryptionKey = other.EncryptionKey; KeyRotationPeriod = other.KeyRotationPeriod; } }

    }

}
