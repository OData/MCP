// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OData.Mcp.AspNetCore.Hosting;
using Microsoft.OData.Mcp.Core.Catalog;
using Microsoft.OData.Mcp.Core.Parsing;
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
        /// A form-elicited action stays on <c>tools/call</c> even when this request advertised Tasks.
        /// </summary>
        [TestMethod]
        public void SelectExecutionMode_FormActionWithTasks_IsSynchronous()
        {
            var request = FormSessionRequest(
                new CallToolRequestParams
                {
                    Name = "action_ship",
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

        /// <summary>
        /// Before-invoke stashes the call on <see cref="McpHttpContextAccessor"/>.
        /// </summary>
        [TestMethod]
        public async Task BeforeInvokeAsync_StashesCallTool()
        {
            var accessor = new McpHttpContextAccessor
            {
                HttpContext = new DefaultHttpContext()
            };
            var request = Context(new CallToolRequestParams { Name = "action_ship" });
            var services = new ServiceCollection();
            services.AddSingleton(accessor);
            request.Services = services.BuildServiceProvider();

            var result = await ODataMcpAnnotatedActionTasks.BeforeInvokeAsync(request, CancellationToken.None);

            result.Should().BeNull();
            accessor.CallTool.Should().BeSameAs(request);
        }

        /// <summary>
        /// TryGetElicitFormAction without services is false.
        /// </summary>
        [TestMethod]
        public void TryGetElicitFormAction_NoServices_IsFalse()
        {
            var request = Context(new CallToolRequestParams { Name = "action_ship" });
            ODataMcpAnnotatedActionTasks.TryGetElicitFormAction(request, out var action).Should().BeFalse();
            action.Should().BeNull();
        }

        /// <summary>
        /// TryGetElicitFormAction without a session factory is false.
        /// </summary>
        [TestMethod]
        public void TryGetElicitFormAction_NoFactory_IsFalse()
        {
            var request = Context(new CallToolRequestParams { Name = "action_ship" });
            request.Services = new ServiceCollection().BuildServiceProvider();
            ODataMcpAnnotatedActionTasks.TryGetElicitFormAction(request, out var action).Should().BeFalse();
            action.Should().BeNull();
        }

        /// <summary>
        /// An HTTP context is forwarded to session resolve.
        /// </summary>
        [TestMethod]
        public void TryGetElicitFormAction_HttpContext_IsForwarded()
        {
            var request = Context(new CallToolRequestParams { Name = "action_ship" });
            var accessor = new Microsoft.AspNetCore.Http.HttpContextAccessor
            {
                HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext()
            };
            var services = new ServiceCollection();
            services.AddSingleton<Microsoft.AspNetCore.Http.IHttpContextAccessor>(accessor);
            var host = new ODataMcpHostOptions();
            services.AddSingleton(Microsoft.Extensions.Options.Options.Create(host));
            var provider = services.BuildServiceProvider();
            services = new ServiceCollection();
            services.AddSingleton(new ODataMcpSessionFactory(
                provider,
                Microsoft.Extensions.Options.Options.Create(host),
                new ODataMcpPipeline(),
                new McpHttpContextAccessor()));
            services.AddSingleton<Microsoft.AspNetCore.Http.IHttpContextAccessor>(accessor);
            request.Services = services.BuildServiceProvider();

            ODataMcpAnnotatedActionTasks.TryGetElicitFormAction(request, out _).Should().BeFalse();
        }

        /// <summary>
        /// TryGetElicitFormAction is false when the factory has no matching session.
        /// </summary>
        [TestMethod]
        public void TryGetElicitFormAction_FactoryThrows_IsFalse()
        {
            var request = Context(new CallToolRequestParams { Name = "action_ship" });
            request.Services = FactoryServices();

            ODataMcpAnnotatedActionTasks.TryGetElicitFormAction(request, out var action).Should().BeFalse();
            action.Should().BeNull();
        }

        /// <summary>
        /// A null before-invoke request is rejected.
        /// </summary>
        [TestMethod]
        public async Task BeforeInvokeAsync_Null_ThrowsArgumentNullException()
        {
            var act = async () => await ODataMcpAnnotatedActionTasks.BeforeInvokeAsync(null!, default);
            await act.Should().ThrowAsync<ArgumentNullException>().WithParameterName("request");
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
        /// Services whose session factory has no discovered routes.
        /// </summary>
        /// <returns>The service provider.</returns>
        internal static IServiceProvider FactoryServices()
        {
            var host = new ODataMcpHostOptions();
            var services = new ServiceCollection();
            services.AddSingleton(Microsoft.Extensions.Options.Options.Create(host));
            var provider = services.BuildServiceProvider();
            var factory = new ODataMcpSessionFactory(
                provider,
                Microsoft.Extensions.Options.Options.Create(host),
                new ODataMcpPipeline(),
                new McpHttpContextAccessor());
            var withFactory = new ServiceCollection();
            withFactory.AddSingleton(factory);
            return withFactory.BuildServiceProvider();
        }

        /// <summary>
        /// Request whose session catalog contains a form-elicited Ship action.
        /// </summary>
        /// <param name="parameters">Call parameters.</param>
        /// <returns>The context.</returns>
        internal static RequestContext<CallToolRequestParams> FormSessionRequest(CallToolRequestParams parameters)
        {
            var xml = """
                <edmx:Edmx Version="4.0" xmlns:edmx="http://docs.oasis-open.org/odata/ns/edmx">
                  <edmx:DataServices>
                    <Schema Namespace="S" xmlns="http://docs.oasis-open.org/odata/ns/edm">
                      <Action Name="Ship">
                        <Annotation Term="Microsoft.OData.Mcp.V1.ElicitForm">
                          <Form xmlns="https://github.com/odata/mcp/vocabularies/Microsoft.OData.Mcp.V1.xml"
                                Message="Ship this order" Name="ship">
                            <Field Name="trackingNumber" Type="string" Required="true"/>
                          </Form>
                        </Annotation>
                      </Action>
                      <EntityContainer Name="Container">
                        <ActionImport Name="Ship" Action="S.Ship" />
                      </EntityContainer>
                    </Schema>
                  </edmx:DataServices>
                </edmx:Edmx>
                """;
            var catalog = new ODataMcpCatalog(new CsdlParser().ParseFromString(xml), new ODataMcpCatalogOptions());
            var session = new ODataMcpSession(catalog, new ODataToolRuntime(catalog, new UnusedFormODataExecutor()), "{}");
            var factory = HostingMoreTests.CreateFactory();
            factory._discovered = true;
            factory._sessions = new Dictionary<string, ODataMcpSession>(StringComparer.OrdinalIgnoreCase)
            {
                [string.Empty] = session
            };

            var services = new ServiceCollection();
            services.AddSingleton(factory);
            var request = Context(parameters);
            request.Services = services.BuildServiceProvider();
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
