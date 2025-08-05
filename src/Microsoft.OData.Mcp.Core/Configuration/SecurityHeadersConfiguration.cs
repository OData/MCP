using System.Collections.Generic;
using System.Linq;

namespace Microsoft.OData.Mcp.Core.Configuration
{
    /// <summary>
    /// Configuration for security-related HTTP headers.
    /// </summary>
    /// <remarks>
    /// Security headers configuration controls the HTTP headers that are sent
    /// with responses to provide additional protection against various web-based
    /// attacks such as XSS, clickjacking, MIME sniffing, and protocol downgrade attacks.
    /// </remarks>
    public sealed class SecurityHeadersConfiguration
    {
        /// <summary>
        /// Gets or sets a value indicating whether HTTP Strict Transport Security (HSTS) is enabled.
        /// </summary>
        /// <value><c>true</c> to enable HSTS; otherwise, <c>false</c>.</value>
        /// <remarks>
        /// HSTS forces browsers to use HTTPS connections and prevents protocol downgrade attacks.
        /// This should be enabled for production environments using HTTPS.
        /// </remarks>
        public bool EnableHsts { get; set; } = true;

        /// <summary>
        /// Gets or sets a value indicating whether X-Content-Type-Options header is enabled.
        /// </summary>
        /// <value><c>true</c> to enable X-Content-Type-Options; otherwise, <c>false</c>.</value>
        /// <remarks>
        /// The X-Content-Type-Options header prevents browsers from MIME sniffing,
        /// which can lead to security vulnerabilities when serving user-uploaded content.
        /// </remarks>
        public bool EnableXContentTypeOptions { get; set; } = true;

        /// <summary>
        /// Gets or sets a value indicating whether X-Frame-Options header is enabled.
        /// </summary>
        /// <value><c>true</c> to enable X-Frame-Options; otherwise, <c>false</c>.</value>
        /// <remarks>
        /// The X-Frame-Options header prevents the page from being embedded in frames,
        /// protecting against clickjacking attacks.
        /// </remarks>
        public bool EnableXFrameOptions { get; set; } = true;

        /// <summary>
        /// Gets or sets the X-Frame-Options header value.
        /// </summary>
        /// <value>The X-Frame-Options directive value.</value>
        /// <remarks>
        /// Valid values are "DENY" (never allow framing), "SAMEORIGIN" (allow framing from same origin),
        /// or "ALLOW-FROM uri" (allow framing from specific URI).
        /// </remarks>
        public string XFrameOptions { get; set; } = "DENY";

        /// <summary>
        /// Creates a security headers configuration optimized for development environments.
        /// </summary>
        /// <returns>A security headers configuration suitable for development use.</returns>
        public static SecurityHeadersConfiguration ForDevelopment() => new() { EnableHsts = false };

        /// <summary>
        /// Creates a security headers configuration optimized for production environments.
        /// </summary>
        /// <returns>A security headers configuration suitable for production use.</returns>
        public static SecurityHeadersConfiguration ForProduction() => new();

        /// <summary>
        /// Validates the security headers configuration.
        /// </summary>
        /// <returns>A collection of validation errors, or empty if the configuration is valid.</returns>
        public IEnumerable<string> Validate() => Enumerable.Empty<string>();

        /// <summary>
        /// Creates a copy of this security headers configuration.
        /// </summary>
        /// <returns>A new instance with the same settings.</returns>
        public SecurityHeadersConfiguration Clone() => new() { EnableHsts = EnableHsts, EnableXContentTypeOptions = EnableXContentTypeOptions, EnableXFrameOptions = EnableXFrameOptions, XFrameOptions = XFrameOptions };

        /// <summary>
        /// Merges another security headers configuration into this one.
        /// </summary>
        /// <param name="other">The configuration to merge into this one.</param>
        public void MergeWith(SecurityHeadersConfiguration other) { if (other != null) { EnableHsts = other.EnableHsts; EnableXContentTypeOptions = other.EnableXContentTypeOptions; EnableXFrameOptions = other.EnableXFrameOptions; XFrameOptions = other.XFrameOptions; } }
    }
}
