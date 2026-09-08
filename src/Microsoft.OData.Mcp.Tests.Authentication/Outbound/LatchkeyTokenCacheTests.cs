// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Latchkey;
using Latchkey.Backends.Dpapi;
using Latchkey.Backends.Files;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.OData.Mcp.Authentication.Outbound;
using Microsoft.OData.Mcp.Tests.Shared.Authentication;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SdkAuth = ModelContextProtocol.Authentication;

namespace Microsoft.OData.Mcp.Tests.Authentication.Outbound
{

    /// <summary>
    /// Exercises <see cref="LatchkeyTokenCache"/> against real Latchkey stores — the in-process
    /// <see cref="LatchkeyBackend.InMemory"/> backend and the on-disk <see cref="LatchkeyBackend.File"/> backend
    /// rooted in a throwaway temp directory — with no mocking anywhere, so CI needs no OS keyring.
    /// </summary>
    /// <remarks>
    /// Every test that touches disk deletes its temp directory in a <c>finally</c> block, and no test asserts on
    /// an OS credential store, so the suite is safe to run headless on any platform.
    /// </remarks>
    [TestClass]
    public class LatchkeyTokenCacheTests
    {

        #region Public Methods

        /// <summary>
        /// <see cref="LatchkeyTokenCache.ComputeKey(Uri, string)"/> is a stable lowercase hex SHA-256 digest that
        /// changes when either the service root or the client id changes.
        /// </summary>
        [TestMethod]
        public void ComputeKey_SameInputs_IsDeterministic_AndDiffersByClientId()
        {
            var root = new Uri("https://api.example.com/odata/");
            var other = new Uri("https://api.example.com/odata2/");

            var key = LatchkeyTokenCache.ComputeKey(root, "client-a");

            key.Should().NotBeNullOrWhiteSpace();
            key.Should().HaveLength(64);
            key.Should().MatchRegex("^[0-9a-f]{64}$");
            key.Should().Be(LatchkeyTokenCache.ComputeKey(root, "client-a"));
            key.Should().NotBe(LatchkeyTokenCache.ComputeKey(root, "client-b"));
            key.Should().NotBe(LatchkeyTokenCache.ComputeKey(other, "client-a"));
        }

        /// <summary>
        /// Twenty concurrent stores interleaved with twenty concurrent reads never throw, every read observes
        /// either nothing or one of the stored access tokens, and a store awaited afterwards is what the cache
        /// returns.
        /// </summary>
        [TestMethod]
        public async Task Concurrent_StoreAndGet_NeverThrows_LastWriteWins()
        {
            var cache = CreateInMemoryCache(NullLogger<LatchkeyTokenCache>.Instance);
            var accessTokens = Enumerable.Range(0, 20).Select(static index => $"access-{index}").ToArray();
            var observed = new ConcurrentBag<string>();

            var work = new List<Task>();
            foreach (var accessToken in accessTokens)
            {
                var token = CreateToken(accessToken);
                work.Add(Task.Run(() => cache.StoreTokensAsync(token, CancellationToken.None).AsTask()));
                work.Add(Task.Run(async () =>
                {
                    var current = await cache.GetTokensAsync(CancellationToken.None).ConfigureAwait(false);
                    if (current is not null)
                    {
                        observed.Add(current.AccessToken);
                    }
                }));
            }

            Func<Task> act = () => Task.WhenAll(work);

            await act.Should().NotThrowAsync();
            observed.Should().BeSubsetOf(accessTokens);

            await cache.StoreTokensAsync(CreateToken("access-final"), CancellationToken.None);
            var final = await cache.GetTokensAsync(CancellationToken.None);

            final.Should().NotBeNull();
            final!.AccessToken.Should().Be("access-final");
        }

        /// <summary>
        /// <c>CreateOptions</c> pins the Latchkey service name and per-OS backend order, and points both the
        /// DPAPI and File backends at the configured token cache directory. Windows must resolve to DPAPI, never
        /// Credential Manager, whose 2560-byte cap is smaller than a typical token container.
        /// </summary>
        [TestMethod]
        public async Task CreateOptions_PinsBackendsAndPaths()
        {
            var directory = CreateTempDirectory();
            try
            {
                var options = LatchkeyTokenCache.CreateOptions(directory, null);

                options.ServiceName.Should().Be(ODataMcpAuthConstants.LatchkeyServiceName);
                options.DisplayName.Should().Be("OData MCP");
                options.Backend.Should().Be(LatchkeyBackend.Auto);
                options.CustomBackend.Should().BeNull();
                options.Backends.Should().NotBeNull();
                options.BackendOptions.Should().HaveCount(2);

                var dpapi = options.BackendOptions.OfType<DpapiBackendOption>().Single();
                dpapi.Path.Should().Be(directory);
                dpapi.Scope.Should().Be(DpapiScope.CurrentUser);

                var file = options.BackendOptions.OfType<FileBackendOption>().Single();
                file.Path.Should().Be(directory);

                LatchkeyTokenCache.CreateOptions(directory, LatchkeyBackend.File).Backend.Should().Be(LatchkeyBackend.File);
                LatchkeyTokenCache.DefaultTokenCacheDirectory().Should().EndWith(Path.Combine("odata-mcp", "tokens"));

                if (OperatingSystem.IsWindows())
                {
                    var store = LatchkeyFactory.Create(options);
                    await store.SetAsync("pinned-backend-probe", new byte[] { 1, 2, 3 }, CancellationToken.None);

                    Directory.EnumerateFileSystemEntries(directory).Should().NotBeEmpty();

                    await store.DeleteAsync("pinned-backend-probe", CancellationToken.None);
                }
            }
            finally
            {
                DeleteTempDirectory(directory);
            }
        }

        /// <summary>
        /// A store that became unusable after the cache was constructed is not a corrupt entry: the failure
        /// propagates, nothing is deleted, and no "unreadable entry" warning is written — otherwise a locked
        /// keyring or a permissions change would destroy a still-valid refresh token.
        /// </summary>
        [TestMethod]
        public async Task GetTokensAsync_BackendUnavailable_PropagatesAndKeepsEntry()
        {
            var backend = new UnavailableSecretBackend();
            var store = LatchkeyFactory.Create(new LatchkeyOptions
            {
                ServiceName = ODataMcpAuthConstants.LatchkeyServiceName,
                CustomBackend = backend
            });
            var provider = new CapturingLoggerProvider();
            using var factory = LoggerFactory.Create(builder => builder.SetMinimumLevel(LogLevel.Trace).AddProvider(provider));
            var key = LatchkeyTokenCache.ComputeKey(new Uri("https://api.example.com/odata/"), "client-a");
            var cache = new LatchkeyTokenCache(store, key, factory.CreateLogger<LatchkeyTokenCache>());

            Func<Task> act = async () => await cache.GetTokensAsync(CancellationToken.None);

            await act.Should().ThrowAsync<LatchkeyBackendUnavailableException>();
            backend.RemoveCallCount.Should().Be(0);
            provider.AllText.Should().NotContain("Discarding unreadable token cache entry");

            await act.Should().ThrowAsync<LatchkeyBackendUnavailableException>();
            backend.RemoveCallCount.Should().Be(0);
        }

        /// <summary>
        /// A cache entry that is not valid JSON is treated as unreadable: the read returns <see langword="null"/>,
        /// the entry is deleted so the next start runs a fresh grant, and the warning names the key but never
        /// the payload.
        /// </summary>
        [TestMethod]
        public async Task GetTokensAsync_CorruptBytes_ReturnsNullAndRemovesEntry()
        {
            var provider = new CapturingLoggerProvider();
            using var factory = LoggerFactory.Create(builder => builder.SetMinimumLevel(LogLevel.Trace).AddProvider(provider));

            var store = LatchkeyFactory.Create(new LatchkeyOptions
            {
                ServiceName = ODataMcpAuthConstants.LatchkeyServiceName,
                Backend = LatchkeyBackend.InMemory
            });
            var key = LatchkeyTokenCache.ComputeKey(new Uri("https://api.example.com/odata/"), "client-a");
            var cache = new LatchkeyTokenCache(store, key, factory.CreateLogger<LatchkeyTokenCache>());

            await store.SetAsync(key, "not json"u8.ToArray(), CancellationToken.None);

            var tokens = await cache.GetTokensAsync(CancellationToken.None);

            tokens.Should().BeNull();
            (await store.ContainsAsync(key, CancellationToken.None)).Should().BeFalse();
            provider.AllText.Should().Contain("Discarding unreadable token cache entry");
            provider.AllText.Should().Contain(key);
            provider.AllText.Should().NotContain("not json");
        }

        /// <summary>
        /// An entry whose bytes were scrambled on disk after it was written is recovered from the same way a
        /// corrupt in-memory entry is: the read returns <see langword="null"/> and the entry is gone, whether the
        /// backend rejects the bytes itself or hands back something that is not a token container.
        /// </summary>
        [TestMethod]
        public async Task GetTokensAsync_CorruptFileOnDisk_ReturnsNullAndRemovesEntry()
        {
            await AssertCorruptOnDiskEntryIsDiscardedAsync(LatchkeyBackend.File);

            if (OperatingSystem.IsWindows())
            {
                await AssertCorruptOnDiskEntryIsDiscardedAsync(LatchkeyBackend.Dpapi);
            }
        }

        /// <summary>
        /// A token stored through one <see cref="LatchkeyTokenCache"/> over the File backend is read back by a
        /// second, independently constructed instance, proving the cache survives a process restart.
        /// </summary>
        [TestMethod]
        public async Task RoundTrip_FileBackend_PersistsAcrossInstances()
        {
            var directory = CreateTempDirectory();
            try
            {
                var root = new Uri("https://api.example.com/odata/");
                var writer = new LatchkeyTokenCache(root, "client-a", directory, LatchkeyBackend.File, NullLogger<LatchkeyTokenCache>.Instance);

                await writer.StoreTokensAsync(CreateToken("access-persisted"), CancellationToken.None);

                var reader = new LatchkeyTokenCache(root, "client-a", directory, LatchkeyBackend.File, NullLogger<LatchkeyTokenCache>.Instance);
                var tokens = await reader.GetTokensAsync(CancellationToken.None);

                tokens.Should().NotBeNull();
                tokens!.AccessToken.Should().Be("access-persisted");
                tokens.ClientId.Should().Be("client-a");
                tokens.ClientSecret.Should().Be("secret-a");
                tokens.AuthorizationServer.Should().Be("https://login.example.com/");
                reader.Key.Should().Be(writer.Key);

                await reader.ClearAsync(CancellationToken.None);

                (await reader.GetTokensAsync(CancellationToken.None)).Should().BeNull();
            }
            finally
            {
                DeleteTempDirectory(directory);
            }
        }

        /// <summary>
        /// Every property of a fully populated <see cref="SdkAuth.TokenContainer"/> survives a round trip, and a
        /// key that was never written reads back as <see langword="null"/> rather than throwing.
        /// </summary>
        [TestMethod]
        public async Task RoundTrip_InMemoryBackend_PreservesAllFields()
        {
            var cache = CreateInMemoryCache(NullLogger<LatchkeyTokenCache>.Instance);
            var empty = CreateInMemoryCache(NullLogger<LatchkeyTokenCache>.Instance);
            var obtainedAt = new DateTimeOffset(2026, 9, 7, 12, 34, 56, TimeSpan.Zero);
            var token = new SdkAuth.TokenContainer
            {
                TokenType = "Bearer",
                AccessToken = "access-all-fields",
                RefreshToken = "refresh-all-fields",
                ExpiresIn = 3600,
                Scope = "read offline_access",
                ObtainedAt = obtainedAt,
                ClientId = "client-all-fields",
                ClientSecret = "secret-all-fields",
                TokenEndpointAuthMethod = "client_secret_post",
                AuthorizationServer = "https://login.example.com/"
            };

            await cache.StoreTokensAsync(token, CancellationToken.None);
            var stored = await cache.GetTokensAsync(CancellationToken.None);

            stored.Should().NotBeNull();
            stored!.TokenType.Should().Be("Bearer");
            stored.AccessToken.Should().Be("access-all-fields");
            stored.RefreshToken.Should().Be("refresh-all-fields");
            stored.ExpiresIn.Should().Be(3600);
            stored.Scope.Should().Be("read offline_access");
            stored.ObtainedAt.Should().Be(obtainedAt);
            stored.ClientId.Should().Be("client-all-fields");
            stored.ClientSecret.Should().Be("secret-all-fields");
            stored.TokenEndpointAuthMethod.Should().Be("client_secret_post");
            stored.AuthorizationServer.Should().Be("https://login.example.com/");

            (await empty.GetTokensAsync(CancellationToken.None)).Should().BeNull();
        }

        /// <summary>
        /// Storing and reading a token writes only the redacted description to the log sink: never the access
        /// token, the refresh token, or the client secret.
        /// </summary>
        [TestMethod]
        public async Task StoreThenGet_WithCapturingLogger_NeverLogsTokenValues()
        {
            var provider = new CapturingLoggerProvider();
            using var factory = LoggerFactory.Create(builder => builder.SetMinimumLevel(LogLevel.Trace).AddProvider(provider));
            var cache = CreateInMemoryCache(factory.CreateLogger<LatchkeyTokenCache>());
            var token = new SdkAuth.TokenContainer
            {
                TokenType = "Bearer",
                AccessToken = "SECRET-ACCESS",
                RefreshToken = "SECRET-REFRESH",
                ClientSecret = "SECRET-CLIENT",
                ExpiresIn = 3600,
                Scope = "read",
                ObtainedAt = DateTimeOffset.UtcNow
            };

            await cache.StoreTokensAsync(token, CancellationToken.None);
            await cache.GetTokensAsync(CancellationToken.None);

            provider.Entries.Should().NotBeEmpty();
            provider.AllText.Should().Contain("type=Bearer");
            provider.AllText.Should().NotContain("SECRET-ACCESS");
            provider.AllText.Should().NotContain("SECRET-REFRESH");
            provider.AllText.Should().NotContain("SECRET-CLIENT");
        }

        /// <summary>
        /// <see cref="LatchkeyTokenCache.VerifyPersistence(string?)"/> reports that a throwaway directory can
        /// round-trip a value, both through the File backend everywhere and through the pinned DPAPI backend on
        /// Windows.
        /// </summary>
        [TestMethod]
        public void VerifyPersistence_TempDirectory_IsTrue()
        {
            var directory = CreateTempDirectory();
            try
            {
                LatchkeyTokenCache.VerifyPersistence(directory, LatchkeyBackend.File).Should().BeTrue();

                if (OperatingSystem.IsWindows())
                {
                    LatchkeyTokenCache.VerifyPersistence(directory).Should().BeTrue();
                }
            }
            finally
            {
                DeleteTempDirectory(directory);
            }
        }

        #endregion

        #region Internal Methods

        /// <summary>
        /// Writes a token through an on-disk backend, scrambles every file the backend produced, and asserts the
        /// next read discards the entry rather than surfacing it or failing forever.
        /// </summary>
        /// <param name="backend">The on-disk backend to exercise: <see cref="LatchkeyBackend.File"/> hands the scrambled bytes back and fails to deserialize; <see cref="LatchkeyBackend.Dpapi"/> fails to unprotect them.</param>
        /// <returns>
        /// A task that completes once the assertions have run.
        /// </returns>
        internal static async Task AssertCorruptOnDiskEntryIsDiscardedAsync(LatchkeyBackend backend)
        {
            var directory = CreateTempDirectory();
            try
            {
                var provider = new CapturingLoggerProvider();
                using var factory = LoggerFactory.Create(builder => builder.SetMinimumLevel(LogLevel.Trace).AddProvider(provider));
                var root = new Uri("https://api.example.com/odata/");
                var cache = new LatchkeyTokenCache(root, "client-a", directory, backend, factory.CreateLogger<LatchkeyTokenCache>());

                await cache.StoreTokensAsync(CreateToken("access-corrupted"), CancellationToken.None);

                var files = Directory.GetFiles(directory, "*", SearchOption.AllDirectories);
                files.Should().NotBeEmpty();
                foreach (var file in files)
                {
                    await File.WriteAllTextAsync(file, "not json", CancellationToken.None);
                }

                var tokens = await cache.GetTokensAsync(CancellationToken.None);

                tokens.Should().BeNull();
                provider.AllText.Should().Contain("Discarding unreadable token cache entry");
                provider.AllText.Should().NotContain("not json");
                (await cache.GetTokensAsync(CancellationToken.None)).Should().BeNull();
            }
            finally
            {
                DeleteTempDirectory(directory);
            }
        }

        /// <summary>
        /// Builds a <see cref="LatchkeyTokenCache"/> over a private, process-local
        /// <see cref="LatchkeyBackend.InMemory"/> store, so the test needs no OS keyring and no disk.
        /// </summary>
        /// <param name="logger">The logger the cache under test writes to.</param>
        /// <returns>
        /// A cache whose backing store is private to this call.
        /// </returns>
        internal static LatchkeyTokenCache CreateInMemoryCache(ILogger<LatchkeyTokenCache> logger)
        {
            var store = LatchkeyFactory.Create(new LatchkeyOptions
            {
                ServiceName = ODataMcpAuthConstants.LatchkeyServiceName,
                Backend = LatchkeyBackend.InMemory
            });

            return new LatchkeyTokenCache(store, LatchkeyTokenCache.ComputeKey(new Uri("https://api.example.com/odata/"), "client-a"), logger);
        }

        /// <summary>
        /// Creates a throwaway directory under the OS temp path for a File or DPAPI backed store.
        /// </summary>
        /// <returns>
        /// The full path of the newly created directory.
        /// </returns>
        internal static string CreateTempDirectory()
        {
            var directory = Path.Combine(Path.GetTempPath(), "odata-mcp-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);

            return directory;
        }

        /// <summary>
        /// Builds a fully populated token container carrying <paramref name="accessToken"/>.
        /// </summary>
        /// <param name="accessToken">The access token value to carry.</param>
        /// <returns>
        /// A container with every optional property populated.
        /// </returns>
        internal static SdkAuth.TokenContainer CreateToken(string accessToken)
        {
            return new SdkAuth.TokenContainer
            {
                TokenType = "Bearer",
                AccessToken = accessToken,
                RefreshToken = "refresh-a",
                ExpiresIn = 3600,
                Scope = "read",
                ObtainedAt = DateTimeOffset.UtcNow,
                ClientId = "client-a",
                ClientSecret = "secret-a",
                TokenEndpointAuthMethod = "client_secret_post",
                AuthorizationServer = "https://login.example.com/"
            };
        }

        /// <summary>
        /// Deletes a directory created by <see cref="CreateTempDirectory"/>, ignoring a store that already
        /// removed it.
        /// </summary>
        /// <param name="directory">The directory to delete.</param>
        internal static void DeleteTempDirectory(string directory)
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, true);
            }
        }

        #endregion

    }

}
