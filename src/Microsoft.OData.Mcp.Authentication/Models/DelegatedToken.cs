using System;
using System.Collections.Generic;

namespace Microsoft.OData.Mcp.Authentication.Models
{

    /// <summary>
    /// Represents a token that has been delegated for use with a downstream service.
    /// </summary>
    /// <remarks>
    /// This class encapsulates the result of token delegation operations, including
    /// the delegated token itself, its metadata, and information about how it was obtained.
    /// </remarks>
    public sealed class DelegatedToken
    {

        #region Properties

        /// <summary>
        /// Gets or sets the delegated access token.
        /// </summary>
        /// <value>The access token that can be used to authenticate with the target service.</value>
        /// <remarks>
        /// This token should be included in the Authorization header when making requests
        /// to the target service. The format is typically "Bearer {AccessToken}".
        /// </remarks>
        public required string AccessToken { get; set; }

        /// <summary>
        /// Gets or sets the type of the token.
        /// </summary>
        /// <value>The token type (e.g., "Bearer", "JWT").</value>
        /// <remarks>
        /// This indicates how the token should be used in HTTP requests. Most OAuth2
        /// implementations use "Bearer" tokens.
        /// </remarks>
        public string TokenType { get; set; } = "Bearer";

        /// <summary>
        /// Gets or sets the refresh token, if available.
        /// </summary>
        /// <value>The refresh token that can be used to obtain new access tokens.</value>
        /// <remarks>
        /// Refresh tokens allow obtaining new access tokens without requiring user
        /// re-authentication. Not all delegation scenarios provide refresh tokens.
        /// </remarks>
        public string? RefreshToken { get; set; }

        /// <summary>
        /// Gets or sets the scopes granted for this token.
        /// </summary>
        /// <value>A collection of OAuth2 scopes that define what the token can access.</value>
        /// <remarks>
        /// These scopes may be a subset of the originally requested scopes, depending
        /// on what the authorization server granted for the target service.
        /// </remarks>
        public List<string> Scopes { get; set; } = [];

        /// <summary>
        /// Gets or sets the target service identifier.
        /// </summary>
        /// <value>The identifier of the service this token is intended for.</value>
        /// <remarks>
        /// This identifies which service configuration was used to obtain the token
        /// and can be used for routing and caching decisions.
        /// </remarks>
        public required string TargetServiceId { get; set; }

        /// <summary>
        /// Gets or sets the target audience for the token.
        /// </summary>
        /// <value>The audience claim for which the token was issued.</value>
        /// <remarks>
        /// This is the intended recipient of the token and should match the
        /// target service's expected audience value.
        /// </remarks>
        public string? TargetAudience { get; set; }

        /// <summary>
        /// Gets or sets the token expiration time.
        /// </summary>
        /// <value>The UTC date and time when the token expires.</value>
        /// <remarks>
        /// After this time, the token will no longer be valid for authentication.
        /// If a refresh token is available, it can be used to obtain a new access token.
        /// </remarks>
        public DateTime? ExpiresAt { get; set; }

        /// <summary>
        /// Gets or sets the time when the token was issued.
        /// </summary>
        /// <value>The UTC date and time when the token was issued.</value>
        /// <remarks>
        /// This timestamp indicates when the token delegation operation completed
        /// successfully and the token became available for use.
        /// </remarks>
        public DateTime IssuedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Gets or sets the delegation strategy used to obtain this token.
        /// </summary>
        /// <value>The strategy that was used for token delegation.</value>
        /// <remarks>
        /// This information can be useful for debugging, auditing, and determining
        /// what operations are possible with the token (e.g., refresh capabilities).
        /// </remarks>
        public TokenForwardingStrategy DelegationStrategy { get; set; }

        /// <summary>
        /// Gets or sets the original token that was used for delegation.
        /// </summary>
        /// <value>The user's original token that was delegated.</value>
        /// <remarks>
        /// This is stored for auditing purposes and potential token refresh operations.
        /// It should be handled securely and not logged or exposed unnecessarily.
        /// </remarks>
        public string? OriginalToken { get; set; }

        /// <summary>
        /// Gets or sets additional metadata about the token delegation.
        /// </summary>
        /// <value>A dictionary of metadata key-value pairs.</value>
        /// <remarks>
        /// This can include information such as the delegation endpoint used,
        /// client credentials applied, or other context that might be useful
        /// for debugging or auditing.
        /// </remarks>
        public Dictionary<string, object> Metadata { get; set; } = [];

        /// <summary>
        /// Gets or sets a value indicating whether this token can be refreshed.
        /// </summary>
        /// <value><c>true</c> if the token can be refreshed; otherwise, <c>false</c>.</value>
        /// <remarks>
        /// This is determined by whether a refresh token is available and the
        /// delegation strategy supports refresh operations.
        /// </remarks>
        public bool CanRefresh => !string.IsNullOrWhiteSpace(RefreshToken);

        /// <summary>
        /// Gets a value indicating whether this token is expired.
        /// </summary>
        /// <value><c>true</c> if the token is expired; otherwise, <c>false</c>.</value>
        public bool IsExpired => ExpiresAt.HasValue && DateTime.UtcNow >= ExpiresAt.Value;

        /// <summary>
        /// Gets the remaining lifetime of the token.
        /// </summary>
        /// <value>The time remaining before the token expires, or null if no expiration is set.</value>
        public TimeSpan? RemainingLifetime
        {
            get
            {
                if (!ExpiresAt.HasValue)
                {
                    return null;
                }

                var remaining = ExpiresAt.Value - DateTime.UtcNow;
                return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
            }
        }

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="DelegatedToken"/> class.
        /// </summary>
        public DelegatedToken()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="DelegatedToken"/> class with the specified access token and target service.
        /// </summary>
        /// <param name="accessToken">The delegated access token.</param>
        /// <param name="targetServiceId">The target service identifier.</param>
        /// <exception cref="ArgumentException">Thrown when <paramref name="accessToken"/> or <paramref name="targetServiceId"/> is null or whitespace.</exception>
        public DelegatedToken(string accessToken, string targetServiceId)
        {
#if NET8_0_OR_GREATER
            ArgumentException.ThrowIfNullOrWhiteSpace(accessToken);
            ArgumentException.ThrowIfNullOrWhiteSpace(targetServiceId);
#else
            if (string.IsNullOrWhiteSpace(accessToken))
            {
                throw new ArgumentException("Access token cannot be null or whitespace.", nameof(accessToken));
            }
            if (string.IsNullOrWhiteSpace(targetServiceId))
            {
                throw new ArgumentException("Target service ID cannot be null or whitespace.", nameof(targetServiceId));
            }
#endif

            AccessToken = accessToken;
            TargetServiceId = targetServiceId;
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Creates a delegated token for pass-through scenarios.
        /// </summary>
        /// <param name="originalToken">The original token to pass through.</param>
        /// <param name="targetServiceId">The target service identifier.</param>
        /// <returns>A delegated token configured for pass-through.</returns>
        /// <exception cref="ArgumentException">Thrown when <paramref name="originalToken"/> or <paramref name="targetServiceId"/> is null or whitespace.</exception>
        public static DelegatedToken CreatePassThrough(string originalToken, string targetServiceId)
        {
#if NET8_0_OR_GREATER
            ArgumentException.ThrowIfNullOrWhiteSpace(originalToken);
            ArgumentException.ThrowIfNullOrWhiteSpace(targetServiceId);
#else
            if (string.IsNullOrWhiteSpace(originalToken))
            {
                throw new ArgumentException("Original token cannot be null or whitespace.", nameof(originalToken));
            }
            if (string.IsNullOrWhiteSpace(targetServiceId))
            {
                throw new ArgumentException("Target service ID cannot be null or whitespace.", nameof(targetServiceId));
            }
#endif

            return new DelegatedToken(originalToken, targetServiceId)
            {
                AccessToken = originalToken,
                TargetServiceId = targetServiceId,
                DelegationStrategy = TokenForwardingStrategy.PassThrough,
                OriginalToken = originalToken
            };
        }

        /// <summary>
        /// Creates a delegated token from a token exchange result.
        /// </summary>
        /// <param name="exchangedToken">The token received from the exchange.</param>
        /// <param name="targetServiceId">The target service identifier.</param>
        /// <param name="originalToken">The original token that was exchanged.</param>
        /// <returns>A delegated token configured for token exchange.</returns>
        /// <exception cref="ArgumentException">Thrown when <paramref name="exchangedToken"/> or <paramref name="targetServiceId"/> is null or whitespace.</exception>
        public static DelegatedToken CreateFromExchange(string exchangedToken, string targetServiceId, string? originalToken = null)
        {
#if NET8_0_OR_GREATER
            ArgumentException.ThrowIfNullOrWhiteSpace(exchangedToken);
            ArgumentException.ThrowIfNullOrWhiteSpace(targetServiceId);
#else
            if (string.IsNullOrWhiteSpace(exchangedToken))
            {
                throw new ArgumentException("Exchanged token cannot be null or whitespace.", nameof(exchangedToken));
            }
            if (string.IsNullOrWhiteSpace(targetServiceId))
            {
                throw new ArgumentException("Target service ID cannot be null or whitespace.", nameof(targetServiceId));
            }
#endif

            return new DelegatedToken(exchangedToken, targetServiceId)
            {
                AccessToken = exchangedToken,
                TargetServiceId = targetServiceId,
                DelegationStrategy = TokenForwardingStrategy.Exchange,
                OriginalToken = originalToken
            };
        }

        /// <summary>
        /// Creates a delegated token from an on-behalf-of flow result.
        /// </summary>
        /// <param name="onBehalfOfToken">The token received from the on-behalf-of flow.</param>
        /// <param name="targetServiceId">The target service identifier.</param>
        /// <param name="originalToken">The original token used for the on-behalf-of flow.</param>
        /// <returns>A delegated token configured for on-behalf-of flow.</returns>
        /// <exception cref="ArgumentException">Thrown when <paramref name="onBehalfOfToken"/> or <paramref name="targetServiceId"/> is null or whitespace.</exception>
        public static DelegatedToken CreateFromOnBehalfOf(string onBehalfOfToken, string targetServiceId, string? originalToken = null)
        {
#if NET8_0_OR_GREATER
            ArgumentException.ThrowIfNullOrWhiteSpace(onBehalfOfToken);
            ArgumentException.ThrowIfNullOrWhiteSpace(targetServiceId);
#else
            if (string.IsNullOrWhiteSpace(onBehalfOfToken))
            {
                throw new ArgumentException("On-behalf-of token cannot be null or whitespace.", nameof(onBehalfOfToken));
            }
            if (string.IsNullOrWhiteSpace(targetServiceId))
            {
                throw new ArgumentException("Target service ID cannot be null or whitespace.", nameof(targetServiceId));
            }
#endif

            return new DelegatedToken(onBehalfOfToken, targetServiceId)
            {
                AccessToken = onBehalfOfToken,
                TargetServiceId = targetServiceId,
                DelegationStrategy = TokenForwardingStrategy.OnBehalfOf,
                OriginalToken = originalToken
            };
        }

        /// <summary>
        /// Adds metadata to the delegated token.
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
        /// Determines whether the token should be refreshed based on its expiration time.
        /// </summary>
        /// <param name="refreshThreshold">The time before expiration when refresh should be considered.</param>
        /// <returns><c>true</c> if the token should be refreshed; otherwise, <c>false</c>.</returns>
        public bool ShouldRefresh(TimeSpan refreshThreshold)
        {
            if (!CanRefresh || !ExpiresAt.HasValue)
            {
                return false;
            }

            var timeToExpiration = ExpiresAt.Value - DateTime.UtcNow;
            return timeToExpiration <= refreshThreshold;
        }

        /// <summary>
        /// Gets the authorization header value for HTTP requests.
        /// </summary>
        /// <returns>The complete authorization header value (e.g., "Bearer {token}").</returns>
        public string GetAuthorizationHeaderValue()
        {
            return $"{TokenType} {AccessToken}";
        }

        /// <summary>
        /// Creates a copy of the delegated token with updated values.
        /// </summary>
        /// <param name="newAccessToken">The new access token value.</param>
        /// <param name="newExpiresAt">The new expiration time.</param>
        /// <param name="newRefreshToken">The new refresh token.</param>
        /// <returns>A new delegated token instance with updated values.</returns>
        public DelegatedToken WithUpdatedToken(string newAccessToken, DateTime? newExpiresAt = null, string? newRefreshToken = null)
        {
#if NET8_0_OR_GREATER
            ArgumentException.ThrowIfNullOrWhiteSpace(newAccessToken);
#else
            if (string.IsNullOrWhiteSpace(newAccessToken))
            {
                throw new ArgumentException("New access token cannot be null or whitespace.", nameof(newAccessToken));
            }
#endif

            return new DelegatedToken(newAccessToken, TargetServiceId)
            {
                AccessToken = newAccessToken,
                TargetServiceId = TargetServiceId,
                TokenType = TokenType,
                RefreshToken = newRefreshToken ?? RefreshToken,
                Scopes = new List<string>(Scopes),
                TargetAudience = TargetAudience,
                ExpiresAt = newExpiresAt ?? ExpiresAt,
                IssuedAt = DateTime.UtcNow,
                DelegationStrategy = DelegationStrategy,
                OriginalToken = OriginalToken,
                Metadata = new Dictionary<string, object>(Metadata)
            };
        }

        /// <summary>
        /// Returns a string representation of the delegated token.
        /// </summary>
        /// <returns>A summary of the delegated token.</returns>
        public override string ToString()
        {
            var tokenPreview = AccessToken.Length > 20 ? $"{AccessToken[..10]}...{AccessToken[^10..]}" : AccessToken;
            var expiration = ExpiresAt?.ToString("yyyy-MM-dd HH:mm:ss UTC") ?? "No expiration";
            var scopeCount = Scopes.Count;
            
            return $"Token for {TargetServiceId}: {tokenPreview}, {scopeCount} scopes, expires {expiration}";
        }

        #endregion

    }

}
