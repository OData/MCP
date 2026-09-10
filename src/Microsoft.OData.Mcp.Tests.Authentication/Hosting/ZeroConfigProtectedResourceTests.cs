// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using CloudNimble.Breakdance.AspNetCore;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.OData;
using Microsoft.AspNetCore.OData.Routing.Controllers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.OData.Mcp.AspNetCore.Constants;
using Microsoft.OData.Mcp.AspNetCore.Hosting;
using Microsoft.OData.Mcp.Authentication.Outbound;
using Microsoft.OData.Mcp.Tests.AspNetCore;
using Microsoft.OData.Mcp.Tests.AspNetCore.Fixtures;
using Microsoft.OData.Mcp.Tests.Shared.Authentication;
using Microsoft.OData.Mcp.Tests.Shared.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.OData.Mcp.Tests.Authentication.Hosting
{

    /// <summary>
    /// The shared shape of the zero-config end-to-end: a rich OData 8 host secured by a real
    /// <see cref="LocalAuthorizationServer"/> that answers the <em>bare</em> <c>Bearer</c> challenge every
    /// deployed API answers with, and an authorization server whose own RFC 9728 document is switched off.
    /// Whatever protected resource metadata the CLI finds therefore came from the API, not from the fixture.
    /// </summary>
    /// <remarks>
    /// The CLI runs with no <c>--auth-server</c>, no <c>--resource</c>, and no <c>--client-id</c>: everything
    /// it needs has to come out of the challenge and the document behind it, which is the point of the
    /// feature. Both servers share the <c>http://localhost</c> origin in process, so the <c>"OAuth"</c>
    /// client's traffic is split by <see cref="WellKnownRoutingHandler"/>.
    /// </remarks>
    public abstract class ZeroConfigProtectedResourceHost : AspNetCoreBreakdanceTestBase
    {

        #region Fields

        /// <summary>
        /// The number of protected resource metadata requests the OData host itself received.
        /// </summary>
        internal int _protectedResourceRequests;

        #endregion

        #region Properties

        /// <summary>
        /// Gets the authorization server protecting this host, or <see langword="null"/> before setup.
        /// </summary>
        internal LocalAuthorizationServer? AuthorizationServer { get; set; }

        /// <summary>
        /// Gets the Tools host fixture that owns the outbound session, or <see langword="null"/> before setup.
        /// </summary>
        internal OutboundToolsHostFixture? Outbound { get; set; }

        /// <summary>
        /// Gets a value indicating whether the OData host calls <c>AddProtectedResourceMetadata</c>.
        /// </summary>
        internal abstract bool PublishesProtectedResource { get; }

        #endregion

        #region Test Lifecycle

        /// <summary>
        /// Builds the secured OData host and the outbound Tools fixture that talks to it, without signing in.
        /// </summary>
        /// <remarks>
        /// The session is deliberately not created here: the negative twin asserts on the exception that
        /// creating it throws, and a <c>[TestInitialize]</c> that threw would report as an error rather than
        /// as the assertion it is.
        /// </remarks>
        [TestInitialize]
        public void Setup()
        {
            AuthorizationServer = new LocalAuthorizationServer(new LocalAuthorizationServerOptions
            {
                EnableDynamicClientRegistration = true,
                ProtectedResourceMetadataMode = ProtectedResourceMetadataMode.NotFound
            });
            TestHostBuilder.ConfigureServices((_, services) =>
            {
                services.AddTransient<IStartupFilter>(_ => new RequestCountingStartupFilter(RecordRequest));

                if (PublishesProtectedResource)
                {
                    services.AddProtectedResourceMetadata(options => options.AuthorizationServers.Add(AuthorizationServer.Issuer));
                }

                services.AddSecuredResource(AuthorizationServer, bareBearerChallenge: true);
                services.AddSingleton<CustomerStore>();
                services.AddSingleton<DuplicatesStore>();
                services
                    .AddControllers()
                    .AddApplicationPart(typeof(CustomersController).Assembly)
                    .AddOData(options =>
                    {
                        options.EnableQueryFeatures();
                        options.AddRouteComponents("odata", TestModels.GetRichModel());
                    });
                services.AddODataMcp();
                services.AddMvcCore().AddApplicationPart(typeof(MetadataController).Assembly);
            });
            AddMinimalMvc();
            TestHostBuilder.ConfigureWebHost(web =>
            {
                web.Configure(app =>
                {
                    app.UseRouting();
                    app.UseEndpoints(endpoints => endpoints.MapControllers());
                    app.UseODataMcp();
                });
            });
            TestSetup();

            Outbound = new OutboundToolsHostFixture(AuthorizationServer, TestServer, new Uri("http://localhost/odata/"))
            {
                OAuthHandler = new WellKnownRoutingHandler(TestServer.CreateHandler(), AuthorizationServer.Handler)
            };
            Outbound.Options.ClientId = null;
        }

        /// <summary>
        /// Tears down the outbound host, the secured host, and the authorization server.
        /// </summary>
        [TestCleanup]
        public void TearDown()
        {
            Outbound?.Dispose();
            TestTearDown();
            AuthorizationServer?.Dispose();
        }

        #endregion

        #region Internal Methods

        /// <summary>
        /// Signs the CLI in against this host and builds its tool session.
        /// </summary>
        /// <returns>
        /// A task that completes once the session is ready.
        /// </returns>
        internal Task CreateSessionAsync()
        {
            return Outbound!.CreateSessionAsync(
                TestServer.Services.GetRequiredService<IOptions<ODataMcpHostOptions>>().Value.Catalog,
                CancellationToken.None);
        }

        /// <summary>
        /// Counts protected resource metadata requests the OData host itself received, before anything in its
        /// pipeline can short-circuit them.
        /// </summary>
        /// <param name="context">The request being served.</param>
        internal void RecordRequest(HttpContext context)
        {
            ArgumentNullException.ThrowIfNull(context);

            if (context.Request.Path.StartsWithSegments(ProtectedResourceConstants.WellKnownPath))
            {
                Interlocked.Increment(ref _protectedResourceRequests);
            }
        }

        #endregion

    }

    /// <summary>
    /// One line of configuration on the API is enough for <c>odata-mcp start</c> to sign itself in to a service
    /// that answers nothing but <c>Bearer</c>.
    /// </summary>
    [TestClass]
    public class ZeroConfigProtectedResourceTests : ZeroConfigProtectedResourceHost
    {

        #region Public Methods

        /// <summary>
        /// The CLI reads the API's own protected resource metadata, registers itself, completes a device code
        /// grant, and lists entity sets — with no operator flags at all.
        /// </summary>
        /// <returns>
        /// A task that completes when the test has run.
        /// </returns>
        [TestMethod]
        public async Task Start_WithProtectedResourceMetadata_SignsInAndListsEntitySets()
        {
            await CreateSessionAsync();

            _protectedResourceRequests.Should().BeGreaterThan(0);
            AuthorizationServer!.HitCount("prm").Should().Be(0);
            AuthorizationServer.HitCount("register").Should().Be(1);
            Outbound!.Presenter.Presentations.Should().Be(1);

            var listed = await Outbound.Session.Runtime.InvokeAsync("odata_list_entity_sets", null, CancellationToken.None);

            listed.IsError.Should().BeFalse(listed.Text);
            listed.StructuredContent.Should().Contain("Customers");
        }

        #endregion

        #region Internal Methods

        /// <inheritdoc />
        internal override bool PublishesProtectedResource => true;

        #endregion

    }

    /// <summary>
    /// The negative twin: the same bare <c>Bearer</c> API without <c>AddProtectedResourceMetadata</c> leaves the
    /// CLI with nothing to discover, and it says so rather than guessing.
    /// </summary>
    [TestClass]
    public class ZeroConfigProtectedResourceMissingTests : ZeroConfigProtectedResourceHost
    {

        #region Public Methods

        /// <summary>
        /// With no published document and no operator override, discovery fails first with the spec's message.
        /// </summary>
        /// <returns>
        /// A task that completes when the test has run.
        /// </returns>
        [TestMethod]
        public async Task Start_WithoutProtectedResourceMetadata_Throws()
        {
            Func<Task> act = CreateSessionAsync;

            var assertion = await act.Should().ThrowAsync<OutboundDiscoveryException>();

            assertion.Which.Message.Should().Be("Could not discover an authorization server; pass --auth-server.");
            AuthorizationServer!.HitCount("token").Should().Be(0);
        }

        #endregion

        #region Internal Methods

        /// <inheritdoc />
        internal override bool PublishesProtectedResource => false;

        #endregion

    }

}
