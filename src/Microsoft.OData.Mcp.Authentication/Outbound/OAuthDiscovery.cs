// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using SdkAuth = ModelContextProtocol.Authentication;

namespace Microsoft.OData.Mcp.Authentication.Outbound
{

    /// <summary>
    /// Runs steps 3 through 5 and 8b of the outbound OAuth discovery algorithm: parse the
    /// <c>WWW-Authenticate</c> challenge, fetch RFC 9728 protected resource metadata, select an authorization
    /// server from every candidate base the challenge and that metadata imply, and resolve the RFC 8707
    /// resource indicator.
    /// </summary>
    /// <example>
    /// <code>
    /// var discovery = new OAuthDiscovery(protectedResourceMetadataClient, authorizationServerMetadataClient, logger);
    /// var result = await discovery.DiscoverAsync(
    ///     new Uri("https://graph.microsoft.com/v1.0/"),
    ///     response.Headers.WwwAuthenticate.Select(x =&gt; x.ToString()).ToList(),
    ///     401,
    ///     authorizationServerOverride: null,
    ///     resourceOverride: null,
    ///     cancellationToken);
    ///
    /// Console.WriteLine(result.AuthorizationServer.TokenEndpoint);
    /// </code>
    /// </example>
    /// <remarks>
    /// Fail first, vendor neutral: Microsoft Graph is a trap this algorithm survives generically, never a
    /// special branch. Per <c>AUTH-14</c> a 401, 403, 404, or failed parse on any well-known document is
    /// non-fatal and discovery continues; only a timeout, a repeated 5xx (surfaced by the metadata clients), a
    /// 401/403 with no challenge at all, or an exhausted candidate list is fatal. Per <c>AUTH-13</c> the
    /// challenge <c>client_id</c> is logged as the <em>resource</em> application id and never promoted to a
    /// client id. Tokens and response bodies are never logged.
    /// </remarks>
    public sealed class OAuthDiscovery
    {

        #region Fields

        /// <summary>
        /// The client that probes authorization server metadata for a candidate base URI.
        /// </summary>
        private readonly AuthorizationServerMetadataClient _authorizationServerClient;

        /// <summary>
        /// The logger this instance records each discovery step and its outcome to.
        /// </summary>
        private readonly ILogger<OAuthDiscovery> _logger;

        /// <summary>
        /// The client that fetches RFC 9728 protected resource metadata.
        /// </summary>
        private readonly ProtectedResourceMetadataClient _protectedResourceClient;

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="OAuthDiscovery"/> class.
        /// </summary>
        /// <param name="protectedResourceClient">The client that fetches RFC 9728 protected resource metadata.</param>
        /// <param name="authorizationServerClient">The client that probes authorization server metadata for a candidate base URI.</param>
        /// <param name="logger">The logger this instance records each discovery step and its outcome to.</param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="protectedResourceClient"/>, <paramref name="authorizationServerClient"/>,
        /// or <paramref name="logger"/> is <see langword="null"/>.
        /// </exception>
        public OAuthDiscovery(
            ProtectedResourceMetadataClient protectedResourceClient,
            AuthorizationServerMetadataClient authorizationServerClient,
            ILogger<OAuthDiscovery> logger)
        {
            ArgumentNullException.ThrowIfNull(protectedResourceClient);
            ArgumentNullException.ThrowIfNull(authorizationServerClient);
            ArgumentNullException.ThrowIfNull(logger);

            _authorizationServerClient = authorizationServerClient;
            _logger = logger;
            _protectedResourceClient = protectedResourceClient;
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Collects the authorization server base URIs to probe, in order, per step 5 of the discovery
        /// algorithm.
        /// </summary>
        /// <param name="challenge">The <c>Bearer</c> challenge, or <see langword="null"/> when the response carried none.</param>
        /// <param name="protectedResource">The RFC 9728 protected resource metadata, or <see langword="null"/> when none was found.</param>
        /// <param name="authorizationServerOverride">The operator's <c>--auth-server</c> value, or <see langword="null"/> when unset.</param>
        /// <returns>
        /// Exactly <paramref name="authorizationServerOverride"/> when it is set; otherwise the absolute
        /// <c>authorization_servers</c> entries of <paramref name="protectedResource"/> in order, followed by
        /// every unique remainder of the challenge <c>authorization_uri</c> after stripping each
        /// <see cref="ODataMcpAuthConstants.AuthorizeSuffixes"/> entry, deduplicated preserving first
        /// occurrence. The list is empty when nothing implies a base.
        /// </returns>
        /// <example>
        /// <code>
        /// var challenge = WwwAuthenticateParser.Parse("Bearer authorization_uri=\"https://login.microsoftonline.com/common/oauth2/authorize\"");
        /// var bases = OAuthDiscovery.AuthorizationServerBases(challenge, null, null);
        ///
        /// // https://login.microsoftonline.com/common
        /// // https://login.microsoftonline.com/common/oauth2
        /// </code>
        /// </example>
        /// <remarks>
        /// A single <c>authorization_uri</c> can imply more than one base because a suffix later in
        /// <see cref="ODataMcpAuthConstants.AuthorizeSuffixes"/> may also match; both remainders are probed so a
        /// server that publishes its metadata under either one is found. Non-absolute
        /// <c>authorization_servers</c> entries are skipped silently here;
        /// <see cref="DiscoverAsync(Uri, IReadOnlyList{string}, int, Uri, Uri, CancellationToken)"/> logs them.
        /// </remarks>
        public static IReadOnlyList<Uri> AuthorizationServerBases(OAuthChallenge? challenge, SdkAuth.ProtectedResourceMetadata? protectedResource, Uri? authorizationServerOverride)
        {
            if (authorizationServerOverride is not null)
            {
                return [authorizationServerOverride];
            }

            var bases = new List<Uri>();

            if (protectedResource?.AuthorizationServers is not null)
            {
                foreach (var authorizationServer in protectedResource.AuthorizationServers)
                {
                    if (!string.IsNullOrWhiteSpace(authorizationServer)
                        && Uri.TryCreate(authorizationServer, UriKind.Absolute, out var candidate)
                        && !bases.Contains(candidate))
                    {
                        bases.Add(candidate);
                    }
                }
            }

            if (challenge?.AuthorizationUri is { IsAbsoluteUri: true } authorizationUri)
            {
                var absoluteUri = authorizationUri.AbsoluteUri;
                foreach (var suffix in ODataMcpAuthConstants.AuthorizeSuffixes)
                {
                    if (!absoluteUri.EndsWith(suffix, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    var remainder = absoluteUri[..^suffix.Length];
                    if (!string.IsNullOrWhiteSpace(remainder)
                        && Uri.TryCreate(remainder, UriKind.Absolute, out var candidate)
                        && !bases.Contains(candidate))
                    {
                        bases.Add(candidate);
                    }
                }
            }

            return bases;
        }

        /// <summary>
        /// Discovers the authorization server, grants, scope, and resource indicator for an OData service that
        /// answered with an authentication challenge.
        /// </summary>
        /// <param name="serviceRoot">The absolute OData service root the challenge came from.</param>
        /// <param name="wwwAuthenticate">The <c>WWW-Authenticate</c> header values of that response, in order.</param>
        /// <param name="statusCode">The HTTP status code of that response.</param>
        /// <param name="authorizationServerOverride">The operator's <c>--auth-server</c> value, or <see langword="null"/> when unset.</param>
        /// <param name="resourceOverride">The operator's <c>--resource</c> value, or <see langword="null"/> when unset.</param>
        /// <param name="cancellationToken">The token that cancels discovery.</param>
        /// <returns>
        /// The discovery result, always carrying a usable authorization server.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="serviceRoot"/> or <paramref name="wwwAuthenticate"/> is <see langword="null"/>.</exception>
        /// <exception cref="ArgumentException">Thrown when <paramref name="serviceRoot"/> is not an absolute URI.</exception>
        /// <exception cref="FormatException">Thrown when a <paramref name="wwwAuthenticate"/> value is not a well-formed challenge.</exception>
        /// <exception cref="OutboundDiscoveryException">
        /// Thrown when <paramref name="statusCode"/> is 401 or 403, <paramref name="wwwAuthenticate"/> carries no
        /// challenge, and no <paramref name="authorizationServerOverride"/> is set; when a well-known request
        /// times out or repeats a 5xx; or when no candidate base yields an authorization server.
        /// </exception>
        /// <example>
        /// <code>
        /// var result = await discovery.DiscoverAsync(
        ///     new Uri("http://localhost/odata/"),
        ///     ["Bearer realm=\"\", resource_metadata=\"http://localhost/.well-known/oauth-protected-resource/odata\""],
        ///     401,
        ///     authorizationServerOverride: null,
        ///     resourceOverride: null,
        ///     cancellationToken);
        ///
        /// Console.WriteLine(result.AuthorizationServerBase);              // http://localhost/oauth/v2.0
        /// Console.WriteLine(result.AuthorizationServer.TokenEndpoint);    // http://localhost/oauth/v2.0/token
        /// Console.WriteLine(result.Resource);                             // http://localhost (from the PRM document)
        /// </code>
        /// </example>
        /// <remarks>
        /// Protected resource metadata candidates are probed in the order
        /// <see cref="PrmCandidates(Uri, OAuthChallenge)"/> returns them and probing stops at the first
        /// document, so the Graph trap costs exactly one GET when the challenge named a
        /// <c>resource_metadata</c> URL. Authorization server bases are probed the same way. An
        /// <paramref name="authorizationServerOverride"/> also suppresses the "401 with no
        /// <c>WWW-Authenticate</c>" failure, because the operator has already supplied what that failure asks
        /// for.
        /// </remarks>
        public async Task<OAuthDiscoveryResult> DiscoverAsync(
            Uri serviceRoot,
            IReadOnlyList<string> wwwAuthenticate,
            int statusCode,
            Uri? authorizationServerOverride,
            Uri? resourceOverride,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(serviceRoot);
            ArgumentNullException.ThrowIfNull(wwwAuthenticate);

            if (!serviceRoot.IsAbsoluteUri)
            {
                throw new ArgumentException($"The OData service root must be absolute: {serviceRoot}", nameof(serviceRoot));
            }

            _logger.LogInformation("Discovering authorization for {ServiceRoot} after HTTP {Status}", serviceRoot, statusCode);

            var challenges = WwwAuthenticateParser.ParseAll(wwwAuthenticate);
            if (challenges.Count == 0 && statusCode is 401 or 403 && authorizationServerOverride is null)
            {
                throw new OutboundDiscoveryException($"{statusCode} with no WWW-Authenticate; pass --auth-token / --api-key-header / --auth-server.");
            }

            var challenge = WwwAuthenticateParser.SelectBearer(challenges);
            LogResourceClientId(challenge);

            SdkAuth.ProtectedResourceMetadata? protectedResource = null;
            foreach (var candidate in PrmCandidates(serviceRoot, challenge))
            {
                _logger.LogInformation("Probing protected resource metadata at {Url}", candidate);

                protectedResource = await _protectedResourceClient.TryGetAsync(candidate, cancellationToken).ConfigureAwait(false);
                if (protectedResource is not null)
                {
                    _logger.LogInformation("Protected resource metadata found at {Url}", candidate);

                    break;
                }
            }

            if (protectedResource is null)
            {
                _logger.LogInformation("No protected resource metadata; continuing with challenge and overrides");
            }
            else
            {
                LogNonAbsoluteAuthorizationServers(protectedResource);
            }

            foreach (var authorizationServerBase in AuthorizationServerBases(challenge, protectedResource, authorizationServerOverride))
            {
                _logger.LogInformation("Probing authorization server base {Base}", authorizationServerBase);

                var authorizationServer = await _authorizationServerClient.DiscoverAsync(authorizationServerBase, cancellationToken).ConfigureAwait(false);
                if (authorizationServer is null)
                {
                    continue;
                }

                _logger.LogInformation("Selected authorization server {Issuer} from base {Base}", authorizationServer.Issuer, authorizationServerBase);

                return new OAuthDiscoveryResult(
                    challenge,
                    protectedResource,
                    authorizationServer,
                    authorizationServerBase,
                    authorizationServer.GrantTypesSupported is null ? [] : [.. authorizationServer.GrantTypesSupported],
                    challenge?.Scope,
                    ResolveResource(serviceRoot, protectedResource, resourceOverride));
            }

            throw new OutboundDiscoveryException("Could not discover an authorization server; pass --auth-server.");
        }

        /// <summary>
        /// Builds the RFC 9728 protected resource metadata URLs to probe, in order, per step 4 of the discovery
        /// algorithm.
        /// </summary>
        /// <param name="serviceRoot">The absolute OData service root the challenge came from.</param>
        /// <param name="challenge">The <c>Bearer</c> challenge, or <see langword="null"/> when the response carried none.</param>
        /// <returns>
        /// Exactly the challenge <c>resource_metadata</c> URL when the challenge carries an absolute one;
        /// otherwise the origin well-known URL followed by the path-prefixed one, with the path-prefixed URL
        /// omitted when the service root's path is <c>/</c>.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="serviceRoot"/> is <see langword="null"/>.</exception>
        /// <exception cref="ArgumentException">Thrown when <paramref name="serviceRoot"/> is not an absolute URI.</exception>
        /// <example>
        /// <code>
        /// var candidates = OAuthDiscovery.PrmCandidates(new Uri("http://localhost/odata/"), null);
        ///
        /// // http://localhost/.well-known/oauth-protected-resource
        /// // http://localhost/.well-known/oauth-protected-resource/odata
        /// </code>
        /// </example>
        /// <remarks>
        /// A trailing <c>/</c> on the service root's path is trimmed before the path is appended, so
        /// <c>http://localhost/odata/</c> and <c>http://localhost/odata</c> produce the same candidates.
        /// </remarks>
        public static IReadOnlyList<Uri> PrmCandidates(Uri serviceRoot, OAuthChallenge? challenge)
        {
            ArgumentNullException.ThrowIfNull(serviceRoot);

            if (!serviceRoot.IsAbsoluteUri)
            {
                throw new ArgumentException($"The OData service root must be absolute: {serviceRoot}", nameof(serviceRoot));
            }

            if (challenge?.ResourceMetadata is { IsAbsoluteUri: true } resourceMetadata)
            {
                return [resourceMetadata];
            }

            var origin = serviceRoot.GetLeftPart(UriPartial.Authority);
            var candidates = new List<Uri>
            {
                new($"{origin}{ODataMcpAuthConstants.OAuthProtectedResourceSuffix}")
            };

            var path = serviceRoot.AbsolutePath.TrimEnd('/');
            if (!string.IsNullOrWhiteSpace(path))
            {
                candidates.Add(new Uri($"{origin}{ODataMcpAuthConstants.OAuthProtectedResourceSuffix}{path}"));
            }

            return candidates;
        }

        #endregion

        #region Internal Methods

        /// <summary>
        /// Logs a warning for every <c>authorization_servers</c> entry that is not an absolute URI, because
        /// <see cref="AuthorizationServerBases(OAuthChallenge, SdkAuth.ProtectedResourceMetadata, Uri)"/> skips
        /// those entries silently.
        /// </summary>
        /// <param name="protectedResource">The protected resource metadata whose entries were skipped.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="protectedResource"/> is <see langword="null"/>.</exception>
        internal void LogNonAbsoluteAuthorizationServers(SdkAuth.ProtectedResourceMetadata protectedResource)
        {
            ArgumentNullException.ThrowIfNull(protectedResource);

            if (protectedResource.AuthorizationServers is null)
            {
                return;
            }

            foreach (var authorizationServer in protectedResource.AuthorizationServers)
            {
                if (string.IsNullOrWhiteSpace(authorizationServer) || !Uri.TryCreate(authorizationServer, UriKind.Absolute, out _))
                {
                    _logger.LogWarning("Ignoring non-absolute authorization_servers entry {Entry} in protected resource metadata", authorizationServer);
                }
            }
        }

        /// <summary>
        /// Records what a challenge <c>client_id</c> actually is, and warns when it is the Microsoft Graph
        /// resource application id.
        /// </summary>
        /// <param name="challenge">The <c>Bearer</c> challenge, or <see langword="null"/> when the response carried none.</param>
        /// <remarks>
        /// Per <c>AUTH-13</c> the value identifies the protected resource application; it is never this
        /// process's client id, and it is never surfaced under a name containing <c>ClientId</c> alone.
        /// </remarks>
        internal void LogResourceClientId(OAuthChallenge? challenge)
        {
            if (challenge is null || string.IsNullOrWhiteSpace(challenge.ResourceClientId))
            {
                return;
            }

            _logger.LogInformation("Challenge client_id {ResourceClientId} is the resource application id, not the client id", challenge.ResourceClientId);

            if (string.Equals(challenge.ResourceClientId, ODataMcpAuthConstants.MicrosoftGraphResourceAppId, StringComparison.Ordinal))
            {
                _logger.LogWarning("Challenge client_id is the Microsoft Graph resource application id; pass --client-id with your own application id");
            }
        }

        /// <summary>
        /// Resolves the RFC 8707 resource indicator per step 8b of the discovery algorithm.
        /// </summary>
        /// <param name="serviceRoot">The absolute OData service root the challenge came from.</param>
        /// <param name="protectedResource">The RFC 9728 protected resource metadata, or <see langword="null"/> when none was found.</param>
        /// <param name="resourceOverride">The operator's <c>--resource</c> value, or <see langword="null"/> when unset.</param>
        /// <returns>
        /// The protected resource metadata <c>resource</c> when it is an absolute URI, else
        /// <paramref name="resourceOverride"/>, else <see langword="null"/>.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="serviceRoot"/> is <see langword="null"/>.</exception>
        /// <remarks>
        /// There is no origin fallback. Per <c>specs/v3/AUTHENTICATION.md</c> step 8b an RFC 8707
        /// <c>resource</c> goes out only when the protected resource published one or the operator passed
        /// <c>--resource</c>; otherwise the grant relies on <c>scope</c> alone, because an invented audience is
        /// rejected outright by an authorization server that does not recognize it.
        /// <para>
        /// <paramref name="serviceRoot"/> is still required so callers cannot pass a half-populated discovery
        /// context, and so the origin the identity assertion grant needs stays derivable from the same input.
        /// </para>
        /// </remarks>
        internal static Uri? ResolveResource(Uri serviceRoot, SdkAuth.ProtectedResourceMetadata? protectedResource, Uri? resourceOverride)
        {
            ArgumentNullException.ThrowIfNull(serviceRoot);

            if (!string.IsNullOrWhiteSpace(protectedResource?.Resource)
                && Uri.TryCreate(protectedResource.Resource, UriKind.Absolute, out var resource))
            {
                return resource;
            }

            return resourceOverride;
        }

        #endregion

    }

}
