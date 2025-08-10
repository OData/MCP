// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;

namespace Microsoft.OData.Mcp.Authentication.Models
{

    /// <summary>
    /// Represents the user context extracted from an authenticated request.
    /// </summary>
    /// <remarks>
    /// This class encapsulates the user's identity, authorization information, and
    /// other context data needed for processing MCP requests on behalf of the user.
    /// </remarks>
    public sealed class UserContext
    {

        #region Properties

        /// <summary>
        /// Gets or sets the user's unique identifier.
        /// </summary>
        /// <value>The unique identifier for the authenticated user.</value>
        /// <remarks>
        /// This is typically extracted from the 'sub' (subject) claim in the JWT token
        /// and uniquely identifies the user across the system.
        /// </remarks>
        public required string UserId { get; set; }

        /// <summary>
        /// Gets or sets the user's display name.
        /// </summary>
        /// <value>The display name or username of the authenticated user.</value>
        /// <remarks>
        /// This is typically extracted from claims like 'name', 'preferred_username',
        /// or 'upn' and is used for display purposes in logs and audit trails.
        /// </remarks>
        public string? DisplayName { get; set; }

        /// <summary>
        /// Gets or sets the user's email address.
        /// </summary>
        /// <value>The email address of the authenticated user.</value>
        /// <remarks>
        /// This is typically extracted from the 'email' claim and can be used
        /// for notifications or audit purposes.
        /// </remarks>
        public string? Email { get; set; }

        /// <summary>
        /// Gets or sets the OAuth2 scopes granted to the user.
        /// </summary>
        /// <value>A collection of OAuth2 scopes that define the user's permissions.</value>
        /// <remarks>
        /// These scopes are extracted from the token and determine what operations
        /// the user is authorized to perform through the MCP server.
        /// </remarks>
        public List<string> Scopes { get; set; } = [];

        /// <summary>
        /// Gets or sets the user's roles.
        /// </summary>
        /// <value>A collection of roles assigned to the user.</value>
        /// <remarks>
        /// Roles provide a higher-level grouping of permissions and are typically
        /// extracted from 'roles' or similar claims in the token.
        /// </remarks>
        public List<string> Roles { get; set; } = [];

        /// <summary>
        /// Gets or sets the tenant identifier for multi-tenant scenarios.
        /// </summary>
        /// <value>The identifier of the tenant the user belongs to.</value>
        /// <remarks>
        /// This is used in multi-tenant deployments to isolate data and operations
        /// between different organizational units or customers.
        /// </remarks>
        public string? TenantId { get; set; }

        /// <summary>
        /// Gets or sets the client application identifier.
        /// </summary>
        /// <value>The identifier of the client application that initiated the request.</value>
        /// <remarks>
        /// This identifies which application the user is accessing the MCP server
        /// through, which can be useful for auditing and access control.
        /// </remarks>
        public string? ClientId { get; set; }

        /// <summary>
        /// Gets or sets the issuer of the authentication token.
        /// </summary>
        /// <value>The issuer identifier from the JWT token.</value>
        /// <remarks>
        /// This identifies which authorization server issued the token, which is
        /// important for multi-provider scenarios and security auditing.
        /// </remarks>
        public string? Issuer { get; set; }

        /// <summary>
        /// Gets or sets the audience for which the token was issued.
        /// </summary>
        /// <value>The audience identifier from the JWT token.</value>
        /// <remarks>
        /// This identifies the intended recipient of the token, which should
        /// match the MCP server's configuration.
        /// </remarks>
        public string? Audience { get; set; }

        /// <summary>
        /// Gets or sets the original JWT token.
        /// </summary>
        /// <value>The raw JWT token that was used for authentication.</value>
        /// <remarks>
        /// This token can be forwarded to downstream services for delegation
        /// scenarios while maintaining the user's identity.
        /// </remarks>
        public string? Token { get; set; }

        /// <summary>
        /// Gets or sets the token expiration time.
        /// </summary>
        /// <value>The UTC date and time when the token expires.</value>
        /// <remarks>
        /// This is used to determine when the user's session will expire and
        /// when token refresh might be needed.
        /// </remarks>
        public DateTime? TokenExpiresAt { get; set; }

        /// <summary>
        /// Gets or sets additional user claims.
        /// </summary>
        /// <value>A dictionary of additional claims extracted from the token.</value>
        /// <remarks>
        /// This contains any custom claims that are not covered by the standard
        /// properties but may be needed for authorization or business logic.
        /// </remarks>
        public Dictionary<string, string> AdditionalClaims { get; set; } = [];

        /// <summary>
        /// Gets or sets the authentication method used.
        /// </summary>
        /// <value>The method used to authenticate the user (e.g., "Bearer", "JWT").</value>
        /// <remarks>
        /// This indicates how the user was authenticated, which can be useful
        /// for security auditing and compliance reporting.
        /// </remarks>
        public string? AuthenticationMethod { get; set; }

        /// <summary>
        /// Gets or sets the time when the user was authenticated.
        /// </summary>
        /// <value>The UTC date and time when authentication occurred.</value>
        /// <remarks>
        /// This timestamp is used for session management, auditing, and
        /// security analysis.
        /// </remarks>
        public DateTime AuthenticatedAt { get; set; } = DateTime.UtcNow;

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="UserContext"/> class.
        /// </summary>
        public UserContext()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="UserContext"/> class with the specified user ID.
        /// </summary>
        /// <param name="userId">The user's unique identifier.</param>
        /// <exception cref="ArgumentException">Thrown when <paramref name="userId"/> is null or whitespace.</exception>
        public UserContext(string userId)
        {
ArgumentException.ThrowIfNullOrWhiteSpace(userId);

            UserId = userId;
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Creates a user context from a claims principal.
        /// </summary>
        /// <param name="principal">The claims principal to extract user context from.</param>
        /// <param name="token">The original JWT token (optional).</param>
        /// <returns>A user context populated with information from the claims principal.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="principal"/> is null.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the principal does not contain a subject claim.</exception>
        public static UserContext FromClaimsPrincipal(ClaimsPrincipal principal, string? token = null)
        {
ArgumentNullException.ThrowIfNull(principal);

            var subjectClaim = principal.FindFirst(ClaimTypes.NameIdentifier) ?? 
                              principal.FindFirst("sub");
            
            if (subjectClaim is null)
            {
                throw new InvalidOperationException("Claims principal must contain a subject claim (sub or NameIdentifier).");
            }

            var userContext = new UserContext(subjectClaim.Value)
            {
                UserId = subjectClaim.Value,
                DisplayName = principal.FindFirst(ClaimTypes.Name)?.Value ?? 
                             principal.FindFirst("name")?.Value ?? 
                             principal.FindFirst("preferred_username")?.Value,
                
                Email = principal.FindFirst(ClaimTypes.Email)?.Value ?? 
                       principal.FindFirst("email")?.Value,
                
                TenantId = principal.FindFirst("tid")?.Value ?? 
                          principal.FindFirst("tenant_id")?.Value,
                
                ClientId = principal.FindFirst("client_id")?.Value ?? 
                          principal.FindFirst("azp")?.Value,
                
                Issuer = principal.FindFirst("iss")?.Value,
                
                Audience = principal.FindFirst("aud")?.Value,
                
                Token = token,
                
                AuthenticationMethod = principal.Identity?.AuthenticationType
            };

            // Extract scopes
            var scopeClaims = principal.FindAll("scope").Concat(principal.FindAll("scp"));
            foreach (var scopeClaim in scopeClaims)
            {
                if (!string.IsNullOrWhiteSpace(scopeClaim.Value))
                {
                    // Handle both space-separated and individual scope claims
                    var scopes = scopeClaim.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    userContext.Scopes.AddRange(scopes);
                }
            }

            // Remove duplicates
            userContext.Scopes = userContext.Scopes.Distinct(StringComparer.OrdinalIgnoreCase).ToList();

            // Extract roles
            var roleClaims = principal.FindAll(ClaimTypes.Role).Concat(principal.FindAll("roles"));
            foreach (var roleClaim in roleClaims)
            {
                if (!string.IsNullOrWhiteSpace(roleClaim.Value))
                {
                    userContext.Roles.Add(roleClaim.Value);
                }
            }

            // Remove duplicates
            userContext.Roles = userContext.Roles.Distinct(StringComparer.OrdinalIgnoreCase).ToList();

            // Extract token expiration
            var expClaim = principal.FindFirst("exp");
            if (expClaim is not null && long.TryParse(expClaim.Value, out var exp))
            {
                userContext.TokenExpiresAt = DateTimeOffset.FromUnixTimeSeconds(exp).UtcDateTime;
            }

            // Extract additional claims
            var standardClaims = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                ClaimTypes.NameIdentifier, "sub", ClaimTypes.Name, "name", "preferred_username",
                ClaimTypes.Email, "email", "tid", "tenant_id", "client_id", "azp", "iss", "aud",
                "scope", "scp", ClaimTypes.Role, "roles", "exp", "iat", "nbf", "jti", "nonce"
            };

            foreach (var claim in principal.Claims)
            {
                if (!standardClaims.Contains(claim.Type) && !string.IsNullOrWhiteSpace(claim.Value))
                {
                    userContext.AdditionalClaims[claim.Type] = claim.Value;
                }
            }

            return userContext;
        }

        /// <summary>
        /// Determines whether the user has any of the specified scopes.
        /// </summary>
        /// <param name="requiredScopes">The scopes to check for.</param>
        /// <returns><c>true</c> if the user has at least one of the required scopes; otherwise, <c>false</c>.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="requiredScopes"/> is null.</exception>
        public bool HasAnyScope(IEnumerable<string> requiredScopes)
        {
ArgumentNullException.ThrowIfNull(requiredScopes);

            return requiredScopes.Any(scope => Scopes.Contains(scope, StringComparer.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Determines whether the user has all of the specified scopes.
        /// </summary>
        /// <param name="requiredScopes">The scopes to check for.</param>
        /// <returns><c>true</c> if the user has all of the required scopes; otherwise, <c>false</c>.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="requiredScopes"/> is null.</exception>
        public bool HasAllScopes(IEnumerable<string> requiredScopes)
        {
ArgumentNullException.ThrowIfNull(requiredScopes);

            return requiredScopes.All(scope => Scopes.Contains(scope, StringComparer.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Determines whether the user has any of the specified roles.
        /// </summary>
        /// <param name="requiredRoles">The roles to check for.</param>
        /// <returns><c>true</c> if the user has at least one of the required roles; otherwise, <c>false</c>.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="requiredRoles"/> is null.</exception>
        public bool HasAnyRole(IEnumerable<string> requiredRoles)
        {
ArgumentNullException.ThrowIfNull(requiredRoles);

            return requiredRoles.Any(role => Roles.Contains(role, StringComparer.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Determines whether the user's token is expired.
        /// </summary>
        /// <returns><c>true</c> if the token is expired; otherwise, <c>false</c>.</returns>
        public bool IsTokenExpired()
        {
            return TokenExpiresAt.HasValue && DateTime.UtcNow >= TokenExpiresAt.Value;
        }

        /// <summary>
        /// Gets the remaining time before the token expires.
        /// </summary>
        /// <returns>The remaining time before token expiration, or null if no expiration is set.</returns>
        public TimeSpan? GetRemainingTokenLifetime()
        {
            if (!TokenExpiresAt.HasValue)
            {
                return null;
            }

            var remaining = TokenExpiresAt.Value - DateTime.UtcNow;
            return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
        }

        /// <summary>
        /// Gets an additional claim value by type.
        /// </summary>
        /// <param name="claimType">The claim type to retrieve.</param>
        /// <returns>The claim value if found; otherwise, null.</returns>
        public string? GetAdditionalClaim(string claimType)
        {
            return AdditionalClaims.TryGetValue(claimType, out var value) ? value : null;
        }

        /// <summary>
        /// Returns a string representation of the user context.
        /// </summary>
        /// <returns>A summary of the user context.</returns>
        public override string ToString()
        {
            var displayName = !string.IsNullOrWhiteSpace(DisplayName) ? DisplayName : UserId;
            var scopeCount = Scopes.Count;
            var expiration = TokenExpiresAt?.ToString("yyyy-MM-dd HH:mm:ss UTC") ?? "No expiration";
            
            return $"User: {displayName}, Scopes: {scopeCount}, Expires: {expiration}";
        }

        #endregion

    }

}
