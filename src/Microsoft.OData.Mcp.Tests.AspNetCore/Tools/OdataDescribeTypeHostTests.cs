// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.OData;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OData.Mcp.Tests.AspNetCore.Fixtures;
using Microsoft.OData.Mcp.Tests.Shared;
using Microsoft.OData.Mcp.Tests.Shared.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.OData.Mcp.Tests.AspNetCore.Tools
{

    /// <summary>
    /// A8 host tests for <c>odata_describe_type</c> on the rich convention model.
    /// </summary>
    [TestClass]
    public class OdataDescribeTypeHostTests : ConventionRichHost
    {

        #region Public Methods

        /// <summary>
        /// <c>resources/read</c> type-card keys and property names match describe.
        /// </summary>
        [TestMethod]
        public async Task DescribeType_AgreesWithResourceReadTypeCard()
        {
            var described = await InvokeAsync("odata_describe_type", ToolArguments.Of("name", "Customers", "format", "json"));
            described.IsError.Should().BeFalse(described.Text);
            var resource = Session().Catalog.Resources.Single(item => item.Name == "Customers");
            resource.ReadContents.Should().NotBeNullOrWhiteSpace();

            described.StructuredContent.Should().Be(resource.ReadContents, "the type card is the same compact shape");
            ReadKeys(described.StructuredContent).Should().Equal(ReadKeys(resource.ReadContents));
            ReadPropertyNames(described.StructuredContent).Should().Equal(ReadPropertyNames(resource.ReadContents));
            ReadNavigationNames(described.StructuredContent).Should().Equal(ReadNavigationNames(resource.ReadContents));
        }

        /// <summary>
        /// Binary and stream properties on Documents are omitted from the properties array.
        /// </summary>
        [TestMethod]
        public async Task DescribeType_BinaryStreamProperties_OmittedFromPropertiesArray()
        {
            var result = await InvokeAsync("odata_describe_type", ToolArguments.Of("name", "Documents", "format", "json"));

            result.IsError.Should().BeFalse(result.Text);
            var properties = ReadPropertyNames(result.StructuredContent);
            properties.Should().Contain("Title");
            properties.Should().NotContain("Photo");
            properties.Should().NotContain("File");
            result.StructuredContent.Should().NotContain("Edm.Binary");
            result.StructuredContent.Should().NotContain("Edm.Stream");
        }

        /// <summary>
        /// An emoji type name is not declared.
        /// </summary>
        [TestMethod]
        public async Task DescribeType_EmojiName_IsErrorNotDeclared()
        {
            var result = await InvokeAsync("odata_describe_type", ToolArguments.Of("name", "😀"));

            result.IsError.Should().BeTrue();
            result.Text.Should().Contain("not declared");
        }

        /// <summary>
        /// Extra filter/top arguments are ignored and do not hit OData.
        /// </summary>
        [TestMethod]
        public async Task DescribeType_ExtraFilterTop_IgnoredAndStillDescribes()
        {
            var (runtime, capture) = CreateCapturingRuntime();
            var result = await runtime.InvokeAsync(
                "odata_describe_type",
                ToolArguments.Of("name", "Customers", "filter", "x", "top", 1),
                CancellationToken.None);

            result.IsError.Should().BeFalse(result.Text);
            result.Text.Should().Contain("CustomerId");
            capture.Requests.Should().BeEmpty();
        }

        /// <summary>
        /// The ignored CLR property InternalSecret never appears.
        /// </summary>
        [TestMethod]
        public async Task DescribeType_IgnoredPropertyNeverAppears_InternalSecret()
        {
            var result = await InvokeAsync("odata_describe_type", ToolArguments.Of("name", "Customers", "format", "json"));

            result.IsError.Should().BeFalse(result.Text);
            ReadPropertyNames(result.StructuredContent).Should().NotContain("InternalSecret");
            result.StructuredContent.Should().NotContain("InternalSecret");
        }

        /// <summary>
        /// JSON-RPC <c>tools/call</c> describes Customers.
        /// </summary>
        [TestMethod]
        public async Task DescribeType_JsonRpcToolsCall_OData8()
        {
            using var client = CreateClient();
            using var response = await McpJsonRpc.CallToolAsync(
                client,
                "/odata/mcp",
                "odata_describe_type",
                """{"name":"Customers"}""");
            var body = await McpJsonRpc.ReadBodyAsync(response);

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            body.Should().Contain("CustomerId");
            body.Should().Contain("CompanyName");
        }

        /// <summary>
        /// Omitting <c>name</c> is a missing-required-argument error.
        /// </summary>
        [TestMethod]
        public async Task DescribeType_MissingName_IsErrorMissingRequiredArgument()
        {
            var result = await InvokeAsync("odata_describe_type", new Dictionary<string, JsonElement>());

            result.IsError.Should().BeTrue();
            result.Text.Should().Contain("name");
        }

        /// <summary>
        /// A JSON array name is an error.
        /// </summary>
        [TestMethod]
        public async Task DescribeType_NameAsArray_IsError()
        {
            var result = await InvokeAsync("odata_describe_type", ToolArguments.Of("name", new[] { "Customers" }));

            result.IsError.Should().BeTrue();
            result.Text.Should().Match(text => text.Contains("not declared", StringComparison.Ordinal) || text.Contains("name", StringComparison.Ordinal));
        }

        /// <summary>
        /// A numeric name is coerced to raw text and looked up, then not declared.
        /// </summary>
        [TestMethod]
        public async Task DescribeType_NameAsNumber_CoercedToRawTextThenLookedUp()
        {
            var result = await InvokeAsync("odata_describe_type", ToolArguments.Of("name", 1));

            result.IsError.Should().BeTrue();
            result.Text.Should().Contain("1");
            result.Text.Should().Contain("not declared");
        }

        /// <summary>
        /// A JSON object name is an error.
        /// </summary>
        [TestMethod]
        public async Task DescribeType_NameAsObject_IsError()
        {
            var result = await InvokeAsync("odata_describe_type", ToolArguments.Of("name", new Dictionary<string, int> { ["x"] = 1 }));

            result.IsError.Should().BeTrue();
            result.Text.Should().Match(text => text.Contains("not declared", StringComparison.Ordinal) || text.Contains("name", StringComparison.Ordinal));
        }

        /// <summary>
        /// <c>$metadata</c> is not a declared type.
        /// </summary>
        [TestMethod]
        public async Task DescribeType_NameWithDollarMetadata_IsErrorNotDeclared()
        {
            var result = await InvokeAsync("odata_describe_type", ToolArguments.Of("name", "$metadata"));

            result.IsError.Should().BeTrue();
            result.Text.Should().Contain("not declared");
        }

        /// <summary>
        /// A null name is an error.
        /// </summary>
        [TestMethod]
        public async Task DescribeType_NullName_IsError()
        {
            var result = await InvokeAsync("odata_describe_type", ToolArguments.Of("name", null));

            result.IsError.Should().BeTrue();
            result.Text.Should().Contain("name");
        }

        /// <summary>
        /// Describing the Customers entity set includes CustomerId and the Orders navigation.
        /// </summary>
        [TestMethod]
        public async Task DescribeType_OData8_ByEntitySetCustomers_ContainsCustomerIdAndOrdersNav()
        {
            using var client = CreateClient();
            var metadata = await client.GetAsync("/odata/$metadata");
            var metadataBody = await metadata.Content.ReadAsStringAsync();
            if (metadata.IsSuccessStatusCode)
            {
                metadataBody.Should().Contain("Customer");
            }

            var result = await InvokeAsync("odata_describe_type", ToolArguments.Of("name", "Customers", "format", "json"));
            result.IsError.Should().BeFalse(result.Text);
            ReadKeys(result.StructuredContent).Should().Contain("CustomerId");
            ReadPropertyNames(result.StructuredContent).Should().Contain("CompanyName");
            ReadNavigationNames(result.StructuredContent).Should().Contain("Orders");
            ReadPropertyNames(result.StructuredContent).Should().NotContain("InternalSecret");
        }

        /// <summary>
        /// A full type name succeeds when the namespace is present.
        /// </summary>
        [TestMethod]
        public async Task DescribeType_OData8_ByFullName_IfNamespacePresent()
        {
            var fullName = Session().Catalog.Shapes.Keys.Single(key => key.EndsWith(".Customer", StringComparison.Ordinal));
            fullName.Should().Contain(".");

            var byFull = await InvokeAsync("odata_describe_type", ToolArguments.Of("name", fullName, "format", "json"));
            byFull.IsError.Should().BeFalse(byFull.Text);
            ReadKeys(byFull.StructuredContent).Should().Contain("CustomerId");
        }

        /// <summary>
        /// Describing the Customer type matches describing the Customers set.
        /// </summary>
        [TestMethod]
        public async Task DescribeType_OData8_ByTypeNameCustomer_SameAsSet()
        {
            var bySet = await InvokeAsync("odata_describe_type", ToolArguments.Of("name", "Customers", "format", "json"));
            var byType = await InvokeAsync("odata_describe_type", ToolArguments.Of("name", "Customer", "format", "json"));

            bySet.IsError.Should().BeFalse(bySet.Text);
            byType.IsError.Should().BeFalse(byType.Text);
            ReadKeys(bySet.StructuredContent).Should().Equal(ReadKeys(byType.StructuredContent));
            ReadPropertyNames(bySet.StructuredContent).Should().Equal(ReadPropertyNames(byType.StructuredContent));
            ReadNavigationNames(bySet.StructuredContent).Should().Equal(ReadNavigationNames(byType.StructuredContent));
        }

        /// <summary>
        /// A navigation from the type card is used with <c>odata_navigate</c>.
        /// </summary>
        [TestMethod]
        public async Task DescribeType_ThenNavigate_UsesANavigationFromTheCard()
        {
            var described = await InvokeAsync("odata_describe_type", ToolArguments.Of("name", "Customers"));
            described.IsError.Should().BeFalse(described.Text);
            described.Text.Should().Contain("Orders -> Order[]");

            using var client = CreateClient();
            using var twin = await client.GetAsync("/odata/Customers(1)/Orders");
            var result = await InvokeAsync(
                "odata_navigate",
                ToolArguments.Of("entitySet", "Customers", "key", "1", "navigation", "Orders"));

            if (twin.IsSuccessStatusCode)
            {
                result.IsError.Should().BeFalse(result.Text);
            }
            else
            {
                result.IsError.Should().BeTrue(result.Text);
                result.Text.Should().Contain($"status {(int)twin.StatusCode}");
            }
        }

        /// <summary>
        /// Querying the described set with a property from the card in <c>select</c> succeeds.
        /// </summary>
        [TestMethod]
        public async Task DescribeType_ThenQueryThatSet_UsesAPropertyFromTheCardInSelect()
        {
            var described = await InvokeAsync("odata_describe_type", ToolArguments.Of("name", "Customers"));
            described.IsError.Should().BeFalse(described.Text);
            described.Text.Should().Contain("CompanyName: string");

            var query = await InvokeAsync(
                "odata_query",
                ToolArguments.Of("entitySet", "Customers", "select", "CompanyName"));
            query.IsError.Should().BeFalse(query.Text);
            query.StructuredContent.Should().Contain("CompanyName");
            query.StructuredContent.Should().Contain("Contoso");
        }

        /// <summary>
        /// A Unicode name that is not in the model is not declared.
        /// </summary>
        [TestMethod]
        public async Task DescribeType_UnicodeNameNotInModel_IsError()
        {
            var result = await InvokeAsync("odata_describe_type", ToolArguments.Of("name", "客戶"));

            result.IsError.Should().BeTrue();
            result.Text.Should().Contain("not declared");
        }

        /// <summary>
        /// An unknown type is a catalog error with no OData HTTP and no status 404.
        /// </summary>
        [TestMethod]
        public async Task DescribeType_UnknownTypeGhost_IsErrorNotDeclared_NoHttp404()
        {
            var (runtime, capture) = CreateCapturingRuntime();
            var result = await runtime.InvokeAsync(
                "odata_describe_type",
                ToolArguments.Of("name", "Ghost"),
                CancellationToken.None);

            result.IsError.Should().BeTrue();
            result.Text.Should().Contain("Ghost");
            result.Text.Should().Contain("not declared");
            result.Text.Should().NotContain("status 404");
            capture.Requests.Should().BeEmpty();
        }

        /// <summary>
        /// Passing <c>entitySet</c> instead of <c>name</c> is a missing-name error.
        /// </summary>
        [TestMethod]
        public async Task DescribeType_UsesEntitySetKey_NotName_IsErrorMissingName()
        {
            var result = await InvokeAsync("odata_describe_type", ToolArguments.Of("entitySet", "Customers"));

            result.IsError.Should().BeTrue();
            result.Text.Should().Contain("name");
        }

        /// <summary>
        /// Passing <c>id</c> instead of <c>name</c> is an error.
        /// </summary>
        [TestMethod]
        public async Task DescribeType_UsesIdInsteadOfName_IsError()
        {
            var result = await InvokeAsync("odata_describe_type", ToolArguments.Of("id", "Customer"));

            result.IsError.Should().BeTrue();
            result.Text.Should().Contain("name");
        }

        /// <summary>
        /// A whitespace name is an error.
        /// </summary>
        [TestMethod]
        public async Task DescribeType_WhitespaceName_IsError()
        {
            var result = await InvokeAsync("odata_describe_type", ToolArguments.Of("name", "   "));

            result.IsError.Should().BeTrue();
            result.Text.Should().Contain("name");
        }

        /// <summary>
        /// Entity-set matching is case-insensitive.
        /// </summary>
        [TestMethod]
        public async Task DescribeType_WrongCase_PeopleVsPeople()
        {
            var result = await InvokeAsync("odata_describe_type", ToolArguments.Of("name", "customers", "format", "json"));

            result.IsError.Should().BeFalse(result.Text);
            ReadKeys(result.StructuredContent).Should().Contain("CustomerId");
        }

        #endregion

        #region Internal Methods

        /// <summary>
        /// Reads the <c>key</c> array from a compact type shape.
        /// </summary>
        /// <param name="json">The compact shape JSON (<c>format=json</c> or a type card).</param>
        /// <returns>
        /// Key property names.
        /// </returns>
        internal static IReadOnlyList<string> ReadKeys(string? json)
        {
            using var document = JsonDocument.Parse(json ?? throw new ArgumentNullException(nameof(json)));
            var shape = ReadShape(document.RootElement);
            if (!shape.TryGetProperty("key", out var array) || array.ValueKind != JsonValueKind.Array)
            {
                return [];
            }

            return [.. array.EnumerateArray().Select(item => item.GetString()!)];
        }

        /// <summary>
        /// Reads navigation names from a compact type shape.
        /// </summary>
        /// <param name="json">The compact shape JSON.</param>
        /// <returns>
        /// Navigation names.
        /// </returns>
        internal static IReadOnlyList<string> ReadNavigationNames(string? json)
        {
            return ReadSectionKeys(json, "navs");
        }

        /// <summary>
        /// Reads property names from a compact type shape.
        /// </summary>
        /// <param name="json">The compact shape JSON.</param>
        /// <returns>
        /// Property names.
        /// </returns>
        internal static IReadOnlyList<string> ReadPropertyNames(string? json)
        {
            return ReadSectionKeys(json, "props");
        }

        /// <summary>
        /// Reads the keys of a named object section (<c>props</c>, <c>navs</c>, <c>ops</c>) from a compact type shape.
        /// </summary>
        /// <param name="json">The compact shape JSON.</param>
        /// <param name="section">The section name.</param>
        /// <returns>
        /// The member names in declaration order.
        /// </returns>
        internal static IReadOnlyList<string> ReadSectionKeys(string? json, string section)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(json);
            ArgumentException.ThrowIfNullOrWhiteSpace(section);

            using var document = JsonDocument.Parse(json);
            var shape = ReadShape(document.RootElement);
            if (!shape.TryGetProperty(section, out var members) || members.ValueKind != JsonValueKind.Object)
            {
                return [];
            }

            return [.. members.EnumerateObject().Select(member => member.Name)];
        }

        /// <summary>
        /// Unwraps the single type object under the compact shape's root.
        /// </summary>
        /// <param name="root">The root element.</param>
        /// <returns>
        /// The type object.
        /// </returns>
        internal static JsonElement ReadShape(JsonElement root)
        {
            root.ValueKind.Should().Be(JsonValueKind.Object);

            return root.EnumerateObject().Should().ContainSingle().Subject.Value;
        }

        #endregion

    }

    /// <summary>
    /// Describes a type when the response size guard is tiny.
    /// </summary>
    [TestClass]
    public class DescribeTypeTinyResponseHostTests : ConventionRichHost
    {

        #region Public Methods

        /// <summary>
        /// A tiny <c>MaxResponseBytes</c> rejects the type card.
        /// </summary>
        [TestMethod]
        public async Task DescribeType_MaxResponseBytesTiny_IsError()
        {
            var result = await InvokeAsync("odata_describe_type", ToolArguments.Of("name", "Customers"));

            result.IsError.Should().BeTrue();
            result.Text.Should().Contain("select").And.Contain("top");
        }

        #endregion

        #region Internal Methods

        /// <inheritdoc />
        internal override void ConfigureServices(IServiceCollection services)
        {
            services
                .AddControllers()
                .AddApplicationPart(typeof(CustomersController).Assembly)
                .AddOData(options =>
                {
                    options.EnableQueryFeatures();
                    options.AddRouteComponents("odata", TestModels.GetRichModel());
                });
            services.AddODataMcp(options =>
            {
                options.Catalog.MaxResponseBytes = 10;
            });
        }

        #endregion

    }

    /// <summary>
    /// Describes a set on a 200-entity-set model.
    /// </summary>
    [TestClass]
    public class DescribeTypeWideModelHostTests : ConventionRichHost
    {

        #region Public Methods

        /// <summary>
        /// Describing <c>Rows000</c> succeeds.
        /// </summary>
        [TestMethod]
        public async Task DescribeType_WideModel_DescribeRows000_Succeeds()
        {
            var result = await InvokeAsync("odata_describe_type", ToolArguments.Of("name", "Rows000", "format", "json"));

            result.IsError.Should().BeFalse(result.Text);
            OdataDescribeTypeHostTests.ReadKeys(result.StructuredContent).Should().Contain("Id");
        }

        #endregion

        #region Internal Methods

        /// <inheritdoc />
        internal override void ConfigureServices(IServiceCollection services)
        {
            services
                .AddControllers()
                .AddOData(options =>
                {
                    options.AddRouteComponents("odata", TestModels.GetWideModel(200));
                });
            services.AddODataMcp(options =>
            {
                options.Catalog.MaxNamedTools = 150;
                options.Catalog.MaxResources = 50;
            });
        }

        #endregion

    }

}
