using System.Collections.Generic;
using System.Linq;

namespace Microsoft.OData.Mcp.Core.Configuration
{
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
}
