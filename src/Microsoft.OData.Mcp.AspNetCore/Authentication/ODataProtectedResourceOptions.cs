// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using Microsoft.OData.Mcp.AspNetCore.Hosting;

namespace Microsoft.OData.Mcp.AspNetCore.Authentication
{

    /// <summary>
    /// What an OData API publishes about how to sign in to it: the RFC 8414 authorization servers that issue
    /// its tokens, the scopes a client should ask for, and which route prefixes the published document covers.
    /// </summary>
    /// <example>
    /// <code>
    /// builder.Services.AddODataProtectedResource(options =&gt;
    /// {
    ///     options.AuthorizationServers.Add(new Uri("https://login.microsoftonline.com/contoso.com/v2.0"));
    ///     options.ScopesSupported.Add("api://contoso-odata/Data.Read");
    ///     options.ResourceName = "Contoso OData";
    /// });
    /// </code>
    /// </example>
    /// <remarks>
    /// Nothing here describes how the API validates a token. That stays with whatever authentication handler
    /// the app already registered; this type only describes the API to a client that has not signed in yet.
    /// </remarks>
    public sealed class ODataProtectedResourceOptions
    {

        #region Properties

        /// <summary>
        /// Gets or sets a value indicating whether a <c>401</c> under a covered prefix has its
        /// <c>WWW-Authenticate</c> challenge rewritten to carry <c>resource_metadata</c>.
        /// </summary>
        /// <value>
        /// Defaults to <see langword="true"/>.
        /// </value>
        /// <remarks>
        /// Turn this off only when the app already emits an RFC 9728 hint of its own. A client that never sees
        /// <c>resource_metadata</c> still finds the document through the two well-known URLs, but it spends an
        /// extra request doing so.
        /// </remarks>
        public bool AnnotateChallenges { get; set; } = true;

        /// <summary>
        /// Gets the RFC 8414 issuer URLs of the authorization servers that issue tokens for this API, in the
        /// order a client should try them.
        /// </summary>
        /// <value>
        /// At least one entry is required. Each must be absolute and <c>https</c>; <c>http</c> is accepted only
        /// for loopback hosts so a test or a local identity provider still works.
        /// </value>
        /// <example>
        /// <code>
        /// options.AuthorizationServers.Add(new Uri("https://login.microsoftonline.com/contoso.com/v2.0"));
        /// </code>
        /// </example>
        public List<Uri> AuthorizationServers { get; } = [];

        /// <summary>
        /// Gets the OData route prefixes the published documents cover, without leading or trailing slashes.
        /// </summary>
        /// <value>
        /// Empty by default, which means the prefixes come from
        /// <see cref="ODataMcpRouteDiscovery.Discover(IServiceProvider, ODataMcpHostOptions)"/> on the first
        /// request. A non-empty list replaces discovery outright.
        /// </value>
        /// <example>
        /// <code>
        /// options.Prefixes.Add("odata");
        /// </code>
        /// </example>
        /// <remarks>
        /// Set this only when the routing table cannot be read for a prefix — a hand-rolled OData host, or a
        /// reverse proxy that rewrites the path. A blank entry is rejected by <see cref="Validate"/>: an
        /// OData service mounted at the application root is discovered as the empty prefix on its own, so a
        /// blank entry here is a typo rather than a configuration.
        /// </remarks>
        public List<string> Prefixes { get; } = [];

        /// <summary>
        /// Gets or sets the URL of a page a developer can read to learn how to call this API.
        /// </summary>
        public Uri? ResourceDocumentation { get; set; }

        /// <summary>
        /// Gets or sets the human-readable name of this API, shown to a user during consent.
        /// </summary>
        public string? ResourceName { get; set; }

        /// <summary>
        /// Gets the scope values a client should request to call this API.
        /// </summary>
        /// <value>
        /// Empty by default, in which case a client falls back to whatever scopes its operator configured.
        /// </value>
        public List<string> ScopesSupported { get; } = [];

        #endregion

        #region Public Methods

        /// <summary>
        /// Fails the host when the published document would be unusable.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// Thrown when <see cref="AuthorizationServers"/> is empty, carries a <see langword="null"/>, a
        /// non-absolute, or a non-<c>https</c> non-loopback entry; or when <see cref="Prefixes"/> carries a
        /// blank entry.
        /// </exception>
        /// <example>
        /// <code>
        /// var options = new ODataProtectedResourceOptions();
        /// options.AuthorizationServers.Add(new Uri("https://login.example.com/tenant"));
        ///
        /// options.Validate();
        /// </code>
        /// </example>
        /// <remarks>
        /// Called by <see cref="ODataProtectedResourceStartupFilter"/> before the pipeline is built, so a
        /// misconfigured API refuses to start rather than serving a document that sends every client to an
        /// authorization server that does not exist.
        /// </remarks>
        public void Validate()
        {
            if (AuthorizationServers.Count == 0)
            {
                throw new InvalidOperationException(
                    $"AddODataProtectedResource requires at least one entry in {nameof(ODataProtectedResourceOptions)}.{nameof(AuthorizationServers)}: "
                    + "the issuer URL of the authorization server that issues tokens for this API, for example https://login.microsoftonline.com/contoso.com/v2.0.");
            }

            foreach (var authorizationServer in AuthorizationServers)
            {
                if (authorizationServer is null)
                {
                    throw new InvalidOperationException($"{nameof(ODataProtectedResourceOptions)}.{nameof(AuthorizationServers)} must not contain a null entry.");
                }

                if (!authorizationServer.IsAbsoluteUri)
                {
                    throw new InvalidOperationException(
                        $"The authorization server '{authorizationServer}' in {nameof(ODataProtectedResourceOptions)}.{nameof(AuthorizationServers)} must be an absolute URI, "
                        + "for example https://login.microsoftonline.com/contoso.com/v2.0.");
                }

                if (!IsAllowedAuthorizationServer(authorizationServer))
                {
                    throw new InvalidOperationException(
                        $"The authorization server '{authorizationServer}' in {nameof(ODataProtectedResourceOptions)}.{nameof(AuthorizationServers)} must use https. "
                        + "http is accepted only for loopback hosts such as http://127.0.0.1:5000 or http://localhost:5000.");
                }
            }

            foreach (var prefix in Prefixes)
            {
                if (string.IsNullOrWhiteSpace(prefix))
                {
                    throw new InvalidOperationException(
                        $"{nameof(ODataProtectedResourceOptions)}.{nameof(Prefixes)} must not contain a blank entry. "
                        + "Leave the list empty to discover the OData prefixes from routing, or name each prefix explicitly, for example \"odata\".");
                }
            }
        }

        #endregion

        #region Internal Methods

        /// <summary>
        /// Determines whether an authorization server URL may be published.
        /// </summary>
        /// <param name="authorizationServer">The absolute issuer URL.</param>
        /// <returns>
        /// <see langword="true"/> when the URL is <c>https</c>, or <c>http</c> on a loopback host.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="authorizationServer"/> is <see langword="null"/>.</exception>
        /// <remarks>
        /// The loopback exemption exists so a test host and a locally hosted identity provider can be modelled
        /// without weakening the rule for anything reachable off the machine.
        /// </remarks>
        internal static bool IsAllowedAuthorizationServer(Uri authorizationServer)
        {
            ArgumentNullException.ThrowIfNull(authorizationServer);

            if (Uri.UriSchemeHttps.Equals(authorizationServer.Scheme, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return Uri.UriSchemeHttp.Equals(authorizationServer.Scheme, StringComparison.OrdinalIgnoreCase) && authorizationServer.IsLoopback;
        }

        #endregion

    }

}
