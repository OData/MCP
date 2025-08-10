using System;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.OData.Mcp.Authentication.Models
{

    /// <summary>
    /// Represents authorization metadata extracted from a JWT token for use in downstream services.
    /// </summary>
    /// <remarks>
    /// This class contains the authorization information needed to make decisions about
    /// what operations a user can perform and what data they can access. It's designed
    /// to be lightweight and serializable for caching and delegation scenarios.
    /// </remarks>
    public sealed class AuthorizationMetadata
    {

        #region Properties

        /// <summary>
        /// Gets or sets the user's unique identifier.
        /// </summary>
        /// <value>The subject identifier from the JWT token.</value>
        /// <remarks>
        /// This uniquely identifies the user across all systems and is used for
        /// auditing, logging, and data access control.
        /// </remarks>
        public required string Subject { get; set; }

        /// <summary>
        /// Gets or sets the OAuth2 scopes granted to the user.
        /// </summary>
        /// <value>A collection of scopes that define the user's permissions.</value>
        /// <remarks>
        /// These scopes determine what operations the user is authorized to perform.
        /// They are used for fine-grained authorization decisions throughout the system.
        /// </remarks>
        public List<string> Scopes { get; set; } = [];

        /// <summary>
        /// Gets or sets the user's roles.
        /// </summary>
        /// <value>A collection of roles assigned to the user.</value>
        /// <remarks>
        /// Roles provide a higher-level abstraction over permissions and can be
        /// used for role-based access control (RBAC) scenarios.
        /// </remarks>
        public List<string> Roles { get; set; } = [];

        /// <summary>
        /// Gets or sets the tenant identifier for multi-tenant scenarios.
        /// </summary>
        /// <value>The identifier of the tenant the user belongs to.</value>
        /// <remarks>
        /// This is used to isolate data and operations between different
        /// organizational units or customers in multi-tenant deployments.
        /// </remarks>
        public string? TenantId { get; set; }

        /// <summary>
        /// Gets or sets the client application identifier.
        /// </summary>
        /// <value>The identifier of the client application.</value>
        /// <remarks>
        /// This identifies which application the user is accessing the system
        /// through, which can affect authorization decisions and audit trails.
        /// </remarks>
        public string? ClientId { get; set; }

        /// <summary>
        /// Gets or sets the token issuer.
        /// </summary>
        /// <value>The issuer identifier from the JWT token.</value>
        /// <remarks>
        /// This identifies which authorization server issued the token, which
        /// is important for trust and validation decisions.
        /// </remarks>
        public string? Issuer { get; set; }

        /// <summary>
        /// Gets or sets the token audience.
        /// </summary>
        /// <value>The audience identifier from the JWT token.</value>
        /// <remarks>
        /// This identifies the intended recipient of the token and should match
        /// the service's expected audience value.
        /// </remarks>
        public string? Audience { get; set; }

        /// <summary>
        /// Gets or sets the token expiration time.
        /// </summary>
        /// <value>The UTC date and time when the token expires.</value>
        /// <remarks>
        /// This is used to determine if the authorization is still valid and
        /// when refresh might be needed.
        /// </remarks>
        public DateTime? ExpiresAt { get; set; }

        /// <summary>
        /// Gets or sets the token issued time.
        /// </summary>
        /// <value>The UTC date and time when the token was issued.</value>
        /// <remarks>
        /// This timestamp can be used for auditing and determining the age
        /// of the authorization grant.
        /// </remarks>
        public DateTime? IssuedAt { get; set; }

        /// <summary>
        /// Gets or sets custom authorization attributes.
        /// </summary>
        /// <value>A dictionary of custom attributes that affect authorization decisions.</value>
        /// <remarks>
        /// These attributes can contain business-specific authorization data
        /// such as department, cost center, or data classification levels.
        /// </remarks>
        public Dictionary<string, string> CustomAttributes { get; set; } = [];

        /// <summary>
        /// Gets or sets the authorization context identifier.
        /// </summary>
        /// <value>A unique identifier for this authorization context.</value>
        /// <remarks>
        /// This can be used to correlate authorization decisions across
        /// multiple services and audit logs.
        /// </remarks>
        public string? ContextId { get; set; }

        /// <summary>
        /// Gets or sets the session identifier.
        /// </summary>
        /// <value>The identifier of the user's authentication session.</value>
        /// <remarks>
        /// This links the authorization to a specific user session and can
        /// be used for session management and security monitoring.
        /// </remarks>
        public string? SessionId { get; set; }

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="AuthorizationMetadata"/> class.
        /// </summary>
        public AuthorizationMetadata()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="AuthorizationMetadata"/> class with the specified subject.
        /// </summary>
        /// <param name="subject">The user's subject identifier.</param>
        /// <exception cref="ArgumentException">Thrown when <paramref name="subject"/> is null or whitespace.</exception>
        public AuthorizationMetadata(string subject)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(subject);

            Subject = subject;
            ContextId = Guid.NewGuid().ToString();
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Creates authorization metadata from a user context.
        /// </summary>
        /// <param name="userContext">The user context to extract metadata from.</param>
        /// <returns>Authorization metadata populated with information from the user context.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="userContext"/> is null.</exception>
        public static AuthorizationMetadata FromUserContext(UserContext userContext)
        {
            ArgumentNullException.ThrowIfNull(userContext);

            return new AuthorizationMetadata(userContext.UserId)
            {
                Subject = userContext.UserId,
                Scopes = new List<string>(userContext.Scopes),
                Roles = new List<string>(userContext.Roles),
                TenantId = userContext.TenantId,
                ClientId = userContext.ClientId,
                Issuer = userContext.Issuer,
                Audience = userContext.Audience,
                ExpiresAt = userContext.TokenExpiresAt,
                CustomAttributes = new Dictionary<string, string>(userContext.AdditionalClaims),
                SessionId = userContext.GetAdditionalClaim("sid")
            };
        }

        /// <summary>
        /// Determines whether the authorization has any of the specified scopes.
        /// </summary>
        /// <param name="requiredScopes">The scopes to check for.</param>
        /// <returns><c>true</c> if any of the required scopes are present; otherwise, <c>false</c>.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="requiredScopes"/> is null.</exception>
        public bool HasAnyScope(IEnumerable<string> requiredScopes)
        {
            ArgumentNullException.ThrowIfNull(requiredScopes);

            return requiredScopes.Any(scope => Scopes.Contains(scope, StringComparer.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Determines whether the authorization has all of the specified scopes.
        /// </summary>
        /// <param name="requiredScopes">The scopes to check for.</param>
        /// <returns><c>true</c> if all required scopes are present; otherwise, <c>false</c>.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="requiredScopes"/> is null.</exception>
        public bool HasAllScopes(IEnumerable<string> requiredScopes)
        {
ArgumentNullException.ThrowIfNull(requiredScopes);

            return requiredScopes.All(scope => Scopes.Contains(scope, StringComparer.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Determines whether the authorization has any of the specified roles.
        /// </summary>
        /// <param name="requiredRoles">The roles to check for.</param>
        /// <returns><c>true</c> if any of the required roles are present; otherwise, <c>false</c>.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="requiredRoles"/> is null.</exception>
        public bool HasAnyRole(IEnumerable<string> requiredRoles)
        {
            ArgumentNullException.ThrowIfNull(requiredRoles);

            return requiredRoles.Any(role => Roles.Contains(role, StringComparer.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Determines whether the authorization is expired.
        /// </summary>
        /// <returns><c>true</c> if the authorization is expired; otherwise, <c>false</c>.</returns>
        public bool IsExpired()
        {
            return ExpiresAt.HasValue && DateTime.UtcNow >= ExpiresAt.Value;
        }

        /// <summary>
        /// Gets the remaining time before the authorization expires.
        /// </summary>
        /// <returns>The remaining time before expiration, or null if no expiration is set.</returns>
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
        /// Gets a custom attribute value by key.
        /// </summary>
        /// <param name="key">The attribute key.</param>
        /// <returns>The attribute value if found; otherwise, null.</returns>
        public string? GetCustomAttribute(string key)
        {
            return CustomAttributes.TryGetValue(key, out var value) ? value : null;
        }

        /// <summary>
        /// Adds or updates a custom attribute.
        /// </summary>
        /// <param name="key">The attribute key.</param>
        /// <param name="value">The attribute value.</param>
        /// <exception cref="ArgumentException">Thrown when <paramref name="key"/> is null or whitespace.</exception>
        public void SetCustomAttribute(string key, string value)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(key);

            CustomAttributes[key] = value;
        }

        /// <summary>
        /// Creates a copy of the authorization metadata.
        /// </summary>
        /// <returns>A new instance with the same values as the current instance.</returns>
        public AuthorizationMetadata Clone()
        {
            return new AuthorizationMetadata(Subject)
            {
                Subject = Subject,
                Scopes = new List<string>(Scopes),
                Roles = new List<string>(Roles),
                TenantId = TenantId,
                ClientId = ClientId,
                Issuer = Issuer,
                Audience = Audience,
                ExpiresAt = ExpiresAt,
                IssuedAt = IssuedAt,
                CustomAttributes = new Dictionary<string, string>(CustomAttributes),
                ContextId = ContextId,
                SessionId = SessionId
            };
        }

        /// <summary>
        /// Returns a string representation of the authorization metadata.
        /// </summary>
        /// <returns>A summary of the authorization metadata.</returns>
        public override string ToString()
        {
            var scopeCount = Scopes.Count;
            var roleCount = Roles.Count;
            var expiration = ExpiresAt?.ToString("yyyy-MM-dd HH:mm:ss UTC") ?? "No expiration";
            
            return $"Subject: {Subject}, Scopes: {scopeCount}, Roles: {roleCount}, Expires: {expiration}";
        }

        #endregion

    }

}
