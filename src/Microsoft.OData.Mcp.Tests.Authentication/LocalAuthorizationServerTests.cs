// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OData.Mcp.Tests.Shared.Authentication;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.OData.Mcp.Tests.Authentication
{

    /// <summary>
    /// Tests for <see cref="LocalAuthorizationServer"/>, the real local authorization server the outbound OAuth suites run against.
    /// </summary>
    [TestClass]
    public class LocalAuthorizationServerTests
    {

        #region Fields

        /// <summary>
        /// The RFC 8628 device authorization grant type.
        /// </summary>
        internal const string DeviceCodeGrantType = "urn:ietf:params:oauth:grant-type:device_code";

        /// <summary>
        /// The loopback redirect URI the authorization code tests use.
        /// </summary>
        internal const string RedirectUri = "http://127.0.0.1:8765/callback";

        #endregion

        #region Public Methods

        /// <summary>
        /// The authorize endpoint redirects with code, state, and iss, and the code exchanges once against the matching verifier.
        /// </summary>
        /// <returns>
        /// A task that completes when the test has run.
        /// </returns>
        [TestMethod]
        public async Task Authorize_S256_RedirectsWithCodeStateIss_ThenExchanges()
        {
            using var server = new LocalAuthorizationServer(new LocalAuthorizationServerOptions());
            using var handler = server.Server.CreateHandler();
            using var client = new HttpClient(handler)
            {
                BaseAddress = server.Server.BaseAddress
            };

            var codeVerifier = "eS8xNW9jRlpYb0swbXFOa1AzYlR2SndSdV9yLTdBWQ";
            var codeChallenge = Base64UrlEncoder.Encode(SHA256.HashData(Encoding.ASCII.GetBytes(codeVerifier)));
            var authorizeUri = $"/oauth/v2.0/authorize?response_type=code&client_id=cli&redirect_uri={Uri.EscapeDataString(RedirectUri)}&state=opaque-state&scope={Uri.EscapeDataString("read offline_access")}&code_challenge={codeChallenge}&code_challenge_method=S256";

            using var redirect = await client.GetAsync(authorizeUri);

            redirect.StatusCode.Should().Be(HttpStatusCode.Found);
            redirect.Headers.Location.Should().NotBeNull();

            var location = redirect.Headers.Location!;
            location.GetLeftPart(UriPartial.Path).Should().Be(RedirectUri);

            var parameters = QueryHelpers.ParseQuery(location.Query);
            parameters["state"].ToString().Should().Be("opaque-state");
            parameters["iss"].ToString().Should().Be(server.Issuer.ToString());
            parameters["code"].ToString().Should().NotBeNullOrWhiteSpace();
            server.HitCount("authorize").Should().Be(1);

            var exchange = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["grant_type"] = "authorization_code",
                ["client_id"] = "cli",
                ["code"] = parameters["code"].ToString(),
                ["code_verifier"] = codeVerifier,
                ["redirect_uri"] = RedirectUri
            };

            using var granted = await PostFormAsync(client, "/oauth/v2.0/token", exchange);

            granted.StatusCode.Should().Be(HttpStatusCode.OK);

            using var tokens = await ReadJsonAsync(granted);

            tokens.RootElement.GetProperty("access_token").GetString().Should().NotBeNullOrWhiteSpace();
            tokens.RootElement.GetProperty("refresh_token").GetString().Should().NotBeNullOrWhiteSpace();

            using var replayed = await PostFormAsync(client, "/oauth/v2.0/token", exchange);

            replayed.StatusCode.Should().Be(HttpStatusCode.BadRequest);

            using var replayedBody = await ReadJsonAsync(replayed);

            replayedBody.RootElement.GetProperty("error").GetString().Should().Be("invalid_grant");
            server.HitCount("token:authorization_code").Should().Be(2);
        }

        /// <summary>
        /// The client credentials grant issues an access token that validates against the server's own parameters, and no refresh token.
        /// </summary>
        /// <returns>
        /// A task that completes when the test has run.
        /// </returns>
        [TestMethod]
        public async Task ClientCredentials_Daemon_IssuesToken()
        {
            using var server = new LocalAuthorizationServer(new LocalAuthorizationServerOptions());
            using var client = server.Server.CreateClient();

            using var response = await PostFormAsync(client, "/oauth/v2.0/token", new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["grant_type"] = "client_credentials",
                ["client_id"] = "daemon",
                ["client_secret"] = "daemon-secret",
                ["scope"] = "read write"
            });

            response.StatusCode.Should().Be(HttpStatusCode.OK);

            using var tokens = await ReadJsonAsync(response);

            tokens.RootElement.GetProperty("token_type").GetString().Should().Be("Bearer");
            tokens.RootElement.GetProperty("scope").GetString().Should().Be("read write");
            tokens.RootElement.GetProperty("expires_in").GetInt32().Should().Be(3600);
            tokens.RootElement.TryGetProperty("refresh_token", out _).Should().BeFalse();

            var accessToken = tokens.RootElement.GetProperty("access_token").GetString()!;
            var principal = new JwtSecurityTokenHandler().ValidateToken(accessToken, server.CreateTokenValidationParameters(), out var validated);

            principal.Should().NotBeNull();
            validated.Issuer.Should().Be(server.Issuer.ToString());
            server.HitCount("token:client_credentials").Should().Be(1);
            server.LastTokenRequest.Should().NotBeNull();
            server.LastTokenRequest!["client_id"].Should().Be("daemon");
        }

        /// <summary>
        /// The client credentials grant rejects a confidential client that presents the wrong secret.
        /// </summary>
        /// <returns>
        /// A task that completes when the test has run.
        /// </returns>
        [TestMethod]
        public async Task ClientCredentials_WrongSecret_401()
        {
            using var server = new LocalAuthorizationServer(new LocalAuthorizationServerOptions());
            using var client = server.Server.CreateClient();

            using var response = await PostFormAsync(client, "/oauth/v2.0/token", new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["grant_type"] = "client_credentials",
                ["client_id"] = "daemon",
                ["client_secret"] = "not-the-secret",
                ["scope"] = "read"
            });

            response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

            using var body = await ReadJsonAsync(response);

            body.RootElement.GetProperty("error").GetString().Should().Be("invalid_client");
        }

        /// <summary>
        /// A device code grant stays pending until it is approved, then issues an access token and a refresh token.
        /// </summary>
        /// <returns>
        /// A task that completes when the test has run.
        /// </returns>
        [TestMethod]
        public async Task DeviceCode_PendingUntilApproved_ThenIssuesRefreshToken()
        {
            using var server = new LocalAuthorizationServer(new LocalAuthorizationServerOptions());
            using var client = server.Server.CreateClient();

            using var start = await PostFormAsync(client, "/oauth/v2.0/devicecode", new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["client_id"] = "cli",
                ["scope"] = "read offline_access"
            });

            start.StatusCode.Should().Be(HttpStatusCode.OK);

            using var device = await ReadJsonAsync(start);

            var deviceCode = device.RootElement.GetProperty("device_code").GetString()!;
            var userCode = device.RootElement.GetProperty("user_code").GetString()!;

            userCode.Should().HaveLength(8);
            device.RootElement.GetProperty("interval").GetInt32().Should().Be(1);
            device.RootElement.GetProperty("expires_in").GetInt32().Should().Be(300);
            device.RootElement.GetProperty("verification_uri").GetString().Should().Be("http://localhost/oauth/v2.0/device");
            device.RootElement.GetProperty("verification_uri_complete").GetString().Should().EndWith(userCode);

            var poll = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["grant_type"] = DeviceCodeGrantType,
                ["client_id"] = "cli",
                ["device_code"] = deviceCode
            };

            using var pending = await PostFormAsync(client, "/oauth/v2.0/token", poll);

            pending.StatusCode.Should().Be(HttpStatusCode.BadRequest);

            using var pendingBody = await ReadJsonAsync(pending);

            pendingBody.RootElement.GetProperty("error").GetString().Should().Be("authorization_pending");

            server.Approve(userCode);

            using var granted = await PostFormAsync(client, "/oauth/v2.0/token", poll);

            granted.StatusCode.Should().Be(HttpStatusCode.OK);

            using var tokens = await ReadJsonAsync(granted);

            tokens.RootElement.GetProperty("access_token").GetString().Should().NotBeNullOrWhiteSpace();
            tokens.RootElement.GetProperty("refresh_token").GetString().Should().NotBeNullOrWhiteSpace();
            tokens.RootElement.GetProperty("scope").GetString().Should().Be("read offline_access");
            server.HitCount("token:" + DeviceCodeGrantType).Should().Be(2);
            server.HitCount("devicecode").Should().Be(1);
        }

        /// <summary>
        /// A device code grant that never asked for offline access receives no refresh token.
        /// </summary>
        /// <returns>
        /// A task that completes when the test has run.
        /// </returns>
        [TestMethod]
        public async Task DeviceCode_WithoutOfflineAccess_NoRefreshToken()
        {
            using var server = new LocalAuthorizationServer(new LocalAuthorizationServerOptions());
            using var client = server.Server.CreateClient();

            using var start = await PostFormAsync(client, "/oauth/v2.0/devicecode", new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["client_id"] = "cli",
                ["scope"] = "read"
            });

            using var device = await ReadJsonAsync(start);

            var deviceCode = device.RootElement.GetProperty("device_code").GetString()!;
            var userCode = device.RootElement.GetProperty("user_code").GetString()!;

            using var approval = await client.GetAsync($"/oauth/v2.0/device?user_code={userCode}");

            approval.StatusCode.Should().Be(HttpStatusCode.OK);
            (await approval.Content.ReadAsStringAsync()).Should().Be("approved");
            server.HitCount("device-approve").Should().Be(1);

            using var granted = await PostFormAsync(client, "/oauth/v2.0/token", new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["grant_type"] = DeviceCodeGrantType,
                ["client_id"] = "cli",
                ["device_code"] = deviceCode
            });

            granted.StatusCode.Should().Be(HttpStatusCode.OK);

            using var tokens = await ReadJsonAsync(granted);

            tokens.RootElement.GetProperty("access_token").GetString().Should().NotBeNullOrWhiteSpace();
            tokens.RootElement.TryGetProperty("refresh_token", out _).Should().BeFalse();
        }

        /// <summary>
        /// The legacy document advertises a token endpoint that rejects every request.
        /// </summary>
        /// <returns>
        /// A task that completes when the test has run.
        /// </returns>
        [TestMethod]
        public async Task Metadata_V1Document_TokenEndpointRejects()
        {
            using var server = new LocalAuthorizationServer(new LocalAuthorizationServerOptions());
            using var client = server.Server.CreateClient();

            using var metadata = await client.GetAsync("/oauth/.well-known/openid-configuration");

            metadata.StatusCode.Should().Be(HttpStatusCode.OK);

            using var document = await ReadJsonAsync(metadata);

            document.RootElement.GetProperty("issuer").GetString().Should().Be("http://localhost/oauth");
            document.RootElement.GetProperty("token_endpoint").GetString().Should().Be("http://localhost/oauth/token");
            server.HitCount("metadata-v1").Should().Be(1);

            using var rejected = await PostFormAsync(client, "/oauth/token", new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["grant_type"] = "client_credentials",
                ["client_id"] = "daemon",
                ["client_secret"] = "daemon-secret"
            });

            rejected.StatusCode.Should().Be(HttpStatusCode.BadRequest);

            using var body = await ReadJsonAsync(rejected);

            body.RootElement.GetProperty("error").GetString().Should().Be("invalid_request");
            body.RootElement.GetProperty("error_description").GetString().Should().Be("v1 endpoint");
            server.HitCount("token").Should().Be(0);
        }

        /// <summary>
        /// The versioned OpenID Connect and RFC 8414 documents are identical and point at the versioned token endpoint.
        /// </summary>
        /// <returns>
        /// A task that completes when the test has run.
        /// </returns>
        [TestMethod]
        public async Task Metadata_V2Document_HasTokenEndpointUnderV2()
        {
            using var server = new LocalAuthorizationServer(new LocalAuthorizationServerOptions());
            using var client = server.Server.CreateClient();

            using var openIdConfiguration = await client.GetAsync("/oauth/v2.0/.well-known/openid-configuration");
            using var authorizationServerMetadata = await client.GetAsync("/oauth/v2.0/.well-known/oauth-authorization-server");
            using var canonicalMetadata = await client.GetAsync("/.well-known/oauth-authorization-server/oauth/v2.0");

            openIdConfiguration.StatusCode.Should().Be(HttpStatusCode.OK);
            authorizationServerMetadata.StatusCode.Should().Be(HttpStatusCode.OK);
            canonicalMetadata.StatusCode.Should().Be(HttpStatusCode.OK);

            var openIdBody = await openIdConfiguration.Content.ReadAsStringAsync();

            openIdBody.Should().Be(await authorizationServerMetadata.Content.ReadAsStringAsync());
            openIdBody.Should().Be(await canonicalMetadata.Content.ReadAsStringAsync());

            using var document = JsonDocument.Parse(openIdBody);

            document.RootElement.GetProperty("issuer").GetString().Should().Be("http://localhost/oauth/v2.0");
            document.RootElement.GetProperty("token_endpoint").GetString().Should().Be("http://localhost/oauth/v2.0/token");
            document.RootElement.GetProperty("token_endpoint").GetString().Should().Contain("/v2.0/");
            document.RootElement.GetProperty("device_authorization_endpoint").GetString().Should().Be("http://localhost/oauth/v2.0/devicecode");
            document.RootElement.GetProperty("authorization_response_iss_parameter_supported").GetBoolean().Should().BeTrue();
            document.RootElement.GetProperty("code_challenge_methods_supported").EnumerateArray().Should().ContainSingle(x => x.GetString() == "S256");
            document.RootElement.TryGetProperty("registration_endpoint", out _).Should().BeFalse();
            server.HitCount("metadata-v2").Should().Be(1);
            server.HitCount("metadata-rfc8414-v2").Should().Be(2);
        }

        /// <summary>
        /// Each protected resource metadata mode produces the documented status, headers, and body.
        /// </summary>
        /// <returns>
        /// A task that completes when the test has run.
        /// </returns>
        [TestMethod]
        public async Task Prm_Modes_ReturnExpected()
        {
            using (var server = new LocalAuthorizationServer(new LocalAuthorizationServerOptions()))
            {
                using var client = server.Server.CreateClient();
                using var response = await client.GetAsync("/.well-known/oauth-protected-resource/odata");

                response.StatusCode.Should().Be(HttpStatusCode.OK);

                using var document = await ReadJsonAsync(response);

                document.RootElement.GetProperty("resource").GetString().Should().Be("http://localhost");
                document.RootElement.GetProperty("authorization_servers").EnumerateArray().Should().ContainSingle(x => x.GetString() == "http://localhost/oauth/v2.0");
                document.RootElement.GetProperty("bearer_methods_supported").EnumerateArray().Should().ContainSingle(x => x.GetString() == "header");
                server.HitCount("prm").Should().Be(1);
            }

            using (var server = new LocalAuthorizationServer(new LocalAuthorizationServerOptions { ProtectedResourceMetadataMode = ProtectedResourceMetadataMode.Unauthorized }))
            {
                using var client = server.Server.CreateClient();
                using var response = await client.GetAsync("/.well-known/oauth-protected-resource");

                response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

                var challenge = string.Join(' ', response.Headers.WwwAuthenticate);

                challenge.Should().Contain("authorization_uri=\"http://localhost/oauth/v2.0/authorize\"");
                challenge.Should().Contain("client_id=\"00000003-0000-0000-c000-000000000000\"");
                (await response.Content.ReadAsStringAsync()).Should().Contain("InvalidAuthenticationToken");
            }

            using (var server = new LocalAuthorizationServer(new LocalAuthorizationServerOptions { ProtectedResourceMetadataMode = ProtectedResourceMetadataMode.NotFound }))
            {
                using var client = server.Server.CreateClient();
                using var response = await client.GetAsync("/.well-known/oauth-protected-resource/odata");

                response.StatusCode.Should().Be(HttpStatusCode.NotFound);
            }
        }

        /// <summary>
        /// The refresh token grant rotates the token and rejects the one it replaced.
        /// </summary>
        /// <returns>
        /// A task that completes when the test has run.
        /// </returns>
        [TestMethod]
        public async Task RefreshToken_Rotates_OldOneRejected()
        {
            using var server = new LocalAuthorizationServer(new LocalAuthorizationServerOptions
            {
                PendingPollsBeforeSuccess = 0,
                RequireApproval = false
            });
            using var client = server.Server.CreateClient();

            using var start = await PostFormAsync(client, "/oauth/v2.0/devicecode", new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["client_id"] = "cli",
                ["scope"] = "read offline_access"
            });

            using var device = await ReadJsonAsync(start);

            using var initial = await PostFormAsync(client, "/oauth/v2.0/token", new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["grant_type"] = DeviceCodeGrantType,
                ["client_id"] = "cli",
                ["device_code"] = device.RootElement.GetProperty("device_code").GetString()!
            });

            initial.StatusCode.Should().Be(HttpStatusCode.OK);

            using var initialTokens = await ReadJsonAsync(initial);

            var originalRefreshToken = initialTokens.RootElement.GetProperty("refresh_token").GetString()!;
            var refresh = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["grant_type"] = "refresh_token",
                ["client_id"] = "cli",
                ["refresh_token"] = originalRefreshToken
            };

            using var rotated = await PostFormAsync(client, "/oauth/v2.0/token", refresh);

            rotated.StatusCode.Should().Be(HttpStatusCode.OK);

            using var rotatedTokens = await ReadJsonAsync(rotated);

            rotatedTokens.RootElement.GetProperty("refresh_token").GetString().Should().NotBe(originalRefreshToken);
            rotatedTokens.RootElement.GetProperty("access_token").GetString().Should().NotBeNullOrWhiteSpace();

            using var replayed = await PostFormAsync(client, "/oauth/v2.0/token", refresh);

            replayed.StatusCode.Should().Be(HttpStatusCode.BadRequest);

            using var replayedBody = await ReadJsonAsync(replayed);

            replayedBody.RootElement.GetProperty("error").GetString().Should().Be("invalid_grant");
            server.HitCount("token:refresh_token").Should().Be(2);
        }

        /// <summary>
        /// Dynamic client registration is unreachable until it is enabled, and then issues a usable client identifier.
        /// </summary>
        /// <returns>
        /// A task that completes when the test has run.
        /// </returns>
        [TestMethod]
        public async Task Register_WhenDisabled_404_WhenEnabled_IssuesClientId()
        {
            const string RegistrationRequest = "{\"redirect_uris\":[\"http://127.0.0.1:8765/callback\"],\"grant_types\":[\"authorization_code\"]}";

            using (var server = new LocalAuthorizationServer(new LocalAuthorizationServerOptions()))
            {
                using var client = server.Server.CreateClient();
                using var content = new StringContent(RegistrationRequest, Encoding.UTF8, "application/json");
                using var response = await client.PostAsync("/oauth/v2.0/register", content);

                response.StatusCode.Should().Be(HttpStatusCode.NotFound);
                server.HitCount("register").Should().Be(1);
            }

            using (var server = new LocalAuthorizationServer(new LocalAuthorizationServerOptions { EnableDynamicClientRegistration = true }))
            {
                using var client = server.Server.CreateClient();
                using var content = new StringContent(RegistrationRequest, Encoding.UTF8, "application/json");
                using var response = await client.PostAsync("/oauth/v2.0/register", content);

                response.IsSuccessStatusCode.Should().BeTrue();

                using var registration = await ReadJsonAsync(response);

                registration.RootElement.GetProperty("client_id").GetString().Should().StartWith("dcr-");
                registration.RootElement.GetProperty("token_endpoint_auth_method").GetString().Should().Be("none");
                registration.RootElement.GetProperty("client_id_issued_at").GetInt64().Should().BeGreaterThan(0);
                registration.RootElement.GetProperty("redirect_uris").EnumerateArray().Should().ContainSingle(x => x.GetString() == RedirectUri);
                registration.RootElement.GetProperty("grant_types").EnumerateArray().Should().ContainSingle(x => x.GetString() == "authorization_code");

                using var metadata = await client.GetAsync("/oauth/v2.0/.well-known/openid-configuration");
                using var document = await ReadJsonAsync(metadata);

                document.RootElement.GetProperty("registration_endpoint").GetString().Should().Be("http://localhost/oauth/v2.0/register");
            }
        }

        #endregion

        #region Internal Methods

        /// <summary>
        /// Posts a form-encoded body.
        /// </summary>
        /// <param name="client">The client to post with.</param>
        /// <param name="path">The path to post to.</param>
        /// <param name="fields">The form fields.</param>
        /// <returns>
        /// The response.
        /// </returns>
        internal static async Task<HttpResponseMessage> PostFormAsync(HttpClient client, string path, IEnumerable<KeyValuePair<string, string>> fields)
        {
            using var content = new FormUrlEncodedContent(fields);

            return await client.PostAsync(path, content);
        }

        /// <summary>
        /// Reads a response body as a JSON document.
        /// </summary>
        /// <param name="response">The response to read.</param>
        /// <returns>
        /// The parsed document.
        /// </returns>
        internal static async Task<JsonDocument> ReadJsonAsync(HttpResponseMessage response)
        {
            return JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        }

        #endregion

    }

}
