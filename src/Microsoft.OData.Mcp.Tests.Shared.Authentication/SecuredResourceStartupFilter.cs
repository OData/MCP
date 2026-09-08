// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;

namespace Microsoft.OData.Mcp.Tests.Shared.Authentication
{

    /// <summary>
    /// Prepends <see cref="SecuredResourceMiddleware"/> to the host pipeline so the protected prefix is guarded no
    /// matter how the host's own <c>Configure</c> delegate is written.
    /// </summary>
    /// <remarks>
    /// Registering the middleware through an <see cref="IStartupFilter"/> keeps linked host fixtures untouched: they
    /// keep calling <c>UseRouting</c> / <c>MapControllers</c> and still end up authenticated.
    /// </remarks>
    public sealed class SecuredResourceStartupFilter : IStartupFilter
    {

        #region Fields

        /// <summary>
        /// The path prefix that requires a bearer token.
        /// </summary>
        internal readonly string _protectedPathPrefix;

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="SecuredResourceStartupFilter"/> class.
        /// </summary>
        /// <param name="protectedPathPrefix">The path prefix that requires a bearer token, for example <c>/odata</c>.</param>
        /// <exception cref="ArgumentException">Thrown when <paramref name="protectedPathPrefix"/> is <see langword="null"/>, empty, or whitespace.</exception>
        public SecuredResourceStartupFilter(string protectedPathPrefix)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(protectedPathPrefix);

            _protectedPathPrefix = protectedPathPrefix;
        }

        #endregion

        #region Public Methods

        /// <inheritdoc />
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
        {
            ArgumentNullException.ThrowIfNull(next);

            return app =>
            {
                app.UseMiddleware<SecuredResourceMiddleware>(_protectedPathPrefix);
                next(app);
            };
        }

        #endregion

    }

}
