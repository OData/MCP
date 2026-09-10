// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.OData.Mcp.Authentication.Outbound;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.OData.Mcp.Tests.Authentication.Outbound
{

    /// <summary>
    /// Exercises <see cref="LoopbackAuthorizationCallback"/> over a real <c>127.0.0.1</c> socket: a real
    /// <see cref="HttpListener"/> accepting a real <see cref="HttpClient"/> request, with no mocking.
    /// </summary>
    /// <remarks>
    /// Every test binds an ephemeral port and disposes the listener, so the suite can run concurrently with
    /// anything else on the machine. The authorization response is issued by hand rather than by an
    /// authorization server, because the point here is what the callback accepts and refuses.
    /// </remarks>
    [TestClass]
    public class LoopbackAuthorizationCallbackTests
    {

        #region Public Methods

        /// <summary>
        /// With no override the callback binds the literal loopback address — never the <c>localhost</c> DNS
        /// name — on an ephemeral port under <c>/callback/</c>.
        /// </summary>
        [TestMethod]
        public void Start_NoOverride_BindsEphemeralLoopbackCallback()
        {
            using var callback = LoopbackAuthorizationCallback.Start(null);

            callback.RedirectUri.Host.Should().Be("127.0.0.1");
            callback.RedirectUri.Scheme.Should().Be(Uri.UriSchemeHttp);
            callback.RedirectUri.AbsolutePath.Should().Be("/callback/");
            callback.RedirectUri.Port.Should().BeGreaterThan(0);
            callback.RedirectUri.IsLoopback.Should().BeTrue();
        }

        /// <summary>
        /// A <c>--redirect-uri</c> that is not <c>http</c> on a loopback host is refused with the RFC 8252
        /// section 7.3 policy named in the message: nothing here can listen on a public <c>https</c> address,
        /// and pretending otherwise would strand the operator waiting on a redirect that never arrives.
        /// </summary>
        [TestMethod]
        public void Start_NonLoopbackRedirectOverride_ThrowsNamingThePolicy()
        {
            var act = () => LoopbackAuthorizationCallback.Start(new Uri("https://app.example/cb"));

            act.Should().Throw<OutboundDiscoveryException>()
                .WithMessage("The loopback redirect URI must use http on a loopback host (127.0.0.1, localhost, or ::1)*");
        }

        /// <summary>
        /// An operator's <c>--redirect-uri</c> is used verbatim, because it has to match what their
        /// authorization server registration says.
        /// </summary>
        [TestMethod]
        public void Start_RedirectOverride_UsesItVerbatim()
        {
            var expected = new Uri($"http://127.0.0.1:{FindFreePort()}/oauth-callback/");

            using var callback = LoopbackAuthorizationCallback.Start(expected);

            callback.RedirectUri.Should().Be(expected);
        }

        /// <summary>
        /// A response whose <c>state</c> matches yields the code and the RFC 9207 issuer, answers the browser
        /// with a page, and closes the socket.
        /// </summary>
        [TestMethod]
        public async Task WaitAsync_StateMatches_ReturnsCodeStateAndIssAndDisposes()
        {
            var callback = LoopbackAuthorizationCallback.Start(null);
            var wait = callback.WaitAsync("expected-state", CancellationToken.None);

            using var client = new HttpClient();
            using var response = await client.GetAsync(
                new Uri($"{callback.RedirectUri}?code=the-code&state=expected-state&iss={Uri.EscapeDataString("http://localhost/oauth/v2.0")}"),
                CancellationToken.None);

            var result = await wait;

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            result.Code.Should().Be("the-code");
            result.State.Should().Be("expected-state");
            result.Iss.Should().Be("http://localhost/oauth/v2.0");
            callback._disposed.Should().BeTrue();
        }

        /// <summary>
        /// A response whose <c>state</c> is not an exact match — the loopback CSRF case — is refused with a
        /// <c>400</c>, reported to the caller, and the listener is torn down rather than left for a second
        /// attempt.
        /// </summary>
        [TestMethod]
        public async Task WaitAsync_StateMismatch_ThrowsAndDisposesListener()
        {
            var callback = LoopbackAuthorizationCallback.Start(null);
            var wait = callback.WaitAsync("expected-state", CancellationToken.None);

            using var client = new HttpClient();
            using var response = await client.GetAsync(
                new Uri($"{callback.RedirectUri}?code=the-code&state=forged-state"),
                CancellationToken.None);

            Func<Task> act = () => wait;

            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            await act.Should().ThrowAsync<OutboundDiscoveryException>().WithMessage("Authorization response state mismatch.");
            callback._disposed.Should().BeTrue();
        }

        #endregion

        #region Internal Methods

        /// <summary>
        /// Asks the operating system for a free loopback TCP port a redirect override can claim.
        /// </summary>
        /// <returns>
        /// The port number.
        /// </returns>
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

        #endregion

    }

}
