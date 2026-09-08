// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;

namespace Microsoft.OData.Mcp.Tests.Shared.Authentication
{

    /// <summary>
    /// Configures the behavior of a <see cref="LocalAuthorizationServer"/> instance.
    /// </summary>
    /// <example>
    /// <code>
    /// using var server = new LocalAuthorizationServer(new LocalAuthorizationServerOptions
    /// {
    ///     EnableDynamicClientRegistration = true,
    ///     RequireApproval = false
    /// });
    /// </code>
    /// </example>
    /// <remarks>
    /// Every option is read on each request, so a test may mutate the instance handed to the constructor after the
    /// server has been built.
    /// </remarks>
    public sealed class LocalAuthorizationServerOptions
    {

        #region Properties

        /// <summary>
        /// Gets or sets how long an issued access token remains valid.
        /// </summary>
        /// <value>
        /// Defaults to one hour.
        /// </value>
        public TimeSpan AccessTokenLifetime { get; set; } = TimeSpan.FromHours(1);

        /// <summary>
        /// Gets or sets a value indicating whether the discovery documents advertise
        /// <c>client_id_metadata_document_supported</c>.
        /// </summary>
        /// <value>
        /// Defaults to <see langword="false"/>.
        /// </value>
        /// <remarks>
        /// Set alongside <see cref="EnableDynamicClientRegistration"/> left off to exercise the client id
        /// metadata document branch of client-id resolution, which only runs when there is no registration
        /// endpoint to prefer.
        /// </remarks>
        public bool AdvertiseClientIdMetadataDocument { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the versioned discovery document publishes, and the
        /// authorization response echoes, the authority-only issuer <c>http://localhost</c> instead of
        /// <see cref="LocalAuthorizationServer.Issuer"/>.
        /// </summary>
        /// <value>
        /// Defaults to <see langword="false"/>.
        /// </value>
        /// <remarks>
        /// This is how a fixture models a conforming authorization server whose issuer identifier has no path —
        /// <c>https://accounts.google.com</c> is the canonical example. Such an issuer is the case a client that
        /// compares <c>iss</c> against a parsed <see cref="Uri"/> gets wrong, because
        /// <see cref="Uri.AbsoluteUri"/> appends the path <c>/</c> to one side of the comparison and the wire
        /// value has none.
        /// </remarks>
        public bool AuthorityOnlyIssuer { get; set; }

        /// <summary>
        /// Gets or sets the URI scheme the device authorization response advertises its <c>verification_uri</c>
        /// and <c>verification_uri_complete</c> under.
        /// </summary>
        /// <value>
        /// Defaults to <c>http</c>, which is the loopback scheme every other fixture endpoint uses.
        /// </value>
        /// <remarks>
        /// Set to <c>file</c> — or any other locally-handled scheme — to model an attacker-controlled
        /// authorization server that answers a device authorization request with a URL a client must never open
        /// through the shell or hand to an MCP host as an elicitation URL.
        /// </remarks>
        public string DeviceVerificationUriScheme { get; set; } = "http";

        /// <summary>
        /// Gets or sets a value indicating whether the RFC 7591 dynamic client registration endpoint is reachable.
        /// </summary>
        /// <value>
        /// Defaults to <see langword="false"/>, which mirrors an Entra-like authorization server that requires a
        /// pre-registered client identifier.
        /// </value>
        public bool EnableDynamicClientRegistration { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether a <c>refresh_token</c> grant response omits the
        /// <c>refresh_token</c> field entirely, leaving the presented refresh token valid.
        /// </summary>
        /// <value>
        /// Defaults to <see langword="false"/>, meaning every refresh rotates: a new refresh token is issued and the
        /// presented one is invalidated.
        /// </value>
        /// <remarks>
        /// RFC 6749 section 6 makes the <c>refresh_token</c> field of a refresh response optional, so an
        /// authorization server may answer with an access token alone and expect the client to keep using the
        /// refresh token it already holds. Setting this proves a client preserves the previous refresh token
        /// instead of dropping it.
        /// </remarks>
        public bool OmitRotatedRefreshToken { get; set; }

        /// <summary>
        /// Gets or sets the number of device code polls answered with <c>authorization_pending</c> before the grant
        /// completes on its own.
        /// </summary>
        /// <value>
        /// Defaults to <c>1</c>.
        /// </value>
        /// <remarks>
        /// Only consulted when <see cref="RequireApproval"/> is <see langword="false"/>.
        /// </remarks>
        public int PendingPollsBeforeSuccess { get; set; } = 1;

        /// <summary>
        /// Gets or sets the response shape of the RFC 9728 protected resource metadata endpoints.
        /// </summary>
        /// <value>
        /// Defaults to <see cref="ProtectedResourceMetadataMode.Ok"/>.
        /// </value>
        public ProtectedResourceMetadataMode ProtectedResourceMetadataMode { get; set; } = ProtectedResourceMetadataMode.Ok;

        /// <summary>
        /// Gets or sets a value indicating whether the legacy (v1) discovery document is published alongside the
        /// versioned (v2) documents.
        /// </summary>
        /// <value>
        /// Defaults to <see langword="true"/>.
        /// </value>
        /// <remarks>
        /// The v1 document advertises a token endpoint that rejects every request, so a client that fails to prefer the
        /// versioned document is caught immediately.
        /// </remarks>
        public bool PublishVersionedDocuments { get; set; } = true;

        /// <summary>
        /// Gets or sets a value indicating whether the token endpoint answers every <c>refresh_token</c> grant
        /// with <c>400 invalid_grant</c>, regardless of whether the presented refresh token is known.
        /// </summary>
        /// <value>
        /// Defaults to <see langword="false"/>. Settable mid-test.
        /// </value>
        /// <remarks>
        /// This is how a fixture models an authorization server that has torn down the end user's session:
        /// combined with <see cref="LocalAuthorizationServer.RevokeIssuedTokens"/> it leaves a client with a
        /// rejected access token <em>and</em> nothing to refresh, which is the only state that forces a
        /// mid-session interactive sign-in.
        /// </remarks>
        public bool RejectRefresh { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether a device code grant stays pending until it is explicitly approved.
        /// </summary>
        /// <value>
        /// Defaults to <see langword="true"/>, meaning <see cref="LocalAuthorizationServer.Approve(string)"/> or a
        /// <c>GET</c> against the verification URI must run before the token endpoint issues tokens.
        /// </value>
        public bool RequireApproval { get; set; } = true;

        /// <summary>
        /// Gets or sets the audience value the protected resource expects on access tokens.
        /// </summary>
        /// <value>
        /// Defaults to <c>http://localhost</c>, the origin rather than the OData service path.
        /// </value>
        /// <remarks>
        /// A token minted from a <c>resource</c> form field that carries a path instead of the origin will not validate
        /// against the secured resource, which is how the resource-versus-path trap is detected.
        /// </remarks>
        public string ResourceUri { get; set; } = "http://localhost";

        /// <summary>
        /// Gets or sets a value indicating whether a protected resource rejects every access token this instance
        /// mints, including ones issued after the flag is set.
        /// </summary>
        /// <value>
        /// Defaults to <see langword="false"/>. Settable mid-test, unlike
        /// <see cref="LocalAuthorizationServer.RevokeIssuedTokens"/>, which only invalidates the tokens
        /// outstanding at the moment it is called.
        /// </value>
        /// <remarks>
        /// This is how a fixture models a resource server that will not accept anything the client can obtain,
        /// which is what pins down the "do not answer one <c>invalid_token</c> with two refreshes" rule: the
        /// refreshed token is rejected too, and the send must still cost exactly one token POST.
        /// </remarks>
        public bool RevokeAllTokens { get; set; }

        /// <summary>
        /// Gets the scopes the authorization server advertises in its discovery documents.
        /// </summary>
        public List<string> Scopes { get; } = ["read", "write", "offline_access"];

        /// <summary>
        /// Gets or sets a value indicating whether the first device code poll is answered with <c>slow_down</c>.
        /// </summary>
        /// <value>
        /// Defaults to <see langword="false"/>.
        /// </value>
        public bool SlowDownOnce { get; set; }

        #endregion

    }

}
