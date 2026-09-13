// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;

namespace Microsoft.OData.Mcp.Tests.Shared.Authentication
{

    /// <summary>
    /// Prepends a counting middleware to the host pipeline so every request is recorded before any
    /// authentication middleware can short-circuit it.
    /// </summary>
    /// <example>
    /// <code>
    /// services.AddTransient&lt;IStartupFilter&gt;(_ =&gt; new RequestCountingStartupFilter(RecordRequest));
    /// services.AddSecuredResource(authorizationServer);
    /// </code>
    /// </example>
    /// <remarks>
    /// Startup filters are composed in reverse registration order, so this filter must be registered
    /// <em>before</em> <see cref="Microsoft.Extensions.DependencyInjection.ODataMcp_TestsSharedAuthentication_ServiceCollectionExtensions.AddSecuredResource(Microsoft.Extensions.DependencyInjection.IServiceCollection, LocalAuthorizationServer, string)"/>
    /// for its middleware to run first. That ordering is what lets a fixture count the unauthenticated probe
    /// that draws the <c>401</c> as well as the retry that carries the bearer token.
    /// </remarks>
    public sealed class RequestCountingStartupFilter : IStartupFilter
    {

        #region Fields

        /// <summary>
        /// The callback invoked once per request, before the rest of the pipeline runs.
        /// </summary>
        internal readonly Action<HttpContext> _record;

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="RequestCountingStartupFilter"/> class.
        /// </summary>
        /// <param name="record">The callback invoked once per request, before the rest of the pipeline runs.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="record"/> is <see langword="null"/>.</exception>
        public RequestCountingStartupFilter(Action<HttpContext> record)
        {
            ArgumentNullException.ThrowIfNull(record);

            _record = record;
        }

        #endregion

        #region Public Methods

        /// <inheritdoc />
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
        {
            ArgumentNullException.ThrowIfNull(next);

            return app =>
            {
                app.Use(async (context, nextMiddleware) =>
                {
                    _record(context);

                    await nextMiddleware(context).ConfigureAwait(false);
                });
                next(app);
            };
        }

        #endregion

    }

}
