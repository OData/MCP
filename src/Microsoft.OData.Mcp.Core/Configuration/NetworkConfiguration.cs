// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.OData.Mcp.Core.Configuration
{
    /// <summary>
    /// Configuration for network endpoints, ports, and transport protocols.
    /// </summary>
    /// <remarks>
    /// Network configuration specifies how the MCP server exposes its endpoints
    /// and communicates with clients. The configuration varies based on deployment mode.
    /// </remarks>
    public sealed class NetworkConfiguration
    {
        #region Properties

        /// <summary>
        /// Gets or sets the host address to bind to.
        /// </summary>
        /// <value>The host address or hostname (e.g., "localhost", "0.0.0.0", "example.com").</value>
        /// <remarks>
        /// For sidecar deployments, this determines the network interface to bind to.
        /// For middleware deployments, this is typically inherited from the host application.
        /// </remarks>
        public string Host { get; set; } = "localhost";

        /// <summary>
        /// Gets or sets the port number to listen on.
        /// </summary>
        /// <value>The TCP port number, or null to use the host application's port.</value>
        /// <remarks>
        /// For sidecar deployments, this is the port the MCP server will listen on.
        /// For middleware deployments, this should typically be null to inherit from the host.
        /// </remarks>
        public int? Port { get; set; }

        /// <summary>
        /// Gets or sets the base path for MCP endpoints.
        /// </summary>
        /// <value>The base path prefix for all MCP endpoints (e.g., "/mcp", "/api/mcp").</value>
        /// <remarks>
        /// All MCP endpoints will be prefixed with this path. This allows hosting
        /// MCP endpoints alongside other application endpoints.
        /// </remarks>
        public string BasePath { get; set; } = "/mcp";

        /// <summary>
        /// Gets or sets a value indicating whether to enable HTTPS.
        /// </summary>
        /// <value><c>true</c> to enable HTTPS; otherwise, <c>false</c>.</value>
        /// <remarks>
        /// For production deployments, HTTPS should be enabled for security.
        /// Development environments may disable HTTPS for simplicity.
        /// </remarks>
        public bool EnableHttps { get; set; } = true;

        /// <summary>
        /// Gets or sets the HTTPS port when HTTPS is enabled.
        /// </summary>
        /// <value>The HTTPS port number, or null to use default (443).</value>
        /// <remarks>
        /// This is only used when EnableHttps is true and in sidecar deployment mode.
        /// </remarks>
        public int? HttpsPort { get; set; }

        /// <summary>
        /// Gets or sets the SSL certificate configuration.
        /// </summary>
        /// <value>Configuration for SSL/TLS certificates.</value>
        /// <remarks>
        /// This configuration is used when HTTPS is enabled in sidecar deployment mode.
        /// </remarks>
        public SslConfiguration Ssl { get; set; } = new();

        /// <summary>
        /// Gets or sets a value indicating whether to use the host application's network configuration.
        /// </summary>
        /// <value><c>true</c> to inherit host configuration; otherwise, <c>false</c>.</value>
        /// <remarks>
        /// For middleware deployments, this should typically be true to inherit
        /// the host application's network settings.
        /// </remarks>
        public bool UseHostConfiguration { get; set; } = false;

        /// <summary>
        /// Gets or sets the maximum number of concurrent connections.
        /// </summary>
        /// <value>The maximum number of concurrent client connections.</value>
        /// <remarks>
        /// This helps prevent resource exhaustion from too many simultaneous connections.
        /// </remarks>
        public int MaxConcurrentConnections { get; set; } = 100;

        /// <summary>
        /// Gets or sets the connection timeout.
        /// </summary>
        /// <value>The maximum time to wait for new connections to be established.</value>
        /// <remarks>
        /// Connections that take longer than this timeout will be rejected.
        /// </remarks>
        public TimeSpan ConnectionTimeout { get; set; } = TimeSpan.FromSeconds(30);

        /// <summary>
        /// Gets or sets the request timeout.
        /// </summary>
        /// <value>The maximum time to wait for request processing.</value>
        /// <remarks>
        /// Requests that take longer than this timeout will be cancelled.
        /// </remarks>
        public TimeSpan RequestTimeout { get; set; } = TimeSpan.FromMinutes(5);

        /// <summary>
        /// Gets or sets the keep-alive timeout.
        /// </summary>
        /// <value>The maximum time to keep idle connections alive.</value>
        /// <remarks>
        /// Idle connections will be closed after this timeout to free resources.
        /// </remarks>
        public TimeSpan KeepAliveTimeout { get; set; } = TimeSpan.FromMinutes(2);

        /// <summary>
        /// Gets or sets the maximum request body size.
        /// </summary>
        /// <value>The maximum size in bytes for HTTP request bodies.</value>
        /// <remarks>
        /// Requests larger than this size will be rejected to prevent memory exhaustion.
        /// </remarks>
        public long MaxRequestBodySize { get; set; } = 1024 * 1024; // 1MB

        /// <summary>
        /// Gets or sets the CORS configuration.
        /// </summary>
        /// <value>Configuration for Cross-Origin Resource Sharing.</value>
        /// <remarks>
        /// CORS configuration allows web applications from different domains
        /// to access the MCP server endpoints.
        /// </remarks>
        public CorsConfiguration Cors { get; set; } = new();

        /// <summary>
        /// Gets or sets the compression configuration.
        /// </summary>
        /// <value>Configuration for HTTP response compression.</value>
        /// <remarks>
        /// Compression can reduce bandwidth usage for large responses.
        /// </remarks>
        public CompressionConfiguration Compression { get; set; } = new();

        /// <summary>
        /// Gets or sets custom network properties.
        /// </summary>
        /// <value>A dictionary of custom network configuration values.</value>
        /// <remarks>
        /// Custom properties allow extending the configuration with deployment-specific
        /// network settings.
        /// </remarks>
        public Dictionary<string, object> CustomProperties { get; set; } = [];

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="NetworkConfiguration"/> class.
        /// </summary>
        public NetworkConfiguration()
        {
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Validates the network configuration.
        /// </summary>
        /// <param name="deploymentMode">The deployment mode for context-specific validation.</param>
        /// <returns>A collection of validation errors, or empty if the configuration is valid.</returns>
        public IEnumerable<string> Validate(McpDeploymentMode deploymentMode)
        {
            var errors = new List<string>();

            if (string.IsNullOrWhiteSpace(Host))
            {
                errors.Add("Host cannot be null or whitespace");
            }

            if (string.IsNullOrWhiteSpace(BasePath))
            {
                errors.Add("BasePath cannot be null or whitespace");
            }
            else if (!BasePath.StartsWith('/'))
            {
                errors.Add("BasePath must start with a forward slash");
            }

            // Port validation for sidecar mode
            if (deploymentMode == McpDeploymentMode.Sidecar && !UseHostConfiguration)
            {
                if (!Port.HasValue)
                {
                    errors.Add("Port is required for sidecar deployment when not using host configuration");
                }
                else if (Port.Value <= 0 || Port.Value > 65535)
                {
                    errors.Add("Port must be between 1 and 65535");
                }

                if (EnableHttps && HttpsPort.HasValue)
                {
                    if (HttpsPort.Value <= 0 || HttpsPort.Value > 65535)
                    {
                        errors.Add("HttpsPort must be between 1 and 65535");
                    }
                    if (Port.HasValue && HttpsPort.Value == Port.Value)
                    {
                        errors.Add("HttpsPort cannot be the same as Port");
                    }
                }
            }

            if (MaxConcurrentConnections <= 0)
            {
                errors.Add("MaxConcurrentConnections must be greater than zero");
            }

            if (ConnectionTimeout <= TimeSpan.Zero)
            {
                errors.Add("ConnectionTimeout must be greater than zero");
            }

            if (RequestTimeout <= TimeSpan.Zero)
            {
                errors.Add("RequestTimeout must be greater than zero");
            }

            if (KeepAliveTimeout < TimeSpan.Zero)
            {
                errors.Add("KeepAliveTimeout cannot be negative");
            }

            if (MaxRequestBodySize <= 0)
            {
                errors.Add("MaxRequestBodySize must be greater than zero");
            }

            // Validate SSL configuration if HTTPS is enabled
            if (EnableHttps && deploymentMode == McpDeploymentMode.Sidecar)
            {
                var sslErrors = Ssl.Validate();
                errors.AddRange(sslErrors.Select(e => $"SSL: {e}"));
            }

            // Validate CORS configuration
            var corsErrors = Cors.Validate();
            errors.AddRange(corsErrors.Select(e => $"CORS: {e}"));

            // Validate compression configuration
            var compressionErrors = Compression.Validate();
            errors.AddRange(compressionErrors.Select(e => $"Compression: {e}"));

            return errors;
        }

        /// <summary>
        /// Gets the base URL for the MCP server.
        /// </summary>
        /// <param name="scheme">The URL scheme to use (http or https).</param>
        /// <returns>The base URL for the MCP server.</returns>
        public string GetBaseUrl(string? scheme = null)
        {
            scheme ??= EnableHttps ? "https" : "http";
            
            var port = scheme == "https" ? (HttpsPort ?? 443) : (Port ?? 80);
            var portString = (scheme == "https" && port == 443) || (scheme == "http" && port == 80) 
                ? string.Empty 
                : $":{port}";

            return $"{scheme}://{Host}{portString}";
        }

        /// <summary>
        /// Gets the full URL for an MCP endpoint.
        /// </summary>
        /// <param name="endpoint">The endpoint path (e.g., "tools", "metadata").</param>
        /// <param name="scheme">The URL scheme to use (http or https).</param>
        /// <returns>The complete URL for the endpoint.</returns>
        public string GetEndpointUrl(string endpoint, string? scheme = null)
        {
            if (string.IsNullOrWhiteSpace(endpoint))
            {
                endpoint = string.Empty;
            }

            var baseUrl = GetBaseUrl(scheme);
            var basePath = BasePath.TrimEnd('/');
            var endpointPath = endpoint.StartsWith('/') ? endpoint : $"/{endpoint}";

            return $"{baseUrl}{basePath}{endpointPath}";
        }

        /// <summary>
        /// Creates a copy of this configuration.
        /// </summary>
        /// <returns>A new instance with the same settings.</returns>
        public NetworkConfiguration Clone()
        {
            return new NetworkConfiguration
            {
                Host = Host,
                Port = Port,
                BasePath = BasePath,
                EnableHttps = EnableHttps,
                HttpsPort = HttpsPort,
                Ssl = Ssl.Clone(),
                UseHostConfiguration = UseHostConfiguration,
                MaxConcurrentConnections = MaxConcurrentConnections,
                ConnectionTimeout = ConnectionTimeout,
                RequestTimeout = RequestTimeout,
                KeepAliveTimeout = KeepAliveTimeout,
                MaxRequestBodySize = MaxRequestBodySize,
                Cors = Cors.Clone(),
                Compression = Compression.Clone(),
                CustomProperties = new Dictionary<string, object>(CustomProperties)
            };
        }

        /// <summary>
        /// Merges another configuration into this one, with the other configuration taking precedence.
        /// </summary>
        /// <param name="other">The configuration to merge into this one.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="other"/> is null.</exception>
        public void MergeWith(NetworkConfiguration other)
        {
            ArgumentNullException.ThrowIfNull(other);

            if (!string.IsNullOrWhiteSpace(other.Host)) Host = other.Host;
            if (!string.IsNullOrWhiteSpace(other.BasePath)) BasePath = other.BasePath;
            
            Port = other.Port ?? Port;
            EnableHttps = other.EnableHttps;
            HttpsPort = other.HttpsPort ?? HttpsPort;
            UseHostConfiguration = other.UseHostConfiguration;
            MaxConcurrentConnections = other.MaxConcurrentConnections;
            ConnectionTimeout = other.ConnectionTimeout;
            RequestTimeout = other.RequestTimeout;
            KeepAliveTimeout = other.KeepAliveTimeout;
            MaxRequestBodySize = other.MaxRequestBodySize;

            Ssl.MergeWith(other.Ssl);
            Cors.MergeWith(other.Cors);
            Compression.MergeWith(other.Compression);

            // Merge custom properties
            foreach (var kvp in other.CustomProperties)
            {
                CustomProperties[kvp.Key] = kvp.Value;
            }
        }

        #endregion
    }

    /// <summary>
    /// SSL/TLS certificate configuration.
    /// </summary>
    public sealed class SslConfiguration
    {
        /// <summary>
        /// Gets or sets the path to the SSL certificate file.
        /// </summary>
        /// <value>The file path to the SSL certificate (.pfx, .p12, or .crt file).</value>
        public string? CertificatePath { get; set; }

        /// <summary>
        /// Gets or sets the password for the SSL certificate.
        /// </summary>
        /// <value>The password to decrypt the certificate file.</value>
        public string? CertificatePassword { get; set; }

        /// <summary>
        /// Gets or sets the certificate store location.
        /// </summary>
        /// <value>The certificate store location for loading certificates.</value>
        public CertificateStoreLocation StoreLocation { get; set; } = CertificateStoreLocation.CurrentUser;

        /// <summary>
        /// Gets or sets the certificate store name.
        /// </summary>
        /// <value>The certificate store name for loading certificates.</value>
        public string StoreName { get; set; } = "My";

        /// <summary>
        /// Gets or sets the certificate thumbprint.
        /// </summary>
        /// <value>The thumbprint of the certificate to load from the store.</value>
        public string? Thumbprint { get; set; }

        /// <summary>
        /// Gets or sets the certificate subject name.
        /// </summary>
        /// <value>The subject name of the certificate to load from the store.</value>
        public string? SubjectName { get; set; }

        /// <summary>
        /// Validates the SSL configuration.
        /// </summary>
        /// <returns>Validation errors.</returns>
        public IEnumerable<string> Validate()
        {
            var errors = new List<string>();

            var hasFile = !string.IsNullOrWhiteSpace(CertificatePath);
            var hasThumbprint = !string.IsNullOrWhiteSpace(Thumbprint);
            var hasSubject = !string.IsNullOrWhiteSpace(SubjectName);

            if (!hasFile && !hasThumbprint && !hasSubject)
            {
                errors.Add("SSL certificate must be specified by file path, thumbprint, or subject name");
            }

            return errors;
        }

        /// <summary>
        /// Creates a copy of this configuration.
        /// </summary>
        /// <returns>A new instance with the same settings.</returns>
        public SslConfiguration Clone()
        {
            return new SslConfiguration
            {
                CertificatePath = CertificatePath,
                CertificatePassword = CertificatePassword,
                StoreLocation = StoreLocation,
                StoreName = StoreName,
                Thumbprint = Thumbprint,
                SubjectName = SubjectName
            };
        }

        /// <summary>
        /// Merges another configuration into this one.
        /// </summary>
        /// <param name="other">The configuration to merge.</param>
        public void MergeWith(SslConfiguration other)
        {
            if (other is null) return;

            if (!string.IsNullOrWhiteSpace(other.CertificatePath)) CertificatePath = other.CertificatePath;
            if (!string.IsNullOrWhiteSpace(other.CertificatePassword)) CertificatePassword = other.CertificatePassword;
            if (!string.IsNullOrWhiteSpace(other.StoreName)) StoreName = other.StoreName;
            if (!string.IsNullOrWhiteSpace(other.Thumbprint)) Thumbprint = other.Thumbprint;
            if (!string.IsNullOrWhiteSpace(other.SubjectName)) SubjectName = other.SubjectName;
            StoreLocation = other.StoreLocation;
        }
    }

    /// <summary>
    /// Certificate store locations.
    /// </summary>
    public enum CertificateStoreLocation
    {
        /// <summary>
        /// Current user certificate store.
        /// </summary>
        CurrentUser,

        /// <summary>
        /// Local machine certificate store.
        /// </summary>
        LocalMachine
    }

    /// <summary>
    /// CORS (Cross-Origin Resource Sharing) configuration.
    /// </summary>
    public sealed class CorsConfiguration
    {
        /// <summary>
        /// Gets or sets a value indicating whether CORS is enabled.
        /// </summary>
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// Gets or sets the allowed origins.
        /// </summary>
        public List<string> AllowedOrigins { get; set; } = ["*"];

        /// <summary>
        /// Gets or sets the allowed methods.
        /// </summary>
        public List<string> AllowedMethods { get; set; } = ["GET", "POST", "OPTIONS"];

        /// <summary>
        /// Gets or sets the allowed headers.
        /// </summary>
        public List<string> AllowedHeaders { get; set; } = ["*"];

        /// <summary>
        /// Gets or sets a value indicating whether credentials are allowed.
        /// </summary>
        public bool AllowCredentials { get; set; } = false;

        /// <summary>
        /// Gets or sets the maximum age for preflight requests.
        /// </summary>
        public TimeSpan MaxAge { get; set; } = TimeSpan.FromMinutes(5);

        /// <summary>
        /// Validates the CORS configuration.
        /// </summary>
        /// <returns>Validation errors.</returns>
        public IEnumerable<string> Validate()
        {
            var errors = new List<string>();

            if (MaxAge < TimeSpan.Zero)
            {
                errors.Add("CORS MaxAge cannot be negative");
            }

            return errors;
        }

        /// <summary>
        /// Creates a copy of this configuration.
        /// </summary>
        /// <returns>A new instance with the same settings.</returns>
        public CorsConfiguration Clone()
        {
            return new CorsConfiguration
            {
                Enabled = Enabled,
                AllowedOrigins = new List<string>(AllowedOrigins),
                AllowedMethods = new List<string>(AllowedMethods),
                AllowedHeaders = new List<string>(AllowedHeaders),
                AllowCredentials = AllowCredentials,
                MaxAge = MaxAge
            };
        }

        /// <summary>
        /// Merges another configuration into this one.
        /// </summary>
        /// <param name="other">The configuration to merge.</param>
        public void MergeWith(CorsConfiguration other)
        {
            if (other is null) return;

            Enabled = other.Enabled;
            AllowCredentials = other.AllowCredentials;
            MaxAge = other.MaxAge;

            // Replace collections entirely
            AllowedOrigins = new List<string>(other.AllowedOrigins);
            AllowedMethods = new List<string>(other.AllowedMethods);
            AllowedHeaders = new List<string>(other.AllowedHeaders);
        }
    }

    /// <summary>
    /// HTTP response compression configuration.
    /// </summary>
    public sealed class CompressionConfiguration
    {
        /// <summary>
        /// Gets or sets a value indicating whether compression is enabled.
        /// </summary>
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// Gets or sets the compression algorithms to use.
        /// </summary>
        public List<string> Algorithms { get; set; } = ["gzip", "deflate"];

        /// <summary>
        /// Gets or sets the minimum response size to compress.
        /// </summary>
        public int MinimumSize { get; set; } = 1024; // 1KB

        /// <summary>
        /// Gets or sets the MIME types to compress.
        /// </summary>
        public List<string> MimeTypes { get; set; } =
        [
            "application/json",
            "application/xml",
            "text/plain",
            "text/html",
            "text/css",
            "text/javascript"
        ];

        /// <summary>
        /// Validates the compression configuration.
        /// </summary>
        /// <returns>Validation errors.</returns>
        public IEnumerable<string> Validate()
        {
            var errors = new List<string>();

            if (MinimumSize < 0)
            {
                errors.Add("Compression MinimumSize cannot be negative");
            }

            return errors;
        }

        /// <summary>
        /// Creates a copy of this configuration.
        /// </summary>
        /// <returns>A new instance with the same settings.</returns>
        public CompressionConfiguration Clone()
        {
            return new CompressionConfiguration
            {
                Enabled = Enabled,
                Algorithms = new List<string>(Algorithms),
                MinimumSize = MinimumSize,
                MimeTypes = new List<string>(MimeTypes)
            };
        }

        /// <summary>
        /// Merges another configuration into this one.
        /// </summary>
        /// <param name="other">The configuration to merge.</param>
        public void MergeWith(CompressionConfiguration other)
        {
            if (other is null) return;

            Enabled = other.Enabled;
            MinimumSize = other.MinimumSize;

            // Replace collections entirely
            Algorithms = new List<string>(other.Algorithms);
            MimeTypes = new List<string>(other.MimeTypes);
        }
    }
}