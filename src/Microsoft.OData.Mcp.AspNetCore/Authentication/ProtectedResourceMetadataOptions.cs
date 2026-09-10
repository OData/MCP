// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;

namespace Microsoft.OData.Mcp.AspNetCore.Authentication
{

    /// <summary>
    /// What an API publishes about how to sign in to it: the RFC 8414 authorization servers that issue
    /// its tokens, the scopes a client should ask for, and which route bases the published document covers.
    /// </summary>
    /// <example>
    /// <code>
    /// builder.Services.AddProtectedResourceMetadata(options =&gt;
    /// {
    ///     options.AuthorizationServers.Add(new Uri("https://login.microsoftonline.com/contoso.com/v2.0"));
    ///     options.ScopesSupported.Add("api://contoso-odata/Data.Read");
    ///     options.ResourceName = "Contoso API";
    /// });
    /// </code>
    /// </example>
    /// <remarks>
    /// Nothing here describes how the API validates a token. That stays with whatever authentication handler
    /// the app already registered; this type only describes the API to a client that has not signed in yet.
    /// The default route base is the application root, which is the usual shape of an API that is not sharing
    /// a host with MVC.
    /// </remarks>
    public sealed class ProtectedResourceMetadataOptions
    {

        #region Properties

        /// <summary>
        /// Gets or sets a value indicating whether a <c>401</c> under a covered route base has its
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
        /// Gets the route bases the published documents cover, in the same form Endpoint Routing and Minimal
        /// APIs accept.
        /// </summary>
        /// <value>
        /// Empty by default, which means the application root only. A non-empty list replaces that default.
        /// </value>
        /// <example>
        /// <code>
        /// options.Prefixes.Add("");
        /// options.Prefixes.Add("odata");
        /// options.Prefixes.Add("api/v1");
        /// </code>
        /// </example>
        /// <remarks>
        /// An empty string and <c>/</c> are the application root, matching <c>MapGroup("")</c> and
        /// <c>AddRouteComponents(string.Empty, model)</c>. <c>odata</c> and <c>/odata/</c> both mean the
        /// <c>/odata</c> base. Nested bases such as <c>api/v1</c> are valid. A whitespace-only entry is
        /// rejected by <see cref="Validate"/>: it is not a route base.
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
        /// <see langword="null"/> or whitespace-only entry.
        /// </exception>
        /// <example>
        /// <code>
        /// var options = new ProtectedResourceMetadataOptions();
        /// options.AuthorizationServers.Add(new Uri("https://login.example.com/tenant"));
        ///
        /// options.Validate();
        /// </code>
        /// </example>
        /// <remarks>
        /// Called by <see cref="ProtectedResourceMetadataStartupFilter"/> before the pipeline is built, so a
        /// misconfigured API refuses to start rather than serving a document that sends every client to an
        /// authorization server that does not exist. An empty string and <c>/</c> in <see cref="Prefixes"/>
        /// are the application root and pass.
        /// </remarks>
        public void Validate()
        {
            if (AuthorizationServers.Count == 0)
            {
                throw new InvalidOperationException(
                    $"AddProtectedResourceMetadata requires at least one entry in {nameof(ProtectedResourceMetadataOptions)}.{nameof(AuthorizationServers)}: "
                    + "the issuer URL of the authorization server that issues tokens for this API, for example https://login.microsoftonline.com/contoso.com/v2.0.");
            }

            foreach (var authorizationServer in AuthorizationServers)
            {
                if (authorizationServer is null)
                {
                    throw new InvalidOperationException($"{nameof(ProtectedResourceMetadataOptions)}.{nameof(AuthorizationServers)} must not contain a null entry.");
                }

                if (!authorizationServer.IsAbsoluteUri)
                {
                    throw new InvalidOperationException(
                        $"The authorization server '{authorizationServer}' in {nameof(ProtectedResourceMetadataOptions)}.{nameof(AuthorizationServers)} must be an absolute URI, "
                        + "for example https://login.microsoftonline.com/contoso.com/v2.0.");
                }

                if (!IsAllowedAuthorizationServer(authorizationServer))
                {
                    throw new InvalidOperationException(
                        $"The authorization server '{authorizationServer}' in {nameof(ProtectedResourceMetadataOptions)}.{nameof(AuthorizationServers)} must use https. "
                        + "http is accepted only for loopback hosts such as http://127.0.0.1:5000 or http://localhost:5000.");
                }
            }

            foreach (var prefix in Prefixes)
            {
                if (prefix is null)
                {
                    throw new InvalidOperationException(
                        $"{nameof(ProtectedResourceMetadataOptions)}.{nameof(Prefixes)} must not contain a null entry. "
                        + "Use an empty string or \"/\" for the application root.");
                }

                if (IsRootPrefix(prefix))
                {
                    continue;
                }

                if (string.IsNullOrWhiteSpace(prefix))
                {
                    throw new InvalidOperationException(
                        $"{nameof(ProtectedResourceMetadataOptions)}.{nameof(Prefixes)} must not contain a whitespace-only entry. "
                        + "Use an empty string or \"/\" for the application root, or name each route base explicitly, for example \"odata\" or \"api/v1\".");
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

        /// <summary>
        /// Determines whether a configured prefix is the application root.
        /// </summary>
        /// <param name="prefix">The raw prefix from <see cref="Prefixes"/>.</param>
        /// <returns>
        /// <see langword="true"/> when <paramref name="prefix"/> is an empty string, or only slashes (with
        /// optional surrounding white space). Whitespace-only values are not the root.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="prefix"/> is <see langword="null"/>.</exception>
        internal static bool IsRootPrefix(string prefix)
        {
            ArgumentNullException.ThrowIfNull(prefix);

            if (prefix.Length == 0)
            {
                return true;
            }

            var trimmed = prefix.Trim();
            if (trimmed.Length == 0)
            {
                return false;
            }

            return trimmed.Trim('/').Length == 0;
        }

        /// <summary>
        /// Normalizes a route base the way Endpoint Routing does: trim white space and leading or trailing
        /// slashes, leaving nested segments intact.
        /// </summary>
        /// <param name="prefix">The raw prefix from <see cref="Prefixes"/>.</param>
        /// <returns>
        /// The empty string for the application root; otherwise the prefix without leading or trailing slashes,
        /// for example <c>odata</c> or <c>api/v1</c>.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="prefix"/> is <see langword="null"/>.</exception>
        /// <example>
        /// <code>
        /// ProtectedResourceMetadataOptions.NormalizePrefix("");        // ""
        /// ProtectedResourceMetadataOptions.NormalizePrefix("/");       // ""
        /// ProtectedResourceMetadataOptions.NormalizePrefix("/odata/"); // "odata"
        /// ProtectedResourceMetadataOptions.NormalizePrefix("api/v1");  // "api/v1"
        /// </code>
        /// </example>
        internal static string NormalizePrefix(string prefix)
        {
            ArgumentNullException.ThrowIfNull(prefix);

            return prefix.Trim().Trim('/');
        }

        #endregion

    }

}
