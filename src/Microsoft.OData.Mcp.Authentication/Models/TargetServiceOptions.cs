// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.OData.Mcp.Authentication.Models
{

    /// <summary>
    /// Configuration options for a specific target service in token delegation.
    /// </summary>
    /// <remarks>
    /// These options define how tokens should be handled when making requests to a specific
    /// downstream service. Each service can have its own delegation strategy, scopes,
    /// and authentication requirements.
    /// </remarks>
    public sealed class TargetServiceOptions
    {

        #region Properties

        /// <summary>
        /// Gets or sets the unique identifier for the target service.
        /// </summary>
        /// <value>A unique string that identifies this service configuration.</value>
        /// <remarks>
        /// This identifier is used to look up the appropriate delegation configuration
        /// when making requests to downstream services. It should be unique within
        /// the MCP server's configuration.
        /// </remarks>
        public required string ServiceId { get; set; }

        /// <summary>
        /// Gets or sets the base URL of the target service.
        /// </summary>
        /// <value>The base URL where the service can be accessed.</value>
        /// <remarks>
        /// This URL is used to determine which requests should use this service's
        /// delegation configuration. Requests to URLs starting with this base URL
        /// will use these settings.
        /// </remarks>
        public required string BaseUrl { get; set; }

        /// <summary>
        /// Gets or sets the token forwarding strategy for this service.
        /// </summary>
        /// <value>The strategy to use when forwarding tokens to this service.</value>
        /// <remarks>
        /// If not specified, the global token delegation strategy will be used.
        /// Service-specific strategies allow for fine-grained control over how
        /// different services receive authentication tokens.
        /// </remarks>
        public TokenForwardingStrategy? Strategy { get; set; }

        /// <summary>
        /// Gets or sets the target audience for token exchange operations.
        /// </summary>
        /// <value>The audience claim to request when exchanging tokens for this service.</value>
        /// <remarks>
        /// When using token exchange or on-behalf-of flows, this audience identifies
        /// the target service for the new token. It's typically the service's API
        /// identifier or base URL.
        /// </remarks>
        public string? TargetAudience { get; set; }

        /// <summary>
        /// Gets or sets the scopes to request for this service.
        /// </summary>
        /// <value>A collection of OAuth2 scopes to request when obtaining tokens for this service.</value>
        /// <remarks>
        /// These scopes define the level of access requested for the target service.
        /// They should be the minimum scopes required for the MCP server to perform
        /// its operations on behalf of the user.
        /// </remarks>
        public List<string> Scopes { get; set; } = [];

        /// <summary>
        /// Gets or sets the client credentials for service-to-service authentication.
        /// </summary>
        /// <value>Credentials used when the service requires client authentication.</value>
        /// <remarks>
        /// These credentials are used for OAuth2 flows that require client authentication,
        /// such as on-behalf-of or token exchange. They identify the MCP server to the
        /// authorization server.
        /// </remarks>
        public ClientCredentials? ClientCredentials { get; set; }

        /// <summary>
        /// Gets or sets the token endpoint URL for this service.
        /// </summary>
        /// <value>The URL of the token endpoint for OAuth2 operations.</value>
        /// <remarks>
        /// If not specified, the token endpoint will be discovered from the authorization
        /// server's metadata. Specifying this directly can improve performance and
        /// provide more control over token operations.
        /// </remarks>
        public string? TokenEndpoint { get; set; }

        /// <summary>
        /// Gets or sets additional headers to include in requests to this service.
        /// </summary>
        /// <value>A dictionary of header names and values to include in requests.</value>
        /// <remarks>
        /// These headers are added to all requests made to this service, in addition
        /// to the authentication token. They can be used for service-specific
        /// requirements like API versions or custom authentication schemes.
        /// </remarks>
        public Dictionary<string, string> AdditionalHeaders { get; set; } = [];

        /// <summary>
        /// Gets or sets the timeout for requests to this service.
        /// </summary>
        /// <value>The timeout duration for requests to this service.</value>
        /// <remarks>
        /// If not specified, the global delegation timeout will be used. Service-specific
        /// timeouts allow for different performance expectations for different services.
        /// </remarks>
        public TimeSpan? Timeout { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether to validate tokens before sending to this service.
        /// </summary>
        /// <value><c>true</c> if tokens should be validated before forwarding; <c>null</c> to use global setting.</value>
        /// <remarks>
        /// This setting overrides the global token validation setting for this specific
        /// service. Some services may have different validation requirements or
        /// performance characteristics.
        /// </remarks>
        public bool? ValidateBeforeForwarding { get; set; }

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="TargetServiceOptions"/> class.
        /// </summary>
        public TargetServiceOptions()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="TargetServiceOptions"/> class with the specified service ID and base URL.
        /// </summary>
        /// <param name="serviceId">The unique identifier for the target service.</param>
        /// <param name="baseUrl">The base URL of the target service.</param>
        /// <exception cref="ArgumentException">Thrown when <paramref name="serviceId"/> or <paramref name="baseUrl"/> is null or whitespace.</exception>
        public TargetServiceOptions(string serviceId, string baseUrl)
        {
ArgumentException.ThrowIfNullOrWhiteSpace(serviceId);
            ArgumentException.ThrowIfNullOrWhiteSpace(baseUrl);

            ServiceId = serviceId;
            BaseUrl = baseUrl;
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Determines whether a URL matches this target service configuration.
        /// </summary>
        /// <param name="url">The URL to check.</param>
        /// <returns><c>true</c> if the URL matches this service; otherwise, <c>false</c>.</returns>
        public bool MatchesUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                return false;
            }

            return url.StartsWith(BaseUrl, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Determines whether a URI matches this target service configuration.
        /// </summary>
        /// <param name="uri">The URI to check.</param>
        /// <returns><c>true</c> if the URI matches this service; otherwise, <c>false</c>.</returns>
        public bool MatchesUrl(Uri uri)
        {
ArgumentNullException.ThrowIfNull(uri);

            return MatchesUrl(uri.ToString());
        }

        /// <summary>
        /// Gets the effective token forwarding strategy for this service.
        /// </summary>
        /// <param name="globalStrategy">The global token forwarding strategy.</param>
        /// <returns>The strategy to use for this service.</returns>
        public TokenForwardingStrategy GetEffectiveStrategy(TokenForwardingStrategy globalStrategy)
        {
            return Strategy ?? globalStrategy;
        }

        /// <summary>
        /// Gets the effective timeout for this service.
        /// </summary>
        /// <param name="globalTimeout">The global timeout setting.</param>
        /// <returns>The timeout to use for this service.</returns>
        public TimeSpan GetEffectiveTimeout(TimeSpan globalTimeout)
        {
            return Timeout ?? globalTimeout;
        }

        /// <summary>
        /// Gets the effective validation setting for this service.
        /// </summary>
        /// <param name="globalValidation">The global validation setting.</param>
        /// <returns>The validation setting to use for this service.</returns>
        public bool GetEffectiveValidation(bool globalValidation)
        {
            return ValidateBeforeForwarding ?? globalValidation;
        }

        /// <summary>
        /// Validates the target service options for consistency and completeness.
        /// </summary>
        /// <returns>A collection of validation errors, or an empty collection if the options are valid.</returns>
        public IEnumerable<string> Validate()
        {
            var errors = new List<string>();

            if (string.IsNullOrWhiteSpace(ServiceId))
            {
                errors.Add("Target service ServiceId cannot be null or empty.");
            }

            if (string.IsNullOrWhiteSpace(BaseUrl))
            {
                errors.Add("Target service BaseUrl cannot be null or empty.");
            }
            else if (!Uri.TryCreate(BaseUrl, UriKind.Absolute, out var baseUri))
            {
                errors.Add($"Target service BaseUrl '{BaseUrl}' is not a valid absolute URL.");
            }
            else if (!baseUri.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase) &&
                     !baseUri.Scheme.Equals("http", StringComparison.OrdinalIgnoreCase))
            {
                errors.Add($"Target service BaseUrl '{BaseUrl}' must use HTTP or HTTPS scheme.");
            }

            if (!string.IsNullOrWhiteSpace(TokenEndpoint) &&
                !Uri.TryCreate(TokenEndpoint, UriKind.Absolute, out _))
            {
                errors.Add($"Target service TokenEndpoint '{TokenEndpoint}' is not a valid absolute URL.");
            }

            if (Timeout.HasValue && Timeout.Value <= TimeSpan.Zero)
            {
                errors.Add("Target service Timeout must be greater than zero.");
            }

            if (ClientCredentials is not null)
            {
                var credentialErrors = ClientCredentials.Validate();
                errors.AddRange(credentialErrors.Select(e => $"Target service {ServiceId}: {e}"));
            }

            return errors;
        }

        /// <summary>
        /// Returns a string representation of the target service options.
        /// </summary>
        /// <returns>A summary of the target service configuration.</returns>
        public override string ToString()
        {
            var strategy = Strategy?.ToString() ?? "Global";
            return $"Service: {ServiceId}, URL: {BaseUrl}, Strategy: {strategy}";
        }

        #endregion

    }

}
