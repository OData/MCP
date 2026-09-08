// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Net.Http;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;

namespace Microsoft.OData.Mcp.Tests.Shared.Authentication
{

    /// <summary>
    /// A real, in-process OAuth 2.0 / OpenID Connect authorization server hosted on an ASP.NET Core
    /// <see cref="TestServer"/>, used to exercise the outbound OAuth client end to end.
    /// </summary>
    /// <example>
    /// <code>
    /// using var server = new LocalAuthorizationServer(new LocalAuthorizationServerOptions());
    /// using var client = server.Server.CreateClient();
    ///
    /// var document = await client.GetStringAsync("/oauth/v2.0/.well-known/openid-configuration");
    /// </code>
    /// </example>
    /// <remarks>
    /// This is deliberately not an <see cref="System.Net.Http.HttpMessageHandler"/> fake: every grant is served by real
    /// routing, real form parsing, and real HMAC-signed JSON Web Tokens, so the client under test cannot pass by
    /// accident. Both the authorization server and the protected resource share the <c>http://localhost</c> origin that
    /// <see cref="TestServer"/> defaults to; the versioned issuer lives under <c>/oauth/v2.0</c> while the legacy
    /// document under <c>/oauth</c> advertises a token endpoint that rejects everything.
    /// </remarks>
    public sealed class LocalAuthorizationServer : IDisposable
    {

        #region Fields

        /// <summary>
        /// The client identifier the enterprise identity provider endpoint authenticates the RFC 8693 token
        /// exchange with.
        /// </summary>
        public const string IdpClientId = "idp-client";

        /// <summary>
        /// The client secret that pairs with <see cref="IdpClientId"/>.
        /// </summary>
        public const string IdpClientSecret = "idp-secret";

        /// <summary>
        /// The RFC 8693 grant type the identity provider token endpoint exchanges an id token at.
        /// </summary>
        internal const string GrantTypeTokenExchange = "urn:ietf:params:oauth:grant-type:token-exchange";

        /// <summary>
        /// The RFC 7523 grant type the token endpoint redeems an identity assertion at.
        /// </summary>
        internal const string GrantTypeJwtBearer = "urn:ietf:params:oauth:grant-type:jwt-bearer";

        /// <summary>
        /// The token type URN of an Identity Assertion JWT Authorization Grant.
        /// </summary>
        internal const string TokenTypeIdJag = "urn:ietf:params:oauth:token-type:id-jag";

        /// <summary>
        /// The token type URN of an OpenID Connect id token.
        /// </summary>
        internal const string TokenTypeIdToken = "urn:ietf:params:oauth:token-type:id_token";

        /// <summary>
        /// The <c>token_type</c> RFC 8693 section 2.2.1 requires for an issued token that is not an access
        /// token.
        /// </summary>
        internal const string TokenTypeNotApplicable = "N_A";

        /// <summary>
        /// The client identifier Microsoft Graph advertises on its protected resource metadata challenge.
        /// </summary>
        internal const string GraphClientId = "00000003-0000-0000-c000-000000000000";

        /// <summary>
        /// The scope value that makes an issued token carry a refresh token.
        /// </summary>
        internal const string OfflineAccessScope = "offline_access";

        /// <summary>
        /// The alphabet RFC 8628 user codes are drawn from.
        /// </summary>
        internal const string UserCodeAlphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";

        /// <summary>
        /// The grant types both discovery documents advertise.
        /// </summary>
        internal static readonly string[] SupportedGrantTypes =
        [
            "authorization_code",
            "client_credentials",
            "refresh_token",
            "urn:ietf:params:oauth:grant-type:device_code",
            "urn:ietf:params:oauth:grant-type:jwt-bearer"
        ];

        /// <summary>
        /// The authorization codes issued by the authorize endpoint and not yet redeemed, keyed by code.
        /// </summary>
        internal readonly ConcurrentDictionary<string, AuthorizationCodeGrantState> _authorizationCodes = new(StringComparer.Ordinal);

        /// <summary>
        /// The known clients, keyed by client identifier; a <see langword="null"/> value marks a public client.
        /// </summary>
        internal readonly ConcurrentDictionary<string, string?> _clients = new(StringComparer.Ordinal);

        /// <summary>
        /// The device code grants issued by the device authorization endpoint, keyed by device code.
        /// </summary>
        internal readonly ConcurrentDictionary<string, DeviceCodeGrantState> _deviceCodes = new(StringComparer.Ordinal);

        /// <summary>
        /// The per-endpoint hit counters exposed by <see cref="HitCount(string)"/>.
        /// </summary>
        internal readonly ConcurrentDictionary<string, int> _hits = new(StringComparer.Ordinal);

        /// <summary>
        /// The generic host that owns <see cref="Server"/>.
        /// </summary>
        internal readonly IHost _host;

        /// <summary>
        /// The <c>jti</c> of every access token this instance has minted.
        /// </summary>
        internal readonly ConcurrentDictionary<string, byte> _issuedTokenIds = new(StringComparer.Ordinal);

        /// <summary>
        /// Guards <see cref="_lastDeviceAuthorizationRequest"/> against concurrent device authorization posts.
        /// </summary>
        internal readonly object _lastDeviceAuthorizationRequestLock = new();

        /// <summary>
        /// Guards <see cref="_lastTokenRequest"/> against concurrent token posts.
        /// </summary>
        internal readonly object _lastTokenRequestLock = new();

        /// <summary>
        /// The refresh tokens that have been issued and not yet rotated, keyed by refresh token.
        /// </summary>
        internal readonly ConcurrentDictionary<string, (string Audience, string ClientId, string Scope)> _refreshTokens = new(StringComparer.Ordinal);

        /// <summary>
        /// The <c>jti</c> of every access token <see cref="RevokeIssuedTokens"/> has invalidated.
        /// </summary>
        internal readonly ConcurrentDictionary<string, byte> _revokedTokenIds = new(StringComparer.Ordinal);

        /// <summary>
        /// The form fields of the most recent <c>POST</c> to the device authorization endpoint.
        /// </summary>
        internal IReadOnlyDictionary<string, string>? _lastDeviceAuthorizationRequest;

        /// <summary>
        /// The form fields of the most recent <c>POST</c> to the versioned token endpoint.
        /// </summary>
        internal IReadOnlyDictionary<string, string>? _lastTokenRequest;

        /// <summary>
        /// Whether the one-shot <c>slow_down</c> response has already been served.
        /// </summary>
        internal bool _slowDownIssued;

        #endregion

        #region Properties

        /// <summary>
        /// Gets the versioned authorization endpoint.
        /// </summary>
        /// <value>
        /// <c>http://localhost/oauth/v2.0/authorize</c>.
        /// </value>
        public Uri AuthorizationEndpoint { get; }

        /// <summary>
        /// Gets the RFC 8628 device authorization endpoint.
        /// </summary>
        /// <value>
        /// <c>http://localhost/oauth/v2.0/devicecode</c>.
        /// </value>
        public Uri DeviceAuthorizationEndpoint { get; }

        /// <summary>
        /// Gets a shared message handler that dispatches straight into the hosted authorization server.
        /// </summary>
        public HttpMessageHandler Handler { get; }

        /// <summary>
        /// Gets the issuer identifier of the enterprise identity provider this instance also plays.
        /// </summary>
        /// <remarks>
        /// The identity provider shares this instance's signing key, so the same fixture can mint the id token,
        /// exchange it for an identity assertion, and then validate that assertion at its own token endpoint.
        /// </remarks>
        public Uri IdpIssuer { get; }

        /// <summary>
        /// Gets the enterprise identity provider's RFC 8693 token exchange endpoint.
        /// </summary>
        public Uri IdpTokenEndpoint { get; }

        /// <summary>
        /// Gets the versioned issuer.
        /// </summary>
        /// <value>
        /// <c>http://localhost/oauth/v2.0</c>.
        /// </value>
        public Uri Issuer { get; }

        /// <summary>
        /// Gets the form fields of the most recent <c>POST</c> to the device authorization endpoint.
        /// </summary>
        /// <value>
        /// <see langword="null"/> until the device authorization endpoint has been called at least once.
        /// </value>
        /// <remarks>
        /// Tests use this to prove which <c>client_id</c> and <c>scope</c> the client actually sent — the client
        /// id metadata document case, above all, where the client id is a URI rather than a registered
        /// identifier.
        /// </remarks>
        public IReadOnlyDictionary<string, string>? LastDeviceAuthorizationRequest
        {
            get
            {
                lock (_lastDeviceAuthorizationRequestLock)
                {
                    return _lastDeviceAuthorizationRequest;
                }
            }
        }

        /// <summary>
        /// Gets the form fields of the most recent <c>POST</c> to the versioned token endpoint.
        /// </summary>
        /// <value>
        /// <see langword="null"/> until the token endpoint has been called at least once.
        /// </value>
        /// <remarks>
        /// Tests use this to prove which <c>resource</c>, <c>scope</c>, and <c>grant_type</c> the client actually sent.
        /// </remarks>
        public IReadOnlyDictionary<string, string>? LastTokenRequest
        {
            get
            {
                lock (_lastTokenRequestLock)
                {
                    return _lastTokenRequest;
                }
            }
        }

        /// <summary>
        /// Gets the legacy (v1) issuer.
        /// </summary>
        /// <value>
        /// <c>http://localhost/oauth</c>.
        /// </value>
        /// <remarks>
        /// The token endpoint this document advertises always fails, so a client that selects the legacy document
        /// instead of the versioned one is caught immediately.
        /// </remarks>
        public Uri LegacyIssuer { get; }

        /// <summary>
        /// Gets the options this server was built from.
        /// </summary>
        public LocalAuthorizationServerOptions Options { get; }

        /// <summary>
        /// Gets the RFC 9728 protected resource metadata URI for the OData resource.
        /// </summary>
        /// <value>
        /// <c>http://localhost/.well-known/oauth-protected-resource/odata</c>.
        /// </value>
        public Uri ProtectedResourceMetadataUri { get; }

        /// <summary>
        /// Gets the issuer identifier the versioned discovery document publishes and the authorization response
        /// echoes as its RFC 9207 <c>iss</c>.
        /// </summary>
        /// <value>
        /// <c>http://localhost</c> when <see cref="LocalAuthorizationServerOptions.AuthorityOnlyIssuer"/> is
        /// set; otherwise <see cref="Issuer"/> spelled exactly as this instance publishes it.
        /// </value>
        /// <remarks>
        /// Access tokens are still minted under <see cref="Issuer"/> and
        /// <see cref="CreateTokenValidationParameters"/> still trusts that issuer, so turning the option on
        /// changes only what a client compares its <c>iss</c> against.
        /// </remarks>
        public string PublishedIssuer
        {
            get
            {
                return Options.AuthorityOnlyIssuer ? "http://localhost" : Issuer.ToString();
            }
        }

        /// <summary>
        /// Gets the RFC 7591 dynamic client registration endpoint.
        /// </summary>
        /// <value>
        /// <c>http://localhost/oauth/v2.0/register</c>.
        /// </value>
        /// <remarks>
        /// Only advertised, and only reachable, when <see cref="LocalAuthorizationServerOptions.EnableDynamicClientRegistration"/> is set.
        /// </remarks>
        public Uri RegistrationEndpoint { get; }

        /// <summary>
        /// Gets the hosted test server.
        /// </summary>
        public TestServer Server { get; }

        /// <summary>
        /// Gets the 256-bit HMAC key this instance signs access tokens with.
        /// </summary>
        /// <remarks>
        /// The key is randomly generated per instance, so a token minted by one server never validates against another.
        /// </remarks>
        public byte[] SigningKey { get; }

        /// <summary>
        /// Gets the versioned token endpoint.
        /// </summary>
        /// <value>
        /// <c>http://localhost/oauth/v2.0/token</c>.
        /// </value>
        public Uri TokenEndpoint { get; }

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="LocalAuthorizationServer"/> class and starts hosting it.
        /// </summary>
        /// <param name="options">The behavior switches for this instance.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="options"/> is <see langword="null"/>.</exception>
        /// <remarks>
        /// The clients <c>daemon</c> (secret <c>daemon-secret</c>), <c>cli</c> (public), and
        /// <see cref="IdpClientId"/> (secret <see cref="IdpClientSecret"/>) are seeded before the first request
        /// is served.
        /// </remarks>
        public LocalAuthorizationServer(LocalAuthorizationServerOptions options)
        {
            ArgumentNullException.ThrowIfNull(options);

            Options = options;
            SigningKey = RandomNumberGenerator.GetBytes(32);

            LegacyIssuer = new Uri("http://localhost/oauth");
            Issuer = new Uri("http://localhost/oauth/v2.0");
            AuthorizationEndpoint = new Uri("http://localhost/oauth/v2.0/authorize");
            DeviceAuthorizationEndpoint = new Uri("http://localhost/oauth/v2.0/devicecode");
            TokenEndpoint = new Uri("http://localhost/oauth/v2.0/token");
            RegistrationEndpoint = new Uri("http://localhost/oauth/v2.0/register");
            ProtectedResourceMetadataUri = new Uri("http://localhost/.well-known/oauth-protected-resource/odata");
            IdpIssuer = new Uri("http://localhost/oauth/v2.0/idp");
            IdpTokenEndpoint = new Uri("http://localhost/oauth/v2.0/idp/token");

            RegisterClient("cli", null);
            RegisterClient("daemon", "daemon-secret");
            RegisterClient(IdpClientId, IdpClientSecret);

            _host = new HostBuilder()
                .ConfigureWebHost(web =>
                {
                    web.UseTestServer();
                    web.ConfigureServices(services => services.AddRouting());
                    web.Configure(app =>
                    {
                        app.UseRouting();
                        app.UseEndpoints(MapEndpoints);
                    });
                })
                .Build();
            _host.Start();

            Server = _host.GetTestServer();
            Handler = Server.CreateHandler();
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Approves the pending device code grant that carries the supplied user code.
        /// </summary>
        /// <param name="userCode">The user code shown to the end user.</param>
        /// <exception cref="ArgumentException">Thrown when <paramref name="userCode"/> is <see langword="null"/>, empty, or whitespace.</exception>
        /// <exception cref="InvalidOperationException">Thrown when no pending grant carries the supplied user code.</exception>
        /// <remarks>
        /// Equivalent to a <c>GET</c> against the <c>verification_uri</c> the device authorization response advertised.
        /// </remarks>
        public void Approve(string userCode)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(userCode);

            var grant = _deviceCodes.Values.FirstOrDefault(x => string.Equals(x.UserCode, userCode, StringComparison.OrdinalIgnoreCase));
            if (grant is null)
            {
                throw new InvalidOperationException($"No pending device code grant carries the user code '{userCode}'.");
            }

            grant.Approved = true;
        }

        /// <summary>
        /// Creates the validation parameters a protected resource needs to accept tokens minted by this instance.
        /// </summary>
        /// <returns>
        /// Validation parameters bound to <see cref="SigningKey"/>, both issuers, and
        /// <see cref="LocalAuthorizationServerOptions.ResourceUri"/> as the only valid audience.
        /// </returns>
        /// <remarks>
        /// Audience validation is deliberately on: a client that sends the OData service path as the <c>resource</c>
        /// indicator receives a token the resource rejects.
        /// </remarks>
        public TokenValidationParameters CreateTokenValidationParameters()
        {
            return new TokenValidationParameters
            {
                ClockSkew = TimeSpan.FromSeconds(5),
                IssuerSigningKey = new SymmetricSecurityKey(SigningKey),
                ValidateAudience = true,
                ValidateIssuer = true,
                ValidateIssuerSigningKey = true,
                ValidateLifetime = true,
                ValidAudience = Options.ResourceUri,
                ValidIssuers = [Issuer.ToString(), LegacyIssuer.ToString()]
            };
        }

        /// <inheritdoc />
        public void Dispose()
        {
            Handler.Dispose();
            _host.Dispose();
        }

        /// <summary>
        /// Gets the number of requests an endpoint has served since this instance was created.
        /// </summary>
        /// <param name="endpoint">One of <c>prm</c>, <c>metadata-v1</c>, <c>metadata-v2</c>, <c>metadata-rfc8414-v2</c>, <c>devicecode</c>, <c>device-approve</c>, <c>authorize</c>, <c>token</c>, <c>token:&lt;grant_type&gt;</c>, <c>token-v1</c>, or <c>register</c>.</param>
        /// <returns>
        /// The hit count, or zero when the endpoint has never been reached.
        /// </returns>
        /// <exception cref="ArgumentException">Thrown when <paramref name="endpoint"/> is <see langword="null"/>, empty, or whitespace.</exception>
        public int HitCount(string endpoint)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(endpoint);

            return _hits.TryGetValue(endpoint, out var count) ? count : 0;
        }

        /// <summary>
        /// Determines whether a validated access token was invalidated by a previous call to
        /// <see cref="RevokeIssuedTokens"/>.
        /// </summary>
        /// <param name="token">The security token the resource's JWT bearer handler validated.</param>
        /// <returns>
        /// <see langword="true"/> when <see cref="LocalAuthorizationServerOptions.RevokeAllTokens"/> is set, or
        /// when the token's <c>jti</c> is one this instance minted before the most recent
        /// <see cref="RevokeIssuedTokens"/> call; otherwise <see langword="false"/>.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="token"/> is <see langword="null"/>.</exception>
        /// <example>
        /// <code>
        /// options.Events = new JwtBearerEvents
        /// {
        ///     OnTokenValidated = context =&gt;
        ///     {
        ///         if (authorizationServer.IsRevoked(context.SecurityToken))
        ///         {
        ///             context.Fail("revoked");
        ///         }
        ///
        ///         return Task.CompletedTask;
        ///     }
        /// };
        /// </code>
        /// </example>
        /// <remarks>
        /// Revocation is tracked by token identity rather than by issue time, so a token minted milliseconds
        /// after a revocation — the one a refresh grant hands back on the <c>invalid_token</c> retry path — is
        /// accepted rather than swept up by a one second clock skew.
        /// </remarks>
        public bool IsRevoked(SecurityToken token)
        {
            ArgumentNullException.ThrowIfNull(token);

            return Options.RevokeAllTokens
                || (!string.IsNullOrWhiteSpace(token.Id) && _revokedTokenIds.ContainsKey(token.Id));
        }

        /// <summary>
        /// Mints a valid access token without running a grant.
        /// </summary>
        /// <param name="scope">The space-delimited scope to stamp on the token.</param>
        /// <returns>
        /// A compact JWT that validates against <see cref="CreateTokenValidationParameters"/>.
        /// </returns>
        /// <exception cref="ArgumentException">Thrown when <paramref name="scope"/> is <see langword="null"/>, empty, or whitespace.</exception>
        /// <remarks>
        /// Used by fixtures that need an authenticated client without exercising the token endpoint.
        /// </remarks>
        public string IssueAccessToken(string scope)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(scope);

            return CreateAccessToken(scope, "fixture", Options.ResourceUri);
        }

        /// <summary>
        /// Mints an OpenID Connect id token this instance's identity provider endpoint will accept as the
        /// subject token of an RFC 8693 token exchange.
        /// </summary>
        /// <param name="subject">The end user the id token identifies.</param>
        /// <returns>
        /// A compact JWT issued by <see cref="IdpIssuer"/> for <see cref="IdpClientId"/>.
        /// </returns>
        /// <exception cref="ArgumentException">Thrown when <paramref name="subject"/> is <see langword="null"/>, empty, or whitespace.</exception>
        /// <example>
        /// <code>
        /// File.WriteAllText(idTokenFile, authorizationServer.IssueIdToken("alice@contoso.com"));
        /// </code>
        /// </example>
        /// <remarks>
        /// Used by a test that has to put a real id token somewhere <c>FileIdTokenCallback</c> can read it,
        /// without walking an interactive single sign-on the fixture does not model.
        /// </remarks>
        public string IssueIdToken(string subject)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(subject);

            return CreateIdToken(subject);
        }

        /// <summary>
        /// Seeds a client the token endpoint will recognize.
        /// </summary>
        /// <param name="clientId">The client identifier.</param>
        /// <param name="clientSecret">The client secret, or <see langword="null"/> for a public client.</param>
        /// <exception cref="ArgumentException">Thrown when <paramref name="clientId"/> is <see langword="null"/>, empty, or whitespace.</exception>
        public void RegisterClient(string clientId, string? clientSecret)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(clientId);

            _clients[clientId] = clientSecret;
        }

        /// <summary>
        /// Invalidates every access token minted so far, so a protected resource wired through
        /// <see cref="SecuredResourceServiceCollectionExtensions.AddSecuredResource(Microsoft.Extensions.DependencyInjection.IServiceCollection, LocalAuthorizationServer, string)"/>
        /// answers them with <c>401</c> and <c>error="invalid_token"</c>.
        /// </summary>
        /// <example>
        /// <code>
        /// authorizationServer.RevokeIssuedTokens();
        ///
        /// // The next OData call sees 401 invalid_token and the handler refreshes once, then retries.
        /// </code>
        /// </example>
        /// <remarks>
        /// Tokens minted after this call — including the one a <c>refresh_token</c> grant issues on the retry
        /// path — stay valid, which is what makes the <c>invalid_token</c> refresh-and-retry path testable.
        /// Refresh tokens are untouched: revocation models a resource server that rejects a still-unexpired
        /// access token, not a session the authorization server has torn down.
        /// </remarks>
        public void RevokeIssuedTokens()
        {
            foreach (var tokenId in _issuedTokenIds.Keys)
            {
                _revokedTokenIds[tokenId] = 0;
            }
        }

        #endregion

        #region Internal Methods

        /// <summary>
        /// Mints an HMAC-signed access token.
        /// </summary>
        /// <param name="scope">The space-delimited scope to stamp on the token.</param>
        /// <param name="subject">The subject claim, normally the client identifier.</param>
        /// <param name="audience">The audience claim.</param>
        /// <returns>
        /// The compact JWT.
        /// </returns>
        internal string CreateAccessToken(string scope, string subject, string audience)
        {
            var tokenId = Guid.NewGuid().ToString("N");
            var handler = new JwtSecurityTokenHandler();
            var descriptor = new SecurityTokenDescriptor
            {
                Audience = audience,
                Expires = DateTime.UtcNow.Add(Options.AccessTokenLifetime),
                Issuer = Issuer.ToString(),
                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(SigningKey), SecurityAlgorithms.HmacSha256),
                Subject = new ClaimsIdentity(
                [
                    new Claim("sub", string.IsNullOrWhiteSpace(subject) ? "anonymous" : subject),
                    new Claim("jti", tokenId),
                    new Claim("scope", scope)
                ])
            };
            _issuedTokenIds[tokenId] = 0;

            return handler.WriteToken(handler.CreateToken(descriptor));
        }

        /// <summary>
        /// Builds the versioned RFC 8414 / OpenID Connect discovery document.
        /// </summary>
        /// <returns>
        /// The document, ready to serialize.
        /// </returns>
        internal IReadOnlyDictionary<string, object> CreateAuthorizationServerDocument()
        {
            var document = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["issuer"] = PublishedIssuer,
                ["authorization_endpoint"] = AuthorizationEndpoint.ToString(),
                ["token_endpoint"] = TokenEndpoint.ToString(),
                ["device_authorization_endpoint"] = DeviceAuthorizationEndpoint.ToString(),
                ["grant_types_supported"] = SupportedGrantTypes,
                ["code_challenge_methods_supported"] = new[] { "S256" },
                ["token_endpoint_auth_methods_supported"] = new[] { "client_secret_post", "client_secret_basic", "none" },
                ["scopes_supported"] = Options.Scopes.ToArray(),
                ["response_types_supported"] = new[] { "code" },
                ["authorization_response_iss_parameter_supported"] = true
            };

            if (Options.AdvertiseClientIdMetadataDocument)
            {
                document["client_id_metadata_document_supported"] = true;
            }

            if (Options.EnableDynamicClientRegistration)
            {
                document["registration_endpoint"] = RegistrationEndpoint.ToString();
            }

            return document;
        }

        /// <summary>
        /// Computes the RFC 7636 <c>S256</c> code challenge for a code verifier.
        /// </summary>
        /// <param name="codeVerifier">The code verifier.</param>
        /// <returns>
        /// The base64url-encoded SHA-256 hash of the verifier.
        /// </returns>
        internal static string CreateCodeChallenge(string codeVerifier)
        {
            return Base64UrlEncoder.Encode(SHA256.HashData(Encoding.ASCII.GetBytes(codeVerifier)));
        }

        /// <summary>
        /// Mints an Identity Assertion JWT Authorization Grant for an exchanged id token.
        /// </summary>
        /// <param name="subject">The end user the assertion carries forward from the id token.</param>
        /// <param name="audience">The authorization server the assertion may be redeemed at.</param>
        /// <param name="scope">The requested space-delimited scope, or an empty string when the exchange requested none.</param>
        /// <returns>
        /// The compact JWT.
        /// </returns>
        internal string CreateIdentityAssertion(string subject, string audience, string scope)
        {
            var handler = new JwtSecurityTokenHandler();
            var claims = new List<Claim>
            {
                new("sub", subject),
                new("jti", Guid.NewGuid().ToString("N"))
            };

            if (!string.IsNullOrWhiteSpace(scope))
            {
                claims.Add(new Claim("scope", scope));
            }

            var descriptor = new SecurityTokenDescriptor
            {
                Audience = audience,
                Expires = DateTime.UtcNow.AddMinutes(5),
                Issuer = IdpIssuer.ToString(),
                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(SigningKey), SecurityAlgorithms.HmacSha256),
                Subject = new ClaimsIdentity(claims)
            };

            return handler.WriteToken(handler.CreateToken(descriptor));
        }

        /// <summary>
        /// Mints an OpenID Connect id token for the enterprise identity provider.
        /// </summary>
        /// <param name="subject">The end user the id token identifies.</param>
        /// <returns>
        /// The compact JWT.
        /// </returns>
        internal string CreateIdToken(string subject)
        {
            var handler = new JwtSecurityTokenHandler();
            var descriptor = new SecurityTokenDescriptor
            {
                Audience = IdpClientId,
                Expires = DateTime.UtcNow.AddHours(1),
                Issuer = IdpIssuer.ToString(),
                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(SigningKey), SecurityAlgorithms.HmacSha256),
                Subject = new ClaimsIdentity(
                [
                    new Claim("sub", subject),
                    new Claim("jti", Guid.NewGuid().ToString("N"))
                ])
            };

            return handler.WriteToken(handler.CreateToken(descriptor));
        }

        /// <summary>
        /// Creates an eight character upper-case alphanumeric user code.
        /// </summary>
        /// <returns>
        /// The user code.
        /// </returns>
        internal static string CreateUserCode()
        {
            var buffer = new char[8];
            for (var index = 0; index < buffer.Length; index++)
            {
                buffer[index] = UserCodeAlphabet[RandomNumberGenerator.GetInt32(UserCodeAlphabet.Length)];
            }

            return new string(buffer);
        }

        /// <summary>
        /// Redeems an authorization code, enforcing redirect URI equality and the PKCE challenge.
        /// </summary>
        /// <param name="context">The request being served.</param>
        /// <param name="fields">The posted form fields.</param>
        /// <returns>
        /// A task that completes when the response has been written.
        /// </returns>
        internal Task HandleAuthorizationCodeGrantAsync(HttpContext context, IReadOnlyDictionary<string, string> fields)
        {
            var code = ReadField(fields, "code");
            if (string.IsNullOrWhiteSpace(code) || !_authorizationCodes.TryGetValue(code, out var grant))
            {
                return WriteErrorAsync(context, StatusCodes.Status400BadRequest, "invalid_grant", "unknown authorization code");
            }

            if (!string.Equals(grant.RedirectUri, ReadField(fields, "redirect_uri"), StringComparison.Ordinal))
            {
                return WriteErrorAsync(context, StatusCodes.Status400BadRequest, "invalid_grant", "redirect_uri does not match the authorization request");
            }

            var codeVerifier = ReadField(fields, "code_verifier");
            if (string.IsNullOrWhiteSpace(codeVerifier) || !string.Equals(CreateCodeChallenge(codeVerifier), grant.CodeChallenge, StringComparison.Ordinal))
            {
                return WriteErrorAsync(context, StatusCodes.Status400BadRequest, "invalid_grant", "code_verifier does not match the code_challenge");
            }

            _authorizationCodes.TryRemove(code, out _);

            return WriteTokensAsync(context, grant.Scope, grant.ClientId, ResolveAudience(fields), allowRefreshToken: true);
        }

        /// <summary>
        /// Serves the versioned RFC 8414 authorization server metadata document.
        /// </summary>
        /// <param name="context">The request being served.</param>
        /// <returns>
        /// A task that completes when the response has been written.
        /// </returns>
        /// <remarks>
        /// Reachable at both the RFC 8414 §3.1 canonical form, which inserts the well-known segment between host and
        /// issuer path (<c>/.well-known/oauth-authorization-server/oauth/v2.0</c>), and the path-appended form some
        /// deployments publish (<c>/oauth/v2.0/.well-known/oauth-authorization-server</c>). Both share the
        /// <c>metadata-rfc8414-v2</c> counter.
        /// </remarks>
        internal Task HandleAuthorizationServerMetadataAsync(HttpContext context)
        {
            RecordHit("metadata-rfc8414-v2");

            return WriteJsonAsync(context, StatusCodes.Status200OK, CreateAuthorizationServerDocument());
        }

        /// <summary>
        /// Serves the authorization endpoint, auto-approving a well-formed PKCE request.
        /// </summary>
        /// <param name="context">The request being served.</param>
        /// <returns>
        /// A task that completes when the response has been written.
        /// </returns>
        internal Task HandleAuthorizeAsync(HttpContext context)
        {
            RecordHit("authorize");

            var query = context.Request.Query;
            var clientId = query["client_id"].ToString();
            var codeChallenge = query["code_challenge"].ToString();
            var redirectUri = query["redirect_uri"].ToString();
            var state = query["state"].ToString();

            if (!string.Equals(query["response_type"].ToString(), "code", StringComparison.Ordinal)
                || !string.Equals(query["code_challenge_method"].ToString(), "S256", StringComparison.Ordinal)
                || string.IsNullOrWhiteSpace(clientId)
                || string.IsNullOrWhiteSpace(codeChallenge)
                || string.IsNullOrWhiteSpace(redirectUri)
                || string.IsNullOrWhiteSpace(state))
            {
                return WriteErrorAsync(context, StatusCodes.Status400BadRequest, "invalid_request", "the authorization request is missing a required parameter");
            }

            var grant = new AuthorizationCodeGrantState
            {
                ClientId = clientId,
                Code = Guid.NewGuid().ToString("N"),
                CodeChallenge = codeChallenge,
                RedirectUri = redirectUri,
                Resource = query["resource"].ToString(),
                Scope = query["scope"].ToString()
            };
            _authorizationCodes[grant.Code] = grant;

            var separator = redirectUri.Contains('?') ? "&" : "?";
            context.Response.StatusCode = StatusCodes.Status302Found;
            context.Response.Headers.Location = $"{redirectUri}{separator}code={Uri.EscapeDataString(grant.Code)}&state={Uri.EscapeDataString(state)}&iss={Uri.EscapeDataString(PublishedIssuer)}";

            return Task.CompletedTask;
        }

        /// <summary>
        /// Issues tokens for a confidential client presenting its own credentials.
        /// </summary>
        /// <param name="context">The request being served.</param>
        /// <param name="fields">The posted form fields.</param>
        /// <returns>
        /// A task that completes when the response has been written.
        /// </returns>
        internal Task HandleClientCredentialsGrantAsync(HttpContext context, IReadOnlyDictionary<string, string> fields)
        {
            if (!TryResolveClientCredentials(context, fields, out var clientId, out var clientSecret)
                || string.IsNullOrWhiteSpace(clientSecret)
                || !_clients.TryGetValue(clientId, out var expectedSecret)
                || string.IsNullOrWhiteSpace(expectedSecret)
                || !string.Equals(expectedSecret, clientSecret, StringComparison.Ordinal))
            {
                return WriteErrorAsync(context, StatusCodes.Status401Unauthorized, "invalid_client", "client authentication failed");
            }

            return WriteTokensAsync(context, ReadField(fields, "scope"), clientId, ResolveAudience(fields), allowRefreshToken: false);
        }

        /// <summary>
        /// Serves the device verification URI, approving the grant that carries the supplied user code.
        /// </summary>
        /// <param name="context">The request being served.</param>
        /// <returns>
        /// A task that completes when the response has been written.
        /// </returns>
        internal Task HandleDeviceApprovalAsync(HttpContext context)
        {
            RecordHit("device-approve");

            var userCode = context.Request.Query["user_code"].ToString();
            if (string.IsNullOrWhiteSpace(userCode)
                || !_deviceCodes.Values.Any(x => string.Equals(x.UserCode, userCode, StringComparison.OrdinalIgnoreCase)))
            {
                context.Response.StatusCode = StatusCodes.Status404NotFound;
                context.Response.ContentType = "text/plain; charset=utf-8";

                return context.Response.WriteAsync("unknown user code");
            }

            Approve(userCode);
            context.Response.ContentType = "text/plain; charset=utf-8";

            return context.Response.WriteAsync("approved");
        }

        /// <summary>
        /// Starts an RFC 8628 device authorization grant.
        /// </summary>
        /// <param name="context">The request being served.</param>
        /// <returns>
        /// A task that completes when the response has been written.
        /// </returns>
        internal async Task HandleDeviceAuthorizationAsync(HttpContext context)
        {
            RecordHit("devicecode");

            var form = await context.Request.ReadFormAsync().ConfigureAwait(false);
            var fields = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var field in form)
            {
                fields[field.Key] = field.Value.ToString();
            }

            lock (_lastDeviceAuthorizationRequestLock)
            {
                _lastDeviceAuthorizationRequest = fields;
            }

            var grant = new DeviceCodeGrantState
            {
                ClientId = form["client_id"].ToString(),
                DeviceCode = Guid.NewGuid().ToString("N"),
                Scope = form["scope"].ToString(),
                UserCode = CreateUserCode()
            };
            _deviceCodes[grant.DeviceCode] = grant;

            var verificationUri = $"{Options.DeviceVerificationUriScheme}://localhost/oauth/v2.0/device";

            await WriteJsonAsync(context, StatusCodes.Status200OK, new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["device_code"] = grant.DeviceCode,
                ["user_code"] = grant.UserCode,
                ["verification_uri"] = verificationUri,
                ["verification_uri_complete"] = $"{verificationUri}?user_code={grant.UserCode}",
                ["interval"] = 1,
                ["expires_in"] = 300
            }).ConfigureAwait(false);
        }

        /// <summary>
        /// Polls an RFC 8628 device authorization grant.
        /// </summary>
        /// <param name="context">The request being served.</param>
        /// <param name="fields">The posted form fields.</param>
        /// <returns>
        /// A task that completes when the response has been written.
        /// </returns>
        internal Task HandleDeviceCodeGrantAsync(HttpContext context, IReadOnlyDictionary<string, string> fields)
        {
            var deviceCode = ReadField(fields, "device_code");
            if (string.IsNullOrWhiteSpace(deviceCode) || !_deviceCodes.TryGetValue(deviceCode, out var grant))
            {
                return WriteErrorAsync(context, StatusCodes.Status400BadRequest, "invalid_grant", "unknown device code");
            }

            if (Options.SlowDownOnce && !_slowDownIssued)
            {
                _slowDownIssued = true;

                return WriteErrorAsync(context, StatusCodes.Status400BadRequest, "slow_down", "the client is polling too quickly");
            }

            if (!grant.Approved)
            {
                if (Options.RequireApproval)
                {
                    return WriteErrorAsync(context, StatusCodes.Status400BadRequest, "authorization_pending", "the end user has not yet approved the grant");
                }

                grant.Polls++;
                if (grant.Polls <= Options.PendingPollsBeforeSuccess)
                {
                    return WriteErrorAsync(context, StatusCodes.Status400BadRequest, "authorization_pending", "the end user has not yet approved the grant");
                }

                grant.Approved = true;
            }

            return WriteTokensAsync(context, grant.Scope, grant.ClientId, ResolveAudience(fields), allowRefreshToken: true);
        }

        /// <summary>
        /// Serves the enterprise identity provider's discovery document, so a client that was handed only
        /// <c>IdpUrl</c> can find the token exchange endpoint.
        /// </summary>
        /// <param name="context">The request being served.</param>
        /// <returns>
        /// A task that completes when the response has been written.
        /// </returns>
        internal Task HandleIdpMetadataAsync(HttpContext context)
        {
            RecordHit("idp-metadata");

            return WriteJsonAsync(context, StatusCodes.Status200OK, new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["issuer"] = IdpIssuer.ToString(),
                ["token_endpoint"] = IdpTokenEndpoint.ToString(),
                ["grant_types_supported"] = new[] { "client_credentials", GrantTypeTokenExchange },
                ["token_endpoint_auth_methods_supported"] = new[] { "client_secret_post", "client_secret_basic" }
            });
        }

        /// <summary>
        /// Serves the enterprise identity provider's token endpoint: it mints id tokens for a confidential
        /// client and performs the RFC 8693 exchange that turns one into an identity assertion.
        /// </summary>
        /// <param name="context">The request being served.</param>
        /// <returns>
        /// A task that completes when the response has been written.
        /// </returns>
        /// <remarks>
        /// Both branches require <see cref="IdpClientId"/> and <see cref="IdpClientSecret"/>, so a client that
        /// forgets to authenticate at the identity provider is caught here rather than at the authorization
        /// server one hop later.
        /// </remarks>
        internal async Task HandleIdpTokenAsync(HttpContext context)
        {
            RecordHit("idp-token");

            if (!context.Request.HasFormContentType)
            {
                await WriteErrorAsync(context, StatusCodes.Status400BadRequest, "invalid_request", "the token endpoint requires an application/x-www-form-urlencoded body").ConfigureAwait(false);

                return;
            }

            var form = await context.Request.ReadFormAsync().ConfigureAwait(false);
            var fields = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var field in form)
            {
                fields[field.Key] = field.Value.ToString();
            }

            var grantType = ReadField(fields, "grant_type");
            RecordHit($"idp-token:{grantType}");

            if (!TryResolveClientCredentials(context, fields, out var clientId, out var clientSecret)
                || !string.Equals(clientId, IdpClientId, StringComparison.Ordinal)
                || !string.Equals(clientSecret, IdpClientSecret, StringComparison.Ordinal))
            {
                await WriteErrorAsync(context, StatusCodes.Status401Unauthorized, "invalid_client", "identity provider client authentication failed").ConfigureAwait(false);

                return;
            }

            if (string.Equals(grantType, "client_credentials", StringComparison.Ordinal))
            {
                await WriteJsonAsync(context, StatusCodes.Status200OK, new Dictionary<string, object>(StringComparer.Ordinal)
                {
                    ["access_token"] = CreateAccessToken(ReadField(fields, "scope"), IdpClientId, IdpClientId),
                    ["id_token"] = CreateIdToken("fixture-user"),
                    ["token_type"] = "Bearer",
                    ["expires_in"] = (int)Options.AccessTokenLifetime.TotalSeconds
                }).ConfigureAwait(false);

                return;
            }

            if (string.Equals(grantType, GrantTypeTokenExchange, StringComparison.Ordinal))
            {
                await HandleTokenExchangeAsync(context, fields).ConfigureAwait(false);

                return;
            }

            await WriteErrorAsync(context, StatusCodes.Status400BadRequest, "unsupported_grant_type", $"the grant type '{grantType}' is not supported").ConfigureAwait(false);
        }

        /// <summary>
        /// Redeems an RFC 7523 JWT bearer assertion — an Identity Assertion JWT Authorization Grant this
        /// instance's identity provider minted — for an access token.
        /// </summary>
        /// <param name="context">The request being served.</param>
        /// <param name="fields">The posted form fields.</param>
        /// <returns>
        /// A task that completes when the response has been written.
        /// </returns>
        internal Task HandleJwtBearerGrantAsync(HttpContext context, IReadOnlyDictionary<string, string> fields)
        {
            var assertion = ReadField(fields, "assertion");
            if (string.IsNullOrWhiteSpace(assertion))
            {
                return WriteErrorAsync(context, StatusCodes.Status400BadRequest, "invalid_request", "the jwt-bearer grant requires an assertion");
            }

            if (!TryValidateJwt(assertion, Issuer.ToString(), out var principal))
            {
                return WriteErrorAsync(context, StatusCodes.Status400BadRequest, "invalid_grant", "the assertion is not an identity assertion this authorization server accepts");
            }

            var clientId = ReadField(fields, "client_id");
            var scope = ReadField(fields, "scope");
            if (string.IsNullOrWhiteSpace(scope))
            {
                scope = principal.FindFirst("scope")?.Value ?? string.Empty;
            }

            return WriteTokensAsync(
                context,
                scope,
                string.IsNullOrWhiteSpace(clientId) ? principal.FindFirst("sub")?.Value ?? "anonymous" : clientId,
                ResolveAudience(fields),
                allowRefreshToken: false);
        }

        /// <summary>
        /// Serves the legacy (v1) OpenID Connect discovery document.
        /// </summary>
        /// <param name="context">The request being served.</param>
        /// <returns>
        /// A task that completes when the response has been written.
        /// </returns>
        internal Task HandleLegacyMetadataAsync(HttpContext context)
        {
            RecordHit("metadata-v1");

            if (!Options.PublishVersionedDocuments)
            {
                context.Response.StatusCode = StatusCodes.Status404NotFound;

                return Task.CompletedTask;
            }

            return WriteJsonAsync(context, StatusCodes.Status200OK, new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["issuer"] = LegacyIssuer.ToString(),
                ["authorization_endpoint"] = "http://localhost/oauth/authorize",
                ["token_endpoint"] = "http://localhost/oauth/token",
                ["grant_types_supported"] = SupportedGrantTypes
            });
        }

        /// <summary>
        /// Serves the legacy (v1) token endpoint, which rejects every request.
        /// </summary>
        /// <param name="context">The request being served.</param>
        /// <returns>
        /// A task that completes when the response has been written.
        /// </returns>
        /// <remarks>
        /// This is the trap that proves a client selected the versioned document.
        /// </remarks>
        internal Task HandleLegacyTokenAsync(HttpContext context)
        {
            RecordHit("token-v1");

            return WriteErrorAsync(context, StatusCodes.Status400BadRequest, "invalid_request", "v1 endpoint");
        }

        /// <summary>
        /// Serves the versioned OpenID Connect discovery document.
        /// </summary>
        /// <param name="context">The request being served.</param>
        /// <returns>
        /// A task that completes when the response has been written.
        /// </returns>
        internal Task HandleOpenIdMetadataAsync(HttpContext context)
        {
            RecordHit("metadata-v2");

            return WriteJsonAsync(context, StatusCodes.Status200OK, CreateAuthorizationServerDocument());
        }

        /// <summary>
        /// Serves the RFC 9728 protected resource metadata document in the configured mode.
        /// </summary>
        /// <param name="context">The request being served.</param>
        /// <returns>
        /// A task that completes when the response has been written.
        /// </returns>
        internal Task HandleProtectedResourceMetadataAsync(HttpContext context)
        {
            RecordHit("prm");

            switch (Options.ProtectedResourceMetadataMode)
            {
                case ProtectedResourceMetadataMode.NotFound:
                    context.Response.StatusCode = StatusCodes.Status404NotFound;

                    return Task.CompletedTask;

                case ProtectedResourceMetadataMode.Unauthorized:
                    context.Response.Headers.WWWAuthenticate = $"Bearer realm=\"\", authorization_uri=\"{AuthorizationEndpoint}\", client_id=\"{GraphClientId}\"";

                    return WriteJsonAsync(context, StatusCodes.Status401Unauthorized, new Dictionary<string, object>(StringComparer.Ordinal)
                    {
                        ["error"] = new Dictionary<string, object>(StringComparer.Ordinal)
                        {
                            ["code"] = "InvalidAuthenticationToken"
                        }
                    });

                default:
                    return WriteJsonAsync(context, StatusCodes.Status200OK, new Dictionary<string, object>(StringComparer.Ordinal)
                    {
                        ["resource"] = Options.ResourceUri,
                        ["authorization_servers"] = new[] { Issuer.ToString() },
                        ["scopes_supported"] = Options.Scopes.ToArray(),
                        ["bearer_methods_supported"] = new[] { "header" }
                    });
            }
        }

        /// <summary>
        /// Rotates a refresh token, invalidating the presented one, unless
        /// <see cref="LocalAuthorizationServerOptions.OmitRotatedRefreshToken"/> is set.
        /// </summary>
        /// <param name="context">The request being served.</param>
        /// <param name="fields">The posted form fields.</param>
        /// <returns>
        /// A task that completes when the response has been written.
        /// </returns>
        /// <remarks>
        /// When <see cref="LocalAuthorizationServerOptions.OmitRotatedRefreshToken"/> is set the response carries no
        /// <c>refresh_token</c> at all and the presented one stays valid, which is the RFC 6749 section 6 shape a
        /// client must survive by keeping the refresh token it already holds.
        /// <para>
        /// <see cref="LocalAuthorizationServerOptions.RejectRefresh"/> is checked first and refuses every
        /// refresh outright, which models a torn-down end user session rather than an unknown token.
        /// </para>
        /// </remarks>
        internal Task HandleRefreshTokenGrantAsync(HttpContext context, IReadOnlyDictionary<string, string> fields)
        {
            if (Options.RejectRefresh)
            {
                return WriteErrorAsync(context, StatusCodes.Status400BadRequest, "invalid_grant", "the session backing this refresh token has ended");
            }

            var refreshToken = ReadField(fields, "refresh_token");
            if (string.IsNullOrWhiteSpace(refreshToken))
            {
                return WriteErrorAsync(context, StatusCodes.Status400BadRequest, "invalid_grant", "unknown refresh token");
            }

            if (Options.OmitRotatedRefreshToken)
            {
                if (!_refreshTokens.TryGetValue(refreshToken, out var retained))
                {
                    return WriteErrorAsync(context, StatusCodes.Status400BadRequest, "invalid_grant", "unknown refresh token");
                }

                return WriteTokensAsync(context, retained.Scope, retained.ClientId, ResolveAudience(fields), allowRefreshToken: false);
            }

            if (!_refreshTokens.TryRemove(refreshToken, out var grant))
            {
                return WriteErrorAsync(context, StatusCodes.Status400BadRequest, "invalid_grant", "unknown refresh token");
            }

            return WriteTokensAsync(context, grant.Scope, grant.ClientId, ResolveAudience(fields), allowRefreshToken: true);
        }

        /// <summary>
        /// Serves the RFC 7591 dynamic client registration endpoint.
        /// </summary>
        /// <param name="context">The request being served.</param>
        /// <returns>
        /// A task that completes when the response has been written.
        /// </returns>
        /// <remarks>
        /// A registration that asks for any <c>token_endpoint_auth_method</c> other than <c>none</c> is issued a
        /// <c>client_secret</c> and seeded as a confidential client, so a client credentials grant run against
        /// the freshly registered identifier actually authenticates instead of drawing <c>invalid_client</c>.
        /// </remarks>
        internal async Task HandleRegisterAsync(HttpContext context)
        {
            RecordHit("register");

            if (!Options.EnableDynamicClientRegistration)
            {
                context.Response.StatusCode = StatusCodes.Status404NotFound;

                return;
            }

            var grantTypes = new List<string>();
            var redirectUris = new List<string>();
            var requestedAuthMethod = "none";

            try
            {
                using var document = await JsonDocument.ParseAsync(context.Request.Body).ConfigureAwait(false);
                ReadStringArray(document.RootElement, "grant_types", grantTypes);
                ReadStringArray(document.RootElement, "redirect_uris", redirectUris);

                if (document.RootElement.ValueKind is JsonValueKind.Object
                    && document.RootElement.TryGetProperty("token_endpoint_auth_method", out var authMethod)
                    && authMethod.ValueKind is JsonValueKind.String)
                {
                    requestedAuthMethod = authMethod.GetString()!;
                }
            }
            catch (JsonException)
            {
                grantTypes.Clear();
                redirectUris.Clear();
            }

            var confidential = !string.Equals(requestedAuthMethod, "none", StringComparison.Ordinal);
            var clientId = $"dcr-{Guid.NewGuid():N}";
            var clientSecret = confidential ? $"secret-{Guid.NewGuid():N}" : null;

            RegisterClient(clientId, clientSecret);

            var payload = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["client_id"] = clientId,
                ["client_id_issued_at"] = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                ["redirect_uris"] = redirectUris.ToArray(),
                ["grant_types"] = grantTypes.ToArray(),
                ["token_endpoint_auth_method"] = requestedAuthMethod
            };

            if (clientSecret is not null)
            {
                payload["client_secret"] = clientSecret;
            }

            await WriteJsonAsync(context, StatusCodes.Status201Created, payload).ConfigureAwait(false);
        }

        /// <summary>
        /// Serves the versioned token endpoint and dispatches to the grant handlers.
        /// </summary>
        /// <param name="context">The request being served.</param>
        /// <returns>
        /// A task that completes when the response has been written.
        /// </returns>
        internal async Task HandleTokenAsync(HttpContext context)
        {
            RecordHit("token");

            if (!context.Request.HasFormContentType)
            {
                await WriteErrorAsync(context, StatusCodes.Status400BadRequest, "invalid_request", "the token endpoint requires an application/x-www-form-urlencoded body").ConfigureAwait(false);

                return;
            }

            var form = await context.Request.ReadFormAsync().ConfigureAwait(false);
            var fields = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var field in form)
            {
                fields[field.Key] = field.Value.ToString();
            }

            lock (_lastTokenRequestLock)
            {
                _lastTokenRequest = fields;
            }

            var grantType = ReadField(fields, "grant_type");
            RecordHit($"token:{grantType}");

            var response = grantType switch
            {
                "authorization_code" => HandleAuthorizationCodeGrantAsync(context, fields),
                "client_credentials" => HandleClientCredentialsGrantAsync(context, fields),
                "refresh_token" => HandleRefreshTokenGrantAsync(context, fields),
                "urn:ietf:params:oauth:grant-type:device_code" => HandleDeviceCodeGrantAsync(context, fields),
                GrantTypeJwtBearer => HandleJwtBearerGrantAsync(context, fields),
                _ => WriteErrorAsync(context, StatusCodes.Status400BadRequest, "unsupported_grant_type", $"the grant type '{grantType}' is not supported")
            };

            await response.ConfigureAwait(false);
        }

        /// <summary>
        /// Performs the RFC 8693 token exchange that turns an OpenID Connect id token into an Identity
        /// Assertion JWT Authorization Grant.
        /// </summary>
        /// <param name="context">The request being served.</param>
        /// <param name="fields">The posted form fields.</param>
        /// <returns>
        /// A task that completes when the response has been written.
        /// </returns>
        /// <remarks>
        /// The subject token, its type, the requested token type, and the audience are all enforced, so a
        /// client that sends an access token where an id token belongs — or asks for the wrong issued token
        /// type — fails here rather than producing an assertion the authorization server would silently accept.
        /// </remarks>
        internal Task HandleTokenExchangeAsync(HttpContext context, IReadOnlyDictionary<string, string> fields)
        {
            var audience = ReadField(fields, "audience");
            var requestedTokenType = ReadField(fields, "requested_token_type");
            var subjectToken = ReadField(fields, "subject_token");
            var subjectTokenType = ReadField(fields, "subject_token_type");

            if (string.IsNullOrWhiteSpace(audience))
            {
                return WriteErrorAsync(context, StatusCodes.Status400BadRequest, "invalid_request", "the token exchange requires an audience");
            }

            if (!string.Equals(subjectTokenType, TokenTypeIdToken, StringComparison.Ordinal))
            {
                return WriteErrorAsync(context, StatusCodes.Status400BadRequest, "invalid_request", $"the subject token type must be {TokenTypeIdToken}");
            }

            if (!string.Equals(requestedTokenType, TokenTypeIdJag, StringComparison.Ordinal))
            {
                return WriteErrorAsync(context, StatusCodes.Status400BadRequest, "invalid_request", $"the requested token type must be {TokenTypeIdJag}");
            }

            if (!TryValidateJwt(subjectToken, IdpClientId, out var principal))
            {
                return WriteErrorAsync(context, StatusCodes.Status400BadRequest, "invalid_grant", "the subject token is not an id token this identity provider issued");
            }

            return WriteJsonAsync(context, StatusCodes.Status200OK, new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["access_token"] = CreateIdentityAssertion(principal.FindFirst("sub")?.Value ?? "anonymous", audience, ReadField(fields, "scope")),
                ["issued_token_type"] = TokenTypeIdJag,
                ["token_type"] = TokenTypeNotApplicable,
                ["expires_in"] = 300
            });
        }

        /// <summary>
        /// Maps a single endpoint, binding the handler as a <see cref="RequestDelegate"/>.
        /// </summary>
        /// <param name="endpoints">The endpoint route builder.</param>
        /// <param name="httpMethod">The HTTP method to accept.</param>
        /// <param name="pattern">The route pattern.</param>
        /// <param name="handler">The handler.</param>
        internal static void MapEndpoint(IEndpointRouteBuilder endpoints, string httpMethod, string pattern, RequestDelegate handler)
        {
            endpoints.MapMethods(pattern, [httpMethod], handler);
        }

        /// <summary>
        /// Maps every endpoint this authorization server publishes.
        /// </summary>
        /// <param name="endpoints">The endpoint route builder.</param>
        internal void MapEndpoints(IEndpointRouteBuilder endpoints)
        {
            MapEndpoint(endpoints, HttpMethods.Get, "/.well-known/oauth-authorization-server/oauth/v2.0", HandleAuthorizationServerMetadataAsync);
            MapEndpoint(endpoints, HttpMethods.Get, "/.well-known/oauth-protected-resource", HandleProtectedResourceMetadataAsync);
            MapEndpoint(endpoints, HttpMethods.Get, "/.well-known/oauth-protected-resource/odata", HandleProtectedResourceMetadataAsync);
            MapEndpoint(endpoints, HttpMethods.Get, "/oauth/.well-known/openid-configuration", HandleLegacyMetadataAsync);
            MapEndpoint(endpoints, HttpMethods.Post, "/oauth/token", HandleLegacyTokenAsync);
            MapEndpoint(endpoints, HttpMethods.Get, "/oauth/v2.0/.well-known/oauth-authorization-server", HandleAuthorizationServerMetadataAsync);
            MapEndpoint(endpoints, HttpMethods.Get, "/oauth/v2.0/.well-known/openid-configuration", HandleOpenIdMetadataAsync);
            MapEndpoint(endpoints, HttpMethods.Get, "/oauth/v2.0/authorize", HandleAuthorizeAsync);
            MapEndpoint(endpoints, HttpMethods.Get, "/oauth/v2.0/device", HandleDeviceApprovalAsync);
            MapEndpoint(endpoints, HttpMethods.Post, "/oauth/v2.0/devicecode", HandleDeviceAuthorizationAsync);
            MapEndpoint(endpoints, HttpMethods.Get, "/oauth/v2.0/idp/.well-known/oauth-authorization-server", HandleIdpMetadataAsync);
            MapEndpoint(endpoints, HttpMethods.Get, "/oauth/v2.0/idp/.well-known/openid-configuration", HandleIdpMetadataAsync);
            MapEndpoint(endpoints, HttpMethods.Post, "/oauth/v2.0/idp/token", HandleIdpTokenAsync);
            MapEndpoint(endpoints, HttpMethods.Post, "/oauth/v2.0/register", HandleRegisterAsync);
            MapEndpoint(endpoints, HttpMethods.Post, "/oauth/v2.0/token", HandleTokenAsync);
        }

        /// <summary>
        /// Reads a form field, treating a missing field as empty.
        /// </summary>
        /// <param name="fields">The posted form fields.</param>
        /// <param name="name">The field name.</param>
        /// <returns>
        /// The field value, or <see cref="string.Empty"/>.
        /// </returns>
        internal static string ReadField(IReadOnlyDictionary<string, string> fields, string name)
        {
            return fields.TryGetValue(name, out var value) ? value : string.Empty;
        }

        /// <summary>
        /// Copies a JSON array of strings out of a registration request.
        /// </summary>
        /// <param name="element">The request body root element.</param>
        /// <param name="propertyName">The property to read.</param>
        /// <param name="values">The list the values are appended to.</param>
        internal static void ReadStringArray(JsonElement element, string propertyName, List<string> values)
        {
            if (element.ValueKind is not JsonValueKind.Object
                || !element.TryGetProperty(propertyName, out var array)
                || array.ValueKind is not JsonValueKind.Array)
            {
                return;
            }

            foreach (var item in array.EnumerateArray())
            {
                if (item.ValueKind is JsonValueKind.String)
                {
                    values.Add(item.GetString()!);
                }
            }
        }

        /// <summary>
        /// Increments the hit counter for an endpoint.
        /// </summary>
        /// <param name="endpoint">The counter name.</param>
        internal void RecordHit(string endpoint)
        {
            _hits.AddOrUpdate(endpoint, 1, (_, current) => current + 1);
        }

        /// <summary>
        /// Resolves the audience an issued token carries.
        /// </summary>
        /// <param name="fields">The posted form fields.</param>
        /// <returns>
        /// The <c>resource</c> form field when present, otherwise <see cref="LocalAuthorizationServerOptions.ResourceUri"/>.
        /// </returns>
        internal string ResolveAudience(IReadOnlyDictionary<string, string> fields)
        {
            var resource = ReadField(fields, "resource");

            return string.IsNullOrWhiteSpace(resource) ? Options.ResourceUri : resource;
        }

        /// <summary>
        /// Determines whether a space-delimited scope string contains a scope.
        /// </summary>
        /// <param name="scope">The space-delimited scope string.</param>
        /// <param name="value">The scope to look for.</param>
        /// <returns>
        /// <see langword="true"/> when the scope is present; otherwise <see langword="false"/>.
        /// </returns>
        internal static bool ScopeContains(string scope, string value)
        {
            if (string.IsNullOrWhiteSpace(scope))
            {
                return false;
            }

            return scope.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Any(x => string.Equals(x, value, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Resolves the client credentials from the posted form or the <c>Basic</c> authorization header.
        /// </summary>
        /// <param name="context">The request being served.</param>
        /// <param name="fields">The posted form fields.</param>
        /// <param name="clientId">Receives the client identifier.</param>
        /// <param name="clientSecret">Receives the client secret.</param>
        /// <returns>
        /// <see langword="true"/> when a client identifier was found; otherwise <see langword="false"/>.
        /// </returns>
        internal static bool TryResolveClientCredentials(HttpContext context, IReadOnlyDictionary<string, string> fields, out string clientId, out string clientSecret)
        {
            clientId = ReadField(fields, "client_id");
            clientSecret = ReadField(fields, "client_secret");

            if (!string.IsNullOrWhiteSpace(clientId) && !string.IsNullOrWhiteSpace(clientSecret))
            {
                return true;
            }

            var header = context.Request.Headers.Authorization.ToString();
            if (header.StartsWith("Basic ", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(header["Basic ".Length..].Trim()));
                    var separatorIndex = decoded.IndexOf(':');
                    if (separatorIndex > 0)
                    {
                        clientId = Uri.UnescapeDataString(decoded[..separatorIndex]);
                        clientSecret = Uri.UnescapeDataString(decoded[(separatorIndex + 1)..]);

                        return true;
                    }
                }
                catch (FormatException)
                {
                    return false;
                }
            }

            return !string.IsNullOrWhiteSpace(clientId);
        }

        /// <summary>
        /// Validates a JWT this instance minted, checking its signature, its identity provider issuer, its
        /// lifetime, and its audience.
        /// </summary>
        /// <param name="token">The compact JWT to validate.</param>
        /// <param name="expectedAudience">The audience the token must carry.</param>
        /// <param name="principal">Receives the validated principal, or <see langword="null"/> when validation failed.</param>
        /// <returns>
        /// <see langword="true"/> when the token validates; otherwise <see langword="false"/>.
        /// </returns>
        /// <remarks>
        /// The audience comparison ignores a trailing slash, because a client that was handed an issuer as a
        /// <see cref="Uri"/> may re-serialize it with one; every other check is exact.
        /// </remarks>
        internal bool TryValidateJwt(string token, string expectedAudience, out ClaimsPrincipal principal)
        {
            principal = null!;

            if (string.IsNullOrWhiteSpace(token))
            {
                return false;
            }

            var parameters = new TokenValidationParameters
            {
                ClockSkew = TimeSpan.FromMinutes(1),
                IssuerSigningKey = new SymmetricSecurityKey(SigningKey),
                ValidateAudience = false,
                ValidateIssuer = true,
                ValidateIssuerSigningKey = true,
                ValidateLifetime = true,
                ValidIssuer = IdpIssuer.ToString()
            };

            try
            {
                principal = new JwtSecurityTokenHandler().ValidateToken(token, parameters, out var validated);

                return validated is JwtSecurityToken jwt
                    && jwt.Audiences.Any(audience => string.Equals(audience.TrimEnd('/'), expectedAudience.TrimEnd('/'), StringComparison.Ordinal));
            }
            catch (Exception exception) when (exception is SecurityTokenException or ArgumentException)
            {
                return false;
            }
        }

        /// <summary>
        /// Writes an OAuth error response.
        /// </summary>
        /// <param name="context">The request being served.</param>
        /// <param name="statusCode">The HTTP status code.</param>
        /// <param name="error">The OAuth error code.</param>
        /// <param name="errorDescription">The human readable description.</param>
        /// <returns>
        /// A task that completes when the response has been written.
        /// </returns>
        internal static Task WriteErrorAsync(HttpContext context, int statusCode, string error, string errorDescription)
        {
            return WriteJsonAsync(context, statusCode, new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["error"] = error,
                ["error_description"] = errorDescription
            });
        }

        /// <summary>
        /// Writes a JSON response.
        /// </summary>
        /// <param name="context">The request being served.</param>
        /// <param name="statusCode">The HTTP status code.</param>
        /// <param name="payload">The document to serialize.</param>
        /// <returns>
        /// A task that completes when the response has been written.
        /// </returns>
        internal static Task WriteJsonAsync(HttpContext context, int statusCode, IReadOnlyDictionary<string, object> payload)
        {
            context.Response.StatusCode = statusCode;
            context.Response.ContentType = "application/json; charset=utf-8";

            return context.Response.WriteAsync(JsonSerializer.Serialize(payload));
        }

        /// <summary>
        /// Writes a successful token response, minting a refresh token when the grant allows it.
        /// </summary>
        /// <param name="context">The request being served.</param>
        /// <param name="scope">The granted, space-delimited scope.</param>
        /// <param name="clientId">The client the tokens are issued to.</param>
        /// <param name="audience">The audience the access token carries.</param>
        /// <param name="allowRefreshToken">Whether a refresh token may be issued.</param>
        /// <returns>
        /// A task that completes when the response has been written.
        /// </returns>
        internal Task WriteTokensAsync(HttpContext context, string scope, string clientId, string audience, bool allowRefreshToken)
        {
            var payload = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["access_token"] = CreateAccessToken(scope, clientId, audience),
                ["token_type"] = "Bearer",
                ["expires_in"] = (int)Options.AccessTokenLifetime.TotalSeconds,
                ["scope"] = scope
            };

            if (allowRefreshToken && ScopeContains(scope, OfflineAccessScope))
            {
                var refreshToken = Guid.NewGuid().ToString("N");
                _refreshTokens[refreshToken] = (audience, clientId, scope);
                payload["refresh_token"] = refreshToken;
            }

            return WriteJsonAsync(context, StatusCodes.Status200OK, payload);
        }

        #endregion

    }

}
