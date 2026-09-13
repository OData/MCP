// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.OData.Mcp.Authentication.Outbound;

namespace Microsoft.OData.Mcp.Tests.Shared.Authentication
{

    /// <summary>
    /// Replaces the <c>Authorization</c> header of every request with a bearer token a protected resource
    /// actually accepts.
    /// </summary>
    /// <example>
    /// <code>
    /// using var client = new HttpClient(new BearerStampingHandler(server.IssueAccessToken("read write"))
    /// {
    ///     InnerHandler = resourceServer.CreateHandler()
    /// });
    /// </code>
    /// </example>
    /// <remarks>
    /// This exists because the authenticated suites link their tool tests verbatim from the in-process suites,
    /// and several of those set a placeholder <c>Authorization: Bearer test</c> on the client to prove the
    /// header reaches the controller. Against a real protected resource that placeholder is simply an invalid
    /// token, and the test would fail for a reason it was never written to assert. Stamping at the handler
    /// rather than on <see cref="HttpClient.DefaultRequestHeaders"/> is what survives a caller overwriting the
    /// default.
    /// </remarks>
    public sealed class BearerStampingHandler : DelegatingHandler
    {

        #region Fields

        /// <summary>
        /// The bearer token stamped onto every request.
        /// </summary>
        internal readonly string _accessToken;

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="BearerStampingHandler"/> class.
        /// </summary>
        /// <param name="accessToken">The bearer token to stamp onto every request.</param>
        /// <exception cref="ArgumentException">Thrown when <paramref name="accessToken"/> is <see langword="null"/>, empty, or whitespace.</exception>
        public BearerStampingHandler(string accessToken)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(accessToken);

            _accessToken = accessToken;
        }

        #endregion

        #region Protected Methods

        /// <inheritdoc />
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="request"/> is <see langword="null"/>.</exception>
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            request.Headers.Authorization = new AuthenticationHeaderValue(ODataMcpAuthConstants.BearerScheme, _accessToken);

            return base.SendAsync(request, cancellationToken);
        }

        #endregion

    }

}
