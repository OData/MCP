// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using McMaster.Extensions.CommandLineUtils;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.OData.Mcp.Authentication.Outbound;
using Microsoft.OData.Mcp.Tests.Shared;
using Microsoft.OData.Mcp.Tools.Commands;
using Microsoft.OData.Mcp.Tools.Hosting;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace Microsoft.OData.Mcp.Tests.Tools
{

    /// <summary>
    /// Tests for start-command internals, host construction, and shutdown handling.
    /// </summary>
    [TestClass]
    public class StartCommandTests
    {

        #region Public Methods

        /// <summary>
        /// Missing URLs fail before a host is built.
        /// </summary>
        [TestMethod]
        public async Task OnExecuteAsync_MissingUrl_ReturnsOne()
        {
            var exit = await new StartCommand().OnExecuteAsync();

            exit.Should().Be(1);
        }

        /// <summary>
        /// Invalid URLs fail after the host lifetime is created.
        /// </summary>
        [TestMethod]
        public async Task OnExecuteAsync_InvalidUrl_ReturnsOne()
        {
            var exit = await new StartCommand
            {
                Url = "not-a-url",
                Verbose = true
            }.OnExecuteAsync();

            exit.Should().Be(1);
        }

        /// <summary>
        /// Invalid URLs fail with a non-zero exit.
        /// </summary>
        [TestMethod]
        public async Task ExecuteAsync_InvalidUrl_ReturnsOne()
        {
            using var lifetime = new CancellationTokenSource();
            var exit = await new StartCommand
            {
                Url = "not-a-url"
            }.ExecuteAsync(lifetime);

            exit.Should().Be(1);
        }

        /// <summary>
        /// Verbose failures print the exception.
        /// </summary>
        [TestMethod]
        public async Task ExecuteAsync_InvalidUrlVerbose_ReturnsOne()
        {
            using var lifetime = new CancellationTokenSource();
            var exit = await new StartCommand
            {
                Url = "ftp://example.com",
                Verbose = true
            }.ExecuteAsync(lifetime);

            exit.Should().Be(1);
        }

        /// <summary>
        /// A cancelled lifetime during metadata fetch is a clean shutdown.
        /// </summary>
        [TestMethod]
        public async Task ExecuteAsync_CancelledLifetime_ReturnsZero()
        {
            using var lifetime = new CancellationTokenSource();
            lifetime.Cancel();
            var exit = await new StartCommand
            {
                Url = LiveOData.Northwind
            }.ExecuteAsync(lifetime);

            exit.Should().Be(0);
        }

        /// <summary>
        /// BuildHostAsync constructs a stdio host from live Northwind metadata, wired to the authenticating
        /// <c>"OData"</c> client rather than to a bearer token pasted onto three separate clients.
        /// </summary>
        [TestMethod]
        public async Task BuildHostAsync_Northwind_ConstructsHost()
        {
            using var lifetime = new CancellationTokenSource();
            var command = new StartCommand
            {
                AuthToken = "token",
                Url = LiveOData.Northwind,
                Verbose = true
            };
            using var toolsHost = await command.BuildHostAsync(lifetime);
            var client = toolsHost.Host.Services
                .GetRequiredService<System.Net.Http.IHttpClientFactory>()
                .CreateClient(ODataMcpAuthConstants.ODataHttpClientName);

            toolsHost.Host.Should().NotBeNull();
            toolsHost.Catalog.Tools.Should().NotBeEmpty();
            client.BaseAddress.Should().NotBeNull();
            client.DefaultRequestHeaders.Authorization.Should().BeNull();
        }

        /// <summary>
        /// RunHostAsync returns zero when the token is already cancelled.
        /// </summary>
        [TestMethod]
        public async Task RunHostAsync_Cancelled_ReturnsZero()
        {
            using var lifetime = new CancellationTokenSource();
            var command = new StartCommand
            {
                Url = LiveOData.Northwind
            };
            using var toolsHost = await command.BuildHostAsync(lifetime);
            lifetime.Cancel();

            var exit = await StartCommand.RunHostAsync(toolsHost.Host, lifetime.Token);

            exit.Should().Be(0);
        }

        /// <summary>
        /// A Tools host executes odata_query against live Northwind.
        /// </summary>
        [TestMethod]
        public async Task ToolsMcpHost_QueryProducts_ReturnsJson()
        {
            using var host = await ToolsMcpHost.CreateAsync(LiveOData.Northwind, new OutboundOAuthOptions(), includeStdioMcp: false, verbose: false, lifetime: null, CancellationToken.None);
            var result = await host.Session.Runtime.InvokeAsync(
                "odata_query",
                new Dictionary<string, JsonElement>
                {
                    ["entitySet"] = JsonSerializer.SerializeToElement("Products"),
                    ["top"] = JsonSerializer.SerializeToElement(1)
                },
                CancellationToken.None);

            result.IsError.Should().BeFalse(result.Text);
            result.StructuredContent.Should().Contain("Product");
        }

        /// <summary>
        /// Empty and non-HTTP URLs throw from the host factory.
        /// </summary>
        [TestMethod]
        public async Task CreateAsync_InvalidUrls_Throw()
        {
            var empty = async () => await ToolsMcpHost.CreateAsync(" ", new OutboundOAuthOptions(), includeStdioMcp: false, verbose: false, lifetime: null, CancellationToken.None);
            var relative = async () => await ToolsMcpHost.CreateAsync("not-a-url", new OutboundOAuthOptions(), includeStdioMcp: false, verbose: false, lifetime: null, CancellationToken.None);
            var scheme = async () => await ToolsMcpHost.CreateAsync("ftp://example.com", new OutboundOAuthOptions(), includeStdioMcp: false, verbose: false, lifetime: null, CancellationToken.None);

            await empty.Should().ThrowAsync<ArgumentException>();
            await relative.Should().ThrowAsync<ArgumentException>();
            await scheme.Should().ThrowAsync<ArgumentException>();
        }

        /// <summary>
        /// ReadString returns the raw text for non-string JSON.
        /// </summary>
        [TestMethod]
        public void ReadString_NonString_ReturnsRawText()
        {
            var arguments = new Dictionary<string, JsonElement>
            {
                ["reason"] = JsonSerializer.SerializeToElement(42),
                ["empty"] = JsonSerializer.SerializeToElement((string?)null)
            };

            ToolsMcpHost.ReadString(arguments, "reason").Should().Be("42");
            ToolsMcpHost.ReadString(arguments, "empty").Should().BeNull();
            ToolsMcpHost.ReadString(arguments, "missing").Should().BeNull();
        }

        /// <summary>
        /// Shutdown delay supplied as a string is parsed.
        /// </summary>
        [TestMethod]
        public async Task HandleShutdown_StringDelay_Cancels()
        {
            using var cts = new CancellationTokenSource();
            var request = UninitializedCall("shutdown_server", new Dictionary<string, JsonElement>
            {
                ["reason"] = JsonSerializer.SerializeToElement("done"),
                ["delay_seconds"] = JsonSerializer.SerializeToElement("0")
            });

            var result = await ToolsMcpHost.HandleShutdownAsync(request, new ShutdownServerTool(cts), CancellationToken.None);

            result.Should().NotBeNull();
            cts.IsCancellationRequested.Should().BeTrue();
        }

        /// <summary>
        /// Non-integer delay values are rejected.
        /// </summary>
        [TestMethod]
        public async Task HandleShutdown_InvalidDelay_Throws()
        {
            using var cts = new CancellationTokenSource();
            var request = UninitializedCall("shutdown_server", new Dictionary<string, JsonElement>
            {
                ["delay_seconds"] = JsonSerializer.SerializeToElement("nope")
            });

            var act = async () => await ToolsMcpHost.HandleShutdownAsync(request, new ShutdownServerTool(cts), CancellationToken.None);

            await act.Should().ThrowAsync<ArgumentException>();
        }

        /// <summary>
        /// Missing params are not treated as shutdown.
        /// </summary>
        [TestMethod]
        public async Task HandleShutdown_NullParams_ReturnsNull()
        {
            using var cts = new CancellationTokenSource();
            var request = UninitializedCall(null, null);

            var result = await ToolsMcpHost.HandleShutdownAsync(request, new ShutdownServerTool(cts), CancellationToken.None);

            result.Should().BeNull();
        }

        /// <summary>
        /// Null constructor arguments throw.
        /// </summary>
        [TestMethod]
        public async Task HandleShutdown_NullArguments_Throw()
        {
            using var cts = new CancellationTokenSource();
            var request = UninitializedCall("shutdown_server", null);

            var nullRequest = async () => await ToolsMcpHost.HandleShutdownAsync(null!, new ShutdownServerTool(cts), CancellationToken.None);
            var nullShutdown = async () => await ToolsMcpHost.HandleShutdownAsync(request, null!, CancellationToken.None);

            await nullRequest.Should().ThrowAsync<ArgumentNullException>();
            await nullShutdown.Should().ThrowAsync<ArgumentNullException>();
        }

        /// <summary>
        /// Negative shutdown delays throw.
        /// </summary>
        [TestMethod]
        public async Task ShutdownServerTool_NegativeDelay_Throws()
        {
            using var cts = new CancellationTokenSource();
            var act = async () => await new ShutdownServerTool(cts).InvokeAsync("x", -1, CancellationToken.None);

            await act.Should().ThrowAsync<ArgumentOutOfRangeException>();
        }

        /// <summary>
        /// A one-second delay cancels after the delay.
        /// </summary>
        [TestMethod]
        public async Task ShutdownServerTool_DelayOne_CancelsAfterWait()
        {
            using var cts = new CancellationTokenSource();
            var payload = await new ShutdownServerTool(cts).InvokeAsync("done", 1, CancellationToken.None);

            payload.Should().Contain("done");
            cts.IsCancellationRequested.Should().BeFalse();
            await Task.Delay(TimeSpan.FromSeconds(1.5));
            cts.IsCancellationRequested.Should().BeTrue();
        }

        /// <summary>
        /// Test command with a bearer token still fetches public Northwind metadata.
        /// </summary>
        [TestMethod]
        public async Task TryCommand_AuthToken_NorthwindReturnsZero()
        {
            var exit = await new TryCommand
            {
                AuthToken = "token",
                Url = LiveOData.Northwind
            }.OnExecuteAsync();

            exit.Should().Be(0);
        }

        /// <summary>
        /// A host built without the stdio transport registers no MCP server at all, so the <c>test</c> and
        /// <c>add</c> paths cannot accidentally claim stdout.
        /// </summary>
        [TestMethod]
        public async Task CreateAsync_NoStdio_NoMcpServer()
        {
            using var host = await ToolsMcpHost.CreateAsync(LiveOData.Northwind, new OutboundOAuthOptions(), includeStdioMcp: false, verbose: false, lifetime: null, CancellationToken.None);
            var client = host.Host.Services
                .GetRequiredService<System.Net.Http.IHttpClientFactory>()
                .CreateClient(ODataMcpAuthConstants.ODataHttpClientName);

            host.Host.Services.GetServices<IHostedService>().Should().BeEmpty();
            host.Host.Services.GetService<ShutdownServerTool>().Should().BeNull();
            client.BaseAddress.Should().Be(host.ServiceRoot);
            client.DefaultRequestHeaders.Accept.Should().BeEmpty();
        }

        /// <summary>
        /// Parsing every outbound OAuth flag produces a fully populated, validated options instance.
        /// </summary>
        [TestMethod]
        public void StartCommand_ParsesAllFlags_BuildsOptions()
        {
            var app = new CommandLineApplication<StartCommand>();
            app.Conventions.UseDefaultConventions();
            app.Parse(
                "https://example.com/odata",
                "--client-id", "cid",
                "--client-secret", "csecret",
                "--scopes", "read write",
                "--auth-server", "https://as.example.com",
                "--resource", "https://resource.example.com",
                "--grant", "device_code",
                "--redirect-uri", "https://127.0.0.1:5000/callback/",
                "--token-cache", "/tmp/cache",
                "--auth-timeout", "45",
                "--api-key", "key",
                "--api-key-header", "X-Api-Key",
                "--basic-user", "user",
                "--basic-password", "pass",
                "--client-metadata-document", "https://cimd.example.com/doc.json",
                "--idp-url", "https://idp.example.com",
                "--idp-token-endpoint", "https://idp.example.com/token",
                "--idp-client-id", "idp-cid",
                "--idp-client-secret", "idp-secret",
                "--idp-scope", "openid",
                "--idp-id-token-file", "/tmp/idtoken.txt");
            var command = app.Model;

            var options = command.BuildOptions();

            options.ClientId.Should().Be("cid");
            options.ClientSecret.Should().Be("csecret");
            options.Scopes.Should().BeEquivalentTo(["read", "write"], o => o.WithStrictOrdering());
            options.AuthServer.Should().Be(new Uri("https://as.example.com"));
            options.Resource.Should().Be(new Uri("https://resource.example.com"));
            options.Grant.Should().Be(OutboundGrantKind.DeviceCode);
            options.RedirectUri.Should().Be(new Uri("https://127.0.0.1:5000/callback/"));
            options.TokenCachePath.Should().Be("/tmp/cache");
            options.AuthTimeout.Should().Be(TimeSpan.FromSeconds(45));
            options.ApiKey.Should().Be("key");
            options.ApiKeyHeader.Should().Be("X-Api-Key");
            options.BasicUser.Should().Be("user");
            options.BasicPassword.Should().Be("pass");
            options.ClientMetadataDocumentUri.Should().Be(new Uri("https://cimd.example.com/doc.json"));
            options.IdpUrl.Should().Be(new Uri("https://idp.example.com"));
            options.IdpTokenEndpoint.Should().Be(new Uri("https://idp.example.com/token"));
            options.IdpClientId.Should().Be("idp-cid");
            options.IdpClientSecret.Should().Be("idp-secret");
            options.IdpScope.Should().Be("openid");
            options.IdpIdTokenFile.Should().Be("/tmp/idtoken.txt");
        }

        /// <summary>
        /// <c>--api-key</c> without <c>--api-key-header</c> fails validation.
        /// </summary>
        [TestMethod]
        public void StartCommand_ApiKeyWithoutHeader_BuildOptionsThrows()
        {
            var command = new StartCommand
            {
                ApiKey = "key",
                Url = LiveOData.Northwind
            };

            var act = command.BuildOptions;

            act.Should().Throw<ArgumentException>().WithMessage("*--api-key-header*");
        }

        /// <summary>
        /// An unrecognized <c>--grant</c> value fails validation.
        /// </summary>
        [TestMethod]
        public void StartCommand_InvalidGrant_BuildOptionsThrows()
        {
            var command = new StartCommand
            {
                Grant = "not-a-grant",
                Url = LiveOData.Northwind
            };

            var act = command.BuildOptions;

            act.Should().Throw<ArgumentException>().WithMessage("*--grant*");
        }

        /// <summary>
        /// A relative <c>--auth-server</c> value fails validation.
        /// </summary>
        [TestMethod]
        public void StartCommand_RelativeAuthServer_BuildOptionsThrows()
        {
            var command = new StartCommand
            {
                AuthServer = "not-absolute",
                Url = LiveOData.Northwind
            };

            var act = command.BuildOptions;

            act.Should().Throw<ArgumentException>().WithMessage("*--auth-server*");
        }

        /// <summary>
        /// An unset <c>--client-secret</c> falls back to the <c>ODATA_MCP_CLIENT_SECRET</c> environment variable.
        /// </summary>
        [TestMethod]
        public void StartCommand_ClientSecretFromEnvironment_Binds()
        {
            Environment.SetEnvironmentVariable(ODataMcpAuthConstants.ClientSecretEnvironmentVariable, "env-secret");
            try
            {
                var command = new StartCommand
                {
                    Url = LiveOData.Northwind
                };

                var options = command.BuildOptions();

                options.ClientSecret.Should().Be("env-secret");
            }
            finally
            {
                Environment.SetEnvironmentVariable(ODataMcpAuthConstants.ClientSecretEnvironmentVariable, null);
            }
        }

        /// <summary>
        /// With no outbound OAuth flags set, options fall back to their documented defaults.
        /// </summary>
        [TestMethod]
        public void StartCommand_NoFlags_BuildOptionsDefaults()
        {
            var command = new StartCommand
            {
                Url = LiveOData.Northwind
            };

            var options = command.BuildOptions();

            options.Scopes.Should().BeEmpty();
            options.AuthTimeout.Should().Be(TimeSpan.FromSeconds(300));
            options.Grant.Should().BeNull();
            options.HasExplicitCredentials.Should().BeFalse();
        }

        #endregion

        #region Internal Methods

        /// <summary>
        /// Builds an uninitialized call-tool request.
        /// </summary>
        /// <param name="name">The tool name.</param>
        /// <param name="arguments">Tool arguments.</param>
        /// <returns>
        /// The request context.
        /// </returns>
        internal static RequestContext<CallToolRequestParams> UninitializedCall(string? name, Dictionary<string, JsonElement>? arguments)
        {
            var request = (RequestContext<CallToolRequestParams>)RuntimeHelpers.GetUninitializedObject(typeof(RequestContext<CallToolRequestParams>));
            if (name is not null)
            {
                request.Params = new CallToolRequestParams
                {
                    Arguments = arguments,
                    Name = name
                };
            }

            return request;
        }

        #endregion

    }

}
