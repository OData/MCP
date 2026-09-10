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
    /// Per-call cost of the model-only tools: the describe and list tools a model calls before it touches data.
    /// </summary>
    [MemoryDiagnoser]
    public class DescribeBenchmarks
    {

        #region Fields

        private Dictionary<string, JsonElement> _completeJson = [];
        private Dictionary<string, JsonElement> _completeText = [];
        private Dictionary<string, JsonElement> _typeJson = [];
        private Dictionary<string, JsonElement> _typeText = [];
        private ODataToolRuntime _runtime = null!;

        #endregion

        #region Properties

        /// <summary>
        /// The service the tools describe.
        /// </summary>
        [Params(LiveModels.Northwind, LiveModels.TripPin)]
        public string Service { get; set; } = LiveModels.Northwind;

        #endregion

        #region Public Methods

        /// <summary>
        /// <c>odata_describe_model</c> with <c>detail=complete</c>, compact JSON.
        /// </summary>
        /// <returns>
        /// The tool result.
        /// </returns>
        [Benchmark]
        public Task<ODataToolInvocationResult> DescribeModelCompleteJson()
        {
            return _runtime.InvokeAsync("odata_describe_model", _completeJson, CancellationToken.None);
        }

        /// <summary>
        /// <c>odata_describe_model</c> with <c>detail=complete</c>, declaration text.
        /// </summary>
        /// <returns>
        /// The tool result.
        /// </returns>
        [Benchmark]
        public Task<ODataToolInvocationResult> DescribeModelCompleteText()
        {
            return _runtime.InvokeAsync("odata_describe_model", _completeText, CancellationToken.None);
        }

        /// <summary>
        /// <c>odata_describe_model</c> with the default summary.
        /// </summary>
        /// <returns>
        /// The tool result.
        /// </returns>
        [Benchmark]
        public Task<ODataToolInvocationResult> DescribeModelSummary()
        {
            return _runtime.InvokeAsync("odata_describe_model", null, CancellationToken.None);
        }

        /// <summary>
        /// <c>odata_describe_type</c> for the reference set, compact JSON.
        /// </summary>
        /// <returns>
        /// The tool result.
        /// </returns>
        [Benchmark]
        public Task<ODataToolInvocationResult> DescribeTypeJson()
        {
            return _runtime.InvokeAsync("odata_describe_type", _typeJson, CancellationToken.None);
        }

        /// <summary>
        /// <c>odata_describe_type</c> for the reference set, declaration text (the default).
        /// </summary>
        /// <returns>
        /// The tool result.
        /// </returns>
        [Benchmark(Baseline = true)]
        public Task<ODataToolInvocationResult> DescribeTypeText()
        {
            return _runtime.InvokeAsync("odata_describe_type", _typeText, CancellationToken.None);
        }

        /// <summary>
        /// <c>odata_list_entity_sets</c>.
        /// </summary>
        /// <returns>
        /// The tool result.
        /// </returns>
        [Benchmark]
        public Task<ODataToolInvocationResult> ListEntitySets()
        {
            return _runtime.InvokeAsync("odata_list_entity_sets", null, CancellationToken.None);
        }

        /// <summary>
        /// <c>odata_list_operations</c>.
        /// </summary>
        /// <returns>
        /// The tool result.
        /// </returns>
        [Benchmark]
        public Task<ODataToolInvocationResult> ListOperations()
        {
            return _runtime.InvokeAsync("odata_list_operations", null, CancellationToken.None);
        }

        /// <summary>
        /// Builds the catalog and runtime once; only the tool call is measured.
        /// </summary>
        [GlobalSetup]
        public async Task SetupAsync()
        {
            var service = await LiveModels.LoadAsync(Service).ConfigureAwait(false);
            var catalog = new ODataMcpCatalog(service.Model, new ODataMcpCatalogOptions());
            _runtime = new ODataToolRuntime(catalog, new NoopODataExecutor());
            _typeText = ToolArguments.Of("name", service.EntitySet);
            _typeJson = ToolArguments.Of("name", service.EntitySet, "format", "json");
            _completeText = ToolArguments.Of("detail", "complete");
            _completeJson = ToolArguments.Of("detail", "complete", "format", "json");
        }

        #endregion

    }

}
