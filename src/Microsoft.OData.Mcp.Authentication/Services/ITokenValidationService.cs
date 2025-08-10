// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.OData.Mcp.Authentication.Models;

namespace Microsoft.OData.Mcp.Authentication.Services
{
    /// <summary>
    /// Provides services for validating JWT tokens and extracting user context.
    /// </summary>
    /// <remarks>
    /// This service handles the core token validation logic, including signature verification,
    /// claim extraction, and scope validation. It provides a abstraction layer over the
    /// underlying JWT validation mechanisms.
    /// </remarks>
    public interface ITokenValidationService
    {
        /// <summary>
        /// Validates a JWT token and returns the principal if valid.
        /// </summary>
        /// <param name="token">The JWT token to validate.</param>
        /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
        /// <returns>A task that represents the asynchronous validation operation. The task result contains the claims principal if the token is valid, or null if invalid.</returns>
        /// <exception cref="ArgumentException">Thrown when <paramref name="token"/> is null or whitespace.</exception>
        Task<ClaimsPrincipal?> ValidateTokenAsync(string token, CancellationToken cancellationToken = default);

        /// <summary>
        /// Validates a JWT token with additional validation parameters.
        /// </summary>
        /// <param name="token">The JWT token to validate.</param>
        /// <param name="validationParameters">Additional validation parameters to apply.</param>
        /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
        /// <returns>A task that represents the asynchronous validation operation. The task result contains the validation result.</returns>
        /// <exception cref="ArgumentException">Thrown when <paramref name="token"/> is null or whitespace.</exception>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="validationParameters"/> is null.</exception>
        Task<TokenValidationResult> ValidateTokenAsync(string token, Dictionary<string, object> validationParameters, CancellationToken cancellationToken = default);

        /// <summary>
        /// Extracts the user context from a validated claims principal.
        /// </summary>
        /// <param name="principal">The claims principal from a validated token.</param>
        /// <returns>The user context containing identity and authorization information.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="principal"/> is null.</exception>
        UserContext ExtractUserContext(ClaimsPrincipal principal);

        /// <summary>
        /// Checks if a user has the required scopes for a specific operation.
        /// </summary>
        /// <param name="userContext">The user context to check.</param>
        /// <param name="requiredScopes">The scopes required for the operation.</param>
        /// <returns><c>true</c> if the user has at least one of the required scopes; otherwise, <c>false</c>.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="userContext"/> or <paramref name="requiredScopes"/> is null.</exception>
        bool HasRequiredScopes(UserContext userContext, IEnumerable<string> requiredScopes);

        /// <summary>
        /// Gets the authorization metadata from the JWT token for downstream services.
        /// </summary>
        /// <param name="token">The JWT token to extract metadata from.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains the authorization metadata.</returns>
        /// <exception cref="ArgumentException">Thrown when <paramref name="token"/> is null or whitespace.</exception>
        Task<AuthorizationMetadata> GetAuthorizationMetadataAsync(string token);

        /// <summary>
        /// Determines if a token is expired based on its claims.
        /// </summary>
        /// <param name="principal">The claims principal from a validated token.</param>
        /// <returns><c>true</c> if the token is expired; otherwise, <c>false</c>.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="principal"/> is null.</exception>
        bool IsTokenExpired(ClaimsPrincipal principal);

        /// <summary>
        /// Gets the remaining lifetime of a token.
        /// </summary>
        /// <param name="principal">The claims principal from a validated token.</param>
        /// <returns>The remaining time before the token expires, or null if the token has no expiration.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="principal"/> is null.</exception>
        TimeSpan? GetTokenLifetime(ClaimsPrincipal principal);
    }
}