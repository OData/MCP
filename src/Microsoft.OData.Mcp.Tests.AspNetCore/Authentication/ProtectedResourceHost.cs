// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using CloudNimble.Breakdance.AspNetCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.OData;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Net.Http.Headers;
using Microsoft.OData.Mcp.AspNetCore.Authentication;
using Microsoft.OData.Mcp.AspNetCore.Constants;
using Microsoft.OData.Mcp.Tests.AspNetCore.Security;
using Microsoft.OData.Mcp.Tests.Shared.Models;
using CustomersController = Microsoft.OData.Mcp.Tests.AspNetCore.CustomersController;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.OData.Mcp.Tests.AspNetCore.Authentication
{

    /// <summary>
    /// The shared shape of a zero-config protected resource host: a real OData 8 service, a real JWT bearer
    /// handler that answers the bare <c>Bearer</c> challenge a deployed API answers with, and
    /// <c>AddProtectedResourceMetadata</c> on top. Nothing here registers MCP, because the whole point of the
    /// feature is that an API author does not have to.
    /// </summary>
    /// <remarks>
    /// A handful of probe paths beneath and outside any OData prefix answer with hand-written responses, which
    /// is the only way to exercise a challenge shape the JWT bearer handler will never produce on its own — an
    /// existing <c>resource_metadata</c>, a <c>401</c> with no header at all, and a <c>Basic</c> challenge
    /// sharing the header with <c>Bearer</c>.
    /// </remarks>
    public abstract class ProtectedResourceHost : AspNetCoreBreakdanceTestBase
    {

        #region Fields

        /// <summary>
        /// The issuer URL the published document advertises.
        /// </summary>
        internal const string AuthorizationServer = "https://login.example.com/contoso/v2.0";

        #endregion

        #region Test Lifecycle

        /// <summary>
        /// Builds the protected resource host.
        /// </summary>
        [TestInitialize]
        public void Setup()
        {
            TestHostBuilder.ConfigureServices((_, services) =>
            {
                services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                    .AddJwtBearer(options =>
                    {
                        options.Challenge = ProtectedResourceConstants.BearerScheme;
                        options.TokenValidationParameters = new TokenValidationParameters
                        {
                            IssuerSigningKey = new SymmetricSecurityKey(TestJwt.SigningKey),
                            ValidateAudience = false,
                            ValidateIssuer = false,
                            ValidateIssuerSigningKey = true,
                            ValidateLifetime = false
                        };
                    });
                services.AddAuthorization();
                services
                    .AddControllers()
                    .AddApplicationPart(typeof(CustomersController).Assembly)
                    .AddOData(ConfigureOData);
                services.AddProtectedResourceMetadata(ConfigureProtectedResource);
            });
            AddMinimalMvc();
            TestHostBuilder.ConfigureWebHost(web =>
            {
                web.Configure(app =>
                {
                    app.Use(ProbeAsync);
                    app.Use(ChallengeAsync);
                    app.UseRouting();
                    app.UseAuthentication();
                    app.UseAuthorization();
                    app.UseEndpoints(endpoints => endpoints.MapControllers());
                });
            });
            TestSetup();
        }

        /// <summary>
        /// Tears down the host.
        /// </summary>
        [TestCleanup]
        public void TearDown()
        {
            TestTearDown();
        }

        #endregion

        #region Internal Methods

        /// <summary>
        /// Challenges an unauthenticated request beneath <c>/odata</c>, which is what makes this host answer
        /// the bare <c>Bearer</c> a real API answers with.
        /// </summary>
        /// <param name="context">The request being served.</param>
        /// <param name="next">The next middleware.</param>
        /// <returns>
        /// A task that completes when the request has been handled.
        /// </returns>
        internal async Task ChallengeAsync(HttpContext context, RequestDelegate next)
        {
            if (context.Request.Path.StartsWithSegments("/odata") && !context.Request.Headers.ContainsKey(HeaderNames.Authorization))
            {
                await context.ChallengeAsync(JwtBearerDefaults.AuthenticationScheme);

                return;
            }

            await next(context);
        }

        /// <summary>
        /// Configures the OData routes this host serves. Override to register more than one prefix.
        /// </summary>
        /// <param name="options">The OData options.</param>
        internal virtual void ConfigureOData(ODataOptions options)
        {
            options.EnableQueryFeatures();
            options.AddRouteComponents("odata", TestModels.GetSimpleModel());
        }

        /// <summary>
        /// Configures what this host publishes about itself. Override to add scopes or explicit prefixes.
        /// </summary>
        /// <param name="options">The protected resource options.</param>
        internal virtual void ConfigureProtectedResource(ProtectedResourceMetadataOptions options)
        {
            options.AuthorizationServers.Add(new Uri(AuthorizationServer));
        }

        /// <summary>
        /// Reads a protected resource metadata document.
        /// </summary>
        /// <param name="path">The absolute path of the document.</param>
        /// <returns>
        /// The response and its parsed body.
        /// </returns>
        internal async Task<(HttpResponseMessage Response, JsonElement Body)> GetDocumentAsync(string path)
        {
            using var client = TestServer.CreateClient();
            var response = await client.GetAsync(path);
            var json = await response.Content.ReadAsStringAsync();

            return (response, JsonDocument.Parse(json).RootElement.Clone());
        }

        /// <summary>
        /// Answers the probe paths with the exact challenge shapes under test.
        /// </summary>
        /// <param name="context">The request being served.</param>
        /// <param name="next">The next middleware.</param>
        /// <returns>
        /// A task that completes when the request has been handled.
        /// </returns>
        internal async Task ProbeAsync(HttpContext context, RequestDelegate next)
        {
            var path = context.Request.Path.Value ?? string.Empty;
            if (!path.Contains("/_probe/", StringComparison.OrdinalIgnoreCase))
            {
                await next(context);

                return;
            }

            switch (path[(path.LastIndexOf('/') + 1)..])
            {
                case "existing":
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    context.Response.Headers.WWWAuthenticate = "Bearer realm=\"contoso\", resource_metadata=\"https://existing.example.com/doc\"";

                    return;
                case "none":
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;

                    return;
                case "basic":
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    context.Response.Headers.WWWAuthenticate = "Basic realm=\"legacy\", Bearer";

                    return;
                case "ok":
                    context.Response.StatusCode = StatusCodes.Status200OK;

                    return;
                default:
                    await next(context);

                    return;
            }
        }

        /// <summary>
        /// Reads the raw, unparsed values of a response header.
        /// </summary>
        /// <param name="response">The response to read.</param>
        /// <param name="name">The header name.</param>
        /// <returns>
        /// The header values exactly as the server wrote them, joined by <c>", "</c> when there is more than one.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="response"/> is <see langword="null"/>.</exception>
        /// <remarks>
        /// <see cref="System.Net.Http.Headers.HttpHeaders.NonValidated"/> rather than the typed accessors,
        /// because a typed accessor reserializes what it parsed and this suite asserts on the bytes the
        /// middleware actually wrote.
        /// </remarks>
        internal static string RawHeader(HttpResponseMessage response, string name)
        {
            ArgumentNullException.ThrowIfNull(response);
            ArgumentException.ThrowIfNullOrWhiteSpace(name);

            return response.Headers.NonValidated.TryGetValues(name, out var values)
                ? string.Join(", ", values)
                : string.Empty;
        }

        #endregion

    }

}
