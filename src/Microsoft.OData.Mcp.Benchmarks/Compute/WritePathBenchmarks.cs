// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using Microsoft.OData.Mcp.Benchmarks.Infrastructure;
using Microsoft.OData.Mcp.Core.Catalog;
using Microsoft.OData.Mcp.Tests.Shared;

namespace Microsoft.OData.Mcp.Benchmarks.Compute
{

    /// <summary>
    /// Cost of the pre-HTTP validation on create and update. The executor answers instantly, so what is measured
    /// is argument reading, EDM validation, body normalization, and result formatting.
    /// </summary>
    [MemoryDiagnoser]
    public class WritePathBenchmarks
    {

        #region Fields

        private Dictionary<string, JsonElement> _createInvalid = [];
        private Dictionary<string, JsonElement> _createValid = [];
        private ODataToolRuntime _runtime = null!;
        private Dictionary<string, JsonElement> _updateValid = [];

        #endregion

        #region Properties

        /// <summary>
        /// The service whose reference type is written.
        /// </summary>
        [Params(LiveModels.Northwind, LiveModels.TripPin)]
        public string Service { get; set; } = LiveModels.Northwind;

        #endregion

        #region Public Methods

        /// <summary>
        /// <c>odata_create</c> with a body that omits a required-on-create property, so it fails before HTTP.
        /// </summary>
        /// <returns>
        /// The error result.
        /// </returns>
        [Benchmark]
        public Task<ODataToolInvocationResult> CreateMissingRequired()
        {
            return _runtime.InvokeAsync("odata_create", _createInvalid, CancellationToken.None);
        }

        /// <summary>
        /// <c>odata_create</c> with a complete, valid body.
        /// </summary>
        /// <returns>
        /// The success result.
        /// </returns>
        [Benchmark(Baseline = true)]
        public Task<ODataToolInvocationResult> CreateValid()
        {
            return _runtime.InvokeAsync("odata_create", _createValid, CancellationToken.None);
        }

        /// <summary>
        /// Builds the catalog and runtime once with a canned executor.
        /// </summary>
        [GlobalSetup]
        public async Task SetupAsync()
        {
            var service = await LiveModels.LoadAsync(Service).ConfigureAwait(false);
            var catalog = new ODataMcpCatalog(service.Model, new ODataMcpCatalogOptions());
            _runtime = new ODataToolRuntime(catalog, new CannedODataExecutor());

            var valid = Service == LiveModels.Northwind
                ? new Dictionary<string, object?> { ["CustomerID"] = "BENCH", ["CompanyName"] = "Benchmark Co." }
                : new Dictionary<string, object?> { ["UserName"] = "bench", ["FirstName"] = "Bench", ["Gender"] = "Male", ["FavoriteFeature"] = "Feature1", ["Features"] = new[] { "Feature2" } };
            var invalid = Service == LiveModels.Northwind
                ? new Dictionary<string, object?> { ["CompanyName"] = "Benchmark Co." }
                : new Dictionary<string, object?> { ["FirstName"] = "Bench" };
            var key = Service == LiveModels.Northwind ? "ALFKI" : "russellwhyte";
            var patch = Service == LiveModels.Northwind
                ? new Dictionary<string, object?> { ["CompanyName"] = "Renamed" }
                : new Dictionary<string, object?> { ["LastName"] = "Renamed" };

            _createValid = ToolArguments.Of("entitySet", service.EntitySet, "body", valid);
            _createInvalid = ToolArguments.Of("entitySet", service.EntitySet, "body", invalid);
            _updateValid = ToolArguments.Of("entitySet", service.EntitySet, "key", key, "body", patch);
        }

        /// <summary>
        /// <c>odata_update</c> with a one-property PATCH.
        /// </summary>
        /// <returns>
        /// The success result.
        /// </returns>
        [Benchmark]
        public Task<ODataToolInvocationResult> UpdateValid()
        {
            return _runtime.InvokeAsync("odata_update", _updateValid, CancellationToken.None);
        }

        #endregion

    }

}
