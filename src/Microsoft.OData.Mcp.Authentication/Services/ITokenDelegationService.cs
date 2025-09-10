// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.OData.Mcp.Authentication.Models;

namespace Microsoft.OData.Mcp.Authentication.Services
{

    /// <summary>
    /// Provides services for delegating authentication tokens to downstream services.
    /// </summary>
    /// <remarks>
    /// This service handles the complexities of token delegation, including token forwarding,
    /// exchange, and on-behalf-of flows. It ensures that user identity is preserved while
    /// enabling secure communication with downstream OData services.
    /// </remarks>
    public interface ITokenDelegationService
    {

        /// <summary>
        /// Gets an authentication token for making requests to a specific target service.
        /// </summary>
        /// <param name="originalToken">The original user token.</param>
        /// <param name="targetServiceId">The identifier of the target service.</param>
        /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains the delegated token.</returns>
        /// <exception cref="ArgumentException">Thrown when <paramref name="originalToken"/> or <paramref name="targetServiceId"/> is null or whitespace.</exception>
        Task<DelegatedToken> GetTokenForServiceAsync(string originalToken, string targetServiceId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets an authentication token for making requests to a target URL.
        /// </summary>
        /// <param name="originalToken">The original user token.</param>
        /// <param name="targetUrl">The URL of the target service.</param>
        /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains the delegated token.</returns>
        /// <exception cref="ArgumentException">Thrown when <paramref name="originalToken"/> or <paramref name="targetUrl"/> is null or whitespace.</exception>
        Task<DelegatedToken> GetTokenForUrlAsync(string originalToken, string targetUrl, CancellationToken cancellationToken = default);

        /// <summary>
        /// Exchanges a token for a new token with different scopes or audience.
        /// </summary>
        /// <param name="originalToken">The original token to exchange.</param>
        /// <param name="targetAudience">The audience for the new token.</param>
        /// <param name="requestedScopes">The scopes to request for the new token.</param>
        /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains the exchanged token.</returns>
        /// <exception cref="ArgumentException">Thrown when <paramref name="originalToken"/> or <paramref name="targetAudience"/> is null or whitespace.</exception>
        Task<DelegatedToken> ExchangeTokenAsync(string originalToken, string targetAudience, IEnumerable<string>? requestedScopes = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Performs an OAuth2 on-behalf-of flow to get a token for a downstream service.
        /// </summary>
        /// <param name="originalToken">The original user token.</param>
        /// <param name="targetAudience">The audience for the new token.</param>
        /// <param name="clientCredentials">The client credentials for the on-behalf-of flow.</param>
        /// <param name="requestedScopes">The scopes to request for the new token.</param>
        /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains the on-behalf-of token.</returns>
        /// <exception cref="ArgumentException">Thrown when <paramref name="originalToken"/> or <paramref name="targetAudience"/> is null or whitespace.</exception>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="clientCredentials"/> is null.</exception>
        Task<DelegatedToken> GetOnBehalfOfTokenAsync(string originalToken, string targetAudience, ClientCredentials clientCredentials, IEnumerable<string>? requestedScopes = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Validates that a token is suitable for delegation to a specific service.
        /// </summary>
        /// <param name="token">The token to validate for delegation.</param>
        /// <param name="targetServiceId">The identifier of the target service.</param>
        /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
        /// <returns>A task that represents the asynchronous operation. The task result indicates whether the token is valid for delegation.</returns>
        /// <exception cref="ArgumentException">Thrown when <paramref name="token"/> or <paramref name="targetServiceId"/> is null or whitespace.</exception>
        Task<bool> ValidateTokenForDelegationAsync(string token, string targetServiceId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Refreshes a delegated token if it supports refresh operations.
        /// </summary>
        /// <param name="delegatedToken">The delegated token to refresh.</param>
        /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains the refreshed token.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="delegatedToken"/> is null.</exception>
        Task<DelegatedToken> RefreshTokenAsync(DelegatedToken delegatedToken, CancellationToken cancellationToken = default);

        /// <summary>
        /// Revokes a delegated token if the target service supports token revocation.
        /// </summary>
        /// <param name="delegatedToken">The delegated token to revoke.</param>
        /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="delegatedToken"/> is null.</exception>
        Task RevokeTokenAsync(DelegatedToken delegatedToken, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets the cached token for a specific service and user, if available.
        /// </summary>
        /// <param name="userId">The user identifier.</param>
        /// <param name="targetServiceId">The target service identifier.</param>
        /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains the cached token, or null if not found.</returns>
        /// <exception cref="ArgumentException">Thrown when <paramref name="userId"/> or <paramref name="targetServiceId"/> is null or whitespace.</exception>
        Task<DelegatedToken?> GetCachedTokenAsync(string userId, string targetServiceId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Clears all cached tokens for a specific user.
        /// </summary>
        /// <param name="userId">The user identifier.</param>
        /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        /// <exception cref="ArgumentException">Thrown when <paramref name="userId"/> is null or whitespace.</exception>
        Task ClearCachedTokensAsync(string userId, CancellationToken cancellationToken = default);

    }

}
