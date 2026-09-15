// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Latchkey;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.OData.Mcp.Authentication.Outbound;
using Microsoft.OData.Mcp.Tests.Authentication.Outbound;
using Microsoft.OData.Mcp.Tests.Shared.Authentication;
using Microsoft.OData.Mcp.Tools.Hosting;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ModelContextProtocol.Server;
using SdkAuth = ModelContextProtocol.Authentication;

namespace Microsoft.OData.Mcp.Tests.Authentication.Hosting
{

    /// <summary>
    /// Exercises <see cref="ToolsMcpHost.CreateAsync"/> against a real OAuth protected OData service: the whole
    /// first-start path, the restart path that must cost nothing, and the escape hatch that skips OAuth
    /// entirely.
    /// </summary>
    /// <remarks>
    /// The two named clients are re-pointed at in-process test servers through the <c>configureServices</c>
    /// hook, which is exactly the seam the production host exposes; nothing else about the host is changed, so
    /// these tests exercise the same registrations <c>odata-mcp start</c> builds.
    /// </remarks>
    [TestClass]
    public class ToolsMcpHostOutboundTests
    {

        #region Public Methods

        /// <summary>
        /// The authorization code grant runs end to end through the production host: a real loopback listener,
        /// a real redirect over <c>127.0.0.1</c>, and a catalog that reaches the protected entity set.
        /// </summary>
        [TestMethod]
        public async Task CreateAsync_AuthorizationCodeGrant_RunsLoopbackFlowAndServesCatalog()
        {
            using var authorizationServer = new LocalAuthorizationServer(new LocalAuthorizationServerOptions());
            using var resource = new SecuredStaticResourceServer(authorizationServer, SecuredStaticResourceServer.DefaultCsdl);
            var presenter = new AutoApproveConsentPresenter(authorizationServer);
            var directory = CreateTemporaryDirectory();
            var options = CreateOptions(presenter, directory);
            options.Grant = OutboundGrantKind.AuthorizationCode;

            try
            {
                using var host = await ToolsMcpHost.CreateAsync(
                    resource.ServiceRoot.ToString(),
                    options,
                    includeStdioMcp: false,
                    verbose: false,
                    lifetime: null,
                    CancellationToken.None,
                    Configure(authorizationServer, resource));

                await presenter.LoopbackCompletion!;

                host.Catalog.Tools.Select(tool => tool.Name).Should().Contain("odata_query");
                presenter.Presentations.Should().Be(1);
                presenter.LastRequest!.Kind.Should().Be(OutboundGrantKind.AuthorizationCode);
                authorizationServer.HitCount("authorize").Should().Be(1);
                authorizationServer.HitCount("devicecode").Should().Be(0);
                authorizationServer.HitCount($"token:{ODataMcpAuthConstants.GrantTypeAuthorizationCode}").Should().Be(1);
            }
            finally
            {
                Directory.Delete(directory, recursive: true);
            }
        }

        /// <summary>
        /// A daemon start authenticates as itself and never presents anything to a human.
        /// </summary>
        [TestMethod]
        public async Task CreateAsync_ClientCredentialsGrant_AcquiresWithoutPrompting()
        {
            using var authorizationServer = new LocalAuthorizationServer(new LocalAuthorizationServerOptions());
            using var resource = new SecuredStaticResourceServer(authorizationServer, SecuredStaticResourceServer.DefaultCsdl);
            var presenter = new AutoApproveConsentPresenter(authorizationServer);
            var directory = CreateTemporaryDirectory();

            try
            {
                var options = CreateOptions(presenter, directory);
                options.ClientId = "daemon";
                options.ClientSecret = "daemon-secret";
                options.Grant = OutboundGrantKind.ClientCredentials;

                using var host = await ToolsMcpHost.CreateAsync(
                    resource.ServiceRoot.ToString(),
                    options,
                    includeStdioMcp: false,
                    verbose: false,
                    lifetime: null,
                    CancellationToken.None,
                    Configure(authorizationServer, resource));

                host.Catalog.Tools.Select(tool => tool.Name).Should().Contain("odata_query");
                presenter.Presentations.Should().Be(0);
                authorizationServer.HitCount("devicecode").Should().Be(0);
                authorizationServer.HitCount($"token:{ODataMcpAuthConstants.GrantTypeClientCredentials}").Should().Be(1);
            }
            finally
            {
                Directory.Delete(directory, recursive: true);
            }
        }

        /// <summary>
        /// A pasted bearer token skips discovery, the device code grant, and the token endpoint: the host talks
        /// straight to the resource with the operator's credential.
        /// </summary>
        [TestMethod]
        public async Task CreateAsync_ExplicitAuthToken_ContactsNoAuthorizationServer()
        {
            using var authorizationServer = new LocalAuthorizationServer(new LocalAuthorizationServerOptions());
            using var resource = new SecuredStaticResourceServer(authorizationServer, SecuredStaticResourceServer.DefaultCsdl);
            var options = new OutboundOAuthOptions
            {
                AuthToken = authorizationServer.IssueAccessToken("read")
            };

            using var host = await ToolsMcpHost.CreateAsync(
                resource.ServiceRoot.ToString(),
                options,
                includeStdioMcp: false,
                verbose: false,
                lifetime: null,
                CancellationToken.None,
                Configure(authorizationServer, resource));

            host.Catalog.Tools.Select(tool => tool.Name).Should().Contain("odata_query");
            authorizationServer.HitCount("prm").Should().Be(0);
            authorizationServer.HitCount("devicecode").Should().Be(0);
            authorizationServer.HitCount("token").Should().Be(0);
            resource.HitCount("metadata").Should().Be(1);
        }

        /// <summary>
        /// The stdio host registers the MCP server before <c>Build()</c> and fills the session holder after
        /// <c>$metadata</c>, so the catalog handlers resolve a session without a second service provider.
        /// </summary>
        [TestMethod]
        public async Task CreateAsync_IncludeStdioMcp_RegistersServerAndFillsHolder()
        {
            using var authorizationServer = new LocalAuthorizationServer(new LocalAuthorizationServerOptions());
            using var resource = new SecuredStaticResourceServer(authorizationServer, SecuredStaticResourceServer.DefaultCsdl);
            using var lifetime = new CancellationTokenSource();
            var presenter = new AutoApproveConsentPresenter(authorizationServer);
            var directory = CreateTemporaryDirectory();

            try
            {
                using var host = await ToolsMcpHost.CreateAsync(
                    resource.ServiceRoot.ToString(),
                    CreateOptions(presenter, directory),
                    includeStdioMcp: true,
                    verbose: false,
                    lifetime,
                    CancellationToken.None,
                    Configure(authorizationServer, resource));

                host.Host.Services.GetServices<IHostedService>().Should().NotBeEmpty();
                host.Host.Services.GetService<IOptions<McpServerOptions>>()!.Value.Handlers.CallToolHandler.Should().NotBeNull();
                host.Host.Services.GetService<ShutdownServerTool>().Should().NotBeNull();
                host.Host.Services.GetRequiredService<ToolsMcpSessionHolder>().Session.Should().BeSameAs(host.Session);
            }
            finally
            {
                Directory.Delete(directory, recursive: true);
            }
        }

        /// <summary>
        /// A first start with an empty cache runs the whole device code grant, then serves a working catalog
        /// whose tools reach the protected entity set.
        /// </summary>
        [TestMethod]
        public async Task CreateAsync_ProtectedService_RunsDeviceFlowAndServesCatalog()
        {
            using var authorizationServer = new LocalAuthorizationServer(new LocalAuthorizationServerOptions());
            using var resource = new SecuredStaticResourceServer(authorizationServer, SecuredStaticResourceServer.DefaultCsdl);
            var presenter = new AutoApproveConsentPresenter(authorizationServer);
            var directory = CreateTemporaryDirectory();

            try
            {
                using var host = await ToolsMcpHost.CreateAsync(
                    resource.ServiceRoot.ToString(),
                    CreateOptions(presenter, directory),
                    includeStdioMcp: false,
                    verbose: false,
                    lifetime: null,
                    CancellationToken.None,
                    Configure(authorizationServer, resource));

                var result = await host.Session.Runtime.InvokeAsync(
                    "odata_query",
                    new Dictionary<string, JsonElement>
                    {
                        ["entitySet"] = JsonSerializer.SerializeToElement("Customers")
                    },
                    CancellationToken.None);

                host.Catalog.Tools.Select(tool => tool.Name).Should().Contain("odata_query");
                host.ServiceRoot.Should().Be(resource.ServiceRoot);
                presenter.Presentations.Should().Be(1);
                authorizationServer.HitCount("devicecode").Should().Be(1);
                result.IsError.Should().BeFalse(result.Text);
                result.StructuredContent.Should().Contain("Contoso");
            }
            finally
            {
                Directory.Delete(directory, recursive: true);
            }
        }

        /// <summary>
        /// A restart against the same service root and client id reuses the persisted token: no second device
        /// code grant and not one extra token endpoint request.
        /// </summary>
        [TestMethod]
        public async Task CreateAsync_Restart_ReusesCachedTokenWithNoTokenTraffic()
        {
            using var authorizationServer = new LocalAuthorizationServer(new LocalAuthorizationServerOptions());
            using var resource = new SecuredStaticResourceServer(authorizationServer, SecuredStaticResourceServer.DefaultCsdl);
            var presenter = new AutoApproveConsentPresenter(authorizationServer);
            var directory = CreateTemporaryDirectory();

            try
            {
                using (var first = await ToolsMcpHost.CreateAsync(
                    resource.ServiceRoot.ToString(),
                    CreateOptions(presenter, directory),
                    includeStdioMcp: false,
                    verbose: false,
                    lifetime: null,
                    CancellationToken.None,
                    Configure(authorizationServer, resource)))
                {
                    first.Catalog.Tools.Should().NotBeEmpty();
                }

                var tokenHitsAfterFirstStart = authorizationServer.HitCount("token");
                var metadataHitsAfterFirstStart = resource.HitCount("metadata");

                using var second = await ToolsMcpHost.CreateAsync(
                    resource.ServiceRoot.ToString(),
                    CreateOptions(presenter, directory),
                    includeStdioMcp: false,
                    verbose: false,
                    lifetime: null,
                    CancellationToken.None,
                    Configure(authorizationServer, resource));

                second.Catalog.Tools.Should().NotBeEmpty();
                authorizationServer.HitCount("token").Should().Be(tokenHitsAfterFirstStart);
                authorizationServer.HitCount("devicecode").Should().Be(1);
                presenter.Presentations.Should().Be(1);
                resource.HitCount("metadata").Should().Be(metadataHitsAfterFirstStart + 1);
            }
            finally
            {
                Directory.Delete(directory, recursive: true);
            }
        }

        /// <summary>
        /// A service that answers <c>$metadata</c> without a challenge never runs a grant, so the credential
        /// store is never touched: the token cache directory the operator named is not even created.
        /// </summary>
        [TestMethod]
        public async Task CreateAsync_UnsecuredService_TouchesNoCredentialStore()
        {
            using var authorizationServer = new LocalAuthorizationServer(new LocalAuthorizationServerOptions());
            using var resource = new SecuredStaticResourceServer(authorizationServer, SecuredStaticResourceServer.DefaultCsdl, requireAuthentication: false);
            var directory = Path.Combine(Path.GetTempPath(), $"odata-mcp-untouched-{Guid.NewGuid():N}");
            var options = new OutboundOAuthOptions
            {
                Scopes = ["read"],
                TokenCachePath = directory
            };

            try
            {
                using var host = await ToolsMcpHost.CreateAsync(
                    resource.ServiceRoot.ToString(),
                    options,
                    includeStdioMcp: false,
                    verbose: false,
                    lifetime: null,
                    CancellationToken.None,
                    Configure(authorizationServer, resource));

                host.Catalog.Tools.Select(tool => tool.Name).Should().Contain("odata_query");
                Directory.Exists(directory).Should().BeFalse();
                authorizationServer.HitCount("prm").Should().Be(0);
                authorizationServer.HitCount("devicecode").Should().Be(0);
                resource.HitCount("metadata").Should().Be(1);
            }
            finally
            {
                if (Directory.Exists(directory))
                {
                    Directory.Delete(directory, recursive: true);
                }
            }
        }

        /// <summary>
        /// A credential store that became unusable after the cache over it was built still fails an
        /// acquisition, and fails it with the actionable message rather than a raw Latchkey exception: an
        /// unusable keyring is tolerated on the read path only, never on the path that has to persist a token.
        /// </summary>
        [TestMethod]
        public async Task CreateAsync_UnusableCredentialStoreOnProtectedService_FailsWithRemediation()
        {
            using var authorizationServer = new LocalAuthorizationServer(new LocalAuthorizationServerOptions());
            using var resource = new SecuredStaticResourceServer(authorizationServer, SecuredStaticResourceServer.DefaultCsdl);
            var presenter = new AutoApproveConsentPresenter(authorizationServer);
            var (unusablePath, blockingFile) = CreateUnusableTokenCachePath();
            var options = CreateOptions(presenter, unusablePath);

            try
            {
                Func<Task> act = () => ToolsMcpHost.CreateAsync(
                    resource.ServiceRoot.ToString(),
                    options,
                    includeStdioMcp: false,
                    verbose: false,
                    lifetime: null,
                    CancellationToken.None,
                    ConfigureWithUnavailableCredentialStore(authorizationServer, resource, options));

                await act.Should().ThrowAsync<InvalidOperationException>()
                    .WithMessage("Credential store is not usable on this host; unlock the OS keyring or pass --token-cache.");
                presenter.Presentations.Should().Be(0);
                authorizationServer.HitCount("devicecode").Should().Be(0);
            }
            finally
            {
                File.Delete(blockingFile);
            }
        }

        /// <summary>
        /// A service that answers <c>$metadata</c> without a challenge still serves a working catalog on a host
        /// whose credential store cannot be read at all: every OData send reads the token cache, so an unusable
        /// keyring must not break the path that needs no credential in the first place.
        /// </summary>
        [TestMethod]
        public async Task CreateAsync_UnusableCredentialStoreOnUnsecuredService_ServesCatalog()
        {
            using var authorizationServer = new LocalAuthorizationServer(new LocalAuthorizationServerOptions());
            using var resource = new SecuredStaticResourceServer(authorizationServer, SecuredStaticResourceServer.DefaultCsdl, requireAuthentication: false);
            var directory = CreateTemporaryDirectory();
            var options = new OutboundOAuthOptions
            {
                ClientId = "cli",
                Scopes = ["read"],
                TokenCachePath = directory
            };

            try
            {
                using var host = await ToolsMcpHost.CreateAsync(
                    resource.ServiceRoot.ToString(),
                    options,
                    includeStdioMcp: false,
                    verbose: false,
                    lifetime: null,
                    CancellationToken.None,
                    ConfigureWithUnavailableCredentialStore(authorizationServer, resource, options));

                var result = await host.Session.Runtime.InvokeAsync(
                    "odata_query",
                    new Dictionary<string, JsonElement>
                    {
                        ["entitySet"] = JsonSerializer.SerializeToElement("Customers")
                    },
                    CancellationToken.None);

                host.Catalog.Tools.Select(tool => tool.Name).Should().Contain("odata_query");
                result.IsError.Should().BeFalse(result.Text);
                result.StructuredContent.Should().Contain("Contoso");
                authorizationServer.HitCount("prm").Should().Be(0);
                authorizationServer.HitCount("devicecode").Should().Be(0);
            }
            finally
            {
                Directory.Delete(directory, recursive: true);
            }
        }

        #endregion

        #region Internal Methods

        /// <summary>
        /// Builds the <c>configureServices</c> hook that re-points both named clients at the in-process test
        /// servers, leaving every other registration exactly as the production host made it.
        /// </summary>
        /// <param name="authorizationServer">The authorization server the <c>"OAuth"</c> client dispatches into.</param>
        /// <param name="resource">The protected OData service the <c>"OData"</c> client dispatches into.</param>
        /// <returns>
        /// The hook.
        /// </returns>
        internal static Action<IServiceCollection> Configure(LocalAuthorizationServer authorizationServer, SecuredStaticResourceServer resource)
        {
            return services =>
            {
                services.AddHttpClient(ODataMcpAuthConstants.ODataHttpClientName)
                    .ConfigurePrimaryHttpMessageHandler(() => resource.Server.CreateHandler());
                services.AddHttpClient(ODataMcpAuthConstants.OAuthHttpClientName)
                    .ConfigurePrimaryHttpMessageHandler(() => authorizationServer.Handler);
            };
        }

        /// <summary>
        /// Builds a <c>configureServices</c> hook that re-points both named clients and additionally replaces
        /// the token cache with a real <see cref="LatchkeyTokenCache"/> over a credential store that fails
        /// every read and write.
        /// </summary>
        /// <param name="authorizationServer">The authorization server the <c>"OAuth"</c> client dispatches into.</param>
        /// <param name="resource">The OData service the <c>"OData"</c> client dispatches into.</param>
        /// <param name="options">The options whose client id the cache key is derived from, matching what the host itself would use.</param>
        /// <returns>
        /// The hook.
        /// </returns>
        /// <remarks>
        /// <see cref="UnavailableSecretBackend"/> is a hand-written backend, not a mock: it reproduces a
        /// Secret Service keyring that locked, or a cache directory that lost its permissions, after the cache
        /// over it was constructed.
        /// </remarks>
        internal static Action<IServiceCollection> ConfigureWithUnavailableCredentialStore(
            LocalAuthorizationServer authorizationServer,
            SecuredStaticResourceServer resource,
            OutboundOAuthOptions options)
        {
            var configure = Configure(authorizationServer, resource);

            return services =>
            {
                configure(services);

                var store = LatchkeyFactory.Create(new LatchkeyOptions
                {
                    ServiceName = ODataMcpAuthConstants.LatchkeyServiceName,
                    CustomBackend = new UnavailableSecretBackend()
                });

                services.AddSingleton<SdkAuth.ITokenCache>(provider => new LatchkeyTokenCache(
                    store,
                    LatchkeyTokenCache.ComputeKey(resource.ServiceRoot, options.ClientId ?? "anonymous"),
                    provider.GetRequiredService<ILogger<LatchkeyTokenCache>>()));
            };
        }

        /// <summary>
        /// Builds the options a first start and its restart share: the fixture's seeded public client, an
        /// auto-approving presenter, and a throwaway on-disk token cache.
        /// </summary>
        /// <param name="presenter">The presenter that stands in for a human approving the device code grant.</param>
        /// <param name="tokenCachePath">The directory the token cache persists into.</param>
        /// <returns>
        /// The options.
        /// </returns>
        internal static OutboundOAuthOptions CreateOptions(AutoApproveConsentPresenter presenter, string tokenCachePath)
        {
            return new OutboundOAuthOptions
            {
                ClientId = "cli",
                ConsentPresenter = presenter.PresentAsync,
                Scopes = ["read"],
                TokenCachePath = tokenCachePath
            };
        }

        /// <summary>
        /// Creates a throwaway directory under the OS temp path for the token cache.
        /// </summary>
        /// <returns>
        /// The full path of the newly created directory.
        /// </returns>
        internal static string CreateTemporaryDirectory()
        {
            var directory = Path.Combine(Path.GetTempPath(), $"odata-mcp-host-{Guid.NewGuid():N}");
            Directory.CreateDirectory(directory);

            return directory;
        }

        /// <summary>
        /// Creates a token cache path no backend can round-trip a value through, so
        /// <c>OutboundOAuthClient.EnsureCredentialStoreAsync</c> refuses to run a grant.
        /// </summary>
        /// <returns>
        /// The unusable directory path, and the throwaway file the caller has to delete.
        /// </returns>
        /// <remarks>
        /// The directory is named beneath an existing <em>file</em>, which no operating system will let a
        /// process create a directory under. That reproduces the operator-visible condition — a cache location
        /// that cannot hold a token — on every platform this suite runs on, without depending on a drive letter
        /// or a permissions model.
        /// </remarks>
        internal static (string Path, string BlockingFile) CreateUnusableTokenCachePath()
        {
            var blockingFile = Path.Combine(Path.GetTempPath(), $"odata-mcp-unusable-{Guid.NewGuid():N}");
            File.WriteAllText(blockingFile, string.Empty);

            return (Path.Combine(blockingFile, "tokens"), blockingFile);
        }

        #endregion

    }

}
