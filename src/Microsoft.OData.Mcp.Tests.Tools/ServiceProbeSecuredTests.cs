// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.OData.Mcp.Authentication.Outbound;
using Microsoft.OData.Mcp.Core.Catalog;
using Microsoft.OData.Mcp.Core.Diagnostics;
using Microsoft.OData.Mcp.Core.Parsing;
using Microsoft.OData.Mcp.Tools.Commands;
using Microsoft.OData.Mcp.Tools.Hosting;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ModelContextProtocol.Server;

namespace Microsoft.OData.Mcp.Tests.Tools
{

    /// <summary>
    /// <see cref="ServiceProbe"/> and the <c>try</c> report against in-process services that refuse or garble
    /// requests in each of the ways the probe is meant to tell apart.
    /// </summary>
    [TestClass]
    public class ServiceProbeSecuredTests
    {

        #region Fields

        internal const string Csdl = """
            <?xml version="1.0" encoding="utf-8"?>
            <edmx:Edmx Version="4.0" xmlns:edmx="http://docs.oasis-open.org/odata/ns/edmx">
              <edmx:DataServices>
                <Schema Namespace="Probe" xmlns="http://docs.oasis-open.org/odata/ns/edm">
                  <EntityType Name="Thing">
                    <Key><PropertyRef Name="Id" /></Key>
                    <Property Name="Id" Type="Edm.Int32" Nullable="false" />
                    <Property Name="Name" Type="Edm.String" />
                  </EntityType>
                  <EntityContainer Name="Container">
                    <EntitySet Name="Things" EntityType="Probe.Thing" />
                  </EntityContainer>
                </Schema>
              </edmx:DataServices>
            </edmx:Edmx>
            """;

        #endregion

        #region Public Methods

        /// <summary>
        /// Open metadata with 401 data is the common enterprise shape; the challenge is captured and the report names it.
        /// </summary>
        [TestMethod]
        public async Task Probe_MetadataOpenDataUnauthorized_IsDataSecured()
        {
            using var server = Serve(metadata: Csdl, data: context =>
            {
                context.Response.StatusCode = 401;
                context.Response.Headers.WWWAuthenticate = "Bearer realm=\"things\", resource_metadata=\"https://things.example/.well-known/oauth-protected-resource\"";
                return Task.CompletedTask;
            });

            var result = await Probe(server);

            result.Verdict.Should().Be(ServiceVerdict.DataSecured);
            result.Metadata.Outcome.Should().Be(ProbeOutcome.Passed);
            result.Data.Outcome.Should().Be(ProbeOutcome.Unauthorized);
            result.Data.StatusCode.Should().Be(401);
            result.SecuredStatusCode.Should().Be(401);
            result.Challenges.Should().ContainSingle().Which.Should().StartWith("Bearer realm=\"things\"");
            result.Results.Outcome.Should().Be(ProbeOutcome.Skipped);

            var report = TryCommand.Render(result, "anonymously");
            report.Should().Contain("[3/4] Security  401   metadata is public, data is secured (Bearer realm=\"things\"");
            report.Should().Contain("Verdict: data requires sign-in.");
        }

        /// <summary>
        /// A 401 on <c>$metadata</c> itself stops the probe there and is reported as metadata secured.
        /// </summary>
        [TestMethod]
        public async Task Probe_MetadataUnauthorized_IsMetadataSecured()
        {
            using var server = Serve(metadata: null, data: null, metadataStatus: 401, challenge: "Bearer");

            var result = await Probe(server);

            result.Verdict.Should().Be(ServiceVerdict.MetadataSecured);
            result.Metadata.Outcome.Should().Be(ProbeOutcome.Unauthorized);
            result.Metadata.Challenges.Should().Equal("Bearer");
            result.Data.Outcome.Should().Be(ProbeOutcome.Skipped);
            result.Model.Should().BeNull();
            TryCommand.Render(result, "anonymously").Should().Contain("metadata itself is secured (Bearer)");
        }

        /// <summary>
        /// A 403 counts as secured too, not as a failure.
        /// </summary>
        [TestMethod]
        public async Task Probe_DataForbidden_IsDataSecured()
        {
            using var server = Serve(metadata: Csdl, data: context =>
            {
                context.Response.StatusCode = 403;
                return Task.CompletedTask;
            });

            var result = await Probe(server);

            result.Verdict.Should().Be(ServiceVerdict.DataSecured);
            result.Data.Outcome.Should().Be(ProbeOutcome.Forbidden);
            TryCommand.Render(result, "anonymously").Should().Contain("(no WWW-Authenticate header)");
        }

        /// <summary>
        /// 404 on <c>$metadata</c> means the root is wrong or metadata is disabled: unreachable.
        /// </summary>
        [TestMethod]
        public async Task Probe_MetadataNotFound_IsUnreachable()
        {
            using var server = Serve(metadata: null, data: null, metadataStatus: 404);

            var result = await Probe(server);

            result.Verdict.Should().Be(ServiceVerdict.Unreachable);
            result.Metadata.Outcome.Should().Be(ProbeOutcome.NotFound);
            TryCommand.VerdictLine(result).Should().StartWith("unreachable");
        }

        /// <summary>
        /// A 200 that is HTML (a login page, a portal) is unreadable, and the report quotes the start of it.
        /// </summary>
        [TestMethod]
        public async Task Probe_MetadataIsHtml_IsUnreadable()
        {
            using var server = Serve(metadata: "<html><body>Please sign in</body></html>", data: null, metadataContentType: "text/html");

            var result = await Probe(server);

            result.Verdict.Should().Be(ServiceVerdict.Unreadable);
            result.Metadata.Outcome.Should().Be(ProbeOutcome.Unreadable);
            result.Metadata.Detail.Should().Contain("not CSDL").And.Contain("Please sign in");
        }

        /// <summary>
        /// Data that is not JSON is unreadable even though the request succeeded.
        /// </summary>
        [TestMethod]
        public async Task Probe_DataIsNotJson_IsUnreadable()
        {
            using var server = Serve(metadata: Csdl, data: context =>
            {
                context.Response.ContentType = "application/atom+xml";
                return context.Response.WriteAsync("<feed />");
            });

            var result = await Probe(server);

            result.Verdict.Should().Be(ServiceVerdict.Unreadable);
            result.Data.Outcome.Should().Be(ProbeOutcome.Passed);
            result.Results.Outcome.Should().Be(ProbeOutcome.Unreadable);
            result.Results.Detail.Should().Contain("not JSON");
        }

        /// <summary>
        /// A well-formed row is ready, and the sampled request is the first set with <c>$top=1</c>.
        /// </summary>
        [TestMethod]
        public async Task Probe_OpenService_IsReady()
        {
            string? requested = null;
            using var server = Serve(metadata: Csdl, data: context =>
            {
                requested = context.Request.Path + context.Request.QueryString;
                context.Response.ContentType = "application/json";
                return context.Response.WriteAsync("""{"@odata.context":"$metadata#Things","value":[{"Id":1,"Name":"one"}]}""");
            });

            var result = await Probe(server);

            result.Verdict.Should().Be(ServiceVerdict.Ready);
            requested.Should().Be("/Things?$top=1");
            result.Results.Detail.Should().Be("1 Thing row; all 2 properties match the model");
            TryCommand.Render(result, "anonymously").Should().Contain("Verdict: ready. Next: odata-mcp start http://localhost/");
        }

        /// <summary>
        /// <c>start</c> runs the anonymous probe after building the catalog; when data is secured and no credential
        /// was passed, the model is told so in the instructions preface before its first call.
        /// </summary>
        [TestMethod]
        public async Task CreateAsync_DataSecuredWithoutCredentials_SetsInstructionsPreface()
        {
            using var server = Serve(metadata: Csdl, data: context =>
            {
                context.Response.StatusCode = 401;
                context.Response.Headers.WWWAuthenticate = "Bearer realm=\"things\"";
                return Task.CompletedTask;
            });
            var handler = server.GetTestServer().CreateHandler();

            using var toolsHost = await ToolsMcpHost.CreateAsync(
                "http://localhost/",
                new OutboundOAuthOptions(),
                includeStdioMcp: false,
                verbose: false,
                lifetime: null,
                CancellationToken.None,
                services =>
                {
                    services.AddHttpClient(ODataMcpAuthConstants.ODataHttpClientName).ConfigurePrimaryHttpMessageHandler(() => handler);
                    services.AddHttpClient(Options.DefaultName).ConfigurePrimaryHttpMessageHandler(() => handler);
                });

            var instructions = toolsHost.Host.Services.GetRequiredService<IOptions<McpServerOptions>>().Value.ServerInstructions;
            instructions.Should().StartWith("This service's metadata is public but its data requires sign-in (Things answered 401).");
            instructions.Should().EndWith(ODataMcpInstructions.Default);
        }

        /// <summary>
        /// With an explicit credential the preface is not added: the handler will attach it on the first call.
        /// </summary>
        [TestMethod]
        public async Task CreateAsync_DataSecuredWithToken_KeepsDefaultInstructions()
        {
            using var server = Serve(metadata: Csdl, data: context =>
            {
                context.Response.StatusCode = 401;
                return Task.CompletedTask;
            });
            var handler = server.GetTestServer().CreateHandler();

            using var toolsHost = await ToolsMcpHost.CreateAsync(
                "http://localhost/",
                new OutboundOAuthOptions { AuthToken = "token" },
                includeStdioMcp: false,
                verbose: false,
                lifetime: null,
                CancellationToken.None,
                services =>
                {
                    services.AddHttpClient(ODataMcpAuthConstants.ODataHttpClientName).ConfigurePrimaryHttpMessageHandler(() => handler);
                    services.AddHttpClient(Options.DefaultName).ConfigurePrimaryHttpMessageHandler(() => handler);
                });

            toolsHost.Host.Services.GetRequiredService<IOptions<McpServerOptions>>().Value.ServerInstructions.Should().Be(ODataMcpInstructions.Default);
        }

        #endregion

        #region Internal Methods

        /// <summary>
        /// Runs the probe against a test server's root.
        /// </summary>
        /// <param name="server">The server.</param>
        /// <returns>
        /// The result.
        /// </returns>
        internal static Task<ServiceProbeResult> Probe(IHost server)
        {
            return new ServiceProbe(server.GetTestServer().CreateClient(), new CsdlParser()).ProbeAsync(new Uri("http://localhost/"), CancellationToken.None);
        }

        /// <summary>
        /// Builds a test server that answers <c>$metadata</c> and everything else as directed.
        /// </summary>
        /// <param name="metadata">The metadata body, or <c>null</c> to answer with <paramref name="metadataStatus"/> and no body.</param>
        /// <param name="data">The handler for any other path, or <c>null</c> to answer 404.</param>
        /// <param name="metadataStatus">The status for <c>$metadata</c> when <paramref name="metadata"/> is null.</param>
        /// <param name="metadataContentType">The metadata content type.</param>
        /// <param name="challenge">A <c>WWW-Authenticate</c> value for the metadata response, if any.</param>
        /// <returns>
        /// The server.
        /// </returns>
        internal static IHost Serve(string? metadata, Func<HttpContext, Task>? data, int metadataStatus = 200, string metadataContentType = "application/xml", string? challenge = null)
        {
            return new HostBuilder().ConfigureWebHost(web => web.UseTestServer().Configure(app => app.Run(context =>
            {
                if (context.Request.Path.Value?.EndsWith("$metadata", StringComparison.Ordinal) == true)
                {
                    if (metadata is null)
                    {
                        context.Response.StatusCode = metadataStatus;
                        if (challenge is not null)
                        {
                            context.Response.Headers.WWWAuthenticate = challenge;
                        }

                        return Task.CompletedTask;
                    }

                    context.Response.ContentType = metadataContentType;
                    return context.Response.WriteAsync(metadata);
                }

                if (data is null)
                {
                    context.Response.StatusCode = (int)HttpStatusCode.NotFound;
                    return Task.CompletedTask;
                }

                return data(context);
            }))).Start();
        }

        #endregion

    }

}
