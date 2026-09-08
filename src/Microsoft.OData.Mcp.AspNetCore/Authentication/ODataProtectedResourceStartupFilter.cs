// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Options;

namespace Microsoft.OData.Mcp.AspNetCore.Authentication
{

    /// <summary>
    /// Validates <see cref="ODataProtectedResourceOptions"/> at startup and prepends
    /// <see cref="ODataProtectedResourceMiddleware"/> to the pipeline, no matter how the app wrote its own
    /// <c>Configure</c> delegate.
    /// </summary>
    /// <example>
    /// <code>
    /// services.TryAddEnumerable(ServiceDescriptor.Transient&lt;IStartupFilter, ODataProtectedResourceStartupFilter&gt;());
    /// </code>
    /// </example>
    /// <remarks>
    /// Going in through an <see cref="IStartupFilter"/> is what makes the feature one line for an API author:
    /// there is no <c>UseODataProtectedResource</c> to remember, and no way to accidentally place it after
    /// <c>UseAuthentication</c>, where the metadata document would answer <c>401</c> and tell a client nothing.
    /// <para>
    /// Validation runs here rather than on the first request so a misconfigured API fails the host instead of
    /// failing whichever client happens to arrive first.
    /// </para>
    /// </remarks>
    public sealed class ODataProtectedResourceStartupFilter : IStartupFilter
    {

        #region Fields

        /// <summary>
        /// What this resource publishes about itself.
        /// </summary>
        internal readonly IOptions<ODataProtectedResourceOptions> _options;

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="ODataProtectedResourceStartupFilter"/> class.
        /// </summary>
        /// <param name="options">What this resource publishes about itself.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="options"/> is <see langword="null"/>.</exception>
        public ODataProtectedResourceStartupFilter(IOptions<ODataProtectedResourceOptions> options)
        {
            ArgumentNullException.ThrowIfNull(options);

            _options = options;
        }

        #endregion

        #region Public Methods

        /// <inheritdoc />
        /// <exception cref="InvalidOperationException">Thrown when <see cref="ODataProtectedResourceOptions.Validate"/> rejects the configuration.</exception>
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
        {
            ArgumentNullException.ThrowIfNull(next);

            _options.Value.Validate();

            return app =>
            {
                app.UseMiddleware<ODataProtectedResourceMiddleware>();
                next(app);
            };
        }

        #endregion

    }

}
