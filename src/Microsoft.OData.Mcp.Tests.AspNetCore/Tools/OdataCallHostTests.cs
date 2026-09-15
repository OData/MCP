// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.OData.Mcp.Core.Catalog;
using Microsoft.OData.Mcp.Tests.AspNetCore.Fixtures;
using Microsoft.OData.Mcp.Tests.Shared;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.OData.Mcp.Tests.AspNetCore.Tools
{


    /// <summary>
    /// Convention OData 8 host tests for <c>odata_call</c>.
    /// </summary>
    [TestClass]
    public class OdataCallHostTests : ConventionRichHost
    {

        #region Public Methods

        /// <summary>
        /// Functions are called with GET, never POST.
        /// </summary>
        [TestMethod]
        public async Task OdataCall_405PostOnFunctionOrGetOnAction()
        {
            var (runtime, capture) = CreateCapturingRuntime();
            var function = await runtime.InvokeAsync("odata_call", ToolArguments.Of("name", "MostValuable"), CancellationToken.None);
            function.IsError.Should().BeFalse(function.Text);
            capture.Last!.Method.Should().Be(HttpMethod.Get);
            var action = await runtime.InvokeAsync("odata_call", ToolArguments.Of("name", "Reset"), CancellationToken.None);
            action.IsError.Should().BeFalse(action.Text);
            capture.Last!.Method.Should().Be(HttpMethod.Post);
        }

        /// <summary>
        /// A stringified parameters value is rejected before HTTP; an object (even empty) POSTs JSON to Reset.
        /// </summary>
        [TestMethod]
        public async Task OdataCall_ActionParametersObjectNotString()
        {
            var (runtime, capture) = CreateCapturingRuntime();
            var asString = await runtime.InvokeAsync("odata_call", ToolArguments.Of("name", "Reset", "parameters", "{}"), CancellationToken.None);
            asString.IsError.Should().BeTrue();
            asString.Text.Should().Be("parameters must be a JSON object, not a string.");
            capture.Requests.Should().BeEmpty();

            var asObject = await runtime.InvokeAsync("odata_call", ToolArguments.Of("name", "Reset", "parameters", new { }), CancellationToken.None);
            asObject.IsError.Should().BeFalse(asObject.Text);
            capture.Last!.Method.Should().Be(HttpMethod.Post);
            capture.Last.JsonBody.Should().Be("{}");
        }

        /// <summary>
        /// After listing operations, every unbound function is called once.
        /// </summary>
        [TestMethod]
        public async Task OdataCall_AfterListOperations_EveryUnboundFunctionOnce_OData8OpsOnly()
        {
            var listed = await InvokeAsync("odata_list_operations");
            listed.IsError.Should().BeFalse(listed.Text);
            using var document = JsonDocument.Parse(listed.StructuredContent!);
            var called = 0;
            foreach (var operation in document.RootElement.GetProperty("operations").EnumerateObject())
            {
                if (operation.Value.GetString()!.Contains("// writes", StringComparison.Ordinal))
                {
                    continue;
                }

                var name = operation.Name;
                var arguments = name == "GetStatus"
                    ? ToolArguments.Of("name", name, "parameters", new { code = "open" })
                    : ToolArguments.Of("name", name);
                var result = await InvokeAsync("odata_call", arguments);
                result.IsError.Should().BeFalse(result.Text);
                called++;
            }

            called.Should().BeGreaterThan(0, "the rich model declares unbound functions");
        }

        /// <summary>
        /// Bound operations, when a type lists any, require entitySet and key; they are read from describe_type, not list_operations.
        /// </summary>
        [TestMethod]
        public async Task OdataCall_BoundOps_IfPresent()
        {
            var described = await InvokeAsync("odata_describe_type", ToolArguments.Of("name", "Customers", "format", "json"));
            described.IsError.Should().BeFalse(described.Text);
            using var document = JsonDocument.Parse(described.StructuredContent!);
            var shape = document.RootElement.EnumerateObject().Single().Value;
            if (!shape.TryGetProperty("ops", out var ops))
            {
                return;
            }

            var name = ops.EnumerateObject().First().Name;
            var missingSet = await InvokeAsync("odata_call", ToolArguments.Of("name", name));
            missingSet.IsError.Should().BeTrue(missingSet.Text);
            missingSet.Text.Should().Contain("entitySet");
        }

        /// <summary>
        /// A <c>body</c> argument is not part of the contract and is rejected before HTTP.
        /// </summary>
        [TestMethod]
        public async Task OdataCall_BodyArgument_IsErrorNoHttp()
        {
            var (runtime, capture) = CreateCapturingRuntime();
            var result = await runtime.InvokeAsync("odata_call", ToolArguments.Of("name", "Reset", "body", ""), CancellationToken.None);

            result.IsError.Should().BeTrue();
            result.Text.Should().Be("Unexpected argument 'body'. Put operation arguments in parameters as a JSON object.");
            capture.Last.Should().BeNull();
        }

        /// <summary>
        /// A malformed stringified parameters value never reaches OData.
        /// </summary>
        [TestMethod]
        public async Task OdataCall_ParametersMalformedString_IsError()
        {
            var result = await InvokeAsync("odata_call", ToolArguments.Of("name", "Reset", "parameters", "{"));

            result.IsError.Should().BeTrue();
            result.Text.Should().Be("parameters must be a JSON object, not a string.");
        }

        /// <summary>
        /// The operation name is matched case-insensitively.
        /// </summary>
        [TestMethod]
        public async Task OdataCall_CaseInsensitiveName_mostvaluable()
        {
            var (result, capture) = await CallCapturedAsync("name", "mostvaluable");
            capture.Last!.Method.Should().Be(HttpMethod.Get);
            capture.Last.RelativePath.Should().Be("MostValuable");
            result.IsError.Should().BeFalse(result.Text);
            result.StructuredContent.Should().Contain("42");
        }

        /// <summary>
        /// The call path is the operation, not an entity-set query.
        /// </summary>
        [TestMethod]
        public async Task OdataCall_DoesNotUseOdataQueryPath()
        {
            var (result, capture) = await CallCapturedAsync("name", "MostValuable");
            result.IsError.Should().BeFalse(result.Text);
            capture.Last!.RelativePath.Should().Be("MostValuable");
            capture.Last.RelativePath.Should().NotBe("Customers");
        }

        /// <summary>
        /// <c>$name</c> is ignored, so the required <c>name</c> is missing.
        /// </summary>
        [TestMethod]
        public async Task OdataCall_DollarName_MissingName()
        {
            var (result, capture) = await CallCapturedAsync("$name", "MostValuable");
            result.IsError.Should().BeTrue(result.Text);
            result.Text.Should().Contain("name");
            capture.Requests.Should().BeEmpty();
        }

        /// <summary>
        /// An emoji operation name is not declared.
        /// </summary>
        [TestMethod]
        public async Task OdataCall_EmojiName_NotDeclared()
        {
            var (result, capture) = await CallCapturedAsync("name", "😀");
            result.IsError.Should().BeTrue(result.Text);
            result.Text.Should().Contain("Operation '😀' is not declared in the model.");
            capture.Requests.Should().BeEmpty();
        }

        /// <summary>
        /// An empty name is rejected.
        /// </summary>
        [TestMethod]
        public async Task OdataCall_EmptyName()
        {
            var (result, capture) = await CallCapturedAsync("name", "");
            result.IsError.Should().BeTrue(result.Text);
            result.Text.Should().Contain("name");
            capture.Requests.Should().BeEmpty();
        }

        /// <summary>
        /// Unknown function parameters are an error before HTTP, never scraped into the path.
        /// </summary>
        [TestMethod]
        public async Task OdataCall_ExtraUnknownParamOnFunction_IsErrorNoHttp()
        {
            var (result, capture) = await CallCapturedAsync("name", "MostValuable", "parameters", new { foo = "bar" });
            result.IsError.Should().BeTrue();
            result.Text.Should().Be("Unknown parameter 'foo'. Declared: none.");
            capture.Requests.Should().BeEmpty();
        }

        /// <summary>
        /// <c>function</c> is not accepted in place of <c>name</c>.
        /// </summary>
        [TestMethod]
        public async Task OdataCall_FunctionInsteadOfName()
        {
            var result = await InvokeAsync("odata_call", ToolArguments.Of("function", "MostValuable"));
            result.IsError.Should().BeTrue(result.Text);
            result.Text.Should().Contain("name");
        }

        /// <summary>
        /// <c>id</c> is not accepted in place of <c>name</c>.
        /// </summary>
        [TestMethod]
        public async Task OdataCall_IdInsteadOfName()
        {
            var result = await InvokeAsync("odata_call", ToolArguments.Of("id", "MostValuable"));
            result.IsError.Should().BeTrue(result.Text);
            result.Text.Should().Contain("name");
        }

        /// <summary>
        /// Malformed JSON-RPC is a protocol error.
        /// </summary>
        [TestMethod]
        public async Task OdataCall_JsonRpc_Malformed()
        {
            using var client = CreateClient();
            using var content = McpJsonRpc.Content("{");
            using var response = await client.PostAsync("/odata/mcp", content);
            var body = await McpJsonRpc.ReadBodyAsync(response);
            response.StatusCode.Should().NotBe(HttpStatusCode.NotFound, body);
            response.IsSuccessStatusCode.Should().BeFalse(body);
        }

        /// <summary>
        /// Wrong-content-type JSON-RPC is not a successful call.
        /// </summary>
        [TestMethod]
        public async Task OdataCall_JsonRpc_WrongContentType()
        {
            using var client = CreateClient();
            using var content = new StringContent("""{"jsonrpc":"2.0","id":"1","method":"tools/call","params":{"name":"odata_call","arguments":{"name":"MostValuable"}}}""", Encoding.UTF8, "text/plain");
            using var response = await client.PostAsync("/odata/mcp", content);
            var body = await McpJsonRpc.ReadBodyAsync(response);
            response.StatusCode.Should().NotBe(HttpStatusCode.NotFound, body);
        }

        /// <summary>
        /// JSON-RPC <c>tools/call</c> for MostValuable returns 42.
        /// </summary>
        [TestMethod]
        public async Task OdataCall_JsonRpcToolsCall_MostValuable()
        {
            using var client = CreateClient();
            using var response = await McpJsonRpc.CallToolAsync(client, "/odata/mcp", "odata_call", """{"name":"MostValuable"}""");
            var body = await McpJsonRpc.ReadBodyAsync(response);
            response.IsSuccessStatusCode.Should().BeTrue(body);
            body.Should().Contain("42");
            body.Should().NotContain("\"isError\":true");
        }

        /// <summary>
        /// An oversized undeclared argument is rejected with no HTTP; Reset declares no parameters.
        /// </summary>
        [TestMethod]
        public async Task OdataCall_HugeUnknownParameterOnAction_NoHttp()
        {
            var (result, capture) = await CallCapturedAsync("name", "Reset", "parameters", new { payload = new string('a', 262_145) });
            result.IsError.Should().BeTrue(result.Text);
            result.Text.Should().Be("Unknown parameter 'payload'. Declared: none.");
            capture.Requests.Should().BeEmpty();
        }

        /// <summary>
        /// GetStatus without <c>code</c> omits the parameter from the path.
        /// </summary>
        [TestMethod]
        public async Task OdataCall_MissingFunctionParam_OData400()
        {
            using var client = CreateClient();
            using var twin = await client.GetAsync("/odata/GetStatus");
            var (result, capture) = await CallCapturedAsync("name", "GetStatus");
            capture.Last!.Method.Should().Be(HttpMethod.Get);
            capture.Last.RelativePath.Should().Be("GetStatus");
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
        /// Missing <c>name</c> is an error.
        /// </summary>
        [TestMethod]
        public async Task OdataCall_MissingName_IsError()
        {
            var result = await InvokeAsync("odata_call", ToolArguments.Of());
            result.IsError.Should().BeTrue(result.Text);
            result.Text.Should().Contain("name");
        }

        /// <summary>
        /// A numeric name is looked up as the raw text and is not declared.
        /// </summary>
        [TestMethod]
        public async Task OdataCall_NameAsNumber()
        {
            var (result, capture) = await CallCapturedAsync("name", 1);
            result.IsError.Should().BeTrue(result.Text);
            result.Text.Should().Contain("Operation '1' is not declared in the model.");
            capture.Requests.Should().BeEmpty();
        }

        /// <summary>
        /// Reset is POST /odata/Reset.
        /// </summary>
        [TestMethod]
        public async Task OdataCall_OData8_UnboundActionReset_Post()
        {
            using var client = CreateClient();
            using var twin = await client.PostAsync("/odata/Reset", new StringContent("{}", Encoding.UTF8, "application/json"));
            var twinBody = await twin.Content.ReadAsStringAsync();
            twin.IsSuccessStatusCode.Should().BeTrue(twinBody);
            var (runtime, capture) = CreateCapturingRuntime();
            var result = await runtime.InvokeAsync("odata_call", ToolArguments.Of("name", "Reset"), CancellationToken.None);
            result.IsError.Should().BeFalse(result.Text);
            capture.Last!.Method.Should().Be(HttpMethod.Post);
            capture.Last.RelativePath.Should().Be("Reset");
            capture.Last.JsonBody.Should().Be("{}");
        }

        /// <summary>
        /// GetStatus with code produces a quoted string parameter on the path.
        /// </summary>
        [TestMethod]
        public async Task OdataCall_OData8_UnboundFunctionGetStatusWithCode_PathContainsCode()
        {
            var (result, capture) = await CallCapturedAsync("name", "GetStatus", "parameters", new { code = "open" });
            capture.Last!.Method.Should().Be(HttpMethod.Get);
            capture.Last.RelativePath.Should().Be("GetStatus(code='open')");
            using var client = CreateClient();
            using var twin = await client.GetAsync("/odata/" + capture.Last.RelativePath);
            var twinBody = await twin.Content.ReadAsStringAsync();
            if (twin.IsSuccessStatusCode)
            {
                result.IsError.Should().BeFalse(result.Text);
                result.StructuredContent.Should().Contain("open");
                twinBody.Should().Contain("open");
            }
            else
            {
                result.IsError.Should().BeTrue(result.Text);
            }
        }

        /// <summary>
        /// MostValuable is GET /odata/MostValuable with the same int payload.
        /// </summary>
        [TestMethod]
        public async Task OdataCall_OData8_UnboundFunctionMostValuable_GetMatchesHttp()
        {
            using var client = CreateClient();
            using var twin = await client.GetAsync("/odata/MostValuable");
            var twinBody = await twin.Content.ReadAsStringAsync();
            twin.IsSuccessStatusCode.Should().BeTrue(twinBody);
            twinBody.Should().Contain("42");
            var (result, capture) = await CallCapturedAsync("name", "MostValuable");
            result.IsError.Should().BeFalse(result.Text);
            result.StructuredContent.Should().Contain("42");
            capture.Last!.Method.Should().Be(HttpMethod.Get);
            capture.Last.RelativePath.Should().Be("MostValuable");
            capture.Last.JsonBody.Should().BeNull();
        }

        /// <summary>
        /// <c>operation</c> is not accepted in place of <c>name</c>.
        /// </summary>
        [TestMethod]
        public async Task OdataCall_OperationInsteadOfName_IsErrorMissingName()
        {
            var result = await InvokeAsync("odata_call", ToolArguments.Of("operation", "MostValuable"));
            result.IsError.Should().BeTrue(result.Text);
            result.Text.Should().Contain("name");
        }

        /// <summary>
        /// String function parameters are FormatKey-quoted.
        /// </summary>
        [TestMethod]
        public async Task OdataCall_StringParamUnquotedVsQuoted_FormatKeyQuotesStrings()
        {
            var (_, capture) = await CallCapturedAsync("name", "GetStatus", "parameters", new { code = "open" });
            capture.Last!.RelativePath.Should().Contain("code='open'");
            capture.Last.RelativePath.Should().NotContain("code=open)");
        }

        /// <summary>
        /// A Unicode operation name that is not declared does not hit HTTP.
        /// </summary>
        [TestMethod]
        public async Task OdataCall_UnicodeOperationName_NotDeclaredError()
        {
            var (result, capture) = await CallCapturedAsync("name", "最有價值");
            result.IsError.Should().BeTrue(result.Text);
            result.Text.Should().Contain("is not declared in the model.");
            capture.Requests.Should().BeEmpty();
        }

        /// <summary>
        /// Unbound MostValuable ignores entitySet and key for the path.
        /// </summary>
        [TestMethod]
        public async Task OdataCall_UnboundWithEntitySetAndKey_IsError()
        {
            var (result, capture) = await CallCapturedAsync("name", "MostValuable", "entitySet", "Customers", "key", "1");
            result.IsError.Should().BeTrue();
            result.Text.Should().Be("MostValuable is unbound. Omit entitySet and key.");
            capture.Requests.Should().BeEmpty();
        }

        /// <summary>
        /// Unknown operation Ghost is <c>IsError</c> with no HTTP.
        /// </summary>
        [TestMethod]
        public async Task OdataCall_UnknownOperationGhost_IsErrorNotDeclared_NoHttp()
        {
            var (result, capture) = await CallCapturedAsync("name", "Ghost");
            result.IsError.Should().BeTrue(result.Text);
            result.Text.Should().Be("Operation 'Ghost' is not declared in the model.");
            capture.Requests.Should().BeEmpty();
        }

        /// <summary>
        /// Get-style arguments on an unbound call are rejected: unbound means no entitySet and no key.
        /// </summary>
        [TestMethod]
        public async Task OdataCall_UsingGetArgs_OnUnbound_IsError()
        {
            var (result, capture) = await CallCapturedAsync("name", "MostValuable", "entitySet", "Customers", "key", "1");
            result.IsError.Should().BeTrue();
            result.Text.Should().Contain("is unbound");
            capture.Requests.Should().BeEmpty();
        }

        /// <summary>
        /// Query <c>filter</c> is not a call argument; a stray top-level field is an error before HTTP.
        /// </summary>
        [TestMethod]
        public async Task OdataCall_UsingQueryArgs_FilterTopLevel_IsError()
        {
            var (result, capture) = await CallCapturedAsync("name", "MostValuable", "filter", "true");
            result.IsError.Should().BeTrue();
            result.Text.Should().Be("Unexpected argument 'filter'. Put operation arguments in parameters as a JSON object.");
            capture.Requests.Should().BeEmpty();
        }

        #endregion

        #region Internal Methods

        /// <summary>
        /// Invokes <c>odata_call</c> through a capturing executor.
        /// </summary>
        /// <param name="pairs">Alternating names and values.</param>
        /// <returns>
        /// The tool result and capture wrapper.
        /// </returns>
        internal async Task<(ODataToolInvocationResult Result, CapturingODataExecutor Capture)> CallCapturedAsync(params object?[] pairs)
        {
            var (runtime, capture) = CreateCapturingRuntime();
            var result = await runtime.InvokeAsync("odata_call", ToolArguments.Of(pairs), CancellationToken.None);

            return (result, capture);
        }

        #endregion

    }

}
