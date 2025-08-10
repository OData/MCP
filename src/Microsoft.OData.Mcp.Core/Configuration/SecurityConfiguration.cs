// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.OData.Mcp.Core.Configuration
{
    /// <summary>
    /// Configuration for security policies and restrictions.
    /// </summary>
    /// <remarks>
    /// Security configuration includes CORS policies, rate limiting, request size limits,
    /// and other security-related settings to protect the MCP server from various threats.
    /// </remarks>
    public sealed class SecurityConfiguration
    {
        #region Properties

        /// <summary>
        /// Gets or sets a value indicating whether HTTPS is required.
        /// </summary>
        /// <value><c>true</c> to require HTTPS for all requests; otherwise, <c>false</c>.</value>
        /// <remarks>
        /// HTTPS should be required in production environments to protect data in transit.
        /// Development environments may disable this for convenience.
        /// </remarks>
        public bool RequireHttps { get; set; } = true;

        /// <summary>
        /// Gets or sets a value indicating whether to include detailed error information in responses.
        /// </summary>
        /// <value><c>true</c> to include detailed errors; otherwise, <c>false</c>.</value>
        /// <remarks>
        /// Detailed error information is useful for debugging but can expose sensitive
        /// information to attackers. This should be disabled in production.
        /// </remarks>
        public bool EnableDetailedErrors { get; set; } = false;

        /// <summary>
        /// Gets or sets a value indicating whether rate limiting is enabled.
        /// </summary>
        /// <value><c>true</c> to enable rate limiting; otherwise, <c>false</c>.</value>
        /// <remarks>
        /// Rate limiting protects against denial-of-service attacks and abuse
        /// by limiting the number of requests from individual clients.
        /// </remarks>
        public bool EnableRateLimiting { get; set; } = false;

        /// <summary>
        /// Gets or sets the rate limiting configuration.
        /// </summary>
        /// <value>Configuration for request rate limiting.</value>
        /// <remarks>
        /// Rate limiting configuration specifies the limits, time windows,
        /// and policies for controlling request rates.
        /// </remarks>
        public RateLimitingConfiguration RateLimiting { get; set; } = new();

        /// <summary>
        /// Gets or sets the maximum request size in bytes.
        /// </summary>
        /// <value>The maximum size allowed for HTTP request bodies.</value>
        /// <remarks>
        /// Request size limits prevent memory exhaustion attacks and ensure
        /// predictable resource usage.
        /// </remarks>
        public long MaxRequestSize { get; set; } = 1024 * 1024; // 1MB

        /// <summary>
        /// Gets or sets the maximum query string length.
        /// </summary>
        /// <value>The maximum length allowed for query strings.</value>
        /// <remarks>
        /// Query string length limits prevent URL-based attacks and ensure
        /// compatibility with various web servers and proxies.
        /// </remarks>
        public int MaxQueryStringLength { get; set; } = 2048;

        /// <summary>
        /// Gets or sets the maximum number of query string parameters.
        /// </summary>
        /// <value>The maximum number of parameters allowed in query strings.</value>
        /// <remarks>
        /// Parameter count limits prevent parsing-based attacks and ensure
        /// predictable request processing performance.
        /// </remarks>
        public int MaxQueryParameters { get; set; } = 100;

        /// <summary>
        /// Gets or sets the allowed HTTP methods.
        /// </summary>
        /// <value>A list of HTTP methods that are allowed for requests.</value>
        /// <remarks>
        /// Method restrictions limit the attack surface by only allowing
        /// necessary HTTP methods for the application's functionality.
        /// </remarks>
        public List<string> AllowedHttpMethods { get; set; } = ["GET", "POST", "OPTIONS"];

        /// <summary>
        /// Gets or sets the security headers configuration.
        /// </summary>
        /// <value>Configuration for security-related HTTP headers.</value>
        /// <remarks>
        /// Security headers provide additional protection against various
        /// web-based attacks like XSS, clickjacking, and MIME sniffing.
        /// </remarks>
        public SecurityHeadersConfiguration SecurityHeaders { get; set; } = new();

        /// <summary>
        /// Gets or sets the input validation configuration.
        /// </summary>
        /// <value>Configuration for validating user input.</value>
        /// <remarks>
        /// Input validation helps prevent injection attacks and ensures
        /// data integrity by validating all user-provided data.
        /// </remarks>
        public InputValidationConfiguration InputValidation { get; set; } = new();

        /// <summary>
        /// Gets or sets the content security policy.
        /// </summary>
        /// <value>The Content Security Policy (CSP) header value.</value>
        /// <remarks>
        /// CSP helps prevent XSS attacks by controlling which resources
        /// the browser is allowed to load for the page.
        /// </remarks>
        public string? ContentSecurityPolicy { get; set; }

        /// <summary>
        /// Gets or sets the allowed hosts.
        /// </summary>
        /// <value>A list of hosts that are allowed to make requests to the server.</value>
        /// <remarks>
        /// Host restrictions help prevent host header injection attacks
        /// and ensure requests are only accepted from legitimate sources.
        /// </remarks>
        public List<string> AllowedHosts { get; set; } = ["*"];

        /// <summary>
        /// Gets or sets the IP address restrictions.
        /// </summary>
        /// <value>Configuration for IP-based access control.</value>
        /// <remarks>
        /// IP restrictions provide network-level access control by allowing
        /// or denying requests based on client IP addresses.
        /// </remarks>
        public IpRestrictionConfiguration IpRestrictions { get; set; } = new();

        /// <summary>
        /// Gets or sets the data protection configuration.
        /// </summary>
        /// <value>Configuration for protecting sensitive data.</value>
        /// <remarks>
        /// Data protection configuration specifies how sensitive data should be
        /// encrypted, hashed, or otherwise protected both in transit and at rest.
        /// </remarks>
        public DataProtectionConfiguration DataProtection { get; set; } = new();

        /// <summary>
        /// Gets or sets custom security properties.
        /// </summary>
        /// <value>A dictionary of custom security configuration values.</value>
        /// <remarks>
        /// Custom properties allow extending the configuration with security
        /// settings specific to particular deployment environments or requirements.
        /// </remarks>
        public Dictionary<string, object> CustomProperties { get; set; } = [];

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="SecurityConfiguration"/> class.
        /// </summary>
        public SecurityConfiguration()
        {
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Validates the security configuration.
        /// </summary>
        /// <returns>A collection of validation errors, or empty if the configuration is valid.</returns>
        public IEnumerable<string> Validate()
        {
            var errors = new List<string>();

            if (MaxRequestSize <= 0)
            {
                errors.Add("MaxRequestSize must be greater than zero");
            }

            if (MaxQueryStringLength <= 0)
            {
                errors.Add("MaxQueryStringLength must be greater than zero");
            }

            if (MaxQueryParameters <= 0)
            {
                errors.Add("MaxQueryParameters must be greater than zero");
            }

            if (AllowedHttpMethods.Count == 0)
            {
                errors.Add("At least one HTTP method must be allowed");
            }

            // Validate rate limiting configuration
            if (EnableRateLimiting)
            {
                var rateLimitErrors = RateLimiting.Validate();
                errors.AddRange(rateLimitErrors.Select(e => $"RateLimiting: {e}"));
            }

            // Validate security headers configuration
            var headerErrors = SecurityHeaders.Validate();
            errors.AddRange(headerErrors.Select(e => $"SecurityHeaders: {e}"));

            // Validate input validation configuration
            var inputErrors = InputValidation.Validate();
            errors.AddRange(inputErrors.Select(e => $"InputValidation: {e}"));

            // Validate IP restrictions configuration
            var ipErrors = IpRestrictions.Validate();
            errors.AddRange(ipErrors.Select(e => $"IpRestrictions: {e}"));

            // Validate data protection configuration
            var dataProtectionErrors = DataProtection.Validate();
            errors.AddRange(dataProtectionErrors.Select(e => $"DataProtection: {e}"));

            return errors;
        }

        /// <summary>
        /// Creates a configuration optimized for development environments.
        /// </summary>
        /// <returns>A security configuration suitable for development.</returns>
        public static SecurityConfiguration ForDevelopment()
        {
            return new SecurityConfiguration
            {
                RequireHttps = false,
                EnableDetailedErrors = true,
                EnableRateLimiting = false,
                MaxRequestSize = 10 * 1024 * 1024, // 10MB for development
                AllowedHosts = ["*"],
                SecurityHeaders = SecurityHeadersConfiguration.ForDevelopment(),
                InputValidation = InputValidationConfiguration.Lenient()
            };
        }

        /// <summary>
        /// Creates a configuration optimized for production environments.
        /// </summary>
        /// <returns>A security configuration suitable for production.</returns>
        public static SecurityConfiguration ForProduction()
        {
            return new SecurityConfiguration
            {
                RequireHttps = true,
                EnableDetailedErrors = false,
                EnableRateLimiting = true,
                RateLimiting = RateLimitingConfiguration.ForProduction(),
                MaxRequestSize = 1024 * 1024, // 1MB
                MaxQueryStringLength = 1024,
                MaxQueryParameters = 50,
                SecurityHeaders = SecurityHeadersConfiguration.ForProduction(),
                InputValidation = InputValidationConfiguration.Strict(),
                ContentSecurityPolicy = "default-src 'self'; script-src 'self'; style-src 'self' 'unsafe-inline';",
                DataProtection = DataProtectionConfiguration.ForProduction()
            };
        }

        /// <summary>
        /// Determines whether the specified HTTP method is allowed.
        /// </summary>
        /// <param name="method">The HTTP method to check.</param>
        /// <returns><c>true</c> if the method is allowed; otherwise, <c>false</c>.</returns>
        public bool IsHttpMethodAllowed(string method)
        {
            return !string.IsNullOrWhiteSpace(method) && 
                   AllowedHttpMethods.Contains(method, StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Determines whether the specified host is allowed.
        /// </summary>
        /// <param name="host">The host to check.</param>
        /// <returns><c>true</c> if the host is allowed; otherwise, <c>false</c>.</returns>
        public bool IsHostAllowed(string host)
        {
            if (string.IsNullOrWhiteSpace(host))
            {
                return false;
            }

            if (AllowedHosts.Contains("*"))
            {
                return true;
            }

            return AllowedHosts.Contains(host, StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Creates a copy of this configuration.
        /// </summary>
        /// <returns>A new instance with the same settings.</returns>
        public SecurityConfiguration Clone()
        {
            return new SecurityConfiguration
            {
                RequireHttps = RequireHttps,
                EnableDetailedErrors = EnableDetailedErrors,
                EnableRateLimiting = EnableRateLimiting,
                RateLimiting = RateLimiting.Clone(),
                MaxRequestSize = MaxRequestSize,
                MaxQueryStringLength = MaxQueryStringLength,
                MaxQueryParameters = MaxQueryParameters,
                AllowedHttpMethods = new List<string>(AllowedHttpMethods),
                SecurityHeaders = SecurityHeaders.Clone(),
                InputValidation = InputValidation.Clone(),
                ContentSecurityPolicy = ContentSecurityPolicy,
                AllowedHosts = new List<string>(AllowedHosts),
                IpRestrictions = IpRestrictions.Clone(),
                DataProtection = DataProtection.Clone(),
                CustomProperties = new Dictionary<string, object>(CustomProperties)
            };
        }

        /// <summary>
        /// Merges another configuration into this one, with the other configuration taking precedence.
        /// </summary>
        /// <param name="other">The configuration to merge into this one.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="other"/> is null.</exception>
        public void MergeWith(SecurityConfiguration other)
        {
            ArgumentNullException.ThrowIfNull(other);

            RequireHttps = other.RequireHttps;
            EnableDetailedErrors = other.EnableDetailedErrors;
            EnableRateLimiting = other.EnableRateLimiting;
            MaxRequestSize = other.MaxRequestSize;
            MaxQueryStringLength = other.MaxQueryStringLength;
            MaxQueryParameters = other.MaxQueryParameters;

            if (!string.IsNullOrWhiteSpace(other.ContentSecurityPolicy))
            {
                ContentSecurityPolicy = other.ContentSecurityPolicy;
            }

            RateLimiting.MergeWith(other.RateLimiting);
            SecurityHeaders.MergeWith(other.SecurityHeaders);
            InputValidation.MergeWith(other.InputValidation);
            IpRestrictions.MergeWith(other.IpRestrictions);
            DataProtection.MergeWith(other.DataProtection);

            // Replace collections entirely
            AllowedHttpMethods = new List<string>(other.AllowedHttpMethods);
            AllowedHosts = new List<string>(other.AllowedHosts);

            // Merge custom properties
            foreach (var kvp in other.CustomProperties)
            {
                CustomProperties[kvp.Key] = kvp.Value;
            }
        }

        #endregion
    }
}
