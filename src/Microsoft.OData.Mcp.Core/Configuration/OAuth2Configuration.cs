using System.Collections.Generic;

namespace Microsoft.OData.Mcp.Core.Configuration
{
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
        public List<string> Scopes { get; set; } = [];

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
                Scopes = [.. Scopes]
            };
        }
    }
}
