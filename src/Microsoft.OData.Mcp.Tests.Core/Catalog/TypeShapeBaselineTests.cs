// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using CloudNimble.Breakdance.Assemblies;
using FluentAssertions;
using Microsoft.OData.Mcp.Core.Catalog;
using Microsoft.OData.Mcp.Tests.Shared;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.OData.Mcp.Tests.Core.Catalog
{

    /// <summary>
    /// Locks the payloads the calling AI reads (describe, list, schemas) to Breakdance baseline files.
    /// </summary>
    /// <remarks>
    /// <c>Baselines/TypeShapes/Before/</c> is the snapshot taken before the optimization work and is write-once.
    /// <c>Baselines/TypeShapes/Current/</c> is what these tests assert and is regenerated after each intended change.
    /// To regenerate, uncomment the <c>DataRow</c> and <c>TestMethod</c> attributes on <see cref="WriteTypeShapeBaselinesAsync"/>,
    /// run that test, then comment them again.
    /// </remarks>
    [TestClass]
    public class TypeShapeBaselineTests
    {

        #region Fields

        internal const string BeforeFolder = "Baselines/TypeShapes/Before";

        internal const string CurrentFolder = "Baselines/TypeShapes/Current";

        internal const string NorthwindCreateCustomerInputSchema = "northwind.create_customer.inputschema.json";

        internal const string NorthwindDescribeTypeCustomer = "northwind.describe_type.customer.json";

        internal const string NorthwindListEntitySets = "northwind.list_entity_sets.json";

        internal const string NorthwindListOperations = "northwind.list_operations.json";

        internal const string NorthwindToolsList = "northwind.tools.list.json";

        internal const string TripPinDescribeTypePerson = "trippin.describe_type.person.json";

        internal const string TripPinListOperations = "trippin.list_operations.json";

        private const string projectPath = "..//..//..//";

        #endregion

        #region Public Methods

        /// <summary>
        /// The Northwind <c>create_customer</c> input schema matches the current baseline.
        /// </summary>
        [TestMethod]
        public async Task CreateCustomerInputSchema_Northwind_MatchesCurrentBaseline()
        {
            var payloads = await BuildNorthwindPayloadsAsync();

            payloads[NorthwindCreateCustomerInputSchema].Should().Be(ReadCurrent(NorthwindCreateCustomerInputSchema));
        }

        /// <summary>
        /// <c>odata_describe_type</c> for Northwind Customers matches the current baseline.
        /// </summary>
        [TestMethod]
        public async Task DescribeType_Northwind_Customer_MatchesCurrentBaseline()
        {
            var payloads = await BuildNorthwindPayloadsAsync();

            payloads[NorthwindDescribeTypeCustomer].Should().Be(ReadCurrent(NorthwindDescribeTypeCustomer));
        }

        /// <summary>
        /// <c>odata_describe_type</c> for TripPin People matches the current baseline.
        /// </summary>
        [TestMethod]
        public async Task DescribeType_TripPin_Person_MatchesCurrentBaseline()
        {
            var payloads = await BuildTripPinPayloadsAsync();

            payloads[TripPinDescribeTypePerson].Should().Be(ReadCurrent(TripPinDescribeTypePerson));
        }

        /// <summary>
        /// <c>odata_list_entity_sets</c> for Northwind matches the current baseline.
        /// </summary>
        [TestMethod]
        public async Task ListEntitySets_Northwind_MatchesCurrentBaseline()
        {
            var payloads = await BuildNorthwindPayloadsAsync();

            payloads[NorthwindListEntitySets].Should().Be(ReadCurrent(NorthwindListEntitySets));
        }

        /// <summary>
        /// <c>odata_list_operations</c> for Northwind matches the current baseline.
        /// </summary>
        [TestMethod]
        public async Task ListOperations_Northwind_MatchesCurrentBaseline()
        {
            var payloads = await BuildNorthwindPayloadsAsync();

            payloads[NorthwindListOperations].Should().Be(ReadCurrent(NorthwindListOperations));
        }

        /// <summary>
        /// <c>odata_list_operations</c> for TripPin matches the current baseline.
        /// </summary>
        [TestMethod]
        public async Task ListOperations_TripPin_MatchesCurrentBaseline()
        {
            var payloads = await BuildTripPinPayloadsAsync();

            payloads[TripPinListOperations].Should().Be(ReadCurrent(TripPinListOperations));
        }

        /// <summary>
        /// The ordered Northwind tool list and generic input schemas match the current baseline.
        /// </summary>
        [TestMethod]
        public async Task ToolsList_Northwind_MatchesCurrentBaseline()
        {
            var payloads = await BuildNorthwindPayloadsAsync();

            payloads[NorthwindToolsList].Should().Be(ReadCurrent(NorthwindToolsList));
        }

        /// <summary>
        /// Writes every payload to <c>Current/</c>, and to <c>Before/</c> only when that file does not exist yet.
        /// </summary>
        /// <param name="projectPath">The path to the test project directory.</param>
        //[DataRow(projectPath)]
        //[TestMethod]
        [BreakdanceManifestGenerator]
        public async Task WriteTypeShapeBaselinesAsync(string projectPath)
        {
            var payloads = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var pair in await BuildNorthwindPayloadsAsync())
            {
                payloads[pair.Key] = pair.Value;
            }

            foreach (var pair in await BuildTripPinPayloadsAsync())
            {
                payloads[pair.Key] = pair.Value;
            }

            var beforePath = Path.Combine(projectPath, BeforeFolder);
            var currentPath = Path.Combine(projectPath, CurrentFolder);
            Directory.CreateDirectory(beforePath);
            Directory.CreateDirectory(currentPath);

            foreach (var pair in payloads)
            {
                File.WriteAllText(Path.Combine(currentPath, pair.Key), pair.Value);

                var beforeFile = Path.Combine(beforePath, pair.Key);
                if (!File.Exists(beforeFile))
                {
                    File.WriteAllText(beforeFile, pair.Value);
                }
            }
        }

        #endregion

        #region Internal Methods

        /// <summary>
        /// Builds every Northwind payload keyed by baseline file name.
        /// </summary>
        /// <returns>
        /// File name to payload text.
        /// </returns>
        internal static async Task<Dictionary<string, string>> BuildNorthwindPayloadsAsync()
        {
            var catalog = new ODataMcpCatalog(await LiveMetadata.LoadNorthwindModelAsync(), new ODataMcpCatalogOptions());
            var runtime = new ODataToolRuntime(catalog, new UnusedODataExecutor());

            return new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [NorthwindCreateCustomerInputSchema] = catalog.Tools.Single(tool => tool.Name == "create_customer").InputSchema,
                [NorthwindDescribeTypeCustomer] = await InvokeStructuredAsync(runtime, "odata_describe_type", ToolArguments.Of("name", "Customers")),
                [NorthwindListEntitySets] = await InvokeStructuredAsync(runtime, "odata_list_entity_sets", null),
                [NorthwindListOperations] = await InvokeStructuredAsync(runtime, "odata_list_operations", null),
                [NorthwindToolsList] = BuildToolsList(catalog)
            };
        }

        /// <summary>
        /// Builds every TripPin payload keyed by baseline file name.
        /// </summary>
        /// <returns>
        /// File name to payload text.
        /// </returns>
        internal static async Task<Dictionary<string, string>> BuildTripPinPayloadsAsync()
        {
            var catalog = new ODataMcpCatalog(await LiveMetadata.LoadTripPinModelAsync(), new ODataMcpCatalogOptions());
            var runtime = new ODataToolRuntime(catalog, new UnusedODataExecutor());

            return new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [TripPinDescribeTypePerson] = await InvokeStructuredAsync(runtime, "odata_describe_type", ToolArguments.Of("name", "People")),
                [TripPinListOperations] = await InvokeStructuredAsync(runtime, "odata_list_operations", null)
            };
        }

        /// <summary>
        /// Serializes the ordered tool names plus each generic tool's input schema.
        /// </summary>
        /// <param name="catalog">The catalog.</param>
        /// <returns>
        /// Deterministic JSON.
        /// </returns>
        internal static string BuildToolsList(ODataMcpCatalog catalog)
        {
            var generics = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
            foreach (var tool in catalog.Tools.Where(tool => tool.Name.StartsWith("odata_", StringComparison.Ordinal)))
            {
                generics[tool.Name] = JsonSerializer.Deserialize<JsonElement>(tool.InputSchema);
            }

            var payload = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["tools"] = catalog.Tools.Select(tool => tool.Name).ToList(),
                ["generics"] = generics
            };

            return JsonSerializer.Serialize(payload, ODataMcpCatalog.SchemaSerializerOptions);
        }

        /// <summary>
        /// Invokes a catalog-only tool and returns its structured JSON.
        /// </summary>
        /// <param name="runtime">The runtime.</param>
        /// <param name="name">The tool name.</param>
        /// <param name="arguments">The arguments, if any.</param>
        /// <returns>
        /// The structured content.
        /// </returns>
        internal static async Task<string> InvokeStructuredAsync(ODataToolRuntime runtime, string name, Dictionary<string, JsonElement>? arguments)
        {
            var result = await runtime.InvokeAsync(name, arguments, CancellationToken.None);
            result.IsError.Should().BeFalse(result.Text);
            result.StructuredContent.Should().NotBeNullOrWhiteSpace();

            return result.StructuredContent!;
        }

        /// <summary>
        /// Reads a <c>Current/</c> baseline file.
        /// </summary>
        /// <param name="fileName">The baseline file name.</param>
        /// <returns>
        /// The file contents.
        /// </returns>
        internal static string ReadCurrent(string fileName)
        {
            return File.ReadAllText(Path.Combine(projectPath, CurrentFolder, fileName));
        }

        #endregion

    }

}
