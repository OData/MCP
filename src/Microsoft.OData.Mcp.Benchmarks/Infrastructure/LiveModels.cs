// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.OData.Mcp.Core.Parsing;
using Microsoft.OData.Mcp.Tests.Shared;

namespace Microsoft.OData.Mcp.Benchmarks.Infrastructure
{

    /// <summary>
    /// Loads the live reference services once per process. Both benchmarks and reports run against the same
    /// public Northwind and TripPin instances the tests use.
    /// </summary>
    public static class LiveModels
    {

        #region Fields

        /// <summary>
        /// Service name for the public Northwind V4 service.
        /// </summary>
        public const string Northwind = "Northwind";

        /// <summary>
        /// Service name for the public TripPin RESTier service.
        /// </summary>
        public const string TripPin = "TripPin";

        private static readonly ConcurrentDictionary<string, Task<LiveService>> _cache = new(StringComparer.Ordinal);

        private static readonly HttpClient _http = new();

        #endregion

        #region Public Methods

        /// <summary>
        /// Gets the service names the suites parameterize over.
        /// </summary>
        /// <returns>
        /// <c>Northwind</c> and <c>TripPin</c>.
        /// </returns>
        public static string[] Names()
        {
            return [Northwind, TripPin];
        }

        /// <summary>
        /// Loads one service, fetching <c>$metadata</c> on first use.
        /// </summary>
        /// <param name="name"><see cref="Northwind"/> or <see cref="TripPin"/>.</param>
        /// <returns>
        /// The loaded service.
        /// </returns>
        /// <exception cref="ArgumentException">Thrown when <paramref name="name"/> is not a known service.</exception>
        public static Task<LiveService> LoadAsync(string name)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(name);

            return _cache.GetOrAdd(name, static key => key switch
            {
                Northwind => FetchAsync(Northwind, LiveOData.Northwind, "Customers"),
                TripPin => FetchAsync(TripPin, LiveOData.TripPin, "People"),
                _ => throw new ArgumentException($"Unknown service '{key}'. Use {Northwind} or {TripPin}.", nameof(name))
            });
        }

        #endregion

        #region Internal Methods

        /// <summary>
        /// Fetches and parses one service's metadata.
        /// </summary>
        /// <param name="name">The display name.</param>
        /// <param name="url">The service root.</param>
        /// <param name="entitySet">The entity set used for per-type measurements.</param>
        /// <returns>
        /// The loaded service.
        /// </returns>
        internal static async Task<LiveService> FetchAsync(string name, string url, string entitySet)
        {
            var xml = await _http.GetStringAsync($"{url.TrimEnd('/')}/$metadata").ConfigureAwait(false);
            var model = new CsdlParser().ParseFromString(xml);

            return new LiveService(name, url, xml, model, entitySet);
        }

        #endregion

    }

}
