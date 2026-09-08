// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Microsoft.OData.Mcp.Tests.Shared.Authentication
{

    /// <summary>
    /// A minimal OAuth protected OData service hosted on an ASP.NET Core <see cref="TestServer"/>: it serves one
    /// static CSDL document at <c>/odata/$metadata</c> and one static entity set at <c>/odata/Customers</c>,
    /// both behind the JWT bearer challenge <see cref="LocalAuthorizationServer"/> mints tokens for.
    /// </summary>
    /// <example>
    /// <code>
    /// using var authorizationServer = new LocalAuthorizationServer(new LocalAuthorizationServerOptions());
    /// using var resource = new SecuredStaticResourceServer(authorizationServer, SecuredStaticResourceServer.DefaultCsdl);
    ///
    /// services.AddHttpClient(ODataMcpAuthConstants.ODataHttpClientName, client =&gt; client.BaseAddress = resource.ServiceRoot)
    ///     .ConfigurePrimaryHttpMessageHandler(() =&gt; resource.Server.CreateHandler());
    /// </code>
    /// </example>
    /// <remarks>
    /// Both this resource and its authorization server live on the <c>http://localhost</c> origin
    /// <see cref="TestServer"/> defaults to, so the RFC 9728 <c>resource_metadata</c> hint the challenge carries
    /// resolves against the authorization server fixture without any rewriting. This is deliberately not an
    /// OData service implementation: the point is the authentication path, so the payloads are fixed strings and
    /// no query option is honored.
    /// </remarks>
    public sealed class SecuredStaticResourceServer : IDisposable
    {

        #region Fields

        /// <summary>
        /// A CSDL document declaring a single <c>Customer</c> entity type and its <c>Customers</c> entity set.
        /// </summary>
        public const string DefaultCsdl = """
            <?xml version="1.0" encoding="utf-8"?>
            <edmx:Edmx Version="4.0" xmlns:edmx="http://docs.oasis-open.org/odata/ns/edmx">
              <edmx:DataServices>
                <Schema Namespace="SecuredService" xmlns="http://docs.oasis-open.org/odata/ns/edm">
                  <EntityType Name="Customer">
                    <Key>
                      <PropertyRef Name="Id" />
                    </Key>
                    <Property Name="Id" Type="Edm.Int32" Nullable="false" />
                    <Property Name="CompanyName" Type="Edm.String" />
                  </EntityType>
                  <EntityContainer Name="Container">
                    <EntitySet Name="Customers" EntityType="SecuredService.Customer" />
                  </EntityContainer>
                </Schema>
              </edmx:DataServices>
            </edmx:Edmx>
            """;

        /// <summary>
        /// The body <c>/odata/Customers</c> answers with.
        /// </summary>
        internal const string CustomersJson = """{"value":[{"Id":1,"CompanyName":"Contoso"}]}""";

        /// <summary>
        /// The generic host that owns <see cref="Server"/>.
        /// </summary>
        internal readonly IHost _host;

        /// <summary>
        /// The per-endpoint hit counters exposed by <see cref="HitCount(string)"/>.
        /// </summary>
        internal readonly ConcurrentDictionary<string, int> _hits = new(StringComparer.Ordinal);

        #endregion

        #region Properties

        /// <summary>
        /// Gets the verbatim <c>WWW-Authenticate</c> value this resource answers an unauthenticated request
        /// with, or <see langword="null"/> when the challenge is the one
        /// <see cref="SecuredResourceServiceCollectionExtensions.CreateChallenge(LocalAuthorizationServer, bool)"/>
        /// builds.
        /// </summary>
        /// <remarks>
        /// Set through the constructor because the JWT bearer challenge is captured once, when the options are
        /// configured; it cannot be changed after this instance is built.
        /// </remarks>
        public string? ChallengeOverride { get; }

        /// <summary>
        /// Gets the test server hosting this resource.
        /// </summary>
        public TestServer Server { get; }

        /// <summary>
        /// Gets the OData service root every relative request is resolved against.
        /// </summary>
        public Uri ServiceRoot { get; } = new("http://localhost/odata/");

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="SecuredStaticResourceServer"/> class and starts hosting it.
        /// </summary>
        /// <param name="authorizationServer">The authorization server whose signing key and metadata URI this resource trusts.</param>
        /// <param name="csdl">The CSDL document served at <c>/odata/$metadata</c>; normally <see cref="DefaultCsdl"/>.</param>
        /// <param name="requireAuthentication">Whether <c>/odata</c> is protected by the JWT bearer challenge; <see langword="false"/> serves it anonymously.</param>
        /// <param name="bareBearerChallenge">Whether the challenge is a bare <c>Bearer</c> carrying no RFC 9728 <c>resource_metadata</c> hint.</param>
        /// <param name="challengeOverride">The verbatim <c>WWW-Authenticate</c> value to answer with, or <see langword="null"/> for the built challenge.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="authorizationServer"/> is <see langword="null"/>.</exception>
        /// <exception cref="ArgumentException">Thrown when <paramref name="csdl"/> is <see langword="null"/>, empty, or whitespace.</exception>
        /// <remarks>
        /// <paramref name="requireAuthentication"/> exists so a test can prove what an <em>unauthenticated</em>
        /// service costs: an anonymous resource never answers a challenge, so the outbound client never runs a
        /// grant and never touches the credential store.
        /// <para>
        /// <paramref name="bareBearerChallenge"/> models the ordinary enterprise deployment: a <c>401</c> whose
        /// challenge is nothing but <c>Bearer</c>, from which no authorization server can be discovered.
        /// </para>
        /// <para>
        /// <paramref name="challengeOverride"/> exists so a test can answer with a challenge
        /// <see cref="System.Net.Http.Headers.HttpHeaders"/> refuses to parse — a second scheme spelled with a
        /// character no scheme may carry, for instance — which is what empties the typed
        /// <c>WWW-Authenticate</c> collection while leaving the raw header perfectly readable. It wins over
        /// <paramref name="bareBearerChallenge"/>.
        /// </para>
        /// </remarks>
        public SecuredStaticResourceServer(
            LocalAuthorizationServer authorizationServer,
            string csdl,
            bool requireAuthentication = true,
            bool bareBearerChallenge = false,
            string? challengeOverride = null)
        {
            ArgumentNullException.ThrowIfNull(authorizationServer);
            ArgumentException.ThrowIfNullOrWhiteSpace(csdl);

            ChallengeOverride = challengeOverride;
            _host = new HostBuilder()
                .ConfigureWebHost(web =>
                {
                    web.UseTestServer();
                    web.ConfigureServices(services =>
                    {
                        services.AddRouting();
                        services.AddTransient<IStartupFilter>(_ => new RequestCountingStartupFilter(RecordRequest));

                        if (requireAuthentication)
                        {
                            services.AddSecuredResource(
                                authorizationServer,
                                bareBearerChallenge: bareBearerChallenge,
                                challengeOverride: challengeOverride);
                        }
                    });
                    web.Configure(app =>
                    {
                        app.UseRouting();
                        app.UseEndpoints(endpoints =>
                        {
                            endpoints.MapGet("/odata/$metadata", context =>
                            {
                                context.Response.ContentType = "application/xml; charset=utf-8";

                                return context.Response.WriteAsync(csdl);
                            });
                            endpoints.MapGet("/odata/Customers", context =>
                            {
                                context.Response.ContentType = "application/json; charset=utf-8";

                                return context.Response.WriteAsync(CustomersJson);
                            });
                            endpoints.MapGet("/odata/Forbidden", context =>
                            {
                                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                                context.Response.ContentType = "text/plain; charset=utf-8";

                                return context.Response.WriteAsync("insufficient scope");
                            });
                        });
                    });
                })
                .Build();
            _host.Start();

            Server = _host.GetTestServer();
        }

        #endregion

        #region Public Methods

        /// <inheritdoc />
        public void Dispose()
        {
            _host.Dispose();
        }

        /// <summary>
        /// Gets the number of authenticated requests an endpoint has served since this instance was created.
        /// </summary>
        /// <param name="endpoint">One of <c>metadata</c>, <c>customers</c>, or <c>forbidden</c>.</param>
        /// <returns>
        /// The hit count, or zero when the endpoint has never been reached.
        /// </returns>
        /// <exception cref="ArgumentException">Thrown when <paramref name="endpoint"/> is <see langword="null"/>, empty, or whitespace.</exception>
        /// <remarks>
        /// Every attempt counts, including one the JWT bearer challenge rejects, so a first start that walks the
        /// whole device code grant records exactly two <c>metadata</c> hits: the unauthenticated probe that
        /// draws the <c>401</c> and the retry that carries the bearer token.
        /// </remarks>
        public int HitCount(string endpoint)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(endpoint);

            return _hits.TryGetValue(endpoint, out var count) ? count : 0;
        }

        #endregion

        #region Internal Methods

        /// <summary>
        /// Increments the hit counter for an endpoint.
        /// </summary>
        /// <param name="endpoint">The counter name.</param>
        internal void RecordHit(string endpoint)
        {
            _hits.AddOrUpdate(endpoint, 1, (_, current) => current + 1);
        }

        /// <summary>
        /// Maps a request path onto a counter name and records the attempt.
        /// </summary>
        /// <param name="context">The request being served, before authentication has run.</param>
        internal void RecordRequest(HttpContext context)
        {
            var path = context.Request.Path.Value;

            if (string.Equals(path, "/odata/$metadata", StringComparison.OrdinalIgnoreCase))
            {
                RecordHit("metadata");
            }
            else if (string.Equals(path, "/odata/Customers", StringComparison.OrdinalIgnoreCase))
            {
                RecordHit("customers");
            }
            else if (string.Equals(path, "/odata/Forbidden", StringComparison.OrdinalIgnoreCase))
            {
                RecordHit("forbidden");
            }
        }

        #endregion

    }

}
