// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.OData.Mcp.Authentication.Outbound;

namespace Microsoft.OData.Mcp.Tests.Shared.Authentication
{

    /// <summary>
    /// A consent presenter that stands in for a human at the keyboard: it opens the URL a
    /// <see cref="OutboundConsentRequest"/> carries against a <see cref="LocalAuthorizationServer"/>, which is
    /// exactly what approves the pending grant — a device verification URL for the device code grant, and an
    /// authorize URL whose <c>302</c> is then followed onto the CLI's real loopback socket for the
    /// authorization code grant.
    /// </summary>
    /// <example>
    /// <code>
    /// var presenter = new AutoApproveConsentPresenter(authorizationServer);
    /// var options = new OutboundOAuthOptions
    /// {
    ///     ClientId = "cli",
    ///     ConsentPresenter = presenter.PresentAsync
    /// };
    ///
    /// var token = await client.AcquireAsync(challenges, 401, interactiveAllowed: true, cancellationToken);
    ///
    /// presenter.Presentations.Should().Be(1);
    /// </code>
    /// </example>
    /// <remarks>
    /// The authorize GET runs through <see cref="LocalAuthorizationServer.Handler"/> rather than a real socket,
    /// so it is served by the same in-process fixture the grant exchanges its code against; the redirect that
    /// comes back is then followed over a real TCP connection, because the loopback listener under test is a
    /// real <see cref="System.Net.HttpListener"/> and nothing else would prove it works.
    /// <para>
    /// That second request is deliberately <em>not</em> awaited: a real browser navigates to the loopback URL
    /// while the CLI is still waiting on it, and the CLI only starts waiting after the presenter returns.
    /// Awaiting it here would deadlock the grant. A test that wants to assert on it awaits
    /// <see cref="LoopbackCompletion"/> instead.
    /// </para>
    /// </remarks>
    public sealed class AutoApproveConsentPresenter
    {

        #region Fields

        /// <summary>
        /// The authorization server whose in-process handler the approval GET is dispatched through.
        /// </summary>
        internal readonly LocalAuthorizationServer _authorizationServer;

        #endregion

        #region Properties

        /// <summary>
        /// Gets the consent request most recently presented, or <see langword="null"/> when none has been.
        /// </summary>
        public OutboundConsentRequest? LastRequest { get; internal set; }

        /// <summary>
        /// Gets the status code the CLI's loopback listener answered the followed redirect with, or
        /// <see langword="null"/> when no authorization code grant has been presented or the request failed.
        /// </summary>
        public HttpStatusCode? LoopbackStatusCode { get; internal set; }

        /// <summary>
        /// Gets the task that follows the authorization redirect onto the CLI's loopback listener, or
        /// <see langword="null"/> when no authorization code grant has been presented.
        /// </summary>
        /// <remarks>
        /// Never faults: a failed request leaves <see cref="LoopbackStatusCode"/> <see langword="null"/> instead,
        /// so an unawaited task can never surface as an unobserved exception in an unrelated test.
        /// </remarks>
        public Task? LoopbackCompletion { get; internal set; }

        /// <summary>
        /// Gets the number of times <see cref="PresentAsync(OutboundConsentRequest, CancellationToken)"/> has run
        /// to completion.
        /// </summary>
        public int Presentations { get; internal set; }

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="AutoApproveConsentPresenter"/> class.
        /// </summary>
        /// <param name="server">The authorization server whose in-process handler the approval GET is dispatched through.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="server"/> is <see langword="null"/>.</exception>
        public AutoApproveConsentPresenter(LocalAuthorizationServer server)
        {
            ArgumentNullException.ThrowIfNull(server);

            _authorizationServer = server;
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Approves a pending interactive grant by fetching the URL the request carries.
        /// </summary>
        /// <param name="request">The consent request the outbound OAuth client produced.</param>
        /// <param name="cancellationToken">The token that cancels the approval GET.</param>
        /// <returns>
        /// A task that completes once the authorization server has recorded the approval, or — for the
        /// authorization code grant — once the redirect has been dispatched to the loopback listener.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="request"/> is <see langword="null"/>.</exception>
        /// <exception cref="NotSupportedException">Thrown when <see cref="OutboundConsentRequest.Kind"/> is neither device code nor authorization code.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the authorization endpoint answers something other than a redirect.</exception>
        /// <exception cref="HttpRequestException">Thrown when the authorization server refuses the approval.</exception>
        public async Task PresentAsync(OutboundConsentRequest request, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            if (request.Kind is not OutboundGrantKind.DeviceCode and not OutboundGrantKind.AuthorizationCode)
            {
                throw new NotSupportedException($"AutoApproveConsentPresenter only approves interactive grants, not '{OutboundGrantKindParser.ToWireName(request.Kind)}'.");
            }

            LastRequest = request;

            using var client = new HttpClient(_authorizationServer.Handler, disposeHandler: false);
            using var response = await client.GetAsync(request.Url, cancellationToken).ConfigureAwait(false);

            if (request.Kind is OutboundGrantKind.DeviceCode)
            {
                response.EnsureSuccessStatusCode();
                Presentations++;

                return;
            }

            if (response.StatusCode is not HttpStatusCode.Found || response.Headers.Location is null)
            {
                throw new InvalidOperationException($"The authorization endpoint answered {(int)response.StatusCode} instead of a redirect to the loopback callback.");
            }

            LoopbackCompletion = FollowRedirectAsync(response.Headers.Location);
            Presentations++;
        }

        #endregion

        #region Internal Methods

        /// <summary>
        /// Follows an authorization redirect onto the CLI's real loopback listener, without waiting for it.
        /// </summary>
        /// <param name="location">The absolute loopback callback URL the authorization endpoint redirected to.</param>
        /// <returns>
        /// A task that completes once the loopback listener has answered, recording its status code.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="location"/> is <see langword="null"/>.</exception>
        /// <remarks>
        /// Runs on its own <see cref="HttpClient"/> over a real socket, because the listener under test is a real
        /// <see cref="System.Net.HttpListener"/> that the in-process test server handler cannot reach. Every
        /// failure is swallowed into a <see langword="null"/> <see cref="LoopbackStatusCode"/>: the caller is not
        /// awaiting this, and a grant that never completes is the assertion a test should fail on.
        /// </remarks>
        internal async Task FollowRedirectAsync(Uri location)
        {
            ArgumentNullException.ThrowIfNull(location);

            try
            {
                using var loopbackClient = new HttpClient();
                using var callback = await loopbackClient.GetAsync(location, CancellationToken.None).ConfigureAwait(false);

                LoopbackStatusCode = callback.StatusCode;
            }
            catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
            {
                LoopbackStatusCode = null;
            }
        }

        #endregion

    }

}
