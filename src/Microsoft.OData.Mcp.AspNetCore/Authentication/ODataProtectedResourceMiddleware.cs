// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.OData.Mcp.AspNetCore.Constants;
using Microsoft.OData.Mcp.AspNetCore.Hosting;
using SdkAuth = ModelContextProtocol.Authentication;

namespace Microsoft.OData.Mcp.AspNetCore.Authentication
{

    /// <summary>
    /// Publishes RFC 9728 protected resource metadata for every OData route prefix the app serves, and rewrites
    /// the <c>WWW-Authenticate</c> challenge on a <c>401</c> beneath one of those prefixes so a client is told
    /// where that document lives.
    /// </summary>
    /// <example>
    /// <code>
    /// GET /.well-known/oauth-protected-resource/odata
    /// 200 application/json
    /// {"resource":"https://api.contoso.com/odata","authorization_servers":["https://login.contoso.com/v2.0"],"bearer_methods_supported":["header"]}
    /// </code>
    /// </example>
    /// <remarks>
    /// <see cref="ODataProtectedResourceStartupFilter"/> puts this at the very front of the pipeline, before
    /// authentication and authorization, because a protected resource metadata document that answers <c>401</c>
    /// is useless — a client that follows <c>resource_metadata</c> into a challenge has learned nothing. That
    /// is the "Graph trap" in <c>specs/v3/AUTHENTICATION.md</c>, and the only defence against it is serving the
    /// document anonymously.
    /// <para>
    /// Prefix discovery is deferred to the first request because endpoint data sources are not complete until
    /// the host has started; <see cref="ODataMcpSessionFactory"/> defers for the same reason and reads the same
    /// discovery.
    /// </para>
    /// </remarks>
    public sealed class ODataProtectedResourceMiddleware
    {

        #region Fields

        /// <summary>
        /// Guards the one-time prefix discovery.
        /// </summary>
        internal readonly object _gate = new();

        /// <summary>
        /// The logger the discovered prefixes and any unparseable challenge are recorded to.
        /// </summary>
        internal readonly ILogger<ODataProtectedResourceMiddleware> _logger;

        /// <summary>
        /// The next middleware in the pipeline.
        /// </summary>
        internal readonly RequestDelegate _next;

        /// <summary>
        /// What this resource publishes about itself.
        /// </summary>
        internal readonly IOptions<ODataProtectedResourceOptions> _options;

        /// <summary>
        /// The discovered or configured OData route prefixes, or <see langword="null"/> before the first request.
        /// </summary>
        internal IReadOnlyList<string>? _prefixes;

        /// <summary>
        /// The application service provider route discovery reads endpoint data sources from.
        /// </summary>
        internal readonly IServiceProvider _services;

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="ODataProtectedResourceMiddleware"/> class.
        /// </summary>
        /// <param name="next">The next middleware in the pipeline.</param>
        /// <param name="services">The application service provider route discovery reads endpoint data sources from.</param>
        /// <param name="options">What this resource publishes about itself.</param>
        /// <param name="logger">The logger the discovered prefixes and any unparseable challenge are recorded to.</param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="next"/>, <paramref name="services"/>, <paramref name="options"/>, or
        /// <paramref name="logger"/> is <see langword="null"/>.
        /// </exception>
        public ODataProtectedResourceMiddleware(
            RequestDelegate next,
            IServiceProvider services,
            IOptions<ODataProtectedResourceOptions> options,
            ILogger<ODataProtectedResourceMiddleware> logger)
        {
            ArgumentNullException.ThrowIfNull(next);
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(options);
            ArgumentNullException.ThrowIfNull(logger);

            _logger = logger;
            _next = next;
            _options = options;
            _services = services;
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Serves a protected resource metadata document, or arms the challenge annotation for a request that
        /// falls beneath a covered prefix, then continues the pipeline.
        /// </summary>
        /// <param name="context">The request being served.</param>
        /// <returns>
        /// A task that completes when the request has been handled.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="context"/> is <see langword="null"/>.</exception>
        /// <remarks>
        /// A well-known path that names a prefix this app does not serve is passed through untouched rather
        /// than answered with a <c>404</c>: the path may belong to another component in the same host.
        /// </remarks>
        public async Task InvokeAsync(HttpContext context)
        {
            ArgumentNullException.ThrowIfNull(context);

            if (context.Request.Path.StartsWithSegments(ProtectedResourceConstants.WellKnownPath, StringComparison.OrdinalIgnoreCase, out var remainder))
            {
                if (!TryResolveDocumentPrefix(remainder, out var documentPrefix))
                {
                    await _next(context).ConfigureAwait(false);

                    return;
                }

                if (!HttpMethods.IsGet(context.Request.Method) && !HttpMethods.IsHead(context.Request.Method))
                {
                    context.Response.StatusCode = StatusCodes.Status405MethodNotAllowed;

                    return;
                }

                await WriteMetadataAsync(context, documentPrefix).ConfigureAwait(false);

                return;
            }

            if (_options.Value.AnnotateChallenges && TryMatchPrefix(context.Request.Path, out var prefix))
            {
                var resourceMetadataUrl = MetadataUrl(context, prefix);

                context.Response.OnStarting(() =>
                {
                    Annotate(context, resourceMetadataUrl);

                    return Task.CompletedTask;
                });
            }

            await _next(context).ConfigureAwait(false);
        }

        #endregion

        #region Internal Methods

        /// <summary>
        /// Rewrites the <c>WWW-Authenticate</c> header of a <c>401</c> so it carries <c>resource_metadata</c>.
        /// </summary>
        /// <param name="context">The request whose response is about to start.</param>
        /// <param name="resourceMetadataUrl">The absolute protected resource metadata URL for the matched prefix.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="context"/> is <see langword="null"/>.</exception>
        /// <exception cref="ArgumentException">Thrown when <paramref name="resourceMetadataUrl"/> is <see langword="null"/>, empty, or whitespace.</exception>
        /// <remarks>
        /// Runs from <see cref="HttpResponse.OnStarting(Func{Task})"/>, where a throw would tear down a response
        /// that was otherwise fine, so a header this method cannot parse is left exactly as the app wrote it and
        /// the reason is logged at <see cref="LogLevel.Debug"/>.
        /// </remarks>
        internal void Annotate(HttpContext context, string resourceMetadataUrl)
        {
            ArgumentNullException.ThrowIfNull(context);
            ArgumentException.ThrowIfNullOrWhiteSpace(resourceMetadataUrl);

            try
            {
                if (context.Response.StatusCode != StatusCodes.Status401Unauthorized)
                {
                    return;
                }

                var options = _options.Value;
                var scope = options.ScopesSupported.Count == 0 ? null : string.Join(' ', options.ScopesSupported);
                var existing = context.Response.Headers.WWWAuthenticate;
                if (existing.Count == 0 || existing.All(string.IsNullOrWhiteSpace))
                {
                    context.Response.Headers.WWWAuthenticate = BuildChallenge(resourceMetadataUrl, scope);

                    return;
                }

                var values = new string[existing.Count];
                var annotated = false;
                for (var index = 0; index < existing.Count; index++)
                {
                    var value = existing[index] ?? string.Empty;
                    if (!annotated && TryAnnotateChallenges(value, resourceMetadataUrl, scope, out var rewritten))
                    {
                        annotated = true;
                        values[index] = rewritten;

                        continue;
                    }

                    values[index] = value;
                }

                if (annotated)
                {
                    context.Response.Headers.WWWAuthenticate = values;
                }
            }
            catch (Exception exception) when (exception is ArgumentException or FormatException or InvalidOperationException)
            {
                _logger.LogDebug(exception, "Leaving the WWW-Authenticate header on {Path} untouched; it could not be rewritten", context.Request.Path);
            }
        }

        /// <summary>
        /// Builds the challenge a <c>401</c> that carried no <c>WWW-Authenticate</c> at all is given.
        /// </summary>
        /// <param name="resourceMetadataUrl">The absolute protected resource metadata URL.</param>
        /// <param name="scope">The space-joined scopes, or <see langword="null"/> when none are published.</param>
        /// <returns>
        /// A <c>Bearer</c> challenge carrying <c>resource_metadata</c>, and <c>scope</c> when published.
        /// </returns>
        /// <exception cref="ArgumentException">Thrown when <paramref name="resourceMetadataUrl"/> is <see langword="null"/>, empty, or whitespace.</exception>
        internal static string BuildChallenge(string resourceMetadataUrl, string? scope)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(resourceMetadataUrl);

            var challenge = $"{ProtectedResourceConstants.BearerScheme} {ProtectedResourceConstants.ResourceMetadataParameter}=\"{resourceMetadataUrl}\"";

            return string.IsNullOrWhiteSpace(scope)
                ? challenge
                : $"{challenge}, {ProtectedResourceConstants.ScopeParameter}=\"{scope}\"";
        }

        /// <summary>
        /// Builds the RFC 9728 document for one prefix, in terms of the request that asked for it.
        /// </summary>
        /// <param name="context">The request the document is being served to.</param>
        /// <param name="prefix">The OData route prefix, empty for a service at the application root.</param>
        /// <returns>
        /// The document.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="context"/> or <paramref name="prefix"/> is <see langword="null"/>.</exception>
        /// <remarks>
        /// The scheme, host, and path base come from the request rather than from configuration, so an API
        /// behind a reverse proxy publishes the URL a client actually reached it on — provided the app is
        /// wired to honour the forwarded headers.
        /// </remarks>
        internal SdkAuth.ProtectedResourceMetadata BuildMetadata(HttpContext context, string prefix)
        {
            ArgumentNullException.ThrowIfNull(context);
            ArgumentNullException.ThrowIfNull(prefix);

            var options = _options.Value;

            return new SdkAuth.ProtectedResourceMetadata
            {
                AuthorizationServers = [.. options.AuthorizationServers.Select(authorizationServer => authorizationServer.ToString())],
                BearerMethodsSupported = [ProtectedResourceConstants.HeaderBearerMethod],
                Resource = ResourceIdentifier(context, prefix),
                ResourceDocumentation = options.ResourceDocumentation?.ToString(),
                ResourceName = options.ResourceName,
                ScopesSupported = [.. options.ScopesSupported]
            };
        }

        /// <summary>
        /// Lists the well-known paths a set of prefixes is published at, for the startup log line.
        /// </summary>
        /// <param name="prefixes">The covered prefixes, in discovery order.</param>
        /// <returns>
        /// The origin form followed by one path-suffixed form per non-empty prefix.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="prefixes"/> is <see langword="null"/>.</exception>
        internal static IReadOnlyList<string> DocumentPaths(IReadOnlyList<string> prefixes)
        {
            ArgumentNullException.ThrowIfNull(prefixes);

            var paths = new List<string>();
            if (prefixes.Count > 0)
            {
                paths.Add(ProtectedResourceConstants.WellKnownPath);
            }

            foreach (var prefix in prefixes)
            {
                if (prefix.Length > 0)
                {
                    paths.Add($"{ProtectedResourceConstants.WellKnownPath}/{prefix}");
                }
            }

            return paths;
        }

        /// <summary>
        /// Determines whether a character is a <c>token68</c> character, excluding the trailing <c>=</c> padding.
        /// </summary>
        /// <param name="character">The character to test.</param>
        /// <returns>
        /// <see langword="true"/> when the character may appear in a <c>token68</c>.
        /// </returns>
        internal static bool IsToken68Character(char character)
        {
            return character is (>= 'a' and <= 'z') or (>= 'A' and <= 'Z') or (>= '0' and <= '9') or '-' or '.' or '_' or '~' or '+' or '/';
        }

        /// <summary>
        /// Determines whether a character is an RFC 9110 <c>tchar</c>.
        /// </summary>
        /// <param name="character">The character to test.</param>
        /// <returns>
        /// <see langword="true"/> when the character may appear in a token.
        /// </returns>
        internal static bool IsTokenCharacter(char character)
        {
            return character is (>= 'a' and <= 'z') or (>= 'A' and <= 'Z') or (>= '0' and <= '9')
                or '!' or '#' or '$' or '%' or '&' or '\'' or '*' or '+' or '-' or '.' or '^' or '_' or '`' or '|' or '~';
        }

        /// <summary>
        /// Builds the absolute protected resource metadata URL for one prefix.
        /// </summary>
        /// <param name="context">The request the URL is built in terms of.</param>
        /// <param name="prefix">The OData route prefix, empty for a service at the application root.</param>
        /// <returns>
        /// The path-suffixed well-known URL, or the origin form when <paramref name="prefix"/> is empty.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="context"/> or <paramref name="prefix"/> is <see langword="null"/>.</exception>
        internal static string MetadataUrl(HttpContext context, string prefix)
        {
            ArgumentNullException.ThrowIfNull(context);
            ArgumentNullException.ThrowIfNull(prefix);

            var origin = $"{context.Request.Scheme}://{context.Request.Host.Value}{context.Request.PathBase.Value?.TrimEnd('/')}";
            var trimmed = prefix.Trim('/');

            return trimmed.Length == 0
                ? $"{origin}{ProtectedResourceConstants.WellKnownPath}"
                : $"{origin}{ProtectedResourceConstants.WellKnownPath}/{trimmed}";
        }

        /// <summary>
        /// Returns the OData route prefixes this resource covers, discovering them once on first use.
        /// </summary>
        /// <returns>
        /// The prefixes without leading or trailing slashes, deduplicated, in discovery order.
        /// </returns>
        /// <remarks>
        /// A non-empty <see cref="ODataProtectedResourceOptions.Prefixes"/> replaces discovery outright, which
        /// is the escape hatch for a host whose routing table carries no OData metadata at all.
        /// </remarks>
        internal IReadOnlyList<string> Prefixes()
        {
            if (_prefixes is not null)
            {
                return _prefixes;
            }

            lock (_gate)
            {
                if (_prefixes is not null)
                {
                    return _prefixes;
                }

                var options = _options.Value;
                var discovered = options.Prefixes.Count > 0
                    ? options.Prefixes.Select(prefix => prefix.Trim('/'))
                    : ODataMcpRouteDiscovery
                        .Discover(_services, _services.GetService<IOptions<ODataMcpHostOptions>>()?.Value ?? new ODataMcpHostOptions())
                        .Select(binding => binding.Prefix.Trim('/'));

                var prefixes = new List<string>();
                var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var prefix in discovered)
                {
                    if (seen.Add(prefix))
                    {
                        prefixes.Add(prefix);
                    }
                }

                _prefixes = prefixes;

                _logger.LogInformation(
                    "Publishing RFC 9728 protected resource metadata for OData prefixes [{Prefixes}] at [{Documents}]",
                    string.Join(", ", prefixes.Select(prefix => prefix.Length == 0 ? "(root)" : prefix)),
                    string.Join(", ", DocumentPaths(prefixes)));

                return _prefixes;
            }
        }

        /// <summary>
        /// Reads an RFC 9110 token and leaves the cursor on the first character that is not a <c>tchar</c>.
        /// </summary>
        /// <param name="value">The header value being tokenized.</param>
        /// <param name="index">The cursor to advance.</param>
        /// <returns>
        /// The token, which is empty when the cursor is not on a <c>tchar</c>.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="value"/> is <see langword="null"/>.</exception>
        internal static string ReadToken(string value, ref int index)
        {
            ArgumentNullException.ThrowIfNull(value);

            var start = index;
            while (index < value.Length && IsTokenCharacter(value[index]))
            {
                index++;
            }

            return value[start..index];
        }

        /// <summary>
        /// Builds the RFC 8707 resource identifier one prefix is published under.
        /// </summary>
        /// <param name="context">The request the identifier is built in terms of.</param>
        /// <param name="prefix">The OData route prefix, empty for a service at the application root.</param>
        /// <returns>
        /// <c>{scheme}://{host}{pathBase}/{prefix}</c>, with no trailing slash.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="context"/> or <paramref name="prefix"/> is <see langword="null"/>.</exception>
        internal static string ResourceIdentifier(HttpContext context, string prefix)
        {
            ArgumentNullException.ThrowIfNull(context);
            ArgumentNullException.ThrowIfNull(prefix);

            var origin = $"{context.Request.Scheme}://{context.Request.Host.Value}{context.Request.PathBase.Value?.TrimEnd('/')}";
            var trimmed = prefix.Trim('/');

            return trimmed.Length == 0 ? origin : $"{origin}/{trimmed}";
        }

        /// <summary>
        /// Advances the cursor past optional white space.
        /// </summary>
        /// <param name="value">The header value being tokenized.</param>
        /// <param name="index">The cursor to advance.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="value"/> is <see langword="null"/>.</exception>
        internal static void SkipWhitespace(string value, ref int index)
        {
            ArgumentNullException.ThrowIfNull(value);

            while (index < value.Length && char.IsWhiteSpace(value[index]))
            {
                index++;
            }
        }

        /// <summary>
        /// Advances the cursor past optional white space and the empty list elements RFC 9110 tolerates.
        /// </summary>
        /// <param name="value">The header value being tokenized.</param>
        /// <param name="index">The cursor to advance.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="value"/> is <see langword="null"/>.</exception>
        internal static void SkipWhitespaceAndCommas(string value, ref int index)
        {
            ArgumentNullException.ThrowIfNull(value);

            while (index < value.Length && (char.IsWhiteSpace(value[index]) || value[index] == ','))
            {
                index++;
            }
        }

        /// <summary>
        /// Rewrites one <c>WWW-Authenticate</c> header value so its <c>Bearer</c> challenge carries
        /// <c>resource_metadata</c>.
        /// </summary>
        /// <param name="headerValue">The header value exactly as the app wrote it.</param>
        /// <param name="resourceMetadataUrl">The absolute protected resource metadata URL.</param>
        /// <param name="scope">The space-joined scopes, or <see langword="null"/> when none are published.</param>
        /// <param name="annotated">The rewritten value when this method returns <see langword="true"/>; otherwise <paramref name="headerValue"/>.</param>
        /// <returns>
        /// <see langword="true"/> when a <c>Bearer</c> challenge was rewritten.
        /// </returns>
        /// <example>
        /// <code>
        /// ODataProtectedResourceMiddleware.TryAnnotateChallenges(
        ///     "Basic realm=\"legacy\", Bearer",
        ///     "https://api.contoso.com/.well-known/oauth-protected-resource/odata",
        ///     null,
        ///     out var annotated);
        ///
        /// // Basic realm="legacy", Bearer resource_metadata="https://api.contoso.com/.well-known/oauth-protected-resource/odata"
        /// </code>
        /// </example>
        /// <remarks>
        /// Implements the RFC 9110 section 11.6.1 challenge grammar so a comma inside a quoted-string never
        /// splits a challenge and a following scheme is never mistaken for an auth-param. Every challenge is
        /// copied out of <paramref name="headerValue"/> verbatim; only the first <c>Bearer</c> challenge that
        /// lacks <c>resource_metadata</c> gains parameters. A challenge that already names a document is left
        /// alone — the app knows something this middleware does not. Anything that does not parse returns
        /// <see langword="false"/>, which leaves the header untouched.
        /// </remarks>
        internal static bool TryAnnotateChallenges(string headerValue, string resourceMetadataUrl, string? scope, out string annotated)
        {
            annotated = headerValue;

            if (string.IsNullOrWhiteSpace(headerValue) || string.IsNullOrWhiteSpace(resourceMetadataUrl))
            {
                return false;
            }

            var builder = new StringBuilder(headerValue.Length + resourceMetadataUrl.Length + 64);
            var index = 0;
            var rewritten = false;
            string? pendingScheme = null;
            var pendingSchemeStart = 0;

            while (true)
            {
                string scheme;
                int schemeStart;
                if (pendingScheme is not null)
                {
                    scheme = pendingScheme;
                    schemeStart = pendingSchemeStart;
                    pendingScheme = null;
                }
                else
                {
                    SkipWhitespaceAndCommas(headerValue, ref index);
                    if (index >= headerValue.Length)
                    {
                        break;
                    }

                    schemeStart = index;
                    scheme = ReadToken(headerValue, ref index);
                    if (scheme.Length == 0)
                    {
                        return false;
                    }
                }

                var challengeEnd = index;
                var allowCredentials = true;
                var expectSeparator = false;
                var hasCredentials = false;
                var hasParameters = false;
                var hasResourceMetadata = false;
                var hasScope = false;

                while (true)
                {
                    SkipWhitespace(headerValue, ref index);
                    if (index >= headerValue.Length)
                    {
                        break;
                    }

                    if (headerValue[index] == ',')
                    {
                        index++;
                        allowCredentials = false;
                        expectSeparator = false;

                        continue;
                    }

                    if (expectSeparator)
                    {
                        return false;
                    }

                    if (allowCredentials && TrySkipToken68(headerValue, ref index))
                    {
                        allowCredentials = false;
                        challengeEnd = index;
                        expectSeparator = true;
                        hasCredentials = true;

                        continue;
                    }

                    var nameStart = index;
                    var name = ReadToken(headerValue, ref index);
                    if (name.Length == 0)
                    {
                        return false;
                    }

                    SkipWhitespace(headerValue, ref index);
                    if (index >= headerValue.Length || headerValue[index] != '=')
                    {
                        if (allowCredentials)
                        {
                            return false;
                        }

                        pendingScheme = name;
                        pendingSchemeStart = nameStart;

                        break;
                    }

                    index++;
                    SkipWhitespace(headerValue, ref index);
                    if (index >= headerValue.Length)
                    {
                        return false;
                    }

                    if (headerValue[index] == '"')
                    {
                        if (!TrySkipQuotedString(headerValue, ref index))
                        {
                            return false;
                        }
                    }
                    else if (ReadToken(headerValue, ref index).Length == 0)
                    {
                        return false;
                    }

                    allowCredentials = false;
                    challengeEnd = index;
                    expectSeparator = true;
                    hasParameters = true;
                    hasResourceMetadata |= ProtectedResourceConstants.ResourceMetadataParameter.Equals(name, StringComparison.OrdinalIgnoreCase);
                    hasScope |= ProtectedResourceConstants.ScopeParameter.Equals(name, StringComparison.OrdinalIgnoreCase);
                }

                if (builder.Length > 0)
                {
                    builder.Append(", ");
                }

                builder.Append(headerValue, schemeStart, challengeEnd - schemeStart);

                if (!rewritten
                    && !hasCredentials
                    && !hasResourceMetadata
                    && ProtectedResourceConstants.BearerScheme.Equals(scheme, StringComparison.OrdinalIgnoreCase))
                {
                    builder.Append(hasParameters ? ", " : " ");
                    builder.Append(ProtectedResourceConstants.ResourceMetadataParameter).Append("=\"").Append(resourceMetadataUrl).Append('"');

                    if (!hasScope && !string.IsNullOrWhiteSpace(scope))
                    {
                        builder.Append(", ").Append(ProtectedResourceConstants.ScopeParameter).Append("=\"").Append(scope).Append('"');
                    }

                    rewritten = true;
                }
            }

            if (!rewritten)
            {
                return false;
            }

            annotated = builder.ToString();

            return true;
        }

        /// <summary>
        /// Finds the covered prefix a request path falls beneath.
        /// </summary>
        /// <param name="path">The request path, with the path base already removed.</param>
        /// <param name="prefix">The matched prefix when this method returns <see langword="true"/>.</param>
        /// <returns>
        /// <see langword="true"/> when the path is part of a covered OData service.
        /// </returns>
        /// <remarks>
        /// Matching is segment-safe, so a prefix of <c>odata</c> never claims <c>/odatafoo</c>. A non-empty
        /// prefix is tried before the empty one, so a service mounted at the application root cannot shadow a
        /// more specific sibling.
        /// </remarks>
        internal bool TryMatchPrefix(PathString path, out string prefix)
        {
            var prefixes = Prefixes();
            foreach (var candidate in prefixes)
            {
                if (candidate.Length > 0 && path.StartsWithSegments($"/{candidate}", StringComparison.OrdinalIgnoreCase))
                {
                    prefix = candidate;

                    return true;
                }
            }

            foreach (var candidate in prefixes)
            {
                if (candidate.Length == 0)
                {
                    prefix = candidate;

                    return true;
                }
            }

            prefix = string.Empty;

            return false;
        }

        /// <summary>
        /// Resolves which document a well-known request is asking for.
        /// </summary>
        /// <param name="remainder">The request path after the well-known base, empty for the origin form.</param>
        /// <param name="prefix">The prefix the document describes when this method returns <see langword="true"/>.</param>
        /// <returns>
        /// <see langword="true"/> when this middleware serves the request.
        /// </returns>
        /// <remarks>
        /// The origin form answers for the first prefix in discovery order, which is the whole document when a
        /// host serves exactly one OData service. A suffix that names no known prefix is not ours.
        /// </remarks>
        internal bool TryResolveDocumentPrefix(PathString remainder, out string prefix)
        {
            prefix = string.Empty;

            var prefixes = Prefixes();
            if (prefixes.Count == 0)
            {
                return false;
            }

            var suffix = (remainder.Value ?? string.Empty).Trim('/');
            if (suffix.Length == 0)
            {
                prefix = prefixes[0];

                return true;
            }

            foreach (var candidate in prefixes)
            {
                if (candidate.Length > 0 && candidate.Equals(suffix, StringComparison.OrdinalIgnoreCase))
                {
                    prefix = candidate;

                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Advances the cursor past a quoted-string, honouring <c>\</c> escapes.
        /// </summary>
        /// <param name="value">The header value being tokenized.</param>
        /// <param name="index">The cursor, which must point at the opening quote.</param>
        /// <returns>
        /// <see langword="true"/> when a complete quoted-string was consumed.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="value"/> is <see langword="null"/>.</exception>
        internal static bool TrySkipQuotedString(string value, ref int index)
        {
            ArgumentNullException.ThrowIfNull(value);

            if (index >= value.Length || value[index] != '"')
            {
                return false;
            }

            var scan = index + 1;
            while (scan < value.Length)
            {
                if (value[scan] == '\\')
                {
                    scan += 2;

                    continue;
                }

                if (value[scan] == '"')
                {
                    index = scan + 1;

                    return true;
                }

                scan++;
            }

            return false;
        }

        /// <summary>
        /// Consumes a <c>token68</c> credential when, and only when, the item at the cursor really is one.
        /// </summary>
        /// <param name="value">The header value being tokenized.</param>
        /// <param name="index">The cursor, advanced past the credential only when this method returns <see langword="true"/>.</param>
        /// <returns>
        /// <see langword="true"/> when a <c>token68</c> was consumed.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="value"/> is <see langword="null"/>.</exception>
        /// <remarks>
        /// A <c>token68</c> runs to the end of the header value or to the next comma, so an item such as
        /// <c>realm=api</c> is rejected here and parsed as an auth-param instead.
        /// </remarks>
        internal static bool TrySkipToken68(string value, ref int index)
        {
            ArgumentNullException.ThrowIfNull(value);

            var scan = index;
            while (scan < value.Length && IsToken68Character(value[scan]))
            {
                scan++;
            }

            if (scan == index)
            {
                return false;
            }

            while (scan < value.Length && value[scan] == '=')
            {
                scan++;
            }

            var probe = scan;
            while (probe < value.Length && char.IsWhiteSpace(value[probe]))
            {
                probe++;
            }

            if (probe < value.Length && value[probe] != ',')
            {
                return false;
            }

            index = scan;

            return true;
        }

        /// <summary>
        /// Writes the protected resource metadata document for one prefix.
        /// </summary>
        /// <param name="context">The request being answered.</param>
        /// <param name="prefix">The OData route prefix the document describes.</param>
        /// <returns>
        /// A task that completes once the document has been written.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="context"/> or <paramref name="prefix"/> is <see langword="null"/>.</exception>
        /// <remarks>
        /// A <c>HEAD</c> gets the same status and headers with no body, so a client can probe for the document
        /// without reading it.
        /// </remarks>
        internal async Task WriteMetadataAsync(HttpContext context, string prefix)
        {
            ArgumentNullException.ThrowIfNull(context);
            ArgumentNullException.ThrowIfNull(prefix);

            var metadata = BuildMetadata(context, prefix);

            context.Response.StatusCode = StatusCodes.Status200OK;
            context.Response.ContentType = ProtectedResourceConstants.ContentType;
            context.Response.Headers.CacheControl = ProtectedResourceConstants.CacheControl;

            if (HttpMethods.IsHead(context.Request.Method))
            {
                return;
            }

            await JsonSerializer.SerializeAsync(
                context.Response.Body,
                metadata,
                ODataProtectedResourceJsonContext.Default.ProtectedResourceMetadata,
                context.RequestAborted).ConfigureAwait(false);
        }

        #endregion

    }

}
