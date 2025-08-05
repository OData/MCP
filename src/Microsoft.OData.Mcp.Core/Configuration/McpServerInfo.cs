using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Microsoft.OData.Mcp.Core.Configuration
{
    /// <summary>
    /// Basic information about an MCP server instance.
    /// </summary>
    /// <remarks>
    /// This information is used for identification, documentation, and client discovery.
    /// It's exposed through the server info endpoint and helps clients understand
    /// the capabilities and characteristics of the MCP server.
    /// </remarks>
    public sealed class McpServerInfo
    {
        #region Properties

        /// <summary>
        /// Gets or sets the display name of the MCP server.
        /// </summary>
        /// <value>A human-readable name for the server.</value>
        /// <remarks>
        /// This name is displayed in client interfaces and should clearly identify
        /// the purpose or domain of the MCP server.
        /// </remarks>
        public string Name { get; set; } = "OData MCP Server";

        /// <summary>
        /// Gets or sets the description of the MCP server.
        /// </summary>
        /// <value>A detailed description of the server's purpose and capabilities.</value>
        /// <remarks>
        /// The description helps users understand what the server provides and
        /// how it can be used in their applications.
        /// </remarks>
        public string Description { get; set; } = "Model Context Protocol server for OData services";

        /// <summary>
        /// Gets or sets the version of the MCP server.
        /// </summary>
        /// <value>The semantic version of the server instance.</value>
        /// <remarks>
        /// Version information helps clients determine compatibility and
        /// should follow semantic versioning principles.
        /// </remarks>
        public string Version { get; set; } = "1.0.0";

        /// <summary>
        /// Gets or sets the vendor or organization that created the server.
        /// </summary>
        /// <value>The name of the vendor or organization.</value>
        /// <remarks>
        /// Vendor information helps with support and identification of the
        /// server implementation.
        /// </remarks>
        public string? Vendor { get; set; } = "Microsoft";

        /// <summary>
        /// Gets or sets the contact information for support.
        /// </summary>
        /// <value>Contact information such as email, URL, or phone number.</value>
        /// <remarks>
        /// Contact information provides users with a way to get help or
        /// report issues with the MCP server.
        /// </remarks>
        public string? Contact { get; set; }

        /// <summary>
        /// Gets or sets the license under which the server is distributed.
        /// </summary>
        /// <value>The license identifier or description.</value>
        /// <remarks>
        /// License information helps users understand the terms under which
        /// they can use the MCP server.
        /// </remarks>
        public string? License { get; set; } = "MIT";

        /// <summary>
        /// Gets or sets the URL to the server's documentation.
        /// </summary>
        /// <value>A URL pointing to comprehensive documentation.</value>
        /// <remarks>
        /// Documentation URL provides users with detailed information about
        /// how to use and configure the MCP server.
        /// </remarks>
        public string? DocumentationUrl { get; set; }

        /// <summary>
        /// Gets or sets the URL to the server's source code repository.
        /// </summary>
        /// <value>A URL pointing to the source code repository.</value>
        /// <remarks>
        /// Repository URL allows users to examine the source code, report issues,
        /// or contribute to the development of the MCP server.
        /// </remarks>
        public string? RepositoryUrl { get; set; }

        /// <summary>
        /// Gets or sets the supported MCP protocol version.
        /// </summary>
        /// <value>The version of the MCP protocol supported by this server.</value>
        /// <remarks>
        /// Protocol version information helps clients determine if they are
        /// compatible with the server's implementation.
        /// </remarks>
        public string McpProtocolVersion { get; set; } = "1.0";

        /// <summary>
        /// Gets or sets the server capabilities.
        /// </summary>
        /// <value>A list of capabilities supported by the server.</value>
        /// <remarks>
        /// Capabilities help clients understand what features are available
        /// and how they can interact with the server.
        /// </remarks>
        public List<string> Capabilities { get; set; } = new()
        {
            "tools",
            "authentication",
            "metadata_discovery",
            "odata_integration"
        };

        /// <summary>
        /// Gets or sets additional metadata about the server.
        /// </summary>
        /// <value>A dictionary of custom metadata key-value pairs.</value>
        /// <remarks>
        /// Custom metadata allows extending the server information with
        /// application-specific details that don't fit into standard properties.
        /// </remarks>
        public Dictionary<string, object> Metadata { get; set; } = new();

        /// <summary>
        /// Gets or sets the server instance identifier.
        /// </summary>
        /// <value>A unique identifier for this server instance.</value>
        /// <remarks>
        /// Instance ID helps distinguish between multiple instances of the same
        /// server type and is useful for monitoring and debugging.
        /// </remarks>
        public string InstanceId { get; set; } = Guid.NewGuid().ToString();

        /// <summary>
        /// Gets or sets the timestamp when the server was started.
        /// </summary>
        /// <value>The UTC timestamp when the server instance was created.</value>
        /// <remarks>
        /// Start time provides information about server uptime and can be
        /// useful for monitoring and diagnostics.
        /// </remarks>
        public DateTime StartedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Gets or sets the build information for the server.
        /// </summary>
        /// <value>Information about the build that created this server instance.</value>
        /// <remarks>
        /// Build information helps with troubleshooting and ensures the correct
        /// version is deployed in different environments.
        /// </remarks>
        public BuildInfo? BuildInfo { get; set; }

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="McpServerInfo"/> class.
        /// </summary>
        public McpServerInfo()
        {
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Validates the server information for completeness and correctness.
        /// </summary>
        /// <returns>A collection of validation errors, or empty if the information is valid.</returns>
        public IEnumerable<string> Validate()
        {
            var errors = new List<string>();

            if (string.IsNullOrWhiteSpace(Name))
            {
                errors.Add("Server name is required");
            }

            if (string.IsNullOrWhiteSpace(Description))
            {
                errors.Add("Server description is required");
            }

            if (string.IsNullOrWhiteSpace(Version))
            {
                errors.Add("Server version is required");
            }
            else if (!IsValidSemanticVersion(Version))
            {
                errors.Add("Server version must be a valid semantic version");
            }

            if (string.IsNullOrWhiteSpace(McpProtocolVersion))
            {
                errors.Add("MCP protocol version is required");
            }

            if (string.IsNullOrWhiteSpace(InstanceId))
            {
                errors.Add("Instance ID is required");
            }

            // Validate URLs if provided
            if (!string.IsNullOrWhiteSpace(DocumentationUrl) && !IsValidUrl(DocumentationUrl))
            {
                errors.Add("Documentation URL must be a valid URL");
            }

            if (!string.IsNullOrWhiteSpace(RepositoryUrl) && !IsValidUrl(RepositoryUrl))
            {
                errors.Add("Repository URL must be a valid URL");
            }

            return errors;
        }

        /// <summary>
        /// Adds a capability to the server.
        /// </summary>
        /// <param name="capability">The capability to add.</param>
        /// <exception cref="ArgumentException">Thrown when <paramref name="capability"/> is null or whitespace.</exception>
        public void AddCapability(string capability)
        {
#if NET8_0_OR_GREATER
            ArgumentException.ThrowIfNullOrWhiteSpace(capability);
#else
            if (string.IsNullOrWhiteSpace(capability))
            {
                throw new ArgumentException("Capability cannot be null or whitespace.", nameof(capability));
            }
#endif

            if (!Capabilities.Contains(capability, StringComparer.OrdinalIgnoreCase))
            {
                Capabilities.Add(capability);
            }
        }

        /// <summary>
        /// Removes a capability from the server.
        /// </summary>
        /// <param name="capability">The capability to remove.</param>
        /// <returns><c>true</c> if the capability was removed; otherwise, <c>false</c>.</returns>
        public bool RemoveCapability(string capability)
        {
            if (string.IsNullOrWhiteSpace(capability))
            {
                return false;
            }

            return Capabilities.RemoveAll(c => string.Equals(c, capability, StringComparison.OrdinalIgnoreCase)) > 0;
        }

        /// <summary>
        /// Determines whether the server has the specified capability.
        /// </summary>
        /// <param name="capability">The capability to check for.</param>
        /// <returns><c>true</c> if the server has the capability; otherwise, <c>false</c>.</returns>
        public bool HasCapability(string capability)
        {
            return !string.IsNullOrWhiteSpace(capability) && 
                   Capabilities.Contains(capability, StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Adds metadata to the server information.
        /// </summary>
        /// <param name="key">The metadata key.</param>
        /// <param name="value">The metadata value.</param>
        /// <exception cref="ArgumentException">Thrown when <paramref name="key"/> is null or whitespace.</exception>
        public void AddMetadata(string key, object value)
        {
#if NET8_0_OR_GREATER
            ArgumentException.ThrowIfNullOrWhiteSpace(key);
#else
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new ArgumentException("Metadata key cannot be null or whitespace.", nameof(key));
            }
#endif

            Metadata[key] = value;
        }

        /// <summary>
        /// Gets metadata value by key.
        /// </summary>
        /// <typeparam name="T">The type of the metadata value.</typeparam>
        /// <param name="key">The metadata key.</param>
        /// <returns>The metadata value if found and of the correct type; otherwise, the default value.</returns>
        public T? GetMetadata<T>(string key)
        {
            if (Metadata.TryGetValue(key, out var value) && value is T typedValue)
            {
                return typedValue;
            }
            return default;
        }

        /// <summary>
        /// Gets the server uptime.
        /// </summary>
        /// <returns>The time elapsed since the server was started.</returns>
        public TimeSpan GetUptime()
        {
            return DateTime.UtcNow - StartedAt;
        }

        /// <summary>
        /// Creates a copy of this server information.
        /// </summary>
        /// <returns>A new instance with the same settings.</returns>
        public McpServerInfo Clone()
        {
            return new McpServerInfo
            {
                Name = Name,
                Description = Description,
                Version = Version,
                Vendor = Vendor,
                Contact = Contact,
                License = License,
                DocumentationUrl = DocumentationUrl,
                RepositoryUrl = RepositoryUrl,
                McpProtocolVersion = McpProtocolVersion,
                Capabilities = new List<string>(Capabilities),
                Metadata = new Dictionary<string, object>(Metadata),
                InstanceId = InstanceId,
                StartedAt = StartedAt,
                BuildInfo = BuildInfo?.Clone()
            };
        }

        /// <summary>
        /// Merges another server info into this one, with the other taking precedence.
        /// </summary>
        /// <param name="other">The server info to merge into this one.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="other"/> is null.</exception>
        public void MergeWith(McpServerInfo other)
        {
#if NET8_0_OR_GREATER
            ArgumentNullException.ThrowIfNull(other);
#else
            if (other is null)
            {
                throw new ArgumentNullException(nameof(other));
            }
#endif

            if (!string.IsNullOrWhiteSpace(other.Name)) Name = other.Name;
            if (!string.IsNullOrWhiteSpace(other.Description)) Description = other.Description;
            if (!string.IsNullOrWhiteSpace(other.Version)) Version = other.Version;
            if (!string.IsNullOrWhiteSpace(other.Vendor)) Vendor = other.Vendor;
            if (!string.IsNullOrWhiteSpace(other.Contact)) Contact = other.Contact;
            if (!string.IsNullOrWhiteSpace(other.License)) License = other.License;
            if (!string.IsNullOrWhiteSpace(other.DocumentationUrl)) DocumentationUrl = other.DocumentationUrl;
            if (!string.IsNullOrWhiteSpace(other.RepositoryUrl)) RepositoryUrl = other.RepositoryUrl;
            if (!string.IsNullOrWhiteSpace(other.McpProtocolVersion)) McpProtocolVersion = other.McpProtocolVersion;

            // Merge capabilities
            foreach (var capability in other.Capabilities)
            {
                AddCapability(capability);
            }

            // Merge metadata
            foreach (var kvp in other.Metadata)
            {
                Metadata[kvp.Key] = kvp.Value;
            }

            if (other.BuildInfo is not null)
            {
                BuildInfo = other.BuildInfo.Clone();
            }
        }

        /// <summary>
        /// Returns a string representation of the server information.
        /// </summary>
        /// <returns>A summary of the server information.</returns>
        public override string ToString()
        {
            var uptime = GetUptime();
            return $"{Name} v{Version} (Instance: {InstanceId[..8]}, Uptime: {uptime:d\\.hh\\:mm\\:ss})";
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Validates if a version string follows semantic versioning.
        /// </summary>
        /// <param name="version">The version string to validate.</param>
        /// <returns><c>true</c> if the version is valid; otherwise, <c>false</c>.</returns>
        private static bool IsValidSemanticVersion(string version)
        {
            if (string.IsNullOrWhiteSpace(version))
            {
                return false;
            }

            // Simple semantic version regex pattern
            var pattern = @"^\d+\.\d+\.\d+(?:-[\w\-\.]+)?(?:\+[\w\-\.]+)?$";
            return System.Text.RegularExpressions.Regex.IsMatch(version, pattern);
        }

        /// <summary>
        /// Validates if a string is a valid URL.
        /// </summary>
        /// <param name="url">The URL string to validate.</param>
        /// <returns><c>true</c> if the URL is valid; otherwise, <c>false</c>.</returns>
        private static bool IsValidUrl(string url)
        {
            return Uri.TryCreate(url, UriKind.Absolute, out var validatedUri) &&
                   (validatedUri.Scheme == Uri.UriSchemeHttp || validatedUri.Scheme == Uri.UriSchemeHttps);
        }

        #endregion
    }

    /// <summary>
    /// Build information for the MCP server.
    /// </summary>
    public sealed class BuildInfo
    {
        /// <summary>
        /// Gets or sets the build number or identifier.
        /// </summary>
        /// <value>The build number or identifier from the CI/CD system.</value>
        public string? BuildNumber { get; set; }

        /// <summary>
        /// Gets or sets the commit hash of the source code.
        /// </summary>
        /// <value>The Git commit hash or similar version control identifier.</value>
        public string? CommitHash { get; set; }

        /// <summary>
        /// Gets or sets the branch name from which the build was created.
        /// </summary>
        /// <value>The source control branch name.</value>
        public string? Branch { get; set; }

        /// <summary>
        /// Gets or sets the timestamp when the build was created.
        /// </summary>
        /// <value>The UTC timestamp of the build.</value>
        public DateTime? BuildTimestamp { get; set; }

        /// <summary>
        /// Gets or sets the build configuration (e.g., Debug, Release).
        /// </summary>
        /// <value>The build configuration used to compile the server.</value>
        public string? Configuration { get; set; }

        /// <summary>
        /// Gets or sets the target framework for which the server was built.
        /// </summary>
        /// <value>The .NET target framework (e.g., "net8.0", "net9.0").</value>
        public string? TargetFramework { get; set; }

        /// <summary>
        /// Creates a copy of this build information.
        /// </summary>
        /// <returns>A new instance with the same values.</returns>
        public BuildInfo Clone()
        {
            return new BuildInfo
            {
                BuildNumber = BuildNumber,
                CommitHash = CommitHash,
                Branch = Branch,
                BuildTimestamp = BuildTimestamp,
                Configuration = Configuration,
                TargetFramework = TargetFramework
            };
        }
    }
}