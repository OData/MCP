// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.OData.Mcp.Core.Catalog;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.OData.Mcp.Tests.Core.Catalog
{

    /// <summary>
    /// Generic tool catalog tests.
    /// </summary>
    [TestClass]
    public class ODataMcpCatalogToolTests
    {

        #region Public Methods

        /// <summary>
        /// Generic query tool advertises filter without a $ prefix.
        /// </summary>
        [TestMethod]
        public async Task Catalog_GenericQueryTool_HasFilterWithoutDollar()
        {
            var catalog = new ODataMcpCatalog(await LiveMetadata.LoadNorthwindModelAsync(), new ODataMcpCatalogOptions());
            var query = catalog.Tools.Single(tool => tool.Name == "odata_query");

            query.InputSchema.Should().Contain("\"filter\"");
            query.InputSchema.Should().NotContain("\"$filter\"");
            query.ReadOnlyHint.Should().BeTrue();
        }

        /// <summary>
        /// Generic and named tool descriptions are the short first-call copy from OPTIMIZATION.md §2.
        /// </summary>
        [TestMethod]
        public async Task Catalog_GenericTools_HaveFirstCallDescriptions()
        {
            var catalog = new ODataMcpCatalog(await LiveMetadata.LoadNorthwindModelAsync(), new ODataMcpCatalogOptions());
            var byName = catalog.Tools.ToDictionary(tool => tool.Name, tool => tool.Description);

            byName["odata_list_entity_sets"].Should().Be("Names, types, and keys of declared entity sets.");
            byName["odata_get"].Should().Be("Gets an entity by key. Optional select and expand shape the result.");
            byName["odata_delete"].Should().Be("Deletes an entity by key.");
            byName["odata_navigate"].Should().Be("Follows a navigation property from a key. Accepts the same query options as odata_query.");
            byName["odata_create"].Should().Be("JSON object in body. Include every property the type lists as required on create (odata_describe_type). Client-assigned keys are required; omit store-generated keys.");
            byName["odata_update"].Should().Be("PATCH object in body. Send only fields to change. Omitted fields keep their values. JSON null clears an optional property; never send it for a required one.");
            byName["odata_list_operations"].Should().Be("Unbound operations on the service.");
            byName["odata_query"].Should().Contain("do not include $");
            byName["odata_describe_type"].Should().StartWith("Declared properties, keys, navigations, bound operations, and enums");
            byName["odata_describe_type"].Should().Contain("-> = navigation; [] = collection");
            byName["odata_describe_type"].Should().Contain("// key, store-generated = omit on create");
            byName["odata_describe_type"].Should().NotContain("PATCH", "the PATCH rule lives on odata_update only");
            byName["odata_describe_model"].Should().Contain("Do not read $metadata to explore.");
            byName["odata_call"].Should().StartWith("Call a declared operation by name");
            byName["odata_call"].Should().Contain("using the declared parameter names");
            byName["odata_call"].Should().Contain("Instance-bound (the default for bound)");
            byName["create_customer"].Should().Be("Creates a Customer.");
            byName["update_customer"].Should().Be("PATCH a Customer. Send only fields to change; omit to keep. Do not send JSON null for required properties.");
            byName["get_customer"].Should().Be("Gets a Customer by key.");
            byName["delete_customer"].Should().Be("Deletes a Customer.");
        }

        /// <summary>
        /// Generic tool names are advertised in the documented order.
        /// </summary>
        [TestMethod]
        public async Task Catalog_GenericTools_HaveExpectedNames()
        {
            var catalog = new ODataMcpCatalog(await LiveMetadata.LoadNorthwindModelAsync(), new ODataMcpCatalogOptions());
            var generic = catalog.Tools.Where(tool => tool.Name.StartsWith("odata_", StringComparison.Ordinal)).Select(tool => tool.Name).ToList();

            generic.Should().ContainInOrder(
                "odata_list_entity_sets",
                "odata_describe_type",
                "odata_describe_model",
                "odata_query",
                "odata_get",
                "odata_create",
                "odata_update",
                "odata_delete",
                "odata_navigate",
                "odata_list_operations",
                "odata_call");
        }

        /// <summary>
        /// Every generic tool that takes a key says how to write it: bare scalars, auto-quoted strings, composite pairs.
        /// </summary>
        [TestMethod]
        public async Task Catalog_KeyArguments_DescribeTheLiteralForm()
        {
            var catalog = new ODataMcpCatalog(await LiveMetadata.LoadNorthwindModelAsync(), new ODataMcpCatalogOptions());
            var expected = "Key as text: ALFKI or 10248; strings are quoted for you. Composite: OrderID=10248,ProductID=11.";

            foreach (var name in new[] { "odata_get", "odata_update", "odata_delete", "odata_navigate", "odata_call" })
            {
                var tool = catalog.Tools.Single(candidate => candidate.Name == name);
                using var document = JsonDocument.Parse(tool.InputSchema);
                var key = document.RootElement.GetProperty("properties").GetProperty("key");

                key.GetProperty("type").GetString().Should().Be("string", name);
                key.GetProperty("description").GetString().Should().Be(expected, name);
            }
        }

        /// <summary>
        /// Named get_* tools name the type's own key properties instead of repeating the generic key text per set.
        /// </summary>
        [TestMethod]
        public async Task Catalog_NamedGetTools_NameTheirKeyProperties()
        {
            var catalog = new ODataMcpCatalog(await LiveMetadata.LoadNorthwindModelAsync(), new ODataMcpCatalogOptions());

            using var customer = JsonDocument.Parse(catalog.Tools.Single(tool => tool.Name == "get_customer").InputSchema);
            customer.RootElement.GetProperty("properties").GetProperty("key").GetProperty("description").GetString()
                .Should().Be("CustomerID as text; strings are quoted for you.");

            using var orderDetail = JsonDocument.Parse(catalog.Tools.Single(tool => tool.Name == "get_order_detail").InputSchema);
            orderDetail.RootElement.GetProperty("properties").GetProperty("key").GetProperty("description").GetString()
                .Should().Be("Name=value pairs: OrderID=...,ProductID=...; strings are quoted for you.");
        }

        /// <summary>
        /// Generic create and update advertise body as a JSON object, matching the "do not stringify" rule on odata_call.
        /// </summary>
        [TestMethod]
        public async Task Catalog_GenericCreateAndUpdate_BodyIsAnObject()
        {
            var catalog = new ODataMcpCatalog(await LiveMetadata.LoadNorthwindModelAsync(), new ODataMcpCatalogOptions());

            foreach (var name in new[] { "odata_create", "odata_update" })
            {
                var tool = catalog.Tools.Single(candidate => candidate.Name == name);
                using var document = JsonDocument.Parse(tool.InputSchema);
                var body = document.RootElement.GetProperty("properties").GetProperty("body");

                body.GetProperty("type").GetString().Should().Be("object", name);
                body.GetProperty("description").GetString().Should().EndWith("not a string.", name);
            }
        }

        /// <summary>
        /// odata_get and named get_* accept select and expand; odata_navigate accepts every odata_query option.
        /// </summary>
        [TestMethod]
        public async Task Catalog_GetAndNavigate_AdvertiseQueryOptions()
        {
            var catalog = new ODataMcpCatalog(await LiveMetadata.LoadNorthwindModelAsync(), new ODataMcpCatalogOptions());

            foreach (var name in new[] { "odata_get", "get_customer" })
            {
                using var document = JsonDocument.Parse(catalog.Tools.Single(candidate => candidate.Name == name).InputSchema);
                var properties = document.RootElement.GetProperty("properties");

                properties.TryGetProperty("select", out _).Should().BeTrue(name);
                properties.TryGetProperty("expand", out _).Should().BeTrue(name);
                properties.TryGetProperty("filter", out _).Should().BeFalse(name);
                document.RootElement.GetProperty("required").EnumerateArray().Select(item => item.GetString()).Should().NotContain("select");
            }

            using var navigate = JsonDocument.Parse(catalog.Tools.Single(candidate => candidate.Name == "odata_navigate").InputSchema);
            var navigateProperties = navigate.RootElement.GetProperty("properties");

            foreach (var option in new[] { "filter", "select", "orderby", "expand", "top", "skip", "count" })
            {
                navigateProperties.TryGetProperty(option, out _).Should().BeTrue(option);
            }

            navigateProperties.TryGetProperty("$filter", out _).Should().BeFalse();
            navigate.RootElement.GetProperty("required").EnumerateArray().Select(item => item.GetString()).Should().Equal("entitySet", "key", "navigation");
        }

        /// <summary>
        /// Named list tools carry the enum filter literal pattern when the type exposes enums, and stay short when it does not.
        /// </summary>
        [TestMethod]
        public async Task Catalog_NamedListTools_CarryEnumFilterHint()
        {
            var tripPin = new ODataMcpCatalog(await LiveMetadata.LoadTripPinModelAsync(), new ODataMcpCatalogOptions());
            var northwind = new ODataMcpCatalog(await LiveMetadata.LoadNorthwindModelAsync(), new ODataMcpCatalogOptions());

            var people = tripPin.Tools.Single(tool => tool.Name == "list_people").Description;
            people.Should().Contain("Query parameter names do not include $. Filter enums as Trippin.PersonGender'{value}', Trippin.Feature'{value}'.");

            var customers = northwind.Tools.Single(tool => tool.Name == "list_customers").Description;
            customers.Should().EndWith("Query parameter names do not include $.");
            customers.Should().NotContain("Filter enums");
        }

        #endregion

    }

}
