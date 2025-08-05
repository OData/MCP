using System;
using System.Collections.Generic;
using System.Security.Claims;

namespace Microsoft.OData.Mcp.Authentication.Models
{
    /// <summary>
    /// Represents the result of a token validation operation.
    /// </summary>
    /// <remarks>
    /// This class encapsulates the outcome of token validation, including success/failure status,
    /// the validated principal, and any validation errors that occurred during the process.
    /// </remarks>
    public sealed class TokenValidationResult
    {
        #region Properties

        /// <summary>
        /// Gets or sets a value indicating whether the token validation was successful.
        /// </summary>
        /// <value><c>true</c> if the token is valid; otherwise, <c>false</c>.</value>
        public bool IsValid { get; set; }

        /// <summary>
        /// Gets or sets the claims principal from the validated token.
        /// </summary>
        /// <value>The claims principal if validation was successful; otherwise, <c>null</c>.</value>
        /// <remarks>
        /// This principal contains all the claims extracted from the JWT token and can be
        /// used for authorization decisions and user context extraction.
        /// </remarks>
        public ClaimsPrincipal? Principal { get; set; }

        /// <summary>
        /// Gets or sets the validation error that occurred during token validation.
        /// </summary>
        /// <value>The validation error if validation failed; otherwise, <c>null</c>.</value>
        /// <remarks>
        /// This provides detailed information about why token validation failed,
        /// which can be useful for debugging and security auditing.
        /// </remarks>
        public string? Error { get; set; }

        /// <summary>
        /// Gets or sets the detailed error description.
        /// </summary>
        /// <value>A detailed description of the validation error; otherwise, <c>null</c>.</value>
        /// <remarks>
        /// This provides additional context about the validation failure, such as
        /// specific claims that were invalid or missing.
        /// </remarks>
        public string? ErrorDescription { get; set; }

        /// <summary>
        /// Gets or sets the exception that caused the validation failure.
        /// </summary>
        /// <value>The exception that occurred during validation; otherwise, <c>null</c>.</value>
        /// <remarks>
        /// This is typically used for logging and debugging purposes to understand
        /// the root cause of validation failures.
        /// </remarks>
        public Exception? Exception { get; set; }

        /// <summary>
        /// Gets or sets additional validation metadata.
        /// </summary>
        /// <value>A dictionary of additional information about the validation process.</value>
        /// <remarks>
        /// This can include information such as the validation time, token issuer,
        /// audience, or other metadata that might be useful for auditing or debugging.
        /// </remarks>
        public Dictionary<string, object> Metadata { get; set; } = new();

        /// <summary>
        /// Gets or sets the token expiration time.
        /// </summary>
        /// <value>The UTC date and time when the token expires; otherwise, <c>null</c>.</value>
        /// <remarks>
        /// This is extracted from the token's 'exp' claim and represents when the
        /// token will no longer be valid for authentication.
        /// </remarks>
        public DateTime? ExpiresAt { get; set; }

        /// <summary>
        /// Gets or sets the token issued time.
        /// </summary>
        /// <value>The UTC date and time when the token was issued; otherwise, <c>null</c>.</value>
        /// <remarks>
        /// This is extracted from the token's 'iat' claim and represents when the
        /// token was originally created by the authorization server.
        /// </remarks>
        public DateTime? IssuedAt { get; set; }

        /// <summary>
        /// Gets or sets the token not-before time.
        /// </summary>
        /// <value>The UTC date and time before which the token is not valid; otherwise, <c>null</c>.</value>
        /// <remarks>
        /// This is extracted from the token's 'nbf' claim and represents the earliest
        /// time the token can be used for authentication.
        /// </remarks>
        public DateTime? NotBefore { get; set; }

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="TokenValidationResult"/> class.
        /// </summary>
        public TokenValidationResult()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="TokenValidationResult"/> class for a successful validation.
        /// </summary>
        /// <param name="principal">The validated claims principal.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="principal"/> is null.</exception>
        public TokenValidationResult(ClaimsPrincipal principal)
        {
#if NET8_0_OR_GREATER
            ArgumentNullException.ThrowIfNull(principal);
#else
            if (principal is null)
            {
                throw new ArgumentNullException(nameof(principal));
            }
#endif

            IsValid = true;
            Principal = principal;
            ExtractTimestamps(principal);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="TokenValidationResult"/> class for a failed validation.
        /// </summary>
        /// <param name="error">The validation error.</param>
        /// <param name="errorDescription">The detailed error description.</param>
        /// <param name="exception">The exception that caused the failure.</param>
        /// <exception cref="ArgumentException">Thrown when <paramref name="error"/> is null or whitespace.</exception>
        public TokenValidationResult(string error, string? errorDescription = null, Exception? exception = null)
        {
#if NET8_0_OR_GREATER
            ArgumentException.ThrowIfNullOrWhiteSpace(error);
#else
            if (string.IsNullOrWhiteSpace(error))
            {
                throw new ArgumentException("Error cannot be null or whitespace.", nameof(error));
            }
#endif

            IsValid = false;
            Error = error;
            ErrorDescription = errorDescription;
            Exception = exception;
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Creates a successful validation result.
        /// </summary>
        /// <param name="principal">The validated claims principal.</param>
        /// <returns>A successful token validation result.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="principal"/> is null.</exception>
        public static TokenValidationResult Success(ClaimsPrincipal principal)
        {
            return new TokenValidationResult(principal);
        }

        /// <summary>
        /// Creates a failed validation result.
        /// </summary>
        /// <param name="error">The validation error.</param>
        /// <param name="errorDescription">The detailed error description.</param>
        /// <param name="exception">The exception that caused the failure.</param>
        /// <returns>A failed token validation result.</returns>
        /// <exception cref="ArgumentException">Thrown when <paramref name="error"/> is null or whitespace.</exception>
        public static TokenValidationResult Failure(string error, string? errorDescription = null, Exception? exception = null)
        {
            return new TokenValidationResult(error, errorDescription, exception);
        }

        /// <summary>
        /// Adds metadata to the validation result.
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
        /// Gets the remaining lifetime of the token.
        /// </summary>
        /// <returns>The remaining time before the token expires, or null if no expiration is set.</returns>
        public TimeSpan? GetRemainingLifetime()
        {
            if (!ExpiresAt.HasValue)
            {
                return null;
            }

            var remaining = ExpiresAt.Value - DateTime.UtcNow;
            return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
        }

        /// <summary>
        /// Determines whether the token is currently expired.
        /// </summary>
        /// <returns><c>true</c> if the token is expired; otherwise, <c>false</c>.</returns>
        public bool IsExpired()
        {
            if (!ExpiresAt.HasValue)
            {
                return false;
            }

            return DateTime.UtcNow >= ExpiresAt.Value;
        }

        /// <summary>
        /// Determines whether the token is not yet valid.
        /// </summary>
        /// <returns><c>true</c> if the token is not yet valid; otherwise, <c>false</c>.</returns>
        public bool IsNotYetValid()
        {
            if (!NotBefore.HasValue)
            {
                return false;
            }

            return DateTime.UtcNow < NotBefore.Value;
        }

        /// <summary>
        /// Returns a string representation of the token validation result.
        /// </summary>
        /// <returns>A summary of the validation result.</returns>
        public override string ToString()
        {
            if (IsValid)
            {
                var subject = Principal?.Identity?.Name ?? "Unknown";
                var lifetime = GetRemainingLifetime();
                var lifetimeText = lifetime?.ToString(@"hh\:mm\:ss") ?? "No expiration";
                return $"Valid token for '{subject}', expires in {lifetimeText}";
            }
            else
            {
                return $"Invalid token: {Error}";
            }
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Extracts timestamp claims from the principal.
        /// </summary>
        /// <param name="principal">The claims principal to extract timestamps from.</param>
        private void ExtractTimestamps(ClaimsPrincipal principal)
        {
            // Extract expiration time (exp claim)
            var expClaim = principal.FindFirst("exp");
            if (expClaim is not null && long.TryParse(expClaim.Value, out var exp))
            {
                ExpiresAt = DateTimeOffset.FromUnixTimeSeconds(exp).UtcDateTime;
            }

            // Extract issued at time (iat claim)
            var iatClaim = principal.FindFirst("iat");
            if (iatClaim is not null && long.TryParse(iatClaim.Value, out var iat))
            {
                IssuedAt = DateTimeOffset.FromUnixTimeSeconds(iat).UtcDateTime;
            }

            // Extract not before time (nbf claim)
            var nbfClaim = principal.FindFirst("nbf");
            if (nbfClaim is not null && long.TryParse(nbfClaim.Value, out var nbf))
            {
                NotBefore = DateTimeOffset.FromUnixTimeSeconds(nbf).UtcDateTime;
            }
        }

        #endregion
    }
}