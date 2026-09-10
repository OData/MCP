// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using Microsoft.OData.Mcp.Benchmarks.Infrastructure;
using Microsoft.OData.Mcp.Core.Models;
using Microsoft.OData.Mcp.Core.Parsing;

namespace Microsoft.OData.Mcp.Benchmarks.Compute
{

    /// <summary>
    /// How long it takes to turn a live <c>$metadata</c> document into the Core EDM.
    /// </summary>
    [MemoryDiagnoser]
    public class CsdlParseBenchmarks
    {

        #region Fields

        private string _xml = string.Empty;

        #endregion

        #region Properties

        /// <summary>
        /// The service whose metadata is parsed.
        /// </summary>
        [Params(LiveModels.Northwind, LiveModels.TripPin)]
        public string Service { get; set; } = LiveModels.Northwind;

        #endregion

        #region Public Methods

        /// <summary>
        /// Parses the CSDL document into an <see cref="EdmModel"/>.
        /// </summary>
        /// <returns>
        /// The parsed model.
        /// </returns>
        [Benchmark]
        public EdmModel Parse()
        {
            return new CsdlParser().ParseFromString(_xml);
        }

        /// <summary>
        /// Fetches the metadata once.
        /// </summary>
        [GlobalSetup]
        public async Task SetupAsync()
        {
            _xml = (await LiveModels.LoadAsync(Service).ConfigureAwait(false)).Xml;
        }

        #endregion

    }

}
