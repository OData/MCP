// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.OData.Mcp.Authentication.Models
{
    /// <summary>
    /// Configuration options for OAuth2 token exchange operations.
    /// </summary>
    /// <remarks>
    /// Token exchange allows the MCP server to exchange user tokens for new tokens
    /// with different scopes or audiences, enabling secure delegation to downstream
    /// services while maintaining the user's identity.
    /// </remarks>
    public sealed class TokenExchangeOptions
    {
        #region Properties

        /// <summary>
        /// Gets or sets the token endpoint URL for token exchange operations.
        /// </summary>
        /// <value>The URL of the OAuth2 token endpoint that supports token exchange.</value>
        /// <remarks>
        /// This endpoint must support the RFC 8693 OAuth 2.0 Token Exchange specification.
        /// If not specified, the endpoint will be discovered from the authorization
        /// server's metadata.
        /// </remarks>
        public string? TokenEndpoint { get; set; }

        /// <summary>
        /// Gets or sets the client credentials used for token exchange.
        /// </summary>
        /// <value>The credentials that identify the MCP server to the authorization server.</value>
        /// <remarks>
        /// These credentials are required for token exchange operations as they
        /// authenticate the MCP server's right to exchange tokens on behalf of users.
        /// </remarks>
        public ClientCredentials? ClientCredentials { get; set; }

        /// <summary>
        /// Gets or sets the default subject token type for token exchange.
        /// </summary>
        /// <value>The token type of the input token being exchanged.</value>
        /// <remarks>
        /// Common values include "urn:ietf:params:oauth:token-type:access_token" for
        /// access tokens and "urn:ietf:params:oauth:token-type:jwt" for JWT tokens.
        /// </remarks>
        public string SubjectTokenType { get; set; } = "urn:ietf:params:oauth:token-type:access_token";

        /// <summary>
        /// Gets or sets the requested token type for token exchange.
        /// </summary>
        /// <value>The token type of the output token being requested.</value>
        /// <remarks>
        /// This specifies what type of token should be returned from the exchange.
        /// Common values include access tokens and refresh tokens.
        /// </remarks>
        public string RequestedTokenType { get; set; } = "urn:ietf:params:oauth:token-type:access_token";

        /// <summary>
        /// Gets or sets the default scopes to request during token exchange.
        /// </summary>
        /// <value>A collection of OAuth2 scopes to request for exchanged tokens.</value>
        /// <remarks>
        /// These scopes define the permissions requested for the new token.
        /// The actual scopes granted may be a subset based on the original
        /// token's scopes and the authorization server's policies.
        /// </remarks>
        public List<string> DefaultScopes { get; set; } = [];

        /// <summary>
        /// Gets or sets additional parameters to include in token exchange requests.
        /// </summary>
        /// <value>A dictionary of parameter names and values to include in exchange requests.</value>
        /// <remarks>
        /// These parameters can be used to pass additional context or configuration
        /// to the authorization server during token exchange operations.
        /// </remarks>
        public Dictionary<string, string> AdditionalParameters { get; set; } = [];

        /// <summary>
        /// Gets or sets the timeout for token exchange operations.
        /// </summary>
        /// <value>The maximum time to wait for token exchange operations to complete.</value>
        /// <remarks>
        /// Token exchange operations that exceed this timeout will be cancelled.
        /// This helps prevent hanging requests from impacting system performance.
        /// </remarks>
        public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);

        /// <summary>
        /// Gets or sets the maximum number of retry attempts for failed token exchange operations.
        /// </summary>
        /// <value>The number of times to retry failed token exchange operations.</value>
        /// <remarks>
        /// Retries help handle transient network issues or temporary service
        /// unavailability. The retry policy includes exponential backoff
        /// to avoid overwhelming the authorization server.
        /// </remarks>
        public int MaxRetryAttempts { get; set; } = 3;

        /// <summary>
        /// Gets or sets the base delay between retry attempts.
        /// </summary>
        /// <value>The initial delay before the first retry attempt.</value>
        /// <remarks>
        /// The actual delay uses exponential backoff, so subsequent retries
        /// will have progressively longer delays to reduce load on the
        /// authorization server.
        /// </remarks>
        public TimeSpan RetryDelay { get; set; } = TimeSpan.FromSeconds(1);

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="TokenExchangeOptions"/> class.
        /// </summary>
        public TokenExchangeOptions()
        {
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Validates the token exchange options for consistency and completeness.
        /// </summary>
        /// <returns>A collection of validation errors, or an empty collection if the options are valid.</returns>
        public IEnumerable<string> Validate()
        {
            var errors = new List<string>();

            if (!string.IsNullOrWhiteSpace(TokenEndpoint) &&
                !Uri.TryCreate(TokenEndpoint, UriKind.Absolute, out _))
            {
                errors.Add("TokenEndpoint must be a valid absolute URL when specified.");
            }

            if (string.IsNullOrWhiteSpace(SubjectTokenType))
            {
                errors.Add("SubjectTokenType cannot be null or empty.");
            }

            if (string.IsNullOrWhiteSpace(RequestedTokenType))
            {
                errors.Add("RequestedTokenType cannot be null or empty.");
            }

            if (Timeout <= TimeSpan.Zero)
            {
                errors.Add("Token exchange timeout must be greater than zero.");
            }

            if (MaxRetryAttempts < 0)
            {
                errors.Add("MaxRetryAttempts cannot be negative.");
            }

            if (RetryDelay < TimeSpan.Zero)
            {
                errors.Add("RetryDelay cannot be negative.");
            }

            if (ClientCredentials is not null)
            {
                var credentialErrors = ClientCredentials.Validate();
                errors.AddRange(credentialErrors);
            }

            return errors;
        }

        /// <summary>
        /// Returns a string representation of the token exchange options.
        /// </summary>
        /// <returns>A summary of the token exchange configuration.</returns>
        public override string ToString()
        {
            var endpoint = !string.IsNullOrWhiteSpace(TokenEndpoint) ? TokenEndpoint : "Discovery";
            return $"Token Exchange: Endpoint={endpoint}, SubjectType={SubjectTokenType}, RequestedType={RequestedTokenType}";
        }

        #endregion
    }
}