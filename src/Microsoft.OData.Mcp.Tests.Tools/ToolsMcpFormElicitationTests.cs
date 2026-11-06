// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OData.Mcp.Core.Catalog;
using Microsoft.OData.Mcp.Core.Parsing;
using Microsoft.OData.Mcp.Tools.Hosting;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace Microsoft.OData.Mcp.Tests.Tools
{

    /// <summary>
    /// Tools-host form elicitation fail-first paths.
    /// </summary>
    [TestClass]
    public class ToolsMcpFormElicitationTests
    {

        #region Public Methods

        /// <summary>
        /// A blank tool name is a no-op.
        /// </summary>
        [TestMethod]
        public async Task BeforeInvokeAsync_BlankName_ReturnsNull()
        {
            var request = Context(new CallToolRequestParams { Name = " " });
            var result = await ToolsMcpFormElicitation.BeforeInvokeAsync(request, CancellationToken.None);
            result.Should().BeNull();
        }

        /// <summary>
        /// Missing request services is a no-op.
        /// </summary>
        [TestMethod]
        public async Task BeforeInvokeAsync_NoServices_ReturnsNull()
        {
            var request = (RequestContext<CallToolRequestParams>)RuntimeHelpers.GetUninitializedObject(typeof(RequestContext<CallToolRequestParams>));
            request.Params = new CallToolRequestParams { Name = "action_ship" };
            var result = await ToolsMcpFormElicitation.BeforeInvokeAsync(request, CancellationToken.None);
            result.Should().BeNull();
        }

        /// <summary>
        /// AsReadOnly copies a non-readonly dictionary.
        /// </summary>
        [TestMethod]
        public void AsReadOnly_CopiesDictionary()
        {
            IDictionary<string, JsonElement> arguments = new Dictionary<string, JsonElement>
            {
                ["name"] = JsonSerializer.SerializeToElement("Ship")
            };

            var readOnly = ToolsMcpFormElicitation.AsReadOnly(arguments);
            readOnly.Should().ContainKey("name");
            ToolsMcpFormElicitation.AsReadOnly(null).Should().BeNull();
        }

        /// <summary>
        /// A null request is rejected.
        /// </summary>
        [TestMethod]
        public async Task BeforeInvokeAsync_Null_ThrowsArgumentNullException()
        {
            var act = async () => await ToolsMcpFormElicitation.BeforeInvokeAsync(null!, CancellationToken.None);
            await act.Should().ThrowAsync<ArgumentNullException>();
        }

        /// <summary>
        /// Services without a session holder is a no-op.
        /// </summary>
        [TestMethod]
        public async Task BeforeInvokeAsync_NoHolder_ReturnsNull()
        {
            var request = Context(new CallToolRequestParams { Name = "action_ship" });
            var result = await ToolsMcpFormElicitation.BeforeInvokeAsync(request, CancellationToken.None);
            result.Should().BeNull();
        }

        /// <summary>
        /// A holder without a session is a no-op.
        /// </summary>
        [TestMethod]
        public async Task BeforeInvokeAsync_HolderWithoutSession_ReturnsNull()
        {
            var request = Context(new CallToolRequestParams { Name = "action_ship" });
            request.Services = new ServiceCollection()
                .AddSingleton(new ToolsMcpSessionHolder())
                .BuildServiceProvider();

            var result = await ToolsMcpFormElicitation.BeforeInvokeAsync(request, CancellationToken.None);
            result.Should().BeNull();
        }

        /// <summary>
        /// An unknown tool on a live session is a no-op.
        /// </summary>
        [TestMethod]
        public async Task BeforeInvokeAsync_UnknownTool_ReturnsNull()
        {
            var catalog = FormCatalog();
            var session = new ODataMcpSession(catalog, new ODataToolRuntime(catalog, new UnusedToolsODataExecutor()), "{}");
            var request = Context(new CallToolRequestParams { Name = "odata_query" });
            request.Services = new ServiceCollection()
                .AddSingleton(new ToolsMcpSessionHolder { Session = session })
                .BuildServiceProvider();

            var result = await ToolsMcpFormElicitation.BeforeInvokeAsync(request, CancellationToken.None);
            result.Should().BeNull();
        }

        /// <summary>
        /// Missing params with services is a no-op.
        /// </summary>
        [TestMethod]
        public async Task BeforeInvokeAsync_NullParams_ReturnsNull()
        {
            var request = (RequestContext<CallToolRequestParams>)RuntimeHelpers.GetUninitializedObject(typeof(RequestContext<CallToolRequestParams>));
            request.Services = new ServiceCollection().BuildServiceProvider();
            var result = await ToolsMcpFormElicitation.BeforeInvokeAsync(request, CancellationToken.None);
            result.Should().BeNull();
        }

        /// <summary>
        /// A form action without client form capability is a no-op.
        /// </summary>
        [TestMethod]
        public async Task BeforeInvokeAsync_FormActionWithoutCapability_ReturnsNull()
        {
            var catalog = FormCatalog();
            var session = new ODataMcpSession(catalog, new ODataToolRuntime(catalog, new UnusedToolsODataExecutor()), "{}");
            var request = Context(new CallToolRequestParams { Name = "action_ship" });
            request.Services = new ServiceCollection()
                .AddSingleton(new ToolsMcpSessionHolder { Session = session })
                .BuildServiceProvider();

            var result = await ToolsMcpFormElicitation.BeforeInvokeAsync(request, CancellationToken.None);
            result.Should().BeNull();
        }

        /// <summary>
        /// Call arguments are forwarded when resolving the form action.
        /// </summary>
        [TestMethod]
        public async Task BeforeInvokeAsync_FormActionWithArguments_ReturnsNullWithoutCapability()
        {
            var catalog = FormCatalog();
            var session = new ODataMcpSession(catalog, new ODataToolRuntime(catalog, new UnusedToolsODataExecutor()), "{}");
            var request = Context(new CallToolRequestParams
            {
                Name = "action_ship",
                Arguments = new Dictionary<string, JsonElement>
                {
                    ["key"] = JsonSerializer.SerializeToElement("1")
                }
            });
            request.Services = new ServiceCollection()
                .AddSingleton(new ToolsMcpSessionHolder { Session = session })
                .BuildServiceProvider();

            var result = await ToolsMcpFormElicitation.BeforeInvokeAsync(request, CancellationToken.None);
            result.Should().BeNull();
        }

        /// <summary>
        /// AsReadOnly copies a dictionary that is not already read-only.
        /// </summary>
        [TestMethod]
        public void AsReadOnly_NonReadOnlyDictionary_Copies()
        {
            var arguments = new NonReadOnlyJsonArguments
            {
                ["name"] = JsonSerializer.SerializeToElement("Ship")
            };

            var readOnly = ToolsMcpFormElicitation.AsReadOnly(arguments);
            readOnly.Should().NotBeSameAs(arguments);
            readOnly.Should().ContainKey("name");
        }

        /// <summary>
        /// AsReadOnly returns an already read-only dictionary as-is.
        /// </summary>
        [TestMethod]
        public void AsReadOnly_ReadOnlyDictionary_IsSameInstance()
        {
            IReadOnlyDictionary<string, JsonElement> arguments = new Dictionary<string, JsonElement>
            {
                ["name"] = JsonSerializer.SerializeToElement("Ship")
            };

            ToolsMcpFormElicitation.AsReadOnly((IDictionary<string, JsonElement>)arguments).Should().BeSameAs(arguments);
        }

        #endregion

        #region Internal Methods

        /// <summary>
        /// Builds an uninitialized request context with empty services.
        /// </summary>
        /// <param name="parameters">Call parameters.</param>
        /// <returns>The context.</returns>
        internal static RequestContext<CallToolRequestParams> Context(CallToolRequestParams parameters)
        {
            var request = (RequestContext<CallToolRequestParams>)RuntimeHelpers.GetUninitializedObject(typeof(RequestContext<CallToolRequestParams>));
            request.Params = parameters;
            request.Services = new ServiceCollection().BuildServiceProvider();
            return request;
        }

        /// <summary>
        /// Catalog whose unbound Ship elicits a form.
        /// </summary>
        /// <returns>The catalog.</returns>
        internal static ODataMcpCatalog FormCatalog()
        {
            const string xml = """
                <edmx:Edmx Version="4.0" xmlns:edmx="http://docs.oasis-open.org/odata/ns/edmx">
                  <edmx:DataServices>
                    <Schema Namespace="S" xmlns="http://docs.oasis-open.org/odata/ns/edm">
                      <Action Name="Ship">
                        <Annotation Term="Microsoft.OData.Mcp.V1.ElicitForm">
                          <Form xmlns="https://github.com/odata/mcp/vocabularies/Microsoft.OData.Mcp.V1.xml"
                                Message="Ship this order">
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

            return new ODataMcpCatalog(new CsdlParser().ParseFromString(xml), new ODataMcpCatalogOptions());
        }

        #endregion

    }

}
