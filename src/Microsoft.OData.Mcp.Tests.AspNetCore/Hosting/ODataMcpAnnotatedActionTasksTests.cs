// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.OData.Mcp.AspNetCore.Hosting;
using Microsoft.OData.Mcp.Core.Vocabularies;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ModelContextProtocol.Extensions.Tasks;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace Microsoft.OData.Mcp.Tests.AspNetCore.Hosting
{

    /// <summary>
    /// URL elicitation helpers: named JSON properties, AUTH-12 secret skip, client opt-in.
    /// </summary>
    [TestClass]
    public class ODataMcpAnnotatedActionTasksTests
    {

        #region Public Methods

        /// <summary>
        /// Request <c>_meta</c> with the Tasks extension is an opt-in.
        /// </summary>
        [TestMethod]
        public void ClientOptedIntoTasks_RequestMeta_IsTrue()
        {
            var request = Context(new CallToolRequestParams
            {
                Name = "action_customer_authorize",
                Meta = new JsonObject
                {
                    ["io.modelcontextprotocol/clientCapabilities"] = new JsonObject
                    {
                        ["extensions"] = new JsonObject
                        {
                            [TasksProtocol.ExtensionId] = new JsonObject()
                        }
                    }
                }
            });

            ODataMcpAnnotatedActionTasks.ClientOptedIntoTasks(request).Should().BeTrue();
        }

        /// <summary>
        /// Missing Tasks extension on this request is not an opt-in. Initialize-time
        /// capabilities are ignored; Claude Code omits the extension on a <c>tools/call</c>
        /// that cannot become a task.
        /// </summary>
        [TestMethod]
        public void ClientOptedIntoTasks_MissingExtension_IsFalse()
        {
            var request = Context(new CallToolRequestParams { Name = "action_customer_authorize" });

            ODataMcpAnnotatedActionTasks.ClientOptedIntoTasks(request).Should().BeFalse();
            ODataMcpAnnotatedActionTasks.HasTasksExtension(request.Params?.Meta).Should().BeFalse();
        }

        /// <summary>
        /// URL elicitation without the Tasks extension is not a task opt-in.
        /// </summary>
        [TestMethod]
        public void ClientOptedIntoTasks_UrlElicitationOnly_IsFalse()
        {
            var request = Context(new CallToolRequestParams
            {
                Name = "action_customer_authorize",
                Meta = new JsonObject
                {
                    ["io.modelcontextprotocol/clientCapabilities"] = new JsonObject
                    {
                        ["elicitation"] = new JsonObject
                        {
                            ["url"] = new JsonObject()
                        }
                    }
                }
            });

            ODataMcpAnnotatedActionTasks.ClientOptedIntoTasks(request).Should().BeFalse();
            ODataMcpAnnotatedActionTasks.ClientHasUrlElicitation(request).Should().BeTrue();
        }

        /// <summary>
        /// Declared elicit names win over defaults; nested <c>value</c> is walked.
        /// </summary>
        [TestMethod]
        public void FindElicitUrl_DeclaredName_ReadsNestedValue()
        {
            var result = Result("""{"value":{"connectUri":"https://login.example.test/connect","message":"Open the URL."}}""");

            ODataMcpAnnotatedActionTasks.FindElicitUrl(result, ["connectUri"])
                .Should().Be("https://login.example.test/connect");
        }

        /// <summary>
        /// When the term is omitted, default names are elicited.
        /// </summary>
        [TestMethod]
        public void FindElicitUrl_DefaultNames_ReadsAuthorizationUrl()
        {
            var result = Result("""{"authorizationUrl":"https://login.example.test/authorize"}""");

            ODataMcpAnnotatedActionTasks.FindElicitUrl(result, declared: null)
                .Should().Be("https://login.example.test/authorize");
            McpVocabulary.DefaultElicitUrlProperties.Should().Contain("authorizationUrl");
        }

        /// <summary>
        /// javascript: and relative values are ignored.
        /// </summary>
        [TestMethod]
        public void FindElicitUrl_NonHttp_IsNull()
        {
            var javascript = Result("""{"connectUri":"javascript:alert(1)"}""");
            var relative = Result("""{"connectUri":"/connect"}""");

            ODataMcpAnnotatedActionTasks.FindElicitUrl(javascript, ["connectUri"]).Should().BeNull();
            ODataMcpAnnotatedActionTasks.FindElicitUrl(relative, ["connectUri"]).Should().BeNull();
        }

        /// <summary>
        /// AUTH-12: property names containing token/secret/password/code are never elicited.
        /// </summary>
        [TestMethod]
        public void FindElicitUrl_SecretNames_AreSkipped()
        {
            var result = Result("""{"connect_code":"https://login.example.test/code","accessToken":"https://login.example.test/token","connectUri":"https://login.example.test/connect"}""");

            ODataMcpAnnotatedActionTasks.FindElicitUrl(result, ["connect_code", "accessToken", "connectUri"])
                .Should().Be("https://login.example.test/connect");
            ODataMcpAnnotatedActionTasks.IsSecretPropertyName("connect_code").Should().BeTrue();
            ODataMcpAnnotatedActionTasks.IsSecretPropertyName("accessToken").Should().BeTrue();
            ODataMcpAnnotatedActionTasks.IsSecretPropertyName("connectUri").Should().BeFalse();
        }

        /// <summary>
        /// Ticket message is copied from the Started JSON.
        /// </summary>
        [TestMethod]
        public void ReadTicketMessage_ReadsMessage()
        {
            var result = Result("""{"status":"Started","message":"Open the URL to finish.","connectUri":"https://login.example.test/connect"}""");

            ODataMcpAnnotatedActionTasks.ReadTicketMessage(result).Should().Be("Open the URL to finish.");
        }

        /// <summary>
        /// A tasked action without the per-request Tasks extension stays synchronous so
        /// Claude Code receives <c>connectUri</c> instead of <c>resultType=task</c>.
        /// </summary>
        [TestMethod]
        public void SelectExecutionMode_MissingExtension_IsSynchronous()
        {
            var request = Context(new CallToolRequestParams { Name = "action_customer_authorize" });

            ODataMcpAnnotatedActionTasks.SelectExecutionMode(request)
                .Should().Be(McpTaskExecutionMode.Synchronous);
        }

        /// <summary>
        /// Tasks opt-in without a catalog action stays synchronous. CRUD and unannotated
        /// tools never wrap.
        /// </summary>
        [TestMethod]
        public void SelectExecutionMode_TasksMetaWithoutCatalog_IsSynchronous()
        {
            var request = Context(new CallToolRequestParams
            {
                Name = "action_customer_authorize",
                Meta = new JsonObject
                {
                    ["io.modelcontextprotocol/clientCapabilities"] = new JsonObject
                    {
                        ["extensions"] = new JsonObject
                        {
                            [TasksProtocol.ExtensionId] = new JsonObject()
                        }
                    }
                }
            });

            ODataMcpAnnotatedActionTasks.SelectExecutionMode(request)
                .Should().Be(McpTaskExecutionMode.Synchronous);
        }

        /// <summary>
        /// AfterInvoke leaves one-shot results alone when the client did not advertise URL elicitation.
        /// </summary>
        [TestMethod]
        public async Task AfterInvokeAsync_NoUrlElicitation_ReturnsOriginal()
        {
            var original = Result("""{"connectUri":"https://login.example.test/connect"}""");
            var request = Context(new CallToolRequestParams { Name = "action_customer_authorize" });

            var returned = await ODataMcpAnnotatedActionTasks.AfterInvokeAsync(request, original, default);

            returned.Should().BeSameAs(original);
        }

        /// <summary>
        /// Request <c>_meta</c> with <c>elicitation.url</c> is URL elicitation support.
        /// </summary>
        [TestMethod]
        public void ClientHasUrlElicitation_RequestMeta_IsTrue()
        {
            var request = Context(new CallToolRequestParams
            {
                Name = "action_customer_authorize",
                Meta = new JsonObject
                {
                    ["io.modelcontextprotocol/clientCapabilities"] = new JsonObject
                    {
                        ["elicitation"] = new JsonObject
                        {
                            ["url"] = new JsonObject()
                        }
                    }
                }
            });

            ODataMcpAnnotatedActionTasks.ClientHasUrlElicitation(request).Should().BeTrue();
        }

        #endregion

        #region Internal Methods

        /// <summary>
        /// Builds an uninitialized request context.
        /// </summary>
        /// <param name="parameters">Call parameters.</param>
        /// <returns>
        /// The context.
        /// </returns>
        internal static RequestContext<CallToolRequestParams> Context(CallToolRequestParams parameters)
        {
            var request = (RequestContext<CallToolRequestParams>)RuntimeHelpers.GetUninitializedObject(typeof(RequestContext<CallToolRequestParams>));
            request.Params = parameters;
            return request;
        }

        /// <summary>
        /// Builds a tool result whose text is JSON.
        /// </summary>
        /// <param name="json">Started JSON.</param>
        /// <returns>
        /// The result.
        /// </returns>
        internal static CallToolResult Result(string json)
        {
            using var document = JsonDocument.Parse(json);
            return new CallToolResult
            {
                Content = [new TextContentBlock { Text = json }],
                StructuredContent = document.RootElement.Clone()
            };
        }

        #endregion

    }

}
