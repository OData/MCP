// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using CloudNimble.Breakdance.AspNetCore;
using FluentAssertions;
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
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.OData.Mcp.Tests.AspNetCore.Authentication
{

    /// <summary>
    /// The shared shape of a zero-config protected resource host: a real OData 8 service, a real JWT bearer
    /// handler that answers the bare <c>Bearer</c> challenge a deployed API answers with, and
    /// <c>AddODataProtectedResource</c> on top. Nothing here registers MCP, because the whole point of the
    /// feature is that an API author does not have to.
    /// </summary>
    /// <remarks>
    /// A handful of probe paths beneath and outside the OData prefix answer with hand-written responses, which
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
                services.AddODataProtectedResource(ConfigureProtectedResource);
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
        internal virtual void ConfigureProtectedResource(ODataProtectedResourceOptions options)
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

    /// <summary>
    /// A single-prefix API that publishes no scopes: the shape a first-time <c>AddODataProtectedResource</c>
    /// call produces, and the one whose annotated challenge the CLI reads.
    /// </summary>
    [TestClass]
    public class ODataProtectedResourceTests : ProtectedResourceHost
    {

        #region Public Methods

        /// <summary>
        /// The bare <c>Bearer</c> the JWT bearer handler answers with gains <c>resource_metadata</c>, which is
        /// the whole point of the feature.
        /// </summary>
        /// <returns>
        /// A task that completes when the test has run.
        /// </returns>
        [TestMethod]
        public async Task Challenge_BareBearer_GainsResourceMetadata()
        {
            using var client = TestServer.CreateClient();
            var response = await client.GetAsync("/odata/Customers");

            response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
            RawHeader(response, HeaderNames.WWWAuthenticate).Should().Be(
                "Bearer resource_metadata=\"http://localhost/.well-known/oauth-protected-resource/odata\"");
        }

        /// <summary>
        /// A <c>Basic</c> challenge sharing the header with <c>Bearer</c> survives the rewrite verbatim.
        /// </summary>
        /// <returns>
        /// A task that completes when the test has run.
        /// </returns>
        [TestMethod]
        public async Task Challenge_BasicAlongsideBearer_IsPreserved()
        {
            using var client = TestServer.CreateClient();
            var response = await client.GetAsync("/odata/_probe/basic");

            response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
            RawHeader(response, HeaderNames.WWWAuthenticate).Should().Be(
                "Basic realm=\"legacy\", Bearer resource_metadata=\"http://localhost/.well-known/oauth-protected-resource/odata\"");
        }

        /// <summary>
        /// A challenge that already names a document is left exactly as the app wrote it.
        /// </summary>
        /// <returns>
        /// A task that completes when the test has run.
        /// </returns>
        [TestMethod]
        public async Task Challenge_ExistingResourceMetadata_IsUntouched()
        {
            using var client = TestServer.CreateClient();
            var response = await client.GetAsync("/odata/_probe/existing");

            RawHeader(response, HeaderNames.WWWAuthenticate).Should().Be(
                "Bearer realm=\"contoso\", resource_metadata=\"https://existing.example.com/doc\"");
        }

        /// <summary>
        /// A <c>401</c> that carried no header at all is given a whole challenge.
        /// </summary>
        /// <returns>
        /// A task that completes when the test has run.
        /// </returns>
        [TestMethod]
        public async Task Challenge_NoHeader_GainsBearerChallenge()
        {
            using var client = TestServer.CreateClient();
            var response = await client.GetAsync("/odata/_probe/none");

            response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
            RawHeader(response, HeaderNames.WWWAuthenticate).Should().Be(
                "Bearer resource_metadata=\"http://localhost/.well-known/oauth-protected-resource/odata\"");
        }

        /// <summary>
        /// A <c>401</c> outside every covered prefix is never annotated.
        /// </summary>
        /// <returns>
        /// A task that completes when the test has run.
        /// </returns>
        [TestMethod]
        public async Task Challenge_OutsidePrefix_IsUntouched()
        {
            using var client = TestServer.CreateClient();
            var response = await client.GetAsync("/outside/_probe/none");

            response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
            RawHeader(response, HeaderNames.WWWAuthenticate).Should().BeEmpty();
        }

        /// <summary>
        /// A prefix is matched by whole segments, so <c>odata</c> never claims <c>odatafoo</c>.
        /// </summary>
        /// <returns>
        /// A task that completes when the test has run.
        /// </returns>
        [TestMethod]
        public async Task Challenge_SegmentLookalikePrefix_IsUntouched()
        {
            using var client = TestServer.CreateClient();
            var response = await client.GetAsync("/odatafoo/_probe/none");

            response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
            RawHeader(response, HeaderNames.WWWAuthenticate).Should().BeEmpty();
        }

        /// <summary>
        /// A successful response beneath the prefix is never annotated.
        /// </summary>
        /// <returns>
        /// A task that completes when the test has run.
        /// </returns>
        [TestMethod]
        public async Task Challenge_SuccessfulResponse_IsUntouched()
        {
            using var client = TestServer.CreateClient();
            var response = await client.GetAsync("/odata/_probe/ok");

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            RawHeader(response, HeaderNames.WWWAuthenticate).Should().BeEmpty();
        }

        /// <summary>
        /// The origin form is the document for the only prefix this host serves, and it is cacheable JSON.
        /// </summary>
        /// <returns>
        /// A task that completes when the test has run.
        /// </returns>
        [TestMethod]
        public async Task Document_OriginForm_DescribesTheOnlyPrefix()
        {
            var (response, body) = await GetDocumentAsync(ProtectedResourceConstants.WellKnownPath);

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            response.Content.Headers.ContentType!.MediaType.Should().Be("application/json");
            RawHeader(response, HeaderNames.CacheControl).Should().Be("public, max-age=300");
            body.GetProperty("resource").GetString().Should().Be("http://localhost/odata");
            body.GetProperty("authorization_servers").EnumerateArray().Select(entry => entry.GetString()).Should().ContainSingle()
                .Which.Should().Be(AuthorizationServer);
            body.GetProperty("bearer_methods_supported").EnumerateArray().Select(entry => entry.GetString()).Should().ContainSingle()
                .Which.Should().Be("header");
        }

        /// <summary>
        /// The document is readable without a token, which is what keeps it from being a dead end.
        /// </summary>
        /// <returns>
        /// A task that completes when the test has run.
        /// </returns>
        [TestMethod]
        public async Task Document_PathSuffixedForm_IsAnonymouslyReadable()
        {
            var (response, body) = await GetDocumentAsync($"{ProtectedResourceConstants.WellKnownPath}/odata");

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            RawHeader(response, HeaderNames.WWWAuthenticate).Should().BeEmpty();
            body.GetProperty("resource").GetString().Should().Be("http://localhost/odata");
        }

        /// <summary>
        /// A verb other than <c>GET</c> or <c>HEAD</c> on a document this middleware serves is refused.
        /// </summary>
        /// <returns>
        /// A task that completes when the test has run.
        /// </returns>
        [TestMethod]
        public async Task Document_Post_IsMethodNotAllowed()
        {
            using var client = TestServer.CreateClient();
            using var content = new StringContent("{}");
            var response = await client.PostAsync(ProtectedResourceConstants.WellKnownPath, content);

            response.StatusCode.Should().Be(HttpStatusCode.MethodNotAllowed);
        }

        /// <summary>
        /// A well-known suffix naming a prefix this host does not serve belongs to somebody else.
        /// </summary>
        /// <returns>
        /// A task that completes when the test has run.
        /// </returns>
        [TestMethod]
        public async Task Document_UnknownSuffix_FallsThrough()
        {
            using var client = TestServer.CreateClient();
            var response = await client.GetAsync($"{ProtectedResourceConstants.WellKnownPath}/nope");

            response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        }

        #endregion

    }

    /// <summary>
    /// An API that publishes scopes: the document advertises them and the annotated challenge asks for them.
    /// </summary>
    [TestClass]
    public class ODataProtectedResourceScopedTests : ProtectedResourceHost
    {

        #region Public Methods

        /// <summary>
        /// The annotated challenge carries the published scopes so a client knows what to ask for.
        /// </summary>
        /// <returns>
        /// A task that completes when the test has run.
        /// </returns>
        [TestMethod]
        public async Task Challenge_WithScopes_CarriesScope()
        {
            using var client = TestServer.CreateClient();
            var response = await client.GetAsync("/odata/Customers");

            RawHeader(response, HeaderNames.WWWAuthenticate).Should().Be(
                "Bearer resource_metadata=\"http://localhost/.well-known/oauth-protected-resource/odata\", scope=\"Data.Read Data.Write\"");
        }

        /// <summary>
        /// The published document advertises the same scopes, plus the display metadata a consent screen shows.
        /// </summary>
        /// <returns>
        /// A task that completes when the test has run.
        /// </returns>
        [TestMethod]
        public async Task Document_WithScopes_AdvertisesThem()
        {
            var (_, body) = await GetDocumentAsync($"{ProtectedResourceConstants.WellKnownPath}/odata");

            body.GetProperty("scopes_supported").EnumerateArray().Select(entry => entry.GetString()).Should()
                .BeEquivalentTo(["Data.Read", "Data.Write"]);
            body.GetProperty("resource_name").GetString().Should().Be("Contoso OData");
            body.GetProperty("resource_documentation").GetString().Should().Be("https://docs.example.com/odata");
        }

        #endregion

        #region Internal Methods

        /// <inheritdoc />
        internal override void ConfigureProtectedResource(ODataProtectedResourceOptions options)
        {
            base.ConfigureProtectedResource(options);

            options.ResourceDocumentation = new Uri("https://docs.example.com/odata");
            options.ResourceName = "Contoso OData";
            options.ScopesSupported.Add("Data.Read");
            options.ScopesSupported.Add("Data.Write");
        }

        #endregion

    }

    /// <summary>
    /// An API serving two OData prefixes publishes one document per prefix, and the origin form describes the
    /// first one discovery found.
    /// </summary>
    [TestClass]
    public class ODataProtectedResourceMultiPrefixTests : ProtectedResourceHost
    {

        #region Public Methods

        /// <summary>
        /// A <c>401</c> beneath the second prefix points at the second prefix's document, not the first.
        /// </summary>
        /// <returns>
        /// A task that completes when the test has run.
        /// </returns>
        [TestMethod]
        public async Task Challenge_SecondPrefix_PointsAtItsOwnDocument()
        {
            using var client = TestServer.CreateClient();
            var response = await client.GetAsync("/reporting/_probe/none");

            RawHeader(response, HeaderNames.WWWAuthenticate).Should().Be(
                "Bearer resource_metadata=\"http://localhost/.well-known/oauth-protected-resource/reporting\"");
        }

        /// <summary>
        /// Each prefix gets its own path-suffixed document naming its own resource.
        /// </summary>
        /// <returns>
        /// A task that completes when the test has run.
        /// </returns>
        [TestMethod]
        public async Task Document_EachPrefix_HasItsOwnDocument()
        {
            var (odata, odataBody) = await GetDocumentAsync($"{ProtectedResourceConstants.WellKnownPath}/odata");
            var (reporting, reportingBody) = await GetDocumentAsync($"{ProtectedResourceConstants.WellKnownPath}/reporting");

            odata.StatusCode.Should().Be(HttpStatusCode.OK);
            odataBody.GetProperty("resource").GetString().Should().Be("http://localhost/odata");
            reporting.StatusCode.Should().Be(HttpStatusCode.OK);
            reportingBody.GetProperty("resource").GetString().Should().Be("http://localhost/reporting");
        }

        /// <summary>
        /// The origin form describes the first prefix, which is the one a client that never read a
        /// <c>resource_metadata</c> hint lands on.
        /// </summary>
        /// <returns>
        /// A task that completes when the test has run.
        /// </returns>
        [TestMethod]
        public async Task Document_OriginForm_DescribesTheFirstPrefix()
        {
            var (response, body) = await GetDocumentAsync(ProtectedResourceConstants.WellKnownPath);

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            body.GetProperty("resource").GetString().Should().Be("http://localhost/odata");
        }

        #endregion

        #region Internal Methods

        /// <inheritdoc />
        internal override void ConfigureOData(ODataOptions options)
        {
            options.EnableQueryFeatures();
            options.AddRouteComponents("odata", TestModels.GetSimpleModel());
            options.AddRouteComponents("reporting", TestModels.GetMinimalModel());
        }

        #endregion

    }

    /// <summary>
    /// An explicit prefix list replaces discovery outright, which is the escape hatch for a host whose routing
    /// table carries no OData metadata.
    /// </summary>
    [TestClass]
    public class ODataProtectedResourceExplicitPrefixTests : ProtectedResourceHost
    {

        #region Public Methods

        /// <summary>
        /// The prefix routing actually serves is not published, because the explicit list replaced discovery.
        /// </summary>
        /// <returns>
        /// A task that completes when the test has run.
        /// </returns>
        [TestMethod]
        public async Task Document_DiscoveredPrefix_IsNotPublished()
        {
            using var client = TestServer.CreateClient();
            var response = await client.GetAsync($"{ProtectedResourceConstants.WellKnownPath}/odata");

            response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        }

        /// <summary>
        /// The configured prefix is published even though routing never advertised it.
        /// </summary>
        /// <returns>
        /// A task that completes when the test has run.
        /// </returns>
        [TestMethod]
        public async Task Document_ExplicitPrefix_IsPublished()
        {
            var (response, body) = await GetDocumentAsync($"{ProtectedResourceConstants.WellKnownPath}/custom");

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            body.GetProperty("resource").GetString().Should().Be("http://localhost/custom");
        }

        #endregion

        #region Internal Methods

        /// <inheritdoc />
        internal override void ConfigureProtectedResource(ODataProtectedResourceOptions options)
        {
            base.ConfigureProtectedResource(options);

            options.Prefixes.Add("custom");
        }

        #endregion

    }

    /// <summary>
    /// <see cref="ODataProtectedResourceOptions.Validate"/> is what turns a misconfigured API into a host that
    /// refuses to start rather than one that misdirects every client.
    /// </summary>
    [TestClass]
    public class ODataProtectedResourceOptionsTests
    {

        #region Public Methods

        /// <summary>
        /// A blank explicit prefix is a typo, not a root service.
        /// </summary>
        [TestMethod]
        public void Validate_BlankPrefix_Throws()
        {
            var options = new ODataProtectedResourceOptions();
            options.AuthorizationServers.Add(new Uri("https://login.example.com/contoso/v2.0"));
            options.Prefixes.Add("   ");

            Action act = options.Validate;

            act.Should().Throw<InvalidOperationException>().WithMessage($"*{nameof(ODataProtectedResourceOptions.Prefixes)}*");
        }

        /// <summary>
        /// A loopback authorization server over plain http is how a test host and a local identity provider are
        /// modelled, so it passes.
        /// </summary>
        [TestMethod]
        public void Validate_LoopbackHttpAuthorizationServer_Passes()
        {
            var options = new ODataProtectedResourceOptions();
            options.AuthorizationServers.Add(new Uri("http://127.0.0.1:5000"));

            Action act = options.Validate;

            act.Should().NotThrow();
        }

        /// <summary>
        /// A document with no authorization server tells a client nothing at all.
        /// </summary>
        [TestMethod]
        public void Validate_NoAuthorizationServers_Throws()
        {
            var options = new ODataProtectedResourceOptions();

            Action act = options.Validate;

            act.Should().Throw<InvalidOperationException>().WithMessage($"*{nameof(ODataProtectedResourceOptions.AuthorizationServers)}*");
        }

        /// <summary>
        /// A relative authorization server is not something a client can probe.
        /// </summary>
        [TestMethod]
        public void Validate_NonAbsoluteAuthorizationServer_Throws()
        {
            var options = new ODataProtectedResourceOptions();
            options.AuthorizationServers.Add(new Uri("/oauth/v2.0", UriKind.Relative));

            Action act = options.Validate;

            act.Should().Throw<InvalidOperationException>().WithMessage("*absolute*");
        }

        /// <summary>
        /// A plain http authorization server that is not loopback would carry every credential in the clear.
        /// </summary>
        [TestMethod]
        public void Validate_NonLoopbackHttpAuthorizationServer_Throws()
        {
            var options = new ODataProtectedResourceOptions();
            options.AuthorizationServers.Add(new Uri("http://example.com"));

            Action act = options.Validate;

            act.Should().Throw<InvalidOperationException>().WithMessage("*https*");
        }

        #endregion

    }

}
