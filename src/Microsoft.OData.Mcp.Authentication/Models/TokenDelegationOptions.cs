using System;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.OData.Mcp.Authentication.Models
{
    /// <summary>
    /// Configuration options for token delegation to downstream services.
    /// </summary>
    /// <remarks>
    /// These options control how authentication tokens are forwarded from the MCP server
    /// to downstream OData services and other dependencies. Token delegation preserves
    /// the user's identity throughout the request chain.
    /// </remarks>
    public sealed class TokenDelegationOptions
    {
        #region Properties

        /// <summary>
        /// Gets or sets a value indicating whether token delegation is enabled.
        /// </summary>
        /// <value><c>true</c> if token delegation is enabled; otherwise, <c>false</c>.</value>
        /// <remarks>
        /// When enabled, the MCP server will forward authentication tokens to downstream
        /// services. When disabled, the server may use alternative authentication methods
        /// for downstream calls, such as service-to-service authentication.
        /// </remarks>
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// Gets or sets the token forwarding strategy.
        /// </summary>
        /// <value>The strategy for forwarding tokens to downstream services.</value>
        /// <remarks>
        /// Different strategies provide different levels of security and functionality:
        /// - PassThrough: Forward the original token as-is
        /// - Exchange: Exchange the token for a new one scoped to the downstream service
        /// - OnBehalfOf: Use OAuth2 on-behalf-of flow for token delegation
        /// </remarks>
        public TokenForwardingStrategy Strategy { get; set; } = TokenForwardingStrategy.PassThrough;

        /// <summary>
        /// Gets or sets the target services for token delegation.
        /// </summary>
        /// <value>A collection of service configurations for token forwarding.</value>
        /// <remarks>
        /// Each target service can have its own delegation configuration, including
        /// different forwarding strategies, scopes, and authentication parameters.
        /// </remarks>
        public List<TargetServiceOptions> TargetServices { get; set; } = [];

        /// <summary>
        /// Gets or sets the token exchange options for services that support token exchange.
        /// </summary>
        /// <value>Configuration for OAuth2 token exchange flows.</value>
        /// <remarks>
        /// Token exchange allows the MCP server to obtain tokens with different scopes
        /// or audiences for downstream services while maintaining the user's identity.
        /// </remarks>
        public TokenExchangeOptions TokenExchange { get; set; } = new();

        /// <summary>
        /// Gets or sets a value indicating whether to cache delegated tokens.
        /// </summary>
        /// <value><c>true</c> if delegated tokens should be cached; otherwise, <c>false</c>.</value>
        /// <remarks>
        /// Caching delegated tokens can improve performance by avoiding repeated
        /// token exchange operations. Cached tokens are automatically refreshed
        /// before expiration.
        /// </remarks>
        public bool CacheTokens { get; set; } = true;

        /// <summary>
        /// Gets or sets the cache duration for delegated tokens.
        /// </summary>
        /// <value>The duration to cache delegated tokens.</value>
        /// <remarks>
        /// This duration should be shorter than the token's actual lifetime to ensure
        /// cached tokens don't expire unexpectedly. The cache automatically handles
        /// token refresh when possible.
        /// </remarks>
        public TimeSpan CacheDuration { get; set; } = TimeSpan.FromMinutes(30);

        /// <summary>
        /// Gets or sets the timeout for token delegation operations.
        /// </summary>
        /// <value>The timeout duration for token delegation operations.</value>
        /// <remarks>
        /// This timeout applies to operations like token exchange, on-behalf-of flows,
        /// and communication with token endpoints. Operations that exceed this timeout
        /// will be cancelled.
        /// </remarks>
        public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);

        /// <summary>
        /// Gets or sets the retry policy for failed token delegation operations.
        /// </summary>
        /// <value>Configuration for retrying failed token operations.</value>
        /// <remarks>
        /// Retry policies help handle transient failures in token delegation, such as
        /// network issues or temporary service unavailability. They should be configured
        /// carefully to avoid overwhelming downstream services.
        /// </remarks>
        public RetryPolicyOptions RetryPolicy { get; set; } = new();

        /// <summary>
        /// Gets or sets a value indicating whether to validate delegated tokens before forwarding.
        /// </summary>
        /// <value><c>true</c> if delegated tokens should be validated; otherwise, <c>false</c>.</value>
        /// <remarks>
        /// Validating delegated tokens ensures they are properly formatted and not expired
        /// before forwarding them to downstream services. This can prevent downstream
        /// authentication failures but adds processing overhead.
        /// </remarks>
        public bool ValidateBeforeForwarding { get; set; } = true;

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="TokenDelegationOptions"/> class.
        /// </summary>
        public TokenDelegationOptions()
        {
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Gets the target service options for a specific service identifier.
        /// </summary>
        /// <param name="serviceId">The identifier of the target service.</param>
        /// <returns>The target service options, or <c>null</c> if not found.</returns>
        public TargetServiceOptions? GetTargetService(string serviceId)
        {
            return TargetServices.FirstOrDefault(ts => ts.ServiceId.Equals(serviceId, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Adds or updates target service options.
        /// </summary>
        /// <param name="targetService">The target service options to add or update.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="targetService"/> is null.</exception>
        public void AddOrUpdateTargetService(TargetServiceOptions targetService)
        {
ArgumentNullException.ThrowIfNull(targetService);

            var existing = GetTargetService(targetService.ServiceId);
            if (existing is not null)
            {
                TargetServices.Remove(existing);
            }

            TargetServices.Add(targetService);
        }

        /// <summary>
        /// Validates the token delegation options for consistency and completeness.
        /// </summary>
        /// <returns>A collection of validation errors, or an empty collection if the options are valid.</returns>
        public IEnumerable<string> Validate()
        {
            var errors = new List<string>();

            if (Enabled)
            {
                if (Timeout <= TimeSpan.Zero)
                {
                    errors.Add("Token delegation timeout must be greater than zero.");
                }

                if (CacheTokens && CacheDuration <= TimeSpan.Zero)
                {
                    errors.Add("Token cache duration must be greater than zero when caching is enabled.");
                }

                if (Strategy == TokenForwardingStrategy.Exchange || Strategy == TokenForwardingStrategy.OnBehalfOf)
                {
                    var exchangeErrors = TokenExchange.Validate();
                    errors.AddRange(exchangeErrors);
                }

                var retryErrors = RetryPolicy.Validate();
                errors.AddRange(retryErrors);

                // Validate target services
                var serviceIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var targetService in TargetServices)
                {
                    if (!serviceIds.Add(targetService.ServiceId))
                    {
                        errors.Add($"Duplicate target service ID: {targetService.ServiceId}");
                    }

                    var serviceErrors = targetService.Validate();
                    errors.AddRange(serviceErrors);
                }
            }

            return errors;
        }

        /// <summary>
        /// Returns a string representation of the token delegation options.
        /// </summary>
        /// <returns>A summary of the token delegation configuration.</returns>
        public override string ToString()
        {
            if (!Enabled)
            {
                return "Token Delegation: Disabled";
            }

            return $"Token Delegation: {Strategy}, Cache={CacheTokens}, Targets={TargetServices.Count}";
        }

        #endregion
    }

    /// <summary>
    /// Defines the strategies for forwarding tokens to downstream services.
    /// </summary>
    public enum TokenForwardingStrategy
    {
        /// <summary>
        /// Forward the original token without modification.
        /// </summary>
        PassThrough,

        /// <summary>
        /// Exchange the token for a new one using OAuth2 token exchange.
        /// </summary>
        Exchange,

        /// <summary>
        /// Use OAuth2 on-behalf-of flow to obtain a token for the downstream service.
        /// </summary>
        OnBehalfOf
    }
}