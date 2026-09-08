// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipelines;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.OData.Mcp.Authentication.Outbound;
using Microsoft.OData.Mcp.Tests.Shared.Authentication;
using Microsoft.OData.Mcp.Tools.Hosting;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ModelContextProtocol;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace Microsoft.OData.Mcp.Tests.Authentication.Hosting
{

    /// <summary>
    /// Exercises the mid-session sign-in path of a stdio Tools host end to end: a real
    /// <see cref="McpClient"/> speaking JSON-RPC over in-process pipes to the very
    /// <see cref="McpServerOptions"/> <c>odata-mcp start</c> registers, against a real OAuth protected OData
    /// service whose session the authorization server has torn down.
    /// </summary>
    /// <remarks>
    /// Nothing here calls the host's recovery callback directly — it is not even visible from this assembly.
    /// The only surface these tests touch is <c>tools/call</c>, which is what an LLM host touches, so the
    /// elicitation, the grant, and the retry all have to work through the real protocol or the test fails.
    /// </remarks>
    [TestClass]
    public class ToolsMcpHostElicitationTests
    {

        #region Public Methods

        /// <summary>
        /// A tool call whose still-cached token the resource has stopped accepting, against an authorization
        /// server that refuses to refresh it, elicits the sign-in URL in <c>url</c> mode in that same call,
        /// completes the device code grant the client approves, and answers with the retried tool's real
        /// result — never with a token.
        /// </summary>
        /// <remarks>
        /// Nothing here ages the cached token first: the send path's one refresh attempt fails, so the handler
        /// falls through to acquisition inside the same request, which is what makes the mid-session sign-in a
        /// single tool call rather than two.
        /// </remarks>
        [TestMethod]
        public async Task CallTool_RefreshRejectedMidSession_ElicitsUrlAndRetriesTool()
        {
            var authorizationServerOptions = new LocalAuthorizationServerOptions();
            using var authorizationServer = new LocalAuthorizationServer(authorizationServerOptions);
            using var resource = new SecuredStaticResourceServer(authorizationServer, SecuredStaticResourceServer.DefaultCsdl);
            var presenter = new AutoApproveConsentPresenter(authorizationServer);
            var directory = ToolsMcpHostOutboundTests.CreateTemporaryDirectory();
            using var lifetime = new CancellationTokenSource();

            try
            {
                var options = ToolsMcpHostOutboundTests.CreateOptions(presenter, directory);
                options.Scopes = ["read", ODataMcpAuthConstants.OfflineAccessScope];

                using var host = await ToolsMcpHost.CreateAsync(
                    resource.ServiceRoot.ToString(),
                    options,
                    includeStdioMcp: true,
                    verbose: false,
                    lifetime,
                    CancellationToken.None,
                    ToolsMcpHostOutboundTests.Configure(authorizationServer, resource));

                options.ConsentPresenter.Should().BeNull();
                authorizationServer.HitCount("devicecode").Should().Be(1);

                var clientToServer = new Pipe();
                var serverToClient = new Pipe();

                await using var serverTransport = new StreamServerTransport(
                    clientToServer.Reader.AsStream(),
                    serverToClient.Writer.AsStream(),
                    "odata-mcp-elicitation-test",
                    NullLoggerFactory.Instance);
                await using var server = McpServer.Create(
                    serverTransport,
                    host.Host.Services.GetRequiredService<IOptions<McpServerOptions>>().Value,
                    NullLoggerFactory.Instance,
                    host.Host.Services);

                var serving = server.RunAsync(lifetime.Token);
                var recorded = new List<ElicitRequestParams>();

                await using var client = await McpClient.CreateAsync(
                    new StreamClientTransport(clientToServer.Writer.AsStream(), serverToClient.Reader.AsStream(), NullLoggerFactory.Instance),
                    new McpClientOptions
                    {
                        Capabilities = new ClientCapabilities
                        {
                            Elicitation = new ElicitationCapability { Url = new UrlElicitationCapability() }
                        },
                        Handlers = new McpClientHandlers
                        {
                            ElicitationHandler = (parameters, cancellationToken) => ApproveAsync(authorizationServer, recorded, parameters, cancellationToken)
                        }
                    },
                    NullLoggerFactory.Instance,
                    CancellationToken.None);

                authorizationServerOptions.RejectRefresh = true;
                authorizationServer.RevokeIssuedTokens();

                var result = await client.CallToolAsync(
                    "odata_query",
                    new Dictionary<string, object?> { ["entitySet"] = "Customers" },
                    cancellationToken: CancellationToken.None);

                result.IsError.Should().BeFalse();
                result.Content.OfType<TextContentBlock>().Single().Text.Should().Contain("200");
                JsonSerializer.Serialize(result, McpJsonUtilities.DefaultOptions).Should().Contain("Contoso");

                recorded.Should().ContainSingle();
                recorded[0].Mode.Should().Be("url");
                recorded[0].Url.Should().NotBeNullOrWhiteSpace();
                Guid.TryParseExact(recorded[0].ElicitationId, "D", out _).Should().BeTrue();

                var serialized = JsonSerializer.Serialize(recorded[0], McpJsonUtilities.DefaultOptions);

                serialized.Should().NotContain("device_code");
                serialized.Should().NotContain("access_token");
                serialized.Should().NotContain("refresh_token");

                authorizationServer.HitCount("devicecode").Should().Be(2);
                presenter.Presentations.Should().Be(1);

                await lifetime.CancelAsync();
                await ObserveAsync(serving);
            }
            finally
            {
                Directory.Delete(directory, recursive: true);
            }
        }

        #endregion

        #region Internal Methods

        /// <summary>
        /// Stands in for a human who opens the elicited URL: it records the request and fetches the URL against
        /// the authorization server, which is what approves the pending device code grant.
        /// </summary>
        /// <param name="authorizationServer">The authorization server the approval GET is dispatched through.</param>
        /// <param name="recorded">The list every elicitation request is appended to.</param>
        /// <param name="parameters">The elicitation request the server sent.</param>
        /// <param name="cancellationToken">The token that cancels the approval GET.</param>
        /// <returns>
        /// An accepted result once the authorization server has recorded the approval.
        /// </returns>
        /// <remarks>
        /// Deliberately the whole of what a URL-mode client is allowed to do: open the URL. It never sees a
        /// device code, so it could not complete the grant itself even if it tried — that is the point of
        /// <c>url</c> mode.
        /// </remarks>
        internal static async ValueTask<ElicitResult> ApproveAsync(
            LocalAuthorizationServer authorizationServer,
            List<ElicitRequestParams> recorded,
            ElicitRequestParams? parameters,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(authorizationServer);
            ArgumentNullException.ThrowIfNull(recorded);
            ArgumentNullException.ThrowIfNull(parameters);

            recorded.Add(parameters);

            using var client = new HttpClient(authorizationServer.Handler, disposeHandler: false);
            using var response = await client.GetAsync(new Uri(parameters.Url!, UriKind.Absolute), cancellationToken).ConfigureAwait(false);

            response.EnsureSuccessStatusCode();

            return new ElicitResult { Action = "accept" };
        }

        /// <summary>
        /// Awaits the server loop purely to observe how it ended, so a cancelled run never surfaces as an
        /// unobserved task exception in an unrelated test.
        /// </summary>
        /// <param name="task">The server loop to observe.</param>
        /// <returns>
        /// A task that completes once <paramref name="task"/> has settled.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="task"/> is <see langword="null"/>.</exception>
        internal static async Task ObserveAsync(Task task)
        {
            ArgumentNullException.ThrowIfNull(task);

            try
            {
                await task.ConfigureAwait(false);
            }
            catch (Exception exception) when (exception is OperationCanceledException or IOException or ObjectDisposedException)
            {
                // Cancelling the lifetime is how this loop is meant to end.
            }
        }

        #endregion

    }

}
