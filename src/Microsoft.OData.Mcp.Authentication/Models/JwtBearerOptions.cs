using System;
using System.Collections.Generic;

namespace Microsoft.OData.Mcp.Authentication.Models
{
    /// <summary>
    /// Configuration options for JWT bearer token validation.
    /// </summary>
    /// <remarks>
    /// These options control how JWT tokens are validated by the MCP server when acting
    /// as an OAuth2 resource server. They define the trust relationship with authorization
    /// servers and specify validation requirements.
    /// </remarks>
    public sealed class JwtBearerOptions
    {
        #region Properties

        /// <summary>
        /// Gets or sets the authority URL of the OAuth2 authorization server.
        /// </summary>
        /// <value>The base URL of the authorization server (e.g., "https://login.microsoftonline.com/tenant-id").</value>
        /// <remarks>
        /// This URL is used to discover the authorization server's metadata, including
        /// the JWKS endpoint for token validation keys. The authority must support
        /// OpenID Connect discovery.
        /// </remarks>
        public string? Authority { get; set; }

        /// <summary>
        /// Gets or sets the expected audience for JWT tokens.
        /// </summary>
        /// <value>The audience claim value that must be present in valid tokens.</value>
        /// <remarks>
        /// The audience identifies this MCP server as a valid recipient for the token.
        /// Tokens without the correct audience claim will be rejected. This is typically
        /// the API identifier or base URL of the MCP server.
        /// </remarks>
        public string? Audience { get; set; }

        /// <summary>
        /// Gets or sets the expected issuer for JWT tokens.
        /// </summary>
        /// <value>The issuer claim value that must be present in valid tokens.</value>
        /// <remarks>
        /// The issuer identifies the authorization server that issued the token.
        /// When specified, tokens from other issuers will be rejected. If not specified,
        /// the issuer will be derived from the Authority during metadata discovery.
        /// </remarks>
        public string? Issuer { get; set; }

        /// <summary>
        /// Gets or sets the URL of the JWKS (JSON Web Key Set) endpoint.
        /// </summary>
        /// <value>The URL where JWT signing keys can be retrieved.</value>
        /// <remarks>
        /// If not specified, the JWKS URL will be discovered from the authorization
        /// server's metadata. Manually specifying this can improve startup performance
        /// and provide more control over key retrieval.
        /// </remarks>
        public string? MetadataAddress { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether to validate the token issuer.
        /// </summary>
        /// <value><c>true</c> if the issuer should be validated; otherwise, <c>false</c>.</value>
        /// <remarks>
        /// Issuer validation ensures tokens come from trusted authorization servers.
        /// Disabling this validation reduces security and should only be done in
        /// development scenarios.
        /// </remarks>
        public bool ValidateIssuer { get; set; } = true;

        /// <summary>
        /// Gets or sets a value indicating whether to validate the token audience.
        /// </summary>
        /// <value><c>true</c> if the audience should be validated; otherwise, <c>false</c>.</value>
        /// <remarks>
        /// Audience validation ensures tokens are intended for this service.
        /// Disabling this validation allows tokens intended for other services,
        /// which may be a security risk.
        /// </remarks>
        public bool ValidateAudience { get; set; } = true;

        /// <summary>
        /// Gets or sets a value indicating whether to validate the token lifetime.
        /// </summary>
        /// <value><c>true</c> if the token lifetime should be validated; otherwise, <c>false</c>.</value>
        /// <remarks>
        /// Lifetime validation ensures tokens are not expired or used before their
        /// valid time period. Disabling this validation allows expired tokens,
        /// which is a significant security risk.
        /// </remarks>
        public bool ValidateLifetime { get; set; } = true;

        /// <summary>
        /// Gets or sets a value indicating whether to validate the token signature.
        /// </summary>
        /// <value><c>true</c> if the token signature should be validated; otherwise, <c>false</c>.</value>
        /// <remarks>
        /// Signature validation ensures tokens haven't been tampered with and come
        /// from trusted sources. Disabling this validation should never be done
        /// in production as it completely undermines token security.
        /// </remarks>
        public bool ValidateIssuerSigningKey { get; set; } = true;

        /// <summary>
        /// Gets or sets the clock skew tolerance for token validation.
        /// </summary>
        /// <value>The maximum allowed time difference between token and server clocks.</value>
        /// <remarks>
        /// Clock skew tolerance accounts for small time differences between the
        /// authorization server and MCP server clocks. This prevents valid tokens
        /// from being rejected due to minor time synchronization issues.
        /// </remarks>
        public TimeSpan ClockSkew { get; set; } = TimeSpan.FromMinutes(5);

        /// <summary>
        /// Gets or sets the required OAuth2 scopes for accessing the MCP server.
        /// </summary>
        /// <value>A collection of scope names that must be present in valid tokens.</value>
        /// <remarks>
        /// When specified, tokens must contain at least one of these scopes to be
        /// considered valid. Scopes provide fine-grained authorization control
        /// beyond basic authentication.
        /// </remarks>
        public List<string> RequiredScopes { get; set; } = new();

        /// <summary>
        /// Gets or sets additional token validation parameters.
        /// </summary>
        /// <value>A dictionary of custom validation parameters and their values.</value>
        /// <remarks>
        /// These parameters allow for custom token validation logic beyond the
        /// standard JWT validation. They can be used to enforce additional
        /// security requirements specific to the deployment environment.
        /// </remarks>
        public Dictionary<string, object> AdditionalValidationParameters { get; set; } = new();

        /// <summary>
        /// Gets or sets a value indicating whether to require HTTPS for metadata retrieval.
        /// </summary>
        /// <value><c>true</c> if HTTPS is required for metadata retrieval; otherwise, <c>false</c>.</value>
        /// <remarks>
        /// Requiring HTTPS for metadata retrieval ensures the integrity and confidentiality
        /// of validation keys and other security-critical information. This should be
        /// enabled in production environments.
        /// </remarks>
        public bool RequireHttpsMetadata { get; set; } = true;

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="JwtBearerOptions"/> class.
        /// </summary>
        public JwtBearerOptions()
        {
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Validates the JWT bearer options for consistency and completeness.
        /// </summary>
        /// <returns>A collection of validation errors, or an empty collection if the options are valid.</returns>
        public IEnumerable<string> Validate()
        {
            var errors = new List<string>();

            if (string.IsNullOrWhiteSpace(Authority) && string.IsNullOrWhiteSpace(MetadataAddress))
            {
                errors.Add("Either Authority or MetadataAddress must be specified for JWT bearer authentication.");
            }

            if (!string.IsNullOrWhiteSpace(Authority))
            {
                if (!Uri.TryCreate(Authority, UriKind.Absolute, out var authorityUri))
                {
                    errors.Add("Authority must be a valid absolute URL.");
                }
                else if (RequireHttpsMetadata && !authorityUri.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase))
                {
                    errors.Add("Authority must use HTTPS when RequireHttpsMetadata is enabled.");
                }
            }

            if (!string.IsNullOrWhiteSpace(MetadataAddress))
            {
                if (!Uri.TryCreate(MetadataAddress, UriKind.Absolute, out var metadataUri))
                {
                    errors.Add("MetadataAddress must be a valid absolute URL.");
                }
                else if (RequireHttpsMetadata && !metadataUri.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase))
                {
                    errors.Add("MetadataAddress must use HTTPS when RequireHttpsMetadata is enabled.");
                }
            }

            if (ValidateAudience && string.IsNullOrWhiteSpace(Audience))
            {
                errors.Add("Audience must be specified when ValidateAudience is enabled.");
            }

            if (ValidateIssuer && string.IsNullOrWhiteSpace(Issuer) && string.IsNullOrWhiteSpace(Authority))
            {
                errors.Add("Issuer or Authority must be specified when ValidateIssuer is enabled.");
            }

            if (ClockSkew < TimeSpan.Zero)
            {
                errors.Add("ClockSkew cannot be negative.");
            }

            if (ClockSkew > TimeSpan.FromHours(1))
            {
                errors.Add("ClockSkew should not exceed 1 hour for security reasons.");
            }

            return errors;
        }

        /// <summary>
        /// Returns a string representation of the JWT bearer options.
        /// </summary>
        /// <returns>A summary of the JWT bearer configuration.</returns>
        public override string ToString()
        {
            var authority = !string.IsNullOrWhiteSpace(Authority) ? Authority : "Custom";
            var audience = !string.IsNullOrWhiteSpace(Audience) ? Audience : "Not specified";
            
            return $"JWT Bearer: Authority={authority}, Audience={audience}, ValidateIssuer={ValidateIssuer}, ValidateAudience={ValidateAudience}";
        }

        #endregion
    }
}