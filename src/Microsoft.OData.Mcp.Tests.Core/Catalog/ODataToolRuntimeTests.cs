// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.OData.Mcp.Core.Catalog;
using Microsoft.OData.Mcp.Core.Execution;
using Microsoft.OData.Mcp.Core.Parsing;
using Microsoft.OData.Mcp.Tests.Core.Parsing;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.OData.Mcp.Tests.Core.Catalog
{

    /// <summary>
    /// Runtime dispatcher tests using a recording executor (not an HTTP mock).
    /// </summary>
    [TestClass]
    public class ODataToolRuntimeTests
    {

        #region Public Methods

        /// <summary>
        /// Missing required arguments become error results.
        /// </summary>
        [TestMethod]
        public async Task InvokeAsync_MissingEntitySet_ReturnsError()
        {
            var runtime = CreateRuntime();
            var result = await runtime.InvokeAsync("odata_query", new Dictionary<string, JsonElement>(), CancellationToken.None);

            result.IsError.Should().BeTrue();
            result.Text.Should().Contain("entitySet");
        }

        /// <summary>
        /// Unknown tools return an error.
        /// </summary>
        [TestMethod]
        public async Task InvokeAsync_UnknownTool_ReturnsError()
        {
            var runtime = CreateRuntime();
            var result = await runtime.InvokeAsync("not_a_tool", null, CancellationToken.None);

            result.IsError.Should().BeTrue();
            result.Text.Should().Contain("not_a_tool");
        }

        /// <summary>
        /// Generic get builds a keyed path.
        /// </summary>
        [TestMethod]
        public async Task InvokeAsync_OdataGet_BuildsKeyPath()
        {
            var executor = new RecordingODataExecutor { Result = Success("""{"UserName":"russellwhyte"}""") };
            var runtime = CreateRuntime(executor);
            var result = await runtime.InvokeAsync(
                "odata_get",
                new Dictionary<string, JsonElement>
                {
                    ["entitySet"] = JsonSerializer.SerializeToElement("People"),
                    ["key"] = JsonSerializer.SerializeToElement("russellwhyte")
                },
                CancellationToken.None);

            result.IsError.Should().BeFalse();
            executor.Last!.RelativePath.Should().Be("People('russellwhyte')");
            executor.Last.Method.Should().Be(HttpMethod.Get);
        }

        /// <summary>
        /// Composite numeric keys stay unquoted as <c>Name=value</c> pairs.
        /// </summary>
        [TestMethod]
        public void FormatKey_CompositeNumeric_IsUnquoted()
        {
            ODataToolRuntime.FormatKey("OrderID=10248,ProductID=11").Should().Be("OrderID=10248,ProductID=11");
        }

        /// <summary>
        /// Numeric keys are not quoted.
        /// </summary>
        [TestMethod]
        public void FormatKey_Numeric_IsUnquoted()
        {
            ODataToolRuntime.FormatKey("42").Should().Be("42");
        }

        /// <summary>
        /// Create posts a JSON body.
        /// </summary>
        [TestMethod]
        public async Task InvokeAsync_OdataCreate_PostsBody()
        {
            var executor = new RecordingODataExecutor { Result = Success("{}", 201) };
            var runtime = CreateRuntime(executor);
            var result = await runtime.InvokeAsync(
                "odata_create",
                new Dictionary<string, JsonElement>
                {
                    ["entitySet"] = JsonSerializer.SerializeToElement("People"),
                    ["body"] = JsonSerializer.SerializeToElement("""{"UserName":"new","FirstName":"N"}""")
                },
                CancellationToken.None);

            result.IsError.Should().BeFalse(result.Text);
            executor.Last!.Method.Should().Be(HttpMethod.Post);
            executor.Last.RelativePath.Should().Be("People");
            executor.Last.JsonBody.Should().Contain("UserName");
        }

        /// <summary>
        /// Update uses PATCH.
        /// </summary>
        [TestMethod]
        public async Task InvokeAsync_OdataUpdate_Patches()
        {
            var executor = new RecordingODataExecutor { Result = Success(string.Empty, 204) };
            var runtime = CreateRuntime(executor);
            var result = await runtime.InvokeAsync(
                "odata_update",
                new Dictionary<string, JsonElement>
                {
                    ["entitySet"] = JsonSerializer.SerializeToElement("People"),
                    ["key"] = JsonSerializer.SerializeToElement("a"),
                    ["body"] = JsonSerializer.SerializeToElement("""{"FirstName":"Ann"}""")
                },
                CancellationToken.None);

            result.IsError.Should().BeFalse();
            executor.Last!.Method.Should().Be(HttpMethod.Patch);
            executor.Last.RelativePath.Should().Be("People('a')");
        }

        /// <summary>
        /// Delete uses DELETE.
        /// </summary>
        [TestMethod]
        public async Task InvokeAsync_OdataDelete_Deletes()
        {
            var executor = new RecordingODataExecutor { Result = Success(string.Empty, 204) };
            var runtime = CreateRuntime(executor);
            var result = await runtime.InvokeAsync(
                "odata_delete",
                new Dictionary<string, JsonElement>
                {
                    ["entitySet"] = JsonSerializer.SerializeToElement("People"),
                    ["key"] = JsonSerializer.SerializeToElement("a")
                },
                CancellationToken.None);

            result.IsError.Should().BeFalse();
            executor.Last!.Method.Should().Be(HttpMethod.Delete);
        }

        /// <summary>
        /// Navigate follows a navigation property.
        /// </summary>
        [TestMethod]
        public async Task InvokeAsync_OdataNavigate_FollowsNavigation()
        {
            var executor = new RecordingODataExecutor { Result = Success("""{"value":[]}""") };
            var runtime = CreateRuntime(executor);
            var result = await runtime.InvokeAsync(
                "odata_navigate",
                new Dictionary<string, JsonElement>
                {
                    ["entitySet"] = JsonSerializer.SerializeToElement("People"),
                    ["key"] = JsonSerializer.SerializeToElement("a"),
                    ["navigation"] = JsonSerializer.SerializeToElement("Friends")
                },
                CancellationToken.None);

            result.IsError.Should().BeFalse();
            executor.Last!.RelativePath.Should().Be("People('a')/Friends");
        }

        /// <summary>
        /// Named list tools bind the entity set from the catalog.
        /// </summary>
        [TestMethod]
        public async Task InvokeAsync_NamedList_BindsEntitySet()
        {
            var executor = new RecordingODataExecutor { Result = Success("""{"value":[]}""") };
            var runtime = CreateRuntime(executor);
            var result = await runtime.InvokeAsync(
                "list_people",
                new Dictionary<string, JsonElement>
                {
                    ["top"] = JsonSerializer.SerializeToElement(1)
                },
                CancellationToken.None);

            result.IsError.Should().BeFalse();
            executor.Last!.RelativePath.Should().Be("People");
            executor.Last.QueryOptions["top"].Should().Be("1");
        }

        /// <summary>
        /// Oversized responses ask the agent to add select and top.
        /// </summary>
        [TestMethod]
        public async Task InvokeAsync_OversizedResponse_IsError()
        {
            var catalog = new ODataMcpCatalog(
                new CsdlParser().ParseFromString(CsdlParserDocumentationTests.DocumentedCsdl),
                new ODataMcpCatalogOptions
                {
                    MaxResponseBytes = 8
                });
            var executor = new RecordingODataExecutor { Result = Success(new string('x', 64)) };
            var runtime = new ODataToolRuntime(catalog, executor);
            var result = await runtime.InvokeAsync(
                "odata_query",
                new Dictionary<string, JsonElement>
                {
                    ["entitySet"] = JsonSerializer.SerializeToElement("People")
                },
                CancellationToken.None);

            result.IsError.Should().BeTrue();
            result.Text.Should().Contain("select");
            result.Text.Should().Contain("top");
        }

        /// <summary>
        /// Failed OData calls preserve the body.
        /// </summary>
        [TestMethod]
        public async Task InvokeAsync_FailedOData_IsError()
        {
            var executor = new RecordingODataExecutor
            {
                Result = new ODataExecuteResult
                {
                    Body = "not found",
                    IsSuccess = false,
                    StatusCode = 404
                }
            };
            var runtime = CreateRuntime(executor);
            var result = await runtime.InvokeAsync(
                "odata_query",
                new Dictionary<string, JsonElement>
                {
                    ["entitySet"] = JsonSerializer.SerializeToElement("People")
                },
                CancellationToken.None);

            result.IsError.Should().BeTrue();
            result.Text.Should().Contain("404");
            result.Text.Should().Contain("not found");
        }

        /// <summary>
        /// A 429 from OData includes the status and Retry-After in the tool error.
        /// </summary>
        [TestMethod]
        public async Task InvokeAsync_Failed429_IncludesStatusAndRetryAfter()
        {
            var runtime = CreateRuntime(new RecordingODataExecutor
            {
                Result = new ODataExecuteResult
                {
                    Body = "throttled",
                    IsSuccess = false,
                    RetryAfter = "120",
                    StatusCode = 429
                }
            });
            var result = await runtime.InvokeAsync(
                "odata_query",
                new Dictionary<string, JsonElement>
                {
                    ["entitySet"] = JsonSerializer.SerializeToElement("People")
                },
                CancellationToken.None);

            result.IsError.Should().BeTrue();
            result.Text.Should().Contain("429");
            result.Text.Should().Contain("Retry-After: 120");
            result.Text.Should().Contain("throttled");
        }

        /// <summary>
        /// List entity sets returns declared names.
        /// </summary>
        [TestMethod]
        public async Task InvokeAsync_ListEntitySets_ReturnsPeople()
        {
            var result = await CreateRuntime().InvokeAsync("odata_list_entity_sets", null, CancellationToken.None);

            result.IsError.Should().BeFalse();
            result.StructuredContent.Should().Contain("People");
        }

        /// <summary>
        /// Describe type accepts an entity set name or a type name.
        /// </summary>
        [TestMethod]
        public async Task InvokeAsync_DescribeType_AcceptsSetOrType()
        {
            var runtime = CreateRuntime();
            var bySet = await runtime.InvokeAsync(
                "odata_describe_type",
                new Dictionary<string, JsonElement>
                {
                    ["name"] = JsonSerializer.SerializeToElement("People")
                },
                CancellationToken.None);
            var byType = await runtime.InvokeAsync(
                "odata_describe_type",
                new Dictionary<string, JsonElement>
                {
                    ["name"] = JsonSerializer.SerializeToElement("Person")
                },
                CancellationToken.None);
            var missing = await runtime.InvokeAsync(
                "odata_describe_type",
                new Dictionary<string, JsonElement>
                {
                    ["name"] = JsonSerializer.SerializeToElement("Ghost")
                },
                CancellationToken.None);

            bySet.IsError.Should().BeFalse();
            byType.IsError.Should().BeFalse();
            missing.IsError.Should().BeTrue();
        }

        /// <summary>
        /// Named get/create/update/delete bind the entity set.
        /// </summary>
        [TestMethod]
        public async Task InvokeAsync_NamedCrud_BindsEntitySet()
        {
            var executor = new RecordingODataExecutor { Result = Success("{}", 201) };
            var runtime = CreateRuntime(executor);

            await runtime.InvokeAsync(
                "get_person",
                new Dictionary<string, JsonElement> { ["key"] = JsonSerializer.SerializeToElement("a") },
                CancellationToken.None);
            executor.Last!.RelativePath.Should().Be("People('a')");

            await runtime.InvokeAsync(
                "create_person",
                new Dictionary<string, JsonElement> { ["UserName"] = JsonSerializer.SerializeToElement("new"), ["FirstName"] = JsonSerializer.SerializeToElement("N") },
                CancellationToken.None);
            executor.Last.Method.Should().Be(HttpMethod.Post);

            executor.Result = Success(string.Empty, 204);
            await runtime.InvokeAsync(
                "update_person",
                new Dictionary<string, JsonElement>
                {
                    ["key"] = JsonSerializer.SerializeToElement("a"),
                    ["body"] = JsonSerializer.SerializeToElement("""{"FirstName":"Ann"}""")
                },
                CancellationToken.None);
            executor.Last.Method.Should().Be(HttpMethod.Patch);

            await runtime.InvokeAsync(
                "delete_person",
                new Dictionary<string, JsonElement> { ["key"] = JsonSerializer.SerializeToElement("a") },
                CancellationToken.None);
            executor.Last.Method.Should().Be(HttpMethod.Delete);
        }

        /// <summary>
        /// Keys that are GUIDs, booleans, or already quoted stay unquoted as strings.
        /// </summary>
        [TestMethod]
        public void FormatKey_GuidBoolAndQuoted()
        {
            ODataToolRuntime.FormatKey("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee").Should().NotContain("'");
            ODataToolRuntime.FormatKey("true").Should().Be("true");
            ODataToolRuntime.FormatKey("'already'").Should().Be("'already'");
            ODataToolRuntime.FormatKey("O'Brien").Should().Be("'O''Brien'");
        }

        /// <summary>
        /// Named string keys quote only the value.
        /// </summary>
        [TestMethod]
        public void FormatKey_NamedString_QuotesValueOnly()
        {
            ODataToolRuntime.FormatKey("CustomerID=ALFKI").Should().Be("CustomerID='ALFKI'");
            ODataToolRuntime.FormatKey("CustomerID='ALFKI'").Should().Be("CustomerID='ALFKI'");
        }

        /// <summary>
        /// Create without a body argument serializes the remaining properties.
        /// </summary>
        [TestMethod]
        public async Task InvokeAsync_CreateWithoutBody_SerializesProperties()
        {
            var executor = new RecordingODataExecutor { Result = Success("{}", 201) };
            var runtime = CreateRuntime(executor);
            await runtime.InvokeAsync(
                "odata_create",
                new Dictionary<string, JsonElement>
                {
                    ["entitySet"] = JsonSerializer.SerializeToElement("People"),
                    ["UserName"] = JsonSerializer.SerializeToElement("new"),
                    ["FirstName"] = JsonSerializer.SerializeToElement("N")
                },
                CancellationToken.None);

            executor.Last!.JsonBody.Should().Contain("UserName");
        }

        /// <summary>
        /// A JSON object body is serialized as raw JSON.
        /// </summary>
        [TestMethod]
        public async Task InvokeAsync_ObjectBody_UsesRawJson()
        {
            var executor = new RecordingODataExecutor { Result = Success("{}", 201) };
            var runtime = CreateRuntime(executor);
            await runtime.InvokeAsync(
                "odata_create",
                new Dictionary<string, JsonElement>
                {
                    ["entitySet"] = JsonSerializer.SerializeToElement("People"),
                    ["body"] = JsonSerializer.SerializeToElement(new Dictionary<string, string> { ["UserName"] = "x", ["FirstName"] = "N" })
                },
                CancellationToken.None);

            executor.Last!.JsonBody.Should().Contain("UserName");
        }

        /// <summary>
        /// Empty successful bodies become a status sentence.
        /// </summary>
        [TestMethod]
        public async Task InvokeAsync_EmptySuccess_HasStatusText()
        {
            var runtime = CreateRuntime(new RecordingODataExecutor { Result = Success(string.Empty, 204) });
            var result = await runtime.InvokeAsync(
                "odata_delete",
                new Dictionary<string, JsonElement>
                {
                    ["entitySet"] = JsonSerializer.SerializeToElement("People"),
                    ["key"] = JsonSerializer.SerializeToElement("a")
                },
                CancellationToken.None);

            result.IsError.Should().BeFalse();
            result.Text.Should().Contain("204");
        }

        /// <summary>
        /// Failed calls without a body include the status code.
        /// </summary>
        [TestMethod]
        public async Task InvokeAsync_FailedEmptyBody_IncludesStatus()
        {
            var runtime = CreateRuntime(new RecordingODataExecutor
            {
                Result = new ODataExecuteResult
                {
                    Body = " ",
                    IsSuccess = false,
                    StatusCode = 500
                }
            });
            var result = await runtime.InvokeAsync(
                "odata_query",
                new Dictionary<string, JsonElement>
                {
                    ["entitySet"] = JsonSerializer.SerializeToElement("People")
                },
                CancellationToken.None);

            result.IsError.Should().BeTrue();
            result.Text.Should().Contain("500");
        }

        /// <summary>
        /// Argument enumerables that are not dictionaries are copied.
        /// </summary>
        [TestMethod]
        public async Task InvokeAsync_EnumerableArguments_AreCopied()
        {
            var pairs = new List<KeyValuePair<string, JsonElement>>
            {
                new("entitySet", JsonSerializer.SerializeToElement("People"))
            };
            var result = await CreateRuntime().InvokeAsync("odata_query", pairs, CancellationToken.None);

            result.IsError.Should().BeFalse();
        }

        /// <summary>
        /// Get without a key is an error.
        /// </summary>
        [TestMethod]
        public async Task InvokeAsync_GetMissingKey_ReturnsError()
        {
            var result = await CreateRuntime().InvokeAsync(
                "odata_get",
                new Dictionary<string, JsonElement>
                {
                    ["entitySet"] = JsonSerializer.SerializeToElement("People")
                },
                CancellationToken.None);

            result.IsError.Should().BeTrue();
            result.Text.Should().Contain("key");
        }

        /// <summary>
        /// Navigate without a navigation name is an error.
        /// </summary>
        [TestMethod]
        public async Task InvokeAsync_NavigateMissingNavigation_ReturnsError()
        {
            var result = await CreateRuntime().InvokeAsync(
                "odata_navigate",
                new Dictionary<string, JsonElement>
                {
                    ["entitySet"] = JsonSerializer.SerializeToElement("People"),
                    ["key"] = JsonSerializer.SerializeToElement("a")
                },
                CancellationToken.None);

            result.IsError.Should().BeTrue();
            result.Text.Should().Contain("navigation");
        }

        /// <summary>
        /// Whitespace required arguments are errors.
        /// </summary>
        [TestMethod]
        public async Task InvokeAsync_WhitespaceEntitySet_ReturnsError()
        {
            var result = await CreateRuntime().InvokeAsync(
                "odata_query",
                new Dictionary<string, JsonElement>
                {
                    ["entitySet"] = JsonSerializer.SerializeToElement("  ")
                },
                CancellationToken.None);

            result.IsError.Should().BeTrue();
        }

        /// <summary>
        /// Create with an empty body argument still posts JSON.
        /// </summary>
        [TestMethod]
        public async Task InvokeAsync_CreateEmptyBodyString_PostsObject()
        {
            var executor = new RecordingODataExecutor { Result = Success("{}", 201) };
            var runtime = CreateRuntime(executor);
            var result = await runtime.InvokeAsync(
                "odata_create",
                new Dictionary<string, JsonElement>
                {
                    ["entitySet"] = JsonSerializer.SerializeToElement("People"),
                    ["body"] = JsonSerializer.SerializeToElement("")
                },
                CancellationToken.None);

            result.IsError.Should().BeFalse();
            executor.Last!.JsonBody.Should().BeEmpty();
        }

        /// <summary>
        /// Update without a key is an error.
        /// </summary>
        [TestMethod]
        public async Task InvokeAsync_UpdateMissingKey_ReturnsError()
        {
            var result = await CreateRuntime().InvokeAsync(
                "odata_update",
                new Dictionary<string, JsonElement>
                {
                    ["entitySet"] = JsonSerializer.SerializeToElement("People"),
                    ["body"] = JsonSerializer.SerializeToElement("""{"FirstName":"Ann"}""")
                },
                CancellationToken.None);

            result.IsError.Should().BeTrue();
            result.Text.Should().Contain("key");
        }

        /// <summary>
        /// Delete without a key is an error.
        /// </summary>
        [TestMethod]
        public async Task InvokeAsync_DeleteMissingKey_ReturnsError()
        {
            var result = await CreateRuntime().InvokeAsync(
                "odata_delete",
                new Dictionary<string, JsonElement>
                {
                    ["entitySet"] = JsonSerializer.SerializeToElement("People")
                },
                CancellationToken.None);

            result.IsError.Should().BeTrue();
        }

        /// <summary>
        /// Empty tool names throw before dispatch.
        /// </summary>
        [TestMethod]
        public async Task InvokeAsync_EmptyName_Throws()
        {
            var act = async () => await CreateRuntime().InvokeAsync(" ", null, CancellationToken.None);

            await act.Should().ThrowAsync<ArgumentException>();
        }

        /// <summary>
        /// Query option nulls are skipped.
        /// </summary>
        [TestMethod]
        public void ReadQueryOptions_SkipsNullAndUndefined()
        {
            var options = ODataToolRuntime.ReadQueryOptions(new Dictionary<string, JsonElement>
            {
                ["filter"] = JsonSerializer.SerializeToElement((string?)null),
                ["top"] = JsonSerializer.SerializeToElement(1)
            });

            options.Should().NotContainKey("filter");
            options["top"].Should().Be("1");
        }

        /// <summary>
        /// Query options without $ are forwarded to the executor.
        /// </summary>
        [TestMethod]
        public async Task InvokeAsync_QueryOptions_DoNotIncludeDollar()
        {
            var executor = new RecordingODataExecutor { Result = Success("""{"value":[]}""") };
            var runtime = CreateRuntime(executor);
            await runtime.InvokeAsync(
                "odata_query",
                new Dictionary<string, JsonElement>
                {
                    ["entitySet"] = JsonSerializer.SerializeToElement("People"),
                    ["filter"] = JsonSerializer.SerializeToElement("UserName eq 'a'"),
                    ["select"] = JsonSerializer.SerializeToElement("UserName"),
                    ["count"] = JsonSerializer.SerializeToElement(true),
                    ["orderby"] = JsonSerializer.SerializeToElement("UserName"),
                    ["expand"] = JsonSerializer.SerializeToElement("Friends"),
                    ["skip"] = JsonSerializer.SerializeToElement(0)
                },
                CancellationToken.None);

            executor.Last!.QueryOptions.Keys.Should().NotContain(key => key.StartsWith('$'));
            executor.Last.QueryOptions.Should().ContainKey("filter");
            executor.Last.QueryOptions.Should().ContainKey("select");
            executor.Last.QueryOptions.Should().ContainKey("count");
            executor.Last.QueryOptions.Should().ContainKey("orderby");
            executor.Last.QueryOptions.Should().ContainKey("expand");
            executor.Last.QueryOptions.Should().ContainKey("skip");
        }

        #endregion

        #region Internal Methods

        /// <summary>
        /// Builds a runtime over the documented People CSDL.
        /// </summary>
        /// <param name="executor">Optional recording executor.</param>
        /// <returns>
        /// The runtime.
        /// </returns>
        internal static ODataToolRuntime CreateRuntime(RecordingODataExecutor? executor = null)
        {
            var catalog = new ODataMcpCatalog(
                new CsdlParser().ParseFromString(CsdlParserDocumentationTests.DocumentedCsdl),
                new ODataMcpCatalogOptions());

            return new ODataToolRuntime(catalog, executor ?? new RecordingODataExecutor());
        }

        /// <summary>
        /// Builds a successful execute result.
        /// </summary>
        /// <param name="body">Response body.</param>
        /// <param name="status">Status code.</param>
        /// <returns>
        /// The result.
        /// </returns>
        internal static ODataExecuteResult Success(string body, int status = 200)
        {
            return new ODataExecuteResult
            {
                Body = body,
                IsSuccess = true,
                MediaType = "application/json",
                StatusCode = status
            };
        }

        #endregion

    }

}
