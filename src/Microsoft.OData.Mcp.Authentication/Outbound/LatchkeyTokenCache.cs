// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Latchkey;
using Latchkey.Backends.Dpapi;
using Latchkey.Backends.Files;
using Microsoft.Extensions.Logging;
using LatchkeyApi = Latchkey.Latchkey;
using SdkAuth = ModelContextProtocol.Authentication;

namespace Microsoft.OData.Mcp.Authentication.Outbound
{

    /// <summary>
    /// The durable, single-slot SDK <see cref="SdkAuth.ITokenCache"/> for outbound OAuth, backed by the
    /// operating system credential store through <see href="https://www.nuget.org/packages/Latchkey">Latchkey</see>
    /// so a restart of the same service root and client id does not re-prompt the operator.
    /// </summary>
    /// <example>
    /// <code>
    /// var cache = new LatchkeyTokenCache(new Uri("https://api.example.com/odata/"), "client-a", tokenCachePath: null, logger);
    ///
    /// await cache.StoreTokensAsync(tokens, cancellationToken);
    /// var restored = await cache.GetTokensAsync(cancellationToken);
    /// </code>
    /// </example>
    /// <remarks>
    /// Per <c>AUTH-7</c> this implements the SDK interface directly; it is never wrapped in a new
    /// <c>ICredentialStore</c> or <c>IOutboundTokenCache</c> abstraction. Per <c>AUTH-12</c> no access token,
    /// refresh token, or client secret ever reaches a log sink: every log line that mentions a token routes
    /// through <see cref="OutboundAuthLogRedactor.DescribeToken(SdkAuth.TokenContainer)"/>.
    /// <para>
    /// The backend map is pinned rather than left at <see cref="LatchkeyBackend.Auto"/>'s native seed, because
    /// Windows Credential Manager caps a value at 2560 bytes — smaller than a typical
    /// <see cref="SdkAuth.TokenContainer"/>. Windows therefore uses DPAPI-sealed files, macOS the Keychain,
    /// Linux the Secret Service, and every platform falls back to a plain file store.
    /// </para>
    /// </remarks>
    public sealed class LatchkeyTokenCache : SdkAuth.ITokenCache
    {

        #region Fields

        /// <summary>
        /// The Latchkey <c>DisplayName</c> the operating system prompts an operator with when a store needs it.
        /// </summary>
        private const string LatchkeyDisplayName = "OData MCP";

        /// <summary>
        /// Serializes every read, write, and delete against <see cref="_store"/>, so a concurrent
        /// <see cref="GetTokensAsync(CancellationToken)"/> can never observe a half-written slot.
        /// </summary>
        private readonly SemaphoreSlim _gate = new(1, 1);

        /// <summary>
        /// The logger this instance records cache outcomes to; never a token value.
        /// </summary>
        private readonly ILogger<LatchkeyTokenCache> _logger;

        /// <summary>
        /// The operating system credential store this cache persists its single slot in.
        /// </summary>
        private readonly ILatchkey _store;

        #endregion

        #region Properties

        /// <summary>
        /// Gets the Latchkey key this cache's single slot lives under: the lowercase hex SHA-256 of the service
        /// root and client id this instance was constructed for.
        /// </summary>
        /// <remarks>
        /// The key is a digest, not a credential, so it is safe to log; the values hashed into it are not
        /// secrets either, but they are never logged from here regardless.
        /// </remarks>
        internal string Key { get; }

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="LatchkeyTokenCache"/> class over the operating system
        /// credential store selected for the current platform.
        /// </summary>
        /// <param name="serviceRoot">The absolute OData service root the cached tokens authorize.</param>
        /// <param name="clientId">The OAuth client id the cached tokens were issued to.</param>
        /// <param name="tokenCachePath">The directory the File and DPAPI backends store values in, or <see langword="null"/> for the per-user default.</param>
        /// <param name="logger">The logger this instance records cache outcomes to.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="serviceRoot"/> or <paramref name="logger"/> is <see langword="null"/>.</exception>
        /// <exception cref="ArgumentException">Thrown when <paramref name="clientId"/> is empty or white space, or <paramref name="serviceRoot"/> is not an absolute URI.</exception>
        /// <example>
        /// <code>
        /// services.AddSingleton(sp =&gt; new LatchkeyTokenCache(
        ///     new Uri("https://api.example.com/odata/"),
        ///     "client-a",
        ///     options.TokenCachePath,
        ///     sp.GetRequiredService&lt;ILogger&lt;LatchkeyTokenCache&gt;&gt;()));
        /// services.AddSingleton&lt;ITokenCache&gt;(sp =&gt; sp.GetRequiredService&lt;LatchkeyTokenCache&gt;());
        /// </code>
        /// </example>
        /// <remarks>
        /// The backing store is created eagerly, so an unusable credential store surfaces at construction rather
        /// than on the first OData request. Call <see cref="VerifyPersistence(string?)"/> before constructing to
        /// fail first with an actionable message instead.
        /// </remarks>
        public LatchkeyTokenCache(Uri serviceRoot, string clientId, string? tokenCachePath, ILogger<LatchkeyTokenCache> logger)
            : this(serviceRoot, clientId, tokenCachePath, null, logger)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="LatchkeyTokenCache"/> class over a specific Latchkey
        /// backend, so a test can exercise the File backend on every platform instead of the pinned per-OS map.
        /// </summary>
        /// <param name="serviceRoot">The absolute OData service root the cached tokens authorize.</param>
        /// <param name="clientId">The OAuth client id the cached tokens were issued to.</param>
        /// <param name="tokenCachePath">The directory the File and DPAPI backends store values in, or <see langword="null"/> for the per-user default.</param>
        /// <param name="forceBackend">The single backend to force, or <see langword="null"/> to resolve through the pinned per-OS map.</param>
        /// <param name="logger">The logger this instance records cache outcomes to.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="serviceRoot"/> or <paramref name="logger"/> is <see langword="null"/>.</exception>
        /// <exception cref="ArgumentException">Thrown when <paramref name="clientId"/> is empty or white space, or <paramref name="serviceRoot"/> is not an absolute URI.</exception>
        internal LatchkeyTokenCache(Uri serviceRoot, string clientId, string? tokenCachePath, LatchkeyBackend? forceBackend, ILogger<LatchkeyTokenCache> logger)
        {
            ArgumentNullException.ThrowIfNull(serviceRoot);
            ArgumentException.ThrowIfNullOrWhiteSpace(clientId);
            ArgumentNullException.ThrowIfNull(logger);

            Key = ComputeKey(serviceRoot, clientId);
            _logger = logger;
            _store = LatchkeyFactory.Create(CreateOptions(tokenCachePath, forceBackend));
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="LatchkeyTokenCache"/> class over an already-created
        /// store, so a test can supply the process-local <see cref="LatchkeyBackend.InMemory"/> backend.
        /// </summary>
        /// <param name="store">The Latchkey store this cache persists its single slot in.</param>
        /// <param name="key">The key the single slot lives under; normally <see cref="ComputeKey(Uri, string)"/>.</param>
        /// <param name="logger">The logger this instance records cache outcomes to.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="store"/> or <paramref name="logger"/> is <see langword="null"/>.</exception>
        /// <exception cref="ArgumentException">Thrown when <paramref name="key"/> is empty or white space.</exception>
        internal LatchkeyTokenCache(ILatchkey store, string key, ILogger<LatchkeyTokenCache> logger)
        {
            ArgumentNullException.ThrowIfNull(store);
            ArgumentException.ThrowIfNullOrWhiteSpace(key);
            ArgumentNullException.ThrowIfNull(logger);

            Key = key;
            _logger = logger;
            _store = store;
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Removes this cache's slot from the credential store, so the next start runs a fresh grant.
        /// </summary>
        /// <param name="cancellationToken">The token that cancels the delete.</param>
        /// <returns>
        /// A task that completes once the slot is gone, whether or not it existed.
        /// </returns>
        /// <example>
        /// <code>
        /// await cache.ClearAsync(cancellationToken);
        /// </code>
        /// </example>
        public async Task ClearAsync(CancellationToken cancellationToken)
        {
            await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                await _store.DeleteAsync(Key, cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                _gate.Release();
            }
        }

        /// <summary>
        /// Computes the Latchkey key a service root and client id pair caches its tokens under.
        /// </summary>
        /// <param name="serviceRoot">The absolute OData service root the cached tokens authorize.</param>
        /// <param name="clientId">The OAuth client id the cached tokens were issued to.</param>
        /// <returns>
        /// The lowercase hex SHA-256 digest of <c>serviceRoot.AbsoluteUri + "\n" + clientId</c>, always 64
        /// characters long.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="serviceRoot"/> is <see langword="null"/>.</exception>
        /// <exception cref="ArgumentException">Thrown when <paramref name="clientId"/> is empty or white space, or <paramref name="serviceRoot"/> is not an absolute URI.</exception>
        /// <example>
        /// <code>
        /// var key = LatchkeyTokenCache.ComputeKey(new Uri("https://api.example.com/odata/"), "client-a");
        /// // 64 lowercase hex characters
        /// </code>
        /// </example>
        /// <remarks>
        /// The newline separator keeps the two inputs unambiguous, so a service root that ends with the text of
        /// a client id cannot collide with a different pair. The digest is deterministic across processes and
        /// platforms, which is what lets a restart find the slot the previous run wrote.
        /// </remarks>
        public static string ComputeKey(Uri serviceRoot, string clientId)
        {
            ArgumentNullException.ThrowIfNull(serviceRoot);
            ArgumentException.ThrowIfNullOrWhiteSpace(clientId);

            if (!serviceRoot.IsAbsoluteUri)
            {
                throw new ArgumentException("The service root must be an absolute URI.", nameof(serviceRoot));
            }

            return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(serviceRoot.AbsoluteUri + "\n" + clientId))).ToLowerInvariant();
        }

        /// <summary>
        /// Reads the cached token container for this cache's service root and client id.
        /// </summary>
        /// <param name="cancellationToken">The token that cancels the read.</param>
        /// <returns>
        /// The cached container, or <see langword="null"/> when nothing is cached or the cached bytes could not
        /// be read back as a usable container.
        /// </returns>
        /// <exception cref="LatchkeyBackendUnavailableException">Thrown when the credential store itself became unusable after this cache was constructed.</exception>
        /// <example>
        /// <code>
        /// var tokens = await cache.GetTokensAsync(cancellationToken);
        /// if (tokens is null)
        /// {
        ///     // Run an interactive grant.
        /// }
        /// </code>
        /// </example>
        /// <remarks>
        /// An entry the store cannot unprotect, or that is not valid JSON, deserializes to <see langword="null"/>,
        /// or carries no access token, is unusable — the DPAPI master key may have changed, or a previous write
        /// may have been truncated. Such an entry is deleted and reported at warning level naming only
        /// <see cref="Key"/>, never the payload, so the caller falls through to a fresh grant instead of failing
        /// repeatedly.
        /// <para>
        /// A <see cref="LatchkeyBackendUnavailableException"/> is deliberately excluded from that handling and
        /// propagates instead. It reports that the whole store became unusable after construction — a locked
        /// Secret Service keyring, a cache directory that lost its permissions — not that this entry is corrupt.
        /// Treating it as corruption would misdescribe the fault and, where reads fail while deletes succeed,
        /// would destroy a still-valid refresh token over a transient condition.
        /// </para>
        /// </remarks>
        public async ValueTask<SdkAuth.TokenContainer?> GetTokensAsync(CancellationToken cancellationToken)
        {
            await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                SdkAuth.TokenContainer? tokens;
                try
                {
                    var bytes = await _store.GetBytesAsync(Key, cancellationToken).ConfigureAwait(false);
                    if (bytes is null)
                    {
                        return null;
                    }

                    tokens = JsonSerializer.Deserialize(bytes, OutboundOAuthJsonContext.Default.TokenContainer);
                }
                catch (Exception exception) when (exception is JsonException or LatchkeyException and not LatchkeyBackendUnavailableException)
                {
                    tokens = null;
                }

                if (tokens is null || string.IsNullOrWhiteSpace(tokens.AccessToken))
                {
                    await _store.DeleteAsync(Key, cancellationToken).ConfigureAwait(false);
                    _logger.LogWarning("Discarding unreadable token cache entry {Key}", Key);

                    return null;
                }

                _logger.LogDebug("Loaded cached token: {Token}", OutboundAuthLogRedactor.DescribeToken(tokens));

                return tokens;
            }
            finally
            {
                _gate.Release();
            }
        }

        /// <summary>
        /// Writes a token container into this cache's single slot, replacing whatever it held.
        /// </summary>
        /// <param name="tokens">The container to persist.</param>
        /// <param name="cancellationToken">The token that cancels the write.</param>
        /// <returns>
        /// A task that completes once the container is durable in the credential store.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="tokens"/> is <see langword="null"/>.</exception>
        /// <example>
        /// <code>
        /// await cache.StoreTokensAsync(tokens, cancellationToken);
        /// </code>
        /// </example>
        /// <remarks>
        /// The whole container is persisted, including <see cref="SdkAuth.TokenContainer.ClientId"/>,
        /// <see cref="SdkAuth.TokenContainer.ClientSecret"/>,
        /// <see cref="SdkAuth.TokenContainer.TokenEndpointAuthMethod"/>, and
        /// <see cref="SdkAuth.TokenContainer.AuthorizationServer"/>, so a cold start can refresh without
        /// re-running dynamic client registration.
        /// </remarks>
        public async ValueTask StoreTokensAsync(SdkAuth.TokenContainer tokens, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(tokens);

            await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                var bytes = JsonSerializer.SerializeToUtf8Bytes(tokens, OutboundOAuthJsonContext.Default.TokenContainer);

                await _store.SetAsync(Key, bytes, cancellationToken).ConfigureAwait(false);
                _logger.LogDebug("Stored token: {Token}", OutboundAuthLogRedactor.DescribeToken(tokens));
            }
            finally
            {
                _gate.Release();
            }
        }

        /// <summary>
        /// Reports whether the credential store this cache would use can actually round-trip a value on this
        /// host.
        /// </summary>
        /// <param name="tokenCachePath">The directory the File and DPAPI backends store values in, or <see langword="null"/> for the per-user default.</param>
        /// <returns>
        /// <see langword="true"/> when a throwaway value survives a write, read, and delete; otherwise
        /// <see langword="false"/>.
        /// </returns>
        /// <example>
        /// <code>
        /// if (!LatchkeyTokenCache.VerifyPersistence(options.TokenCachePath))
        /// {
        ///     throw new InvalidOperationException("Credential store is not usable on this host; unlock the OS keyring or pass --token-cache.");
        /// }
        /// </code>
        /// </example>
        /// <remarks>
        /// Call this at startup, before constructing the cache, so an unusable keyring fails first with an
        /// actionable message rather than midway through a grant. It never throws: a store that cannot be
        /// reached reports <see langword="false"/>.
        /// </remarks>
        public static bool VerifyPersistence(string? tokenCachePath)
        {
            return LatchkeyApi.VerifyPersistence(CreateOptions(tokenCachePath));
        }

        #endregion

        #region Internal Methods

        /// <summary>
        /// Builds the Latchkey options this cache runs on, with the service name, per-OS backend order, and
        /// backend directories all pinned.
        /// </summary>
        /// <param name="tokenCachePath">The directory the File and DPAPI backends store values in, or <see langword="null"/> for the per-user default.</param>
        /// <returns>
        /// Options that resolve through the pinned per-OS map.
        /// </returns>
        internal static LatchkeyOptions CreateOptions(string? tokenCachePath)
        {
            return CreateOptions(tokenCachePath, null);
        }

        /// <summary>
        /// Builds the Latchkey options this cache runs on, optionally forcing a single backend.
        /// </summary>
        /// <param name="tokenCachePath">The directory the File and DPAPI backends store values in, or <see langword="null"/> for the per-user default.</param>
        /// <param name="forceBackend">The single backend to force, or <see langword="null"/> to resolve through the pinned per-OS map.</param>
        /// <returns>
        /// Options whose <see cref="LatchkeyOptions.ServiceName"/> is
        /// <see cref="ODataMcpAuthConstants.LatchkeyServiceName"/>, whose backend map is Windows to DPAPI, macOS
        /// to the Keychain, Linux to the Secret Service, and File everywhere as the fallback, and whose DPAPI
        /// and File options both point at the resolved directory.
        /// </returns>
        /// <remarks>
        /// The map is cleared before it is populated, so the native seed — which puts Windows Credential Manager
        /// first — can never precede the pinned order. That seed is exactly what
        /// <c>specs/v3/AUTHENTICATION.md</c> forbids on Windows, because Credential Manager caps a value at 2560
        /// bytes and a token container with a refresh token routinely exceeds that.
        /// </remarks>
        internal static LatchkeyOptions CreateOptions(string? tokenCachePath, LatchkeyBackend? forceBackend)
        {
            var directory = string.IsNullOrWhiteSpace(tokenCachePath) ? DefaultTokenCacheDirectory() : tokenCachePath;

            return new LatchkeyOptions
            {
                ServiceName = ODataMcpAuthConstants.LatchkeyServiceName,
                DisplayName = LatchkeyDisplayName,
                Backend = forceBackend ?? LatchkeyBackend.Auto,
                Backends = new BackendMap()
                    .Clear()
                    .For(OSPlatform.Windows, LatchkeyBackend.Dpapi)
                    .For(OSPlatform.OSX, LatchkeyBackend.MacOSKeychain)
                    .For(OSPlatform.Linux, LatchkeyBackend.SecretService)
                    .ForAll(LatchkeyBackend.File),
                BackendOptions =
                [
                    DpapiBackendOption.Default with { Path = directory },
                    FileBackendOption.Default with { Path = directory }
                ]
            };
        }

        /// <summary>
        /// Computes the per-user directory the File and DPAPI backends fall back to when no
        /// <c>--token-cache</c> directory was configured.
        /// </summary>
        /// <returns>
        /// <c>%LocalAppData%\odata-mcp\tokens</c> on Windows; <c>$HOME/.local/share/odata-mcp/tokens</c>
        /// everywhere else.
        /// </returns>
        internal static string DefaultTokenCacheDirectory()
        {
            if (OperatingSystem.IsWindows())
            {
                return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "odata-mcp", "tokens");
            }

            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "share", "odata-mcp", "tokens");
        }

        /// <summary>
        /// Reports whether the credential store described by these arguments can round-trip a value, optionally
        /// forcing a single backend.
        /// </summary>
        /// <param name="tokenCachePath">The directory the File and DPAPI backends store values in, or <see langword="null"/> for the per-user default.</param>
        /// <param name="forceBackend">The single backend to force, or <see langword="null"/> to resolve through the pinned per-OS map.</param>
        /// <returns>
        /// <see langword="true"/> when a throwaway value survives a write, read, and delete; otherwise
        /// <see langword="false"/>.
        /// </returns>
        internal static bool VerifyPersistence(string? tokenCachePath, LatchkeyBackend? forceBackend)
        {
            return LatchkeyApi.VerifyPersistence(CreateOptions(tokenCachePath, forceBackend));
        }

        #endregion

    }

}
