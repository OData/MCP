// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.OData.Mcp.Authentication.Outbound;

namespace Microsoft.OData.Mcp.Tools.Hosting
{

    /// <summary>
    /// The Tools host's consent presenter: it writes the sign-in URL and user code to stderr and tries to open
    /// the operator's browser, then returns so the outbound OAuth client can complete the grant.
    /// </summary>
    /// <example>
    /// <code>
    /// options.ConsentPresenter ??= StdioConsentPresenter.PresentAsync;
    /// </code>
    /// </example>
    /// <remarks>
    /// Deliberately free of any dependency on an MCP server: this runs during host construction, before a
    /// session exists, and blocking the console at that point is the intended behavior. stdout belongs to the
    /// MCP transport, so every byte this writes goes to stderr. Nothing here ever prints an access token, a
    /// refresh token, or a device code — a user code and a verification URL are the only things a human needs.
    /// </remarks>
    public static class StdioConsentPresenter
    {

        #region Public Methods

        /// <summary>
        /// Presents a consent request on stderr and best-effort opens it in the operator's browser.
        /// </summary>
        /// <param name="request">The consent request the outbound OAuth client produced.</param>
        /// <param name="cancellationToken">The token that cancels the presentation.</param>
        /// <returns>
        /// A completed task once the request has been written; the grant itself is completed by the caller.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="request"/> is <see langword="null"/>.</exception>
        /// <exception cref="OperationCanceledException">Thrown when <paramref name="cancellationToken"/> is already cancelled.</exception>
        /// <example>
        /// <code>
        /// await StdioConsentPresenter.PresentAsync(request, cancellationToken);
        /// // stderr: Sign in at http://localhost/oauth/v2.0/device?user_code=ABCD1234
        /// // stderr: Code: ABCD1234
        /// </code>
        /// </example>
        /// <remarks>
        /// A browser that cannot be launched — a headless container, a locked-down desktop, no shell handler —
        /// is not an error: the URL is already on stderr and the operator can open it anywhere, so the failure
        /// is swallowed rather than aborting a sign-in that can still succeed.
        /// </remarks>
        public static Task PresentAsync(OutboundConsentRequest request, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            cancellationToken.ThrowIfCancellationRequested();

            Console.Error.WriteLine($"Sign in at {request.Url}");
            if (!string.IsNullOrWhiteSpace(request.UserCode))
            {
                Console.Error.WriteLine($"Code: {request.UserCode}");
            }

            TryOpenBrowser(request.Url);

            return Task.CompletedTask;
        }

        #endregion

        #region Internal Methods

        /// <summary>
        /// Hands a URL to the operating system's default handler, swallowing every failure.
        /// </summary>
        /// <param name="url">The absolute URL to open.</param>
        /// <returns>
        /// <see langword="true"/> when a process was started; otherwise <see langword="false"/>.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="url"/> is <see langword="null"/>.</exception>
        internal static bool TryOpenBrowser(Uri url)
        {
            ArgumentNullException.ThrowIfNull(url);

            try
            {
                using var process = Process.Start(new ProcessStartInfo
                {
                    FileName = url.AbsoluteUri,
                    UseShellExecute = true
                });

                return process is not null;
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                return false;
            }
        }

        #endregion

    }

}
