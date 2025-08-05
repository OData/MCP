using System;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.OData.Mcp.Core.Configuration
{
    /// <summary>
    /// Configuration for connecting to and interacting with OData services.
    /// </summary>
    /// <remarks>
    /// This configuration specifies how the MCP server discovers and communicates
    /// with the underlying OData service, including metadata endpoints, authentication,
    /// and operational parameters.
    /// </remarks>
    public sealed class ODataServiceConfiguration
    {
        #region Properties

        /// <summary>
        /// Gets or sets the base URL of the OData service.
        /// </summary>
        /// <value>The root URL of the OData service.</value>
        /// <remarks>
        /// For sidecar deployments, this is the external URL of the OData service.
        /// For middleware deployments, this can be null to use the host application's base URL.
        /// </remarks>
        public string? BaseUrl { get; set; }

        /// <summary>
        /// Gets or sets the path to the OData metadata endpoint.
        /// </summary>
        /// <value>The relative path to the metadata endpoint (typically "/$metadata").</value>
        /// <remarks>
        /// This path is appended to the base URL to construct the full metadata endpoint URL.
        /// The metadata endpoint must return CSDL (Common Schema Definition Language) XML.
        /// </remarks>
        public string MetadataPath { get; set; } = "/$metadata";

        /// <summary>
        /// Gets or sets a value indicating whether to automatically discover metadata.
        /// </summary>
        /// <value><c>true</c> to automatically fetch and parse metadata; otherwise, <c>false</c>.</value>
        /// <remarks>
        /// When enabled, the MCP server will automatically fetch metadata from the configured
        /// endpoint and generate tools based on the discovered schema.
        /// </remarks>
        public bool AutoDiscoverMetadata { get; set; } = true;

        /// <summary>
        /// Gets or sets the interval for refreshing metadata.
        /// </summary>
        /// <value>The time interval between metadata refresh attempts.</value>
        /// <remarks>
        /// The MCP server will periodically refresh metadata to detect schema changes.
        /// Set to <see cref="TimeSpan.Zero"/> to disable automatic refresh.
        /// </remarks>
        public TimeSpan RefreshInterval { get; set; } = TimeSpan.FromMinutes(30);

        /// <summary>
        /// Gets or sets the timeout for HTTP requests to the OData service.
        /// </summary>
        /// <value>The maximum time to wait for HTTP responses.</value>
        /// <remarks>
        /// This timeout applies to all HTTP requests made to the OData service,
        /// including metadata discovery and data operations.
        /// </remarks>
        public TimeSpan RequestTimeout { get; set; } = TimeSpan.FromMinutes(2);

        /// <summary>
        /// Gets or sets the maximum number of retry attempts for failed requests.
        /// </summary>
        /// <value>The number of retry attempts for failed HTTP requests.</value>
        /// <remarks>
        /// When requests to the OData service fail due to transient errors,
        /// the MCP server will retry up to this number of times.
        /// </remarks>
        public int MaxRetryAttempts { get; set; } = 3;

        /// <summary>
        /// Gets or sets the base delay for retry attempts.
        /// </summary>
        /// <value>The initial delay before the first retry attempt.</value>
        /// <remarks>
        /// Subsequent retries will use exponential backoff based on this initial delay.
        /// </remarks>
        public TimeSpan RetryDelay { get; set; } = TimeSpan.FromSeconds(1);

        /// <summary>
        /// Gets or sets a value indicating whether to use the host application context.
        /// </summary>
        /// <value><c>true</c> to use host context for middleware deployments; otherwise, <c>false</c>.</value>
        /// <remarks>
        /// When enabled in middleware deployments, the MCP server will use the host
        /// application's HTTP context to make requests to the OData service.
        /// </remarks>
        public bool UseHostContext { get; set; } = false;

        /// <summary>
        /// Gets or sets the authentication configuration for the OData service.
        /// </summary>
        /// <value>Configuration for authenticating with the OData service.</value>
        /// <remarks>
        /// This configuration specifies how the MCP server authenticates with the
        /// underlying OData service when making requests on behalf of users.
        /// </remarks>
        public ODataAuthenticationConfiguration Authentication { get; set; } = new();

        /// <summary>
        /// Gets or sets the default headers to include in OData requests.
        /// </summary>
        /// <value>A dictionary of header name-value pairs to include in all requests.</value>
        /// <remarks>
        /// These headers will be added to all HTTP requests made to the OData service
        /// and can be used for custom authentication, tracing, or service identification.
        /// </remarks>
        public Dictionary<string, string> DefaultHeaders { get; set; } = new();

        /// <summary>
        /// Gets or sets the supported OData versions.
        /// </summary>
        /// <value>A list of OData versions supported by the service.</value>
        /// <remarks>
        /// This information helps the MCP server understand which OData features
        /// are available and how to construct appropriate requests.
        /// </remarks>
        public List<string> SupportedODataVersions { get; set; } = new() { "4.0", "4.01" };

        /// <summary>
        /// Gets or sets the maximum page size for query results.
        /// </summary>
        /// <value>The maximum number of entities to return in a single query response.</value>
        /// <remarks>
        /// This setting helps prevent excessively large responses that could impact
        /// performance or memory usage. Set to null for no limit.
        /// </remarks>
        public int? MaxPageSize { get; set; } = 1000;

        /// <summary>
        /// Gets or sets a value indicating whether to follow next links in paged results.
        /// </summary>
        /// <value><c>true</c> to automatically follow next links; otherwise, <c>false</c>.</value>
        /// <remarks>
        /// When enabled, the MCP server will automatically follow OData next links
        /// to retrieve additional pages of results.
        /// </remarks>
        public bool FollowNextLinks { get; set; } = true;

        /// <summary>
        /// Gets or sets the maximum number of pages to follow.
        /// </summary>
        /// <value>The maximum number of result pages to retrieve.</value>
        /// <remarks>
        /// This setting prevents infinite loops when following next links and
        /// limits the total amount of data retrieved in a single operation.
        /// </remarks>
        public int MaxPages { get; set; } = 10;

        /// <summary>
        /// Gets or sets a value indicating whether to validate SSL certificates.
        /// </summary>
        /// <value><c>true</c> to validate SSL certificates; otherwise, <c>false</c>.</value>
        /// <remarks>
        /// For development environments, SSL validation can be disabled to work
        /// with self-signed certificates. This should always be enabled in production.
        /// </remarks>
        public bool ValidateSSL { get; set; } = true;

        /// <summary>
        /// Gets or sets custom configuration properties.
        /// </summary>
        /// <value>A dictionary of custom configuration values.</value>
        /// <remarks>
        /// Custom properties allow extending the configuration with service-specific
        /// settings that don't fit into the standard configuration properties.
        /// </remarks>
        public Dictionary<string, object> CustomProperties { get; set; } = new();

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="ODataServiceConfiguration"/> class.
        /// </summary>
        public ODataServiceConfiguration()
        {
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Validates the OData service configuration.
        /// </summary>
        /// <param name="deploymentMode">The deployment mode for context-specific validation.</param>
        /// <returns>A collection of validation errors, or empty if the configuration is valid.</returns>
        public IEnumerable<string> Validate(McpDeploymentMode deploymentMode)
        {
            var errors = new List<string>();

            // Base URL validation depends on deployment mode
            if (deploymentMode == McpDeploymentMode.Sidecar)
            {
                if (string.IsNullOrWhiteSpace(BaseUrl))
                {
                    errors.Add("BaseUrl is required for sidecar deployment");
                }
                else if (!IsValidUrl(BaseUrl))
                {
                    errors.Add("BaseUrl must be a valid HTTP or HTTPS URL");
                }
            }

            if (string.IsNullOrWhiteSpace(MetadataPath))
            {
                errors.Add("MetadataPath is required");
            }
            else if (!MetadataPath.StartsWith('/'))
            {
                errors.Add("MetadataPath must start with a forward slash");
            }

            if (RefreshInterval < TimeSpan.Zero)
            {
                errors.Add("RefreshInterval cannot be negative");
            }

            if (RequestTimeout <= TimeSpan.Zero)
            {
                errors.Add("RequestTimeout must be greater than zero");
            }

            if (MaxRetryAttempts < 0)
            {
                errors.Add("MaxRetryAttempts cannot be negative");
            }

            if (RetryDelay < TimeSpan.Zero)
            {
                errors.Add("RetryDelay cannot be negative");
            }

            if (MaxPageSize.HasValue && MaxPageSize.Value <= 0)
            {
                errors.Add("MaxPageSize must be greater than zero when specified");
            }

            if (MaxPages <= 0)
            {
                errors.Add("MaxPages must be greater than zero");
            }

            // Validate authentication configuration
            var authErrors = Authentication.Validate();
            errors.AddRange(authErrors.Select(e => $"Authentication: {e}"));

            return errors;
        }

        /// <summary>
        /// Gets the full metadata URL for the OData service.
        /// </summary>
        /// <param name="hostBaseUrl">The host base URL for middleware deployments.</param>
        /// <returns>The complete URL to the metadata endpoint.</returns>
        public string GetMetadataUrl(string? hostBaseUrl = null)
        {
            var baseUrl = BaseUrl;
            
            if (string.IsNullOrWhiteSpace(baseUrl))
            {
                if (UseHostContext && !string.IsNullOrWhiteSpace(hostBaseUrl))
                {
                    baseUrl = hostBaseUrl;
                }
                else
                {
                    throw new InvalidOperationException("Cannot construct metadata URL: no base URL available");
                }
            }

            return $"{baseUrl.TrimEnd('/')}{MetadataPath}";
        }

        /// <summary>
        /// Gets the full URL for an OData entity set.
        /// </summary>
        /// <param name="entitySetName">The name of the entity set.</param>
        /// <param name="hostBaseUrl">The host base URL for middleware deployments.</param>
        /// <returns>The complete URL to the entity set.</returns>
        /// <exception cref="ArgumentException">Thrown when <paramref name="entitySetName"/> is null or whitespace.</exception>
        public string GetEntitySetUrl(string entitySetName, string? hostBaseUrl = null)
        {
#if NET8_0_OR_GREATER
            ArgumentException.ThrowIfNullOrWhiteSpace(entitySetName);
#else
            if (string.IsNullOrWhiteSpace(entitySetName))
            {
                throw new ArgumentException("Entity set name cannot be null or whitespace.", nameof(entitySetName));
            }
#endif

            var baseUrl = BaseUrl;
            
            if (string.IsNullOrWhiteSpace(baseUrl))
            {
                if (UseHostContext && !string.IsNullOrWhiteSpace(hostBaseUrl))
                {
                    baseUrl = hostBaseUrl;
                }
                else
                {
                    throw new InvalidOperationException("Cannot construct entity set URL: no base URL available");
                }
            }

            return $"{baseUrl.TrimEnd('/')}/{entitySetName}";
        }

        /// <summary>
        /// Adds a default header to be included in all OData requests.
        /// </summary>
        /// <param name="name">The header name.</param>
        /// <param name="value">The header value.</param>
        /// <exception cref="ArgumentException">Thrown when <paramref name="name"/> is null or whitespace.</exception>
        public void AddDefaultHeader(string name, string value)
        {
#if NET8_0_OR_GREATER
            ArgumentException.ThrowIfNullOrWhiteSpace(name);
#else
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("Header name cannot be null or whitespace.", nameof(name));
            }
#endif

            DefaultHeaders[name] = value ?? string.Empty;
        }

        /// <summary>
        /// Removes a default header.
        /// </summary>
        /// <param name="name">The header name to remove.</param>
        /// <returns><c>true</c> if the header was removed; otherwise, <c>false</c>.</returns>
        public bool RemoveDefaultHeader(string name)
        {
            return !string.IsNullOrWhiteSpace(name) && DefaultHeaders.Remove(name);
        }

        /// <summary>
        /// Adds a custom property to the configuration.
        /// </summary>
        /// <param name="key">The property key.</param>
        /// <param name="value">The property value.</param>
        /// <exception cref="ArgumentException">Thrown when <paramref name="key"/> is null or whitespace.</exception>
        public void AddCustomProperty(string key, object value)
        {
#if NET8_0_OR_GREATER
            ArgumentException.ThrowIfNullOrWhiteSpace(key);
#else
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new ArgumentException("Property key cannot be null or whitespace.", nameof(key));
            }
#endif

            CustomProperties[key] = value;
        }

        /// <summary>
        /// Gets a custom property value.
        /// </summary>
        /// <typeparam name="T">The type of the property value.</typeparam>
        /// <param name="key">The property key.</param>
        /// <returns>The property value if found and of the correct type; otherwise, the default value.</returns>
        public T? GetCustomProperty<T>(string key)
        {
            if (CustomProperties.TryGetValue(key, out var value) && value is T typedValue)
            {
                return typedValue;
            }
            return default;
        }

        /// <summary>
        /// Creates a copy of this configuration.
        /// </summary>
        /// <returns>A new instance with the same settings.</returns>
        public ODataServiceConfiguration Clone()
        {
            return new ODataServiceConfiguration
            {
                BaseUrl = BaseUrl,
                MetadataPath = MetadataPath,
                AutoDiscoverMetadata = AutoDiscoverMetadata,
                RefreshInterval = RefreshInterval,
                RequestTimeout = RequestTimeout,
                MaxRetryAttempts = MaxRetryAttempts,
                RetryDelay = RetryDelay,
                UseHostContext = UseHostContext,
                Authentication = Authentication.Clone(),
                DefaultHeaders = new Dictionary<string, string>(DefaultHeaders),
                SupportedODataVersions = new List<string>(SupportedODataVersions),
                MaxPageSize = MaxPageSize,
                FollowNextLinks = FollowNextLinks,
                MaxPages = MaxPages,
                ValidateSSL = ValidateSSL,
                CustomProperties = new Dictionary<string, object>(CustomProperties)
            };
        }

        /// <summary>
        /// Merges another configuration into this one, with the other configuration taking precedence.
        /// </summary>
        /// <param name="other">The configuration to merge into this one.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="other"/> is null.</exception>
        public void MergeWith(ODataServiceConfiguration other)
        {
#if NET8_0_OR_GREATER
            ArgumentNullException.ThrowIfNull(other);
#else
            if (other is null)
            {
                throw new ArgumentNullException(nameof(other));
            }
#endif

            if (!string.IsNullOrWhiteSpace(other.BaseUrl)) BaseUrl = other.BaseUrl;
            if (!string.IsNullOrWhiteSpace(other.MetadataPath)) MetadataPath = other.MetadataPath;
            
            AutoDiscoverMetadata = other.AutoDiscoverMetadata;
            RefreshInterval = other.RefreshInterval;
            RequestTimeout = other.RequestTimeout;
            MaxRetryAttempts = other.MaxRetryAttempts;
            RetryDelay = other.RetryDelay;
            UseHostContext = other.UseHostContext;
            MaxPageSize = other.MaxPageSize ?? MaxPageSize;
            FollowNextLinks = other.FollowNextLinks;
            MaxPages = other.MaxPages;
            ValidateSSL = other.ValidateSSL;

            Authentication.MergeWith(other.Authentication);

            // Merge headers
            foreach (var kvp in other.DefaultHeaders)
            {
                DefaultHeaders[kvp.Key] = kvp.Value;
            }

            // Merge supported versions
            foreach (var version in other.SupportedODataVersions)
            {
                if (!SupportedODataVersions.Contains(version))
                {
                    SupportedODataVersions.Add(version);
                }
            }

            // Merge custom properties
            foreach (var kvp in other.CustomProperties)
            {
                CustomProperties[kvp.Key] = kvp.Value;
            }
        }

        #endregion

        #region Private Methods

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
    /// Authentication configuration for connecting to OData services.
    /// </summary>
    public sealed class ODataAuthenticationConfiguration
    {
        /// <summary>
        /// Gets or sets the authentication type for the OData service.
        /// </summary>
        /// <value>The type of authentication to use when connecting to the OData service.</value>
        public ODataAuthenticationType Type { get; set; } = ODataAuthenticationType.None;

        /// <summary>
        /// Gets or sets the API key for API key authentication.
        /// </summary>
        /// <value>The API key value.</value>
        public string? ApiKey { get; set; }

        /// <summary>
        /// Gets or sets the API key header name.
        /// </summary>
        /// <value>The name of the header to include the API key in.</value>
        public string ApiKeyHeader { get; set; } = "X-API-Key";

        /// <summary>
        /// Gets or sets the bearer token for bearer token authentication.
        /// </summary>
        /// <value>The bearer token value.</value>
        public string? BearerToken { get; set; }

        /// <summary>
        /// Gets or sets the basic authentication credentials.
        /// </summary>
        /// <value>The username and password for basic authentication.</value>
        public BasicAuthenticationCredentials? BasicAuth { get; set; }

        /// <summary>
        /// Gets or sets the OAuth2 configuration.
        /// </summary>
        /// <value>Configuration for OAuth2 client credentials flow.</value>
        public OAuth2Configuration? OAuth2 { get; set; }

        /// <summary>
        /// Validates the authentication configuration.
        /// </summary>
        /// <returns>A collection of validation errors, or empty if the configuration is valid.</returns>
        public IEnumerable<string> Validate()
        {
            var errors = new List<string>();

            switch (Type)
            {
                case ODataAuthenticationType.ApiKey:
                    if (string.IsNullOrWhiteSpace(ApiKey))
                    {
                        errors.Add("ApiKey is required for API key authentication");
                    }
                    if (string.IsNullOrWhiteSpace(ApiKeyHeader))
                    {
                        errors.Add("ApiKeyHeader is required for API key authentication");
                    }
                    break;

                case ODataAuthenticationType.Bearer:
                    if (string.IsNullOrWhiteSpace(BearerToken))
                    {
                        errors.Add("BearerToken is required for bearer token authentication");
                    }
                    break;

                case ODataAuthenticationType.Basic:
                    if (BasicAuth is null)
                    {
                        errors.Add("BasicAuth is required for basic authentication");
                    }
                    else
                    {
                        var basicErrors = BasicAuth.Validate();
                        errors.AddRange(basicErrors.Select(e => $"BasicAuth: {e}"));
                    }
                    break;

                case ODataAuthenticationType.OAuth2:
                    if (OAuth2 is null)
                    {
                        errors.Add("OAuth2 is required for OAuth2 authentication");
                    }
                    else
                    {
                        var oauthErrors = OAuth2.Validate();
                        errors.AddRange(oauthErrors.Select(e => $"OAuth2: {e}"));
                    }
                    break;
            }

            return errors;
        }

        /// <summary>
        /// Creates a copy of this configuration.
        /// </summary>
        /// <returns>A new instance with the same settings.</returns>
        public ODataAuthenticationConfiguration Clone()
        {
            return new ODataAuthenticationConfiguration
            {
                Type = Type,
                ApiKey = ApiKey,
                ApiKeyHeader = ApiKeyHeader,
                BearerToken = BearerToken,
                BasicAuth = BasicAuth?.Clone(),
                OAuth2 = OAuth2?.Clone()
            };
        }

        /// <summary>
        /// Merges another configuration into this one.
        /// </summary>
        /// <param name="other">The configuration to merge.</param>
        public void MergeWith(ODataAuthenticationConfiguration other)
        {
            if (other is null) return;

            Type = other.Type;
            if (!string.IsNullOrWhiteSpace(other.ApiKey)) ApiKey = other.ApiKey;
            if (!string.IsNullOrWhiteSpace(other.ApiKeyHeader)) ApiKeyHeader = other.ApiKeyHeader;
            if (!string.IsNullOrWhiteSpace(other.BearerToken)) BearerToken = other.BearerToken;
            if (other.BasicAuth is not null) BasicAuth = other.BasicAuth.Clone();
            if (other.OAuth2 is not null) OAuth2 = other.OAuth2.Clone();
        }
    }

    /// <summary>
    /// Defines the authentication types for OData services.
    /// </summary>
    public enum ODataAuthenticationType
    {
        /// <summary>
        /// No authentication required.
        /// </summary>
        None,

        /// <summary>
        /// API key authentication using a custom header.
        /// </summary>
        ApiKey,

        /// <summary>
        /// Bearer token authentication using the Authorization header.
        /// </summary>
        Bearer,

        /// <summary>
        /// Basic authentication using username and password.
        /// </summary>
        Basic,

        /// <summary>
        /// OAuth2 client credentials flow.
        /// </summary>
        OAuth2
    }

    /// <summary>
    /// Basic authentication credentials.
    /// </summary>
    public sealed class BasicAuthenticationCredentials
    {
        /// <summary>
        /// Gets or sets the username.
        /// </summary>
        public string Username { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the password.
        /// </summary>
        public string Password { get; set; } = string.Empty;

        /// <summary>
        /// Validates the credentials.
        /// </summary>
        /// <returns>Validation errors.</returns>
        public IEnumerable<string> Validate()
        {
            var errors = new List<string>();
            if (string.IsNullOrWhiteSpace(Username)) errors.Add("Username is required");
            if (string.IsNullOrWhiteSpace(Password)) errors.Add("Password is required");
            return errors;
        }

        /// <summary>
        /// Creates a copy of these credentials.
        /// </summary>
        /// <returns>A new instance with the same values.</returns>
        public BasicAuthenticationCredentials Clone()
        {
            return new BasicAuthenticationCredentials { Username = Username, Password = Password };
        }
    }

    /// <summary>
    /// OAuth2 configuration for client credentials flow.
    /// </summary>
    public sealed class OAuth2Configuration
    {
        /// <summary>
        /// Gets or sets the OAuth2 token endpoint URL.
        /// </summary>
        public string TokenEndpoint { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the client ID.
        /// </summary>
        public string ClientId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the client secret.
        /// </summary>
        public string ClientSecret { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the OAuth2 scopes to request.
        /// </summary>
        public List<string> Scopes { get; set; } = new();

        /// <summary>
        /// Validates the OAuth2 configuration.
        /// </summary>
        /// <returns>Validation errors.</returns>
        public IEnumerable<string> Validate()
        {
            var errors = new List<string>();
            if (string.IsNullOrWhiteSpace(TokenEndpoint)) errors.Add("TokenEndpoint is required");
            if (string.IsNullOrWhiteSpace(ClientId)) errors.Add("ClientId is required");
            if (string.IsNullOrWhiteSpace(ClientSecret)) errors.Add("ClientSecret is required");
            return errors;
        }

        /// <summary>
        /// Creates a copy of this configuration.
        /// </summary>
        /// <returns>A new instance with the same values.</returns>
        public OAuth2Configuration Clone()
        {
            return new OAuth2Configuration
            {
                TokenEndpoint = TokenEndpoint,
                ClientId = ClientId,
                ClientSecret = ClientSecret,
                Scopes = new List<string>(Scopes)
            };
        }
    }
}