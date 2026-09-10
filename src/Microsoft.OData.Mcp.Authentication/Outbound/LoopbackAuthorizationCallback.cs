// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SdkAuth = ModelContextProtocol.Authentication;

namespace Microsoft.OData.Mcp.Authentication.Outbound
{

    /// <summary>
    /// The RFC 8252 loopback redirect endpoint the authorization code grant hands to the authorization server:
    /// an <see cref="HttpListener"/> bound to <c>http://127.0.0.1:{ephemeral}/callback/</c> that accepts exactly
    /// one authorization response, binds it to the expected <c>state</c>, and hands back the SDK
    /// <see cref="SdkAuth.AuthorizationResult"/>.
    /// </summary>
    /// <example>
    /// <code>
    /// using var callback = LoopbackAuthorizationCallback.Start(redirectOverride: null);
    ///
    /// var begin = grant.Begin(metadata, clientId, scope, resource, callback.RedirectUri);
    /// // ... the operator completes sign-in in a browser ...
    /// var result = await callback.WaitAsync(begin.State, cancellationToken);
    /// </code>
    /// </example>
    /// <remarks>
    /// The default host is the literal <c>127.0.0.1</c> and never <c>localhost</c>, per
    /// <c>specs/v3/AUTHENTICATION.md</c> "Security &amp; Privacy": a DNS name can resolve somewhere else and a
    /// second process listening on the same name would be handed the authorization code. The port is chosen by
    /// binding a <see cref="TcpListener"/> to port zero and reading back what the operating system assigned, so
    /// two concurrent sign-ins never collide. Per <c>AUTH-12</c> nothing here logs, echoes, or renders the
    /// <c>code</c>: the browser page says only that the window can be closed.
    /// </remarks>
    public sealed class LoopbackAuthorizationCallback : IDisposable
    {

        #region Fields

        /// <summary>
        /// The body written to the browser once an authorization response has been accepted.
        /// </summary>
        internal const string CompletionHtml = "<!doctype html><html><head><title>Sign-in complete</title></head><body><p>Sign-in complete. You can close this window.</p></body></html>";

        /// <summary>
        /// The path the default loopback redirect URI ends in.
        /// </summary>
        internal const string DefaultCallbackPath = "/callback/";

        /// <summary>
        /// The body written to the browser when the authorization response fails validation.
        /// </summary>
        internal const string FailureHtml = "<!doctype html><html><head><title>Sign-in failed</title></head><body><p>Sign-in failed. Return to the terminal for details.</p></body></html>";

        /// <summary>
        /// Whether <see cref="Dispose"/> has already closed <see cref="_listener"/>.
        /// </summary>
        internal bool _disposed;

        /// <summary>
        /// The listener this instance accepts the single authorization response on.
        /// </summary>
        internal readonly HttpListener _listener;

        #endregion

        #region Properties

        /// <summary>
        /// Gets the absolute redirect URI this instance is listening on, which is exactly the
        /// <c>redirect_uri</c> the authorization request and the token request must both carry.
        /// </summary>
        public Uri RedirectUri { get; }

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="LoopbackAuthorizationCallback"/> class over an
        /// already-started listener.
        /// </summary>
        /// <param name="listener">The started listener the single authorization response is accepted on.</param>
        /// <param name="redirectUri">The absolute redirect URI <paramref name="listener"/> is bound to.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="listener"/> or <paramref name="redirectUri"/> is <see langword="null"/>.</exception>
        /// <remarks>
        /// Internal because a caller that owns the listener would also have to own the prefix and the port
        /// selection; <see cref="Start(Uri)"/> is the only supported way to create one.
        /// </remarks>
        internal LoopbackAuthorizationCallback(HttpListener listener, Uri redirectUri)
        {
            ArgumentNullException.ThrowIfNull(listener);
            ArgumentNullException.ThrowIfNull(redirectUri);

            _listener = listener;
            RedirectUri = redirectUri;
        }

        #endregion

        #region Public Methods

        /// <inheritdoc />
        /// <remarks>
        /// Idempotent: a callback whose <see cref="WaitAsync(string, CancellationToken)"/> already ran is
        /// disposed a second time by the caller's <c>using</c> without error.
        /// </remarks>
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _listener.Close();
        }

        /// <summary>
        /// Binds a loopback listener for the authorization response and starts accepting connections.
        /// </summary>
        /// <param name="redirectOverride">The operator's <c>--redirect-uri</c>, used verbatim; or <see langword="null"/> to bind <c>http://127.0.0.1:{ephemeral}/callback/</c>.</param>
        /// <returns>
        /// A started callback whose <see cref="RedirectUri"/> is the effective redirect URI.
        /// </returns>
        /// <exception cref="ArgumentException">Thrown when <paramref name="redirectOverride"/> is not an absolute URI.</exception>
        /// <exception cref="OutboundDiscoveryException">
        /// Thrown when <paramref name="redirectOverride"/> is not <c>http</c> on a loopback host, and when the
        /// operating system refuses to bind the redirect URI.
        /// </exception>
        /// <example>
        /// <code>
        /// using var callback = LoopbackAuthorizationCallback.Start(options.RedirectUri);
        ///
        /// Console.Error.WriteLine($"Waiting for the authorization response on {callback.RedirectUri}");
        /// </code>
        /// </example>
        /// <remarks>
        /// The free port is discovered by binding a <see cref="TcpListener"/> to port zero and immediately
        /// releasing it, then handing that port to <see cref="HttpListener"/>. A bind failure — an override
        /// naming a port another process already owns, or a platform where <see cref="HttpListener"/> needs a
        /// URL reservation — fails first here with the prefix in the message, rather than half way through a
        /// sign-in the operator has already started in a browser.
        /// <para>
        /// An override is also held to the RFC 8252 section 7.3 policy this class exists to implement: it must
        /// be <c>http</c> on <c>127.0.0.1</c>, <c>localhost</c>, or <c>::1</c>. A public <c>https</c> redirect
        /// cannot be listened on here at all, and accepting one would leave the operator waiting on a listener
        /// the authorization server is never going to reach.
        /// </para>
        /// </remarks>
        public static LoopbackAuthorizationCallback Start(Uri? redirectOverride)
        {
            if (redirectOverride is not null && !redirectOverride.IsAbsoluteUri)
            {
                throw new ArgumentException($"The loopback redirect URI must be absolute: {redirectOverride}", nameof(redirectOverride));
            }

            if (redirectOverride is not null
                && (!string.Equals(redirectOverride.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) || !redirectOverride.IsLoopback))
            {
                throw new OutboundDiscoveryException(
                    $"The loopback redirect URI must use http on a loopback host (127.0.0.1, localhost, or ::1): {redirectOverride}");
            }

            var redirectUri = redirectOverride ?? new Uri($"http://{IPAddress.Loopback}:{FindFreePort()}{DefaultCallbackPath}", UriKind.Absolute);
            var prefix = CreatePrefix(redirectUri);
            var listener = new HttpListener();

            listener.Prefixes.Add(prefix);

            try
            {
                listener.Start();
            }
            catch (Exception exception) when (exception is HttpListenerException or PlatformNotSupportedException)
            {
                listener.Close();

                throw new OutboundDiscoveryException($"Could not listen for the authorization response on {prefix}: {exception.Message}", exception);
            }

            return new LoopbackAuthorizationCallback(listener, redirectUri);
        }

        /// <summary>
        /// Accepts exactly one authorization response, validates its <c>state</c>, and returns the SDK result
        /// the token exchange runs on.
        /// </summary>
        /// <param name="expectedState">The <c>state</c> the authorization request was started with, matched exactly.</param>
        /// <param name="cancellationToken">The token that abandons the wait, aborting the listener.</param>
        /// <returns>
        /// The authorization response's <c>code</c>, <c>state</c>, and RFC 9207 <c>iss</c>.
        /// </returns>
        /// <exception cref="ArgumentException">Thrown when <paramref name="expectedState"/> is <see langword="null"/>, empty, or whitespace.</exception>
        /// <exception cref="OperationCanceledException">Thrown when <paramref name="cancellationToken"/> is cancelled before a response arrives.</exception>
        /// <exception cref="OutboundDiscoveryException">
        /// Thrown when the response carries no <c>state</c>, a <c>state</c> that is not an exact match, or no
        /// <c>code</c>; and when the listener stops before a response arrives.
        /// </exception>
        /// <exception cref="OAuthTokenException">Thrown when the authorization server redirected an RFC 6749 section 4.1.2.1 error instead of a code.</exception>
        /// <example>
        /// <code>
        /// using var callback = LoopbackAuthorizationCallback.Start(null);
        /// var begin = grant.Begin(metadata, clientId, scope, resource, callback.RedirectUri);
        ///
        /// var result = await callback.WaitAsync(begin.State, cancellationToken);
        /// var response = await grant.ExchangeAsync(metadata, result, begin.CodeVerifier, clientId, callback.RedirectUri, resource, cancellationToken);
        /// </code>
        /// </example>
        /// <remarks>
        /// The listener is closed before this method returns or throws, so a mismatched <c>state</c> — the
        /// loopback CSRF case — leaves no socket a second attacker could reach. The comparison is
        /// <see cref="StringComparison.Ordinal"/> on the whole value: a prefix match would defeat the point of
        /// binding the request to the response.
        /// </remarks>
        public async Task<SdkAuth.AuthorizationResult> WaitAsync(string expectedState, CancellationToken cancellationToken)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(expectedState);

            try
            {
                HttpListenerContext context;

                using (cancellationToken.Register(static state => ((HttpListener)state!).Abort(), _listener))
                {
                    try
                    {
                        context = await _listener.GetContextAsync().ConfigureAwait(false);
                    }
                    catch (Exception exception) when (exception is HttpListenerException or ObjectDisposedException or InvalidOperationException)
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        throw new OutboundDiscoveryException($"The loopback listener on {RedirectUri} stopped before the authorization response arrived.", exception);
                    }
                }

                var parameters = ParseQuery(context.Request.Url?.Query);
                var state = ReadParameter(parameters, ODataMcpAuthConstants.StateParameter);

                if (!string.Equals(state, expectedState, StringComparison.Ordinal))
                {
                    await RespondAsync(context, (int)HttpStatusCode.BadRequest, FailureHtml).ConfigureAwait(false);

                    throw new OutboundDiscoveryException("Authorization response state mismatch.");
                }

                var error = ReadParameter(parameters, ODataMcpAuthConstants.ErrorParameter);
                if (!string.IsNullOrWhiteSpace(error))
                {
                    await RespondAsync(context, (int)HttpStatusCode.BadRequest, FailureHtml).ConfigureAwait(false);

                    throw new OAuthTokenException(error, ReadParameter(parameters, ODataMcpAuthConstants.ErrorDescriptionParameter), 0);
                }

                var code = ReadParameter(parameters, ODataMcpAuthConstants.CodeParameter);
                if (string.IsNullOrWhiteSpace(code))
                {
                    await RespondAsync(context, (int)HttpStatusCode.BadRequest, FailureHtml).ConfigureAwait(false);

                    throw new OutboundDiscoveryException("Authorization response carries no code.");
                }

                await RespondAsync(context, (int)HttpStatusCode.OK, CompletionHtml).ConfigureAwait(false);

                return new SdkAuth.AuthorizationResult
                {
                    Code = code,
                    Iss = ReadParameter(parameters, ODataMcpAuthConstants.IssParameter),
                    State = state
                };
            }
            finally
            {
                Dispose();
            }
        }

        #endregion

        #region Internal Methods

        /// <summary>
        /// Turns a redirect URI into the <see cref="HttpListener"/> prefix that serves it.
        /// </summary>
        /// <param name="redirectUri">The absolute redirect URI.</param>
        /// <returns>
        /// The scheme, authority, and path of <paramref name="redirectUri"/>, always ending in <c>/</c>.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="redirectUri"/> is <see langword="null"/>.</exception>
        /// <remarks>
        /// <see cref="HttpListener"/> refuses a prefix that does not end in a slash, and any query string or
        /// fragment on the redirect URI is not part of the prefix it matches against.
        /// </remarks>
        internal static string CreatePrefix(Uri redirectUri)
        {
            ArgumentNullException.ThrowIfNull(redirectUri);

            var prefix = redirectUri.GetLeftPart(UriPartial.Path);

            return prefix.EndsWith('/') ? prefix : $"{prefix}/";
        }

        /// <summary>
        /// Asks the operating system for a free loopback TCP port.
        /// </summary>
        /// <returns>
        /// The port number a listener bound to port zero was assigned.
        /// </returns>
        /// <remarks>
        /// The probe listener is stopped before the port is returned, so the <see cref="HttpListener"/> that
        /// follows can take it. A port that another process grabs in that window surfaces as the bind failure
        /// <see cref="Start(Uri)"/> reports, which is a better outcome than reserving ports this class does not
        /// use.
        /// </remarks>
        internal static int FindFreePort()
        {
            var probe = new TcpListener(IPAddress.Loopback, 0);

            probe.Start();

            try
            {
                return ((IPEndPoint)probe.LocalEndpoint).Port;
            }
            finally
            {
                probe.Stop();
            }
        }

        /// <summary>
        /// Parses an authorization response query string into its parameters.
        /// </summary>
        /// <param name="query">The raw query string, with or without its leading <c>?</c>; may be <see langword="null"/>.</param>
        /// <returns>
        /// The decoded parameters, keyed ordinally; empty when <paramref name="query"/> carries none.
        /// </returns>
        /// <remarks>
        /// Hand-rolled rather than taken from <c>System.Web</c> so this package keeps the same dependency set on
        /// every target framework. <c>+</c> is decoded as a space before percent-decoding, which is the
        /// <c>application/x-www-form-urlencoded</c> rule browsers apply to a query string.
        /// </remarks>
        internal static IReadOnlyDictionary<string, string> ParseQuery(string? query)
        {
            var parameters = new Dictionary<string, string>(StringComparer.Ordinal);

            if (string.IsNullOrWhiteSpace(query))
            {
                return parameters;
            }

            foreach (var pair in query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
            {
                var separatorIndex = pair.IndexOf('=');
                if (separatorIndex <= 0)
                {
                    continue;
                }

                var name = Uri.UnescapeDataString(pair[..separatorIndex].Replace('+', ' '));
                if (!parameters.ContainsKey(name))
                {
                    parameters[name] = Uri.UnescapeDataString(pair[(separatorIndex + 1)..].Replace('+', ' '));
                }
            }

            return parameters;
        }

        /// <summary>
        /// Reads one authorization response parameter.
        /// </summary>
        /// <param name="parameters">The parsed parameters.</param>
        /// <param name="name">The parameter name.</param>
        /// <returns>
        /// The value, or <see langword="null"/> when the response did not carry it.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="parameters"/> is <see langword="null"/>.</exception>
        internal static string? ReadParameter(IReadOnlyDictionary<string, string> parameters, string name)
        {
            ArgumentNullException.ThrowIfNull(parameters);

            return parameters.TryGetValue(name, out var value) ? value : null;
        }

        /// <summary>
        /// Writes the browser's answer and closes the response.
        /// </summary>
        /// <param name="context">The accepted request.</param>
        /// <param name="statusCode">The HTTP status code to answer with.</param>
        /// <param name="html">The HTML body to write.</param>
        /// <returns>
        /// A task that completes once the response has been written and closed.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="context"/> is <see langword="null"/>.</exception>
        /// <remarks>
        /// A browser that has already navigated away turns the write into an <see cref="HttpListenerException"/>;
        /// that is swallowed, because the authorization response has already been captured and failing the grant
        /// over a closed browser tab would help nobody.
        /// </remarks>
        internal static async Task RespondAsync(HttpListenerContext context, int statusCode, string html)
        {
            ArgumentNullException.ThrowIfNull(context);

            var body = Encoding.UTF8.GetBytes(html);

            try
            {
                context.Response.StatusCode = statusCode;
                context.Response.ContentType = "text/html; charset=utf-8";
                context.Response.ContentLength64 = body.Length;

                await context.Response.OutputStream.WriteAsync(body).ConfigureAwait(false);
                context.Response.Close();
            }
            catch (Exception exception) when (exception is HttpListenerException or ObjectDisposedException or IOException)
            {
                // The browser closed the connection; the authorization response is already captured.
            }
        }

        #endregion

    }

}
