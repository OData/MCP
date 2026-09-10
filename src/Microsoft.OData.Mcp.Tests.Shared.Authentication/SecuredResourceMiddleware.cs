// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;

namespace Microsoft.OData.Mcp.Tests.Shared.Authentication
{

    /// <summary>
    /// Requires a valid bearer token on every request beneath a path prefix, regardless of how the host built the rest
    /// of its pipeline.
    /// </summary>
    /// <remarks>
    /// Failing authentication is answered with <see cref="AuthenticationHttpContextExtensions.ChallengeAsync(HttpContext, string)"/>
    /// against the JWT bearer scheme, so the response carries the <c>WWW-Authenticate</c> value configured on
    /// <see cref="JwtBearerOptions.Challenge"/> — which is how the RFC 9728 <c>resource_metadata</c> hint reaches the
    /// client.
    /// </remarks>
    public sealed class SecuredResourceMiddleware
    {

        #region Fields

        /// <summary>
        /// The next middleware in the pipeline.
        /// </summary>
        internal readonly RequestDelegate _next;

        /// <summary>
        /// The path prefix that requires a bearer token.
        /// </summary>
        internal readonly string _protectedPathPrefix;

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="SecuredResourceMiddleware"/> class.
        /// </summary>
        /// <param name="next">The next middleware in the pipeline.</param>
        /// <param name="protectedPathPrefix">The path prefix that requires a bearer token, for example <c>/odata</c>.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="next"/> is <see langword="null"/>.</exception>
        /// <exception cref="ArgumentException">Thrown when <paramref name="protectedPathPrefix"/> is <see langword="null"/>, empty, or whitespace.</exception>
        public SecuredResourceMiddleware(RequestDelegate next, string protectedPathPrefix)
        {
            ArgumentNullException.ThrowIfNull(next);
            ArgumentException.ThrowIfNullOrWhiteSpace(protectedPathPrefix);

            _next = next;
            _protectedPathPrefix = protectedPathPrefix;
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Authenticates the request when it falls beneath the protected prefix, then continues the pipeline.
        /// </summary>
        /// <param name="context">The request being served.</param>
        /// <returns>
        /// A task that completes when the request has been handled.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="context"/> is <see langword="null"/>.</exception>
        public async Task InvokeAsync(HttpContext context)
        {
            ArgumentNullException.ThrowIfNull(context);

            if (context.Request.Path.StartsWithSegments(_protectedPathPrefix))
            {
                var result = await context.AuthenticateAsync(JwtBearerDefaults.AuthenticationScheme).ConfigureAwait(false);
                if (!result.Succeeded)
                {
                    await context.ChallengeAsync(JwtBearerDefaults.AuthenticationScheme).ConfigureAwait(false);

                    return;
                }

                context.User = result.Principal;
            }

            await _next(context).ConfigureAwait(false);
        }

        #endregion

    }

}
