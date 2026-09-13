// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.OData.Mcp.Core.Catalog;
using Microsoft.OData.Mcp.Core.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.OData.Mcp.Tests.Core.Catalog
{

    /// <summary>
    /// Catalog and runtime tests for few-set, operations-only, and wide models, plus request guards.
    /// </summary>
    [TestClass]
    public class CatalogComplexityTests
    {

        #region Public Methods

        /// <summary>
        /// A model with one set and no operations still advertises the generic tools.
        /// </summary>
        [TestMethod]
        public void Catalog_SingleSetNoOperations_HasGenericsAndNamedFamily()
        {
            var catalog = new ODataMcpCatalog(CreateSetOnlyModel(), new ODataMcpCatalogOptions());

            catalog.Tools.Select(tool => tool.Name).Should().Contain("odata_query");
            catalog.Tools.Select(tool => tool.Name).Should().Contain("list_customers");
            catalog.Tools.Select(tool => tool.Name).Should().Contain("odata_list_operations");
            catalog.Resources.Select(resource => resource.Name).Should().Contain("Customers");
        }

        /// <summary>
        /// An operations-only model lists functions and actions and has no named CRUD tools.
        /// </summary>
        [TestMethod]
        public async Task Catalog_OperationsOnly_ListsAndCallsOperations()
        {
            var catalog = new ODataMcpCatalog(CreateOperationsOnlyModel(), new ODataMcpCatalogOptions());
            var executor = new RecordingODataExecutor();
            var runtime = new ODataToolRuntime(catalog, executor);

            catalog.Tools.Where(tool => tool.EntitySetName is not null).Should().BeEmpty();
            var listed = await runtime.InvokeAsync("odata_list_operations", null, CancellationToken.None);
            listed.IsError.Should().BeFalse();
            listed.StructuredContent.Should().Contain("GetStatus").And.Contain("Reset");

            var sets = await runtime.InvokeAsync("odata_list_entity_sets", null, CancellationToken.None);
            sets.StructuredContent.Should().Contain("entitySets");

            var status = await runtime.InvokeAsync(
                "odata_call",
                new Dictionary<string, JsonElement>
                {
                    ["name"] = JsonSerializer.SerializeToElement("GetStatus"),
                    ["parameters"] = JsonSerializer.SerializeToElement(new { code = "open" })
                },
                CancellationToken.None);
            status.IsError.Should().BeFalse();
            executor.Last!.Method.Should().Be(HttpMethod.Get);
            executor.Last.RelativePath.Should().Contain("GetStatus");
            executor.Last.RelativePath.Should().Contain("code=");

            var reset = await runtime.InvokeAsync(
                "odata_call",
                new Dictionary<string, JsonElement>
                {
                    ["name"] = JsonSerializer.SerializeToElement("Reset")
                },
                CancellationToken.None);
            reset.IsError.Should().BeFalse();
            executor.Last.Method.Should().Be(HttpMethod.Post);

            var missing = await runtime.InvokeAsync(
                "odata_call",
                new Dictionary<string, JsonElement>
                {
                    ["name"] = JsonSerializer.SerializeToElement("Ghost")
                },
                CancellationToken.None);
            missing.IsError.Should().BeTrue();
        }

        /// <summary>
        /// Two hundred entity sets do not explode named tools, resources, or completions.
        /// </summary>
        [TestMethod]
        public void Catalog_TwoHundredSets_RespectsCaps()
        {
            var catalog = new ODataMcpCatalog(CreateWideModel(200), new ODataMcpCatalogOptions
            {
                MaxCompletionValues = 50,
                MaxNamedTools = 150,
                MaxResources = 50
            });

            catalog.Tools.Count.Should().BeLessThanOrEqualTo(150);
            catalog.Resources.Count.Should().BeLessThanOrEqualTo(50);
            catalog.Resources.Select(resource => resource.Name).Should().Contain("$metadata");
            catalog.CompleteEntitySetNames(string.Empty).Count.Should().Be(50);
            catalog.CompleteEntitySetNames("Rows1").Count.Should().BeLessThanOrEqualTo(50);
        }

        /// <summary>
        /// Queries without top do not invent a top option.
        /// </summary>
        [TestMethod]
        public async Task Query_MissingTop_DoesNotAddTop()
        {
            var executor = new RecordingODataExecutor();
            var runtime = ODataToolRuntimeTests.CreateRuntime(executor);
            await runtime.InvokeAsync(
                "odata_query",
                new Dictionary<string, JsonElement>
                {
                    ["entitySet"] = JsonSerializer.SerializeToElement("People")
                },
                CancellationToken.None);

            executor.Last!.QueryOptions.Should().NotContainKey("top");
        }

        /// <summary>
        /// Specified top values are forwarded unchanged.
        /// </summary>
        [TestMethod]
        public async Task Query_HugeTop_IsForwarded()
        {
            var executor = new RecordingODataExecutor();
            var runtime = ODataToolRuntimeTests.CreateRuntime(executor);
            await runtime.InvokeAsync(
                "odata_query",
                new Dictionary<string, JsonElement>
                {
                    ["entitySet"] = JsonSerializer.SerializeToElement("People"),
                    ["top"] = JsonSerializer.SerializeToElement(50_000)
                },
                CancellationToken.None);

            executor.Last!.QueryOptions["top"].Should().Be("50000");
        }

        /// <summary>
        /// Specified negative top values are forwarded unchanged.
        /// </summary>
        [TestMethod]
        public async Task Query_NegativeTop_IsForwarded()
        {
            var executor = new RecordingODataExecutor();
            var runtime = ODataToolRuntimeTests.CreateRuntime(executor);
            await runtime.InvokeAsync(
                "odata_query",
                new Dictionary<string, JsonElement>
                {
                    ["entitySet"] = JsonSerializer.SerializeToElement("People"),
                    ["top"] = JsonSerializer.SerializeToElement(-1)
                },
                CancellationToken.None);

            executor.Last!.QueryOptions["top"].Should().Be("-1");
        }

        /// <summary>
        /// Count, skip, and top are forwarded together.
        /// </summary>
        [TestMethod]
        public async Task Query_CountSkipAndTop_AreForwardedTogether()
        {
            var executor = new RecordingODataExecutor();
            var runtime = ODataToolRuntimeTests.CreateRuntime(executor);
            await runtime.InvokeAsync(
                "odata_query",
                new Dictionary<string, JsonElement>
                {
                    ["count"] = JsonSerializer.SerializeToElement(true),
                    ["entitySet"] = JsonSerializer.SerializeToElement("People"),
                    ["skip"] = JsonSerializer.SerializeToElement(2),
                    ["top"] = JsonSerializer.SerializeToElement(1)
                },
                CancellationToken.None);

            executor.Last!.QueryOptions["count"].Should().Be("true");
            executor.Last.QueryOptions["skip"].Should().Be("2");
            executor.Last.QueryOptions["top"].Should().Be("1");
        }

        /// <summary>
        /// Skip without top does not invent a top option.
        /// </summary>
        [TestMethod]
        public async Task Query_SkipWithoutTop_DoesNotAddTop()
        {
            var executor = new RecordingODataExecutor();
            var runtime = ODataToolRuntimeTests.CreateRuntime(executor);
            await runtime.InvokeAsync(
                "odata_query",
                new Dictionary<string, JsonElement>
                {
                    ["entitySet"] = JsonSerializer.SerializeToElement("People"),
                    ["skip"] = JsonSerializer.SerializeToElement(2)
                },
                CancellationToken.None);

            executor.Last!.QueryOptions["skip"].Should().Be("2");
            executor.Last.QueryOptions.Should().NotContainKey("top");
        }

        /// <summary>
        /// Oversized filters are rejected before they reach OData.
        /// </summary>
        [TestMethod]
        public async Task Query_HugeFilter_IsError()
        {
            var result = await ODataToolRuntimeTests.CreateRuntime().InvokeAsync(
                "odata_query",
                new Dictionary<string, JsonElement>
                {
                    ["entitySet"] = JsonSerializer.SerializeToElement("People"),
                    ["filter"] = JsonSerializer.SerializeToElement(new string('x', 3_000))
                },
                CancellationToken.None);

            result.IsError.Should().BeTrue();
            result.Text.Should().Contain("filter");
        }

        /// <summary>
        /// Oversized create bodies are rejected.
        /// </summary>
        [TestMethod]
        public async Task Create_HugeBody_IsError()
        {
            var result = await ODataToolRuntimeTests.CreateRuntime().InvokeAsync(
                "odata_create",
                new Dictionary<string, JsonElement>
                {
                    ["entitySet"] = JsonSerializer.SerializeToElement("People"),
                    ["body"] = JsonSerializer.SerializeToElement(new string('a', 300_000))
                },
                CancellationToken.None);

            result.IsError.Should().BeTrue();
            result.Text.Should().Contain("body");
        }

        /// <summary>
        /// Bound operations require a key.
        /// </summary>
        [TestMethod]
        public async Task Call_BoundWithoutKey_IsError()
        {
            var catalog = new ODataMcpCatalog(CreateOperationsOnlyModel(), new ODataMcpCatalogOptions());
            var runtime = new ODataToolRuntime(catalog, new RecordingODataExecutor());
            var result = await runtime.InvokeAsync(
                "odata_call",
                new Dictionary<string, JsonElement>
                {
                    ["name"] = JsonSerializer.SerializeToElement("IsPremium")
                },
                CancellationToken.None);

            result.IsError.Should().BeTrue();
            result.Text.Should().Contain("entitySet");
        }

        #endregion

        #region Internal Methods

        /// <summary>
        /// Builds a Core model with one entity set and no operations.
        /// </summary>
        /// <returns>
        /// The model.
        /// </returns>
        internal static EdmModel CreateSetOnlyModel()
        {
            var model = new EdmModel();
            var type = new EdmEntityType("Customer", "Sales")
            {
                Name = "Customer",
                Namespace = "Sales"
            };
            type.Key.Add("CustomerId");
            type.Properties.Add(new EdmProperty("CustomerId", "Edm.Int32")
            {
                Name = "CustomerId",
                Nullable = false,
                Type = "Edm.Int32"
            });
            model.AddEntityType(type);
            var container = new EdmEntityContainer("Container", "Sales")
            {
                Name = "Container",
                Namespace = "Sales"
            };
            container.AddEntitySet(new EdmEntitySet("Customers", type.FullName)
            {
                EntityType = type.FullName,
                Name = "Customers"
            });
            model.AddEntityContainer(container);

            return model;
        }

        /// <summary>
        /// Builds a Core model with unbound and bound operations and no entity sets.
        /// </summary>
        /// <returns>
        /// The model.
        /// </returns>
        internal static EdmModel CreateOperationsOnlyModel()
        {
            var model = new EdmModel();
            var status = new EdmFunction("GetStatus", "Ops")
            {
                Name = "GetStatus",
                Namespace = "Ops",
                ReturnType = "Edm.String"
            };
            status.Parameters.Add(new EdmParameter("code", "Edm.String")
            {
                Name = "code",
                Type = "Edm.String"
            });
            model.Functions.Add(status);
            model.Functions.Add(new EdmFunction("IsPremium", "Ops")
            {
                BindingParameterType = "Ops.Customer",
                IsBound = true,
                Name = "IsPremium",
                Namespace = "Ops",
                ReturnType = "Edm.Boolean"
            });
            model.Functions[^1].Parameters.Add(new EdmParameter("binding", "Ops.Customer")
            {
                Name = "binding",
                Type = "Ops.Customer"
            });
            model.Actions.Add(new EdmAction("Reset", "Ops")
            {
                Name = "Reset",
                Namespace = "Ops"
            });
            model.AddEntityContainer(new EdmEntityContainer("Container", "Ops")
            {
                Name = "Container",
                Namespace = "Ops"
            });

            return model;
        }

        /// <summary>
        /// Builds a Core model with many entity sets.
        /// </summary>
        /// <param name="count">The number of sets.</param>
        /// <returns>
        /// The model.
        /// </returns>
        internal static EdmModel CreateWideModel(int count)
        {
            var model = new EdmModel();
            var container = new EdmEntityContainer("Container", "Wide")
            {
                Name = "Container",
                Namespace = "Wide"
            };
            for (var i = 0; i < count; i++)
            {
                var typeName = $"Row{i:D3}";
                var type = new EdmEntityType(typeName, "Wide")
                {
                    Name = typeName,
                    Namespace = "Wide"
                };
                type.Key.Add("Id");
                type.Properties.Add(new EdmProperty("Id", "Edm.Int32")
                {
                    Name = "Id",
                    Nullable = false,
                    Type = "Edm.Int32"
                });
                model.AddEntityType(type);
                container.AddEntitySet(new EdmEntitySet($"Rows{i:D3}", type.FullName)
                {
                    EntityType = type.FullName,
                    Name = $"Rows{i:D3}"
                });
            }

            model.AddEntityContainer(container);

            return model;
        }

        #endregion

    }

}
