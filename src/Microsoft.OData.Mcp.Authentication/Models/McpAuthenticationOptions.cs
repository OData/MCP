using System;
using System.Collections.Generic;

namespace Microsoft.OData.Mcp.Authentication.Models
{

    /// <summary>
    /// Configuration options for MCP server authentication.
    /// </summary>
    /// <remarks>
    /// These options control how the MCP server validates and delegates authentication tokens.
    /// The server acts as an OAuth2 resource server, validating tokens issued by external
    /// authorization servers and optionally forwarding them to downstream OData services.
    /// </remarks>
    public sealed class McpAuthenticationOptions
    {

        #region Properties

        /// <summary>
        /// Gets or sets a value indicating whether authentication is enabled.
        /// </summary>
        /// <value><c>true</c> if authentication is enabled; otherwise, <c>false</c>.</value>
        /// <remarks>
        /// When disabled, the MCP server will allow all requests without authentication.
        /// This is useful for development scenarios or internal deployments where authentication
        /// is handled at a different layer.
        /// </remarks>
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// Gets or sets the authentication scheme to use.
        /// </summary>
        /// <value>The authentication scheme name (e.g., "Bearer", "JWT").</value>
        /// <remarks>
        /// This determines which authentication handler will be used to validate incoming requests.
        /// The default is "Bearer" for JWT bearer token authentication.
        /// </remarks>
        public string Scheme { get; set; } = "Bearer";

        /// <summary>
        /// Gets or sets the JWT bearer token options.
        /// </summary>
        /// <value>Configuration for JWT token validation.</value>
        /// <remarks>
        /// These options control how JWT tokens are validated, including issuer validation,
        /// audience validation, and token lifetime checks.
        /// </remarks>
        public JwtBearerOptions JwtBearer { get; set; } = new();

        /// <summary>
        /// Gets or sets the token delegation options.
        /// </summary>
        /// <value>Configuration for token delegation to downstream services.</value>
        /// <remarks>
        /// These options control how tokens are forwarded to OData services and other
        /// downstream dependencies that require authentication.
        /// </remarks>
        public TokenDelegationOptions TokenDelegation { get; set; } = new();

        /// <summary>
        /// Gets or sets the scope-based authorization options.
        /// </summary>
        /// <value>Configuration for OAuth2 scope-based access control.</value>
        /// <remarks>
        /// These options define which OAuth2 scopes are required for different MCP operations
        /// and how scope-based authorization is enforced.
        /// </remarks>
        public ScopeAuthorizationOptions ScopeAuthorization { get; set; } = new();

        /// <summary>
        /// Gets or sets a value indicating whether to require HTTPS for authentication.
        /// </summary>
        /// <value><c>true</c> if HTTPS is required for authentication; otherwise, <c>false</c>.</value>
        /// <remarks>
        /// When true, the server will reject authentication attempts over insecure connections.
        /// This should be enabled in production environments to protect authentication tokens.
        /// </remarks>
        public bool RequireHttps { get; set; } = true;

        /// <summary>
        /// Gets or sets the timeout for authentication operations.
        /// </summary>
        /// <value>The timeout duration for authentication operations.</value>
        /// <remarks>
        /// This timeout applies to operations like token validation, metadata discovery,
        /// and communication with authorization servers.
        /// </remarks>
        public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);

        /// <summary>
        /// Gets or sets the cache duration for authentication metadata.
        /// </summary>
        /// <value>The duration to cache authentication metadata like JWKS keys.</value>
        /// <remarks>
        /// Caching authentication metadata improves performance by avoiding repeated
        /// requests to authorization servers. The cache is automatically refreshed
        /// when metadata expires.
        /// </remarks>
        public TimeSpan MetadataCacheDuration { get; set; } = TimeSpan.FromMinutes(5);

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="McpAuthenticationOptions"/> class.
        /// </summary>
        public McpAuthenticationOptions()
        {
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Validates the authentication options for consistency and completeness.
        /// </summary>
        /// <returns>A collection of validation errors, or an empty collection if the options are valid.</returns>
        public IEnumerable<string> Validate()
        {
            var errors = new List<string>();

            if (Enabled)
            {
                if (string.IsNullOrWhiteSpace(Scheme))
                {
                    errors.Add("Authentication scheme cannot be null or empty when authentication is enabled.");
                }

                var jwtErrors = JwtBearer.Validate();
                errors.AddRange(jwtErrors);

                var tokenDelegationErrors = TokenDelegation.Validate();
                errors.AddRange(tokenDelegationErrors);

                var scopeErrors = ScopeAuthorization.Validate();
                errors.AddRange(scopeErrors);

                if (Timeout <= TimeSpan.Zero)
                {
                    errors.Add("Authentication timeout must be greater than zero.");
                }

                if (MetadataCacheDuration < TimeSpan.Zero)
                {
                    errors.Add("Metadata cache duration cannot be negative.");
                }
            }

            return errors;
        }

        /// <summary>
        /// Returns a string representation of the authentication options.
        /// </summary>
        /// <returns>A summary of the authentication configuration.</returns>
        public override string ToString()
        {
            if (!Enabled)
            {
                return "Authentication: Disabled";
            }

            return $"Authentication: {Scheme}, RequireHttps: {RequireHttps}, Timeout: {Timeout}";
        }

        #endregion

    }

}
