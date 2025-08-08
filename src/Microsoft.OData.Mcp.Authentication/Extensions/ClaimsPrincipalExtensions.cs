using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;

namespace System.Security.Claims
{
    /// <summary>
    /// Extension methods for <see cref="ClaimsPrincipal"/> to extract user information.
    /// </summary>
    /// <remarks>
    /// These extensions provide convenient methods to extract user scopes, roles, and other
    /// information from JWT tokens and other authentication schemes.
    /// </remarks>
    public static class ClaimsPrincipalExtensions
    {
        #region Constants

        /// <summary>
        /// The claim type for user scopes.
        /// </summary>
        public const string ScopeClaimType = "scope";

        /// <summary>
        /// The claim type for user roles.
        /// </summary>
        public const string RoleClaimType = ClaimTypes.Role;

        /// <summary>
        /// Alternative claim type for user roles in some JWT implementations.
        /// </summary>
        public const string AlternativeRoleClaimType = "roles";

        /// <summary>
        /// Alternative claim type for user scopes in some JWT implementations.
        /// </summary>
        public const string AlternativeScopeClaimType = "scopes";

        #endregion

        #region Public Methods

        /// <summary>
        /// Gets the user scopes from the claims principal.
        /// </summary>
        /// <param name="principal">The claims principal to extract scopes from.</param>
        /// <returns>A collection of user scopes, or empty if no scopes are found.</returns>
        /// <remarks>
        /// This method looks for scopes in various claim types commonly used by different
        /// authentication providers. Scopes are typically space-separated values in a single claim.
        /// </remarks>
        /// <example>
        /// <code>
        /// var userScopes = User.GetUserScopes();
        /// if (userScopes.Contains("read:users"))
        /// {
        ///     // User has permission to read users
        /// }
        /// </code>
        /// </example>
        public static IEnumerable<string> GetUserScopes(this ClaimsPrincipal principal)
        {
            if (principal?.Identity?.IsAuthenticated != true)
            {
                return Enumerable.Empty<string>();
            }

            var scopes = new List<string>();

            // Look for scope claims in various formats
            var scopeClaims = principal.Claims.Where(c =>
                string.Equals(c.Type, ScopeClaimType, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(c.Type, AlternativeScopeClaimType, StringComparison.OrdinalIgnoreCase))
                .ToList();

            foreach (var claim in scopeClaims)
            {
                if (!string.IsNullOrWhiteSpace(claim.Value))
                {
                    // Scopes are often space-separated in a single claim
                    var scopeValues = claim.Value.Split(new[] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries)
                        .Select(s => s.Trim())
                        .Where(s => !string.IsNullOrEmpty(s));
                    
                    scopes.AddRange(scopeValues);
                }
            }

            return scopes.Distinct();
        }

        /// <summary>
        /// Gets the user roles from the claims principal.
        /// </summary>
        /// <param name="principal">The claims principal to extract roles from.</param>
        /// <returns>A collection of user roles, or empty if no roles are found.</returns>
        /// <remarks>
        /// This method looks for roles in various claim types commonly used by different
        /// authentication providers. Roles can be in individual claims or comma-separated values.
        /// </remarks>
        /// <example>
        /// <code>
        /// var userRoles = User.GetUserRoles();
        /// if (userRoles.Contains("Administrator"))
        /// {
        ///     // User has administrator role
        /// }
        /// </code>
        /// </example>
        public static IEnumerable<string> GetUserRoles(this ClaimsPrincipal principal)
        {
            if (principal?.Identity?.IsAuthenticated != true)
            {
                return Enumerable.Empty<string>();
            }

            var roles = new List<string>();

            // Look for role claims in various formats
            var roleClaims = principal.Claims.Where(c =>
                string.Equals(c.Type, RoleClaimType, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(c.Type, AlternativeRoleClaimType, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(c.Type, ClaimTypes.Role, StringComparison.OrdinalIgnoreCase))
                .ToList();

            foreach (var claim in roleClaims)
            {
                if (!string.IsNullOrWhiteSpace(claim.Value))
                {
                    // Roles might be comma-separated in a single claim
                    var roleValues = claim.Value.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                        .Select(r => r.Trim())
                        .Where(r => !string.IsNullOrEmpty(r));
                    
                    roles.AddRange(roleValues);
                }
            }

            return roles.Distinct();
        }

        /// <summary>
        /// Gets the user identifier from the claims principal.
        /// </summary>
        /// <param name="principal">The claims principal to extract the user ID from.</param>
        /// <returns>The user identifier, or null if not found.</returns>
        /// <remarks>
        /// This method looks for the user identifier in common claim types used by
        /// different authentication providers.
        /// </remarks>
        public static string? GetUserId(this ClaimsPrincipal principal)
        {
            if (principal?.Identity?.IsAuthenticated != true)
            {
                return null;
            }

            // Try various common claim types for user ID
            var userIdClaim = principal.Claims.FirstOrDefault(c =>
                string.Equals(c.Type, ClaimTypes.NameIdentifier, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(c.Type, "sub", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(c.Type, "user_id", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(c.Type, "uid", StringComparison.OrdinalIgnoreCase));

            return userIdClaim?.Value;
        }

        /// <summary>
        /// Gets the username from the claims principal.
        /// </summary>
        /// <param name="principal">The claims principal to extract the username from.</param>
        /// <returns>The username, or null if not found.</returns>
        /// <remarks>
        /// This method looks for the username in common claim types used by
        /// different authentication providers.
        /// </remarks>
        public static string? GetUserName(this ClaimsPrincipal principal)
        {
            if (principal?.Identity?.IsAuthenticated != true)
            {
                return null;
            }

            // Try various common claim types for username
            var userNameClaim = principal.Claims.FirstOrDefault(c =>
                string.Equals(c.Type, ClaimTypes.Name, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(c.Type, "preferred_username", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(c.Type, "username", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(c.Type, "name", StringComparison.OrdinalIgnoreCase));

            return userNameClaim?.Value;
        }

        /// <summary>
        /// Gets the user email from the claims principal.
        /// </summary>
        /// <param name="principal">The claims principal to extract the email from.</param>
        /// <returns>The user email, or null if not found.</returns>
        /// <remarks>
        /// This method looks for the email in common claim types used by
        /// different authentication providers.
        /// </remarks>
        public static string? GetUserEmail(this ClaimsPrincipal principal)
        {
            if (principal?.Identity?.IsAuthenticated != true)
            {
                return null;
            }

            // Try various common claim types for email
            var emailClaim = principal.Claims.FirstOrDefault(c =>
                string.Equals(c.Type, ClaimTypes.Email, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(c.Type, "email", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(c.Type, "emails", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(c.Type, "preferred_email", StringComparison.OrdinalIgnoreCase));

            return emailClaim?.Value;
        }

        /// <summary>
        /// Determines whether the user has the specified scope.
        /// </summary>
        /// <param name="principal">The claims principal to check.</param>
        /// <param name="scope">The scope to check for.</param>
        /// <returns><c>true</c> if the user has the specified scope; otherwise, <c>false</c>.</returns>
        public static bool HasScope(this ClaimsPrincipal principal, string scope)
        {
            if (string.IsNullOrWhiteSpace(scope))
            {
                return false;
            }

            return principal.GetUserScopes().Contains(scope, StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Determines whether the user has the specified role.
        /// </summary>
        /// <param name="principal">The claims principal to check.</param>
        /// <param name="role">The role to check for.</param>
        /// <returns><c>true</c> if the user has the specified role; otherwise, <c>false</c>.</returns>
        public static bool HasRole(this ClaimsPrincipal principal, string role)
        {
            if (string.IsNullOrWhiteSpace(role))
            {
                return false;
            }

            return principal.GetUserRoles().Contains(role, StringComparer.OrdinalIgnoreCase);
        }

        #endregion
    }
}