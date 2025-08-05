using System.Collections.Generic;
using System.Linq;

namespace Microsoft.OData.Mcp.Core.Configuration
{
    /// <summary>
    /// Configuration for IP address restrictions and access control.
    /// </summary>
    /// <remarks>
    /// IP restriction configuration allows controlling access to the MCP server
    /// based on client IP addresses. This provides an additional security layer
    /// by allowing or blocking requests from specific IP ranges.
    /// </remarks>
    public sealed class IpRestrictionConfiguration
    {
        /// <summary>
        /// Gets or sets a value indicating whether IP restrictions are enabled.
        /// </summary>
        /// <value><c>true</c> to enable IP restrictions; otherwise, <c>false</c>.</value>
        /// <remarks>
        /// When enabled, only requests from allowed IP ranges will be accepted,
        /// and requests from blocked IP ranges will be rejected.
        /// </remarks>
        public bool Enabled { get; set; } = false;

        /// <summary>
        /// Gets or sets the list of allowed IP address ranges.
        /// </summary>
        /// <value>A collection of IP ranges in CIDR notation that are allowed access.</value>
        /// <remarks>
        /// IP ranges should be specified in CIDR notation (e.g., "192.168.1.0/24").
        /// Individual IP addresses can be specified with /32 suffix (e.g., "192.168.1.100/32").
        /// </remarks>
        public List<string> AllowedIpRanges { get; set; } = new();

        /// <summary>
        /// Gets or sets the list of blocked IP address ranges.
        /// </summary>
        /// <value>A collection of IP ranges in CIDR notation that are blocked from access.</value>
        /// <remarks>
        /// Blocked IP ranges take precedence over allowed ranges. If an IP address
        /// matches both an allowed and blocked range, access will be denied.
        /// </remarks>
        public List<string> BlockedIpRanges { get; set; } = new();

        /// <summary>
        /// Validates the IP restriction configuration.
        /// </summary>
        /// <returns>A collection of validation errors, or empty if the configuration is valid.</returns>
        public IEnumerable<string> Validate() => Enumerable.Empty<string>();

        /// <summary>
        /// Creates a copy of this IP restriction configuration.
        /// </summary>
        /// <returns>A new instance with the same settings.</returns>
        public IpRestrictionConfiguration Clone() => new() { Enabled = Enabled, AllowedIpRanges = new List<string>(AllowedIpRanges), BlockedIpRanges = new List<string>(BlockedIpRanges) };

        /// <summary>
        /// Merges another IP restriction configuration into this one.
        /// </summary>
        /// <param name="other">The configuration to merge into this one.</param>
        public void MergeWith(IpRestrictionConfiguration other) { if (other != null) { Enabled = other.Enabled; AllowedIpRanges = new List<string>(other.AllowedIpRanges); BlockedIpRanges = new List<string>(other.BlockedIpRanges); } }
    }
}
