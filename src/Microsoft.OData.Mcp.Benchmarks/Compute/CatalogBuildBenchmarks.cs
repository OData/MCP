// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using Microsoft.OData.Mcp.Benchmarks.Infrastructure;
using Microsoft.OData.Mcp.Core.Catalog;
using Microsoft.OData.Mcp.Core.Models;

namespace Microsoft.OData.Mcp.Benchmarks.Compute
{

    /// <summary>
    /// Cost of building the MCP catalog from a parsed model: resources, generic and named tools, typed schemas,
    /// and (for a static model) every type shape up front.
    /// </summary>
    [MemoryDiagnoser]
    public class CatalogBuildBenchmarks
    {

        #region Fields

        private EdmModel _model = new();

        #endregion

        #region Properties

        /// <summary>
        /// The service whose model is cataloged.
        /// </summary>
        [Params(LiveModels.Northwind, LiveModels.TripPin)]
        public string Service { get; set; } = LiveModels.Northwind;

        #endregion

        #region Public Methods

        /// <summary>
        /// Builds a catalog for a model that may change, so shapes are computed on demand instead of cached.
        /// </summary>
        /// <returns>
        /// The catalog.
        /// </returns>
        [Benchmark]
        public ODataMcpCatalog DynamicModel()
        {
            return new ODataMcpCatalog(_model, new ODataMcpCatalogOptions { IsDynamicModel = true });
        }

        /// <summary>
        /// Loads the model once.
        /// </summary>
        [GlobalSetup]
        public async Task SetupAsync()
        {
            _model = (await LiveModels.LoadAsync(Service).ConfigureAwait(false)).Model;
        }

        /// <summary>
        /// Builds a catalog for a static model, filling the shape cache for every entity type.
        /// </summary>
        /// <returns>
        /// The catalog.
        /// </returns>
        [Benchmark(Baseline = true)]
        public ODataMcpCatalog StaticModel()
        {
            return new ODataMcpCatalog(_model, new ODataMcpCatalogOptions());
        }

        #endregion

    }

}
