// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OData.Mcp.Core.Catalog;
using Microsoft.OData.Mcp.Core.Parsing;
using Microsoft.OData.Mcp.Tests.Core.Parsing;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace Microsoft.OData.Mcp.Tests.Core.Catalog
{

    /// <summary>
    /// MCP handler mapping tests against a real catalog session.
    /// </summary>
    [TestClass]
    public class ODataMcpHandlerTests
    {

        #region Public Methods

        /// <summary>
        /// Extra tools are appended to the catalog list.
        /// </summary>
        [TestMethod]
        public async Task ListToolsAsync_AppendsExtraTools()
        {
            var session = Session();
            var result = await ODataMcpHandlerExtensions.ListToolsAsync(
                new ServiceCollection().BuildServiceProvider(),
                _ => session,
                _ => [new Tool { Name = "shutdown_server" }],
                CancellationToken.None);

            result.Tools.Select(tool => tool.Name).Should().Contain("odata_query");
            result.Tools.Select(tool => tool.Name).Should().Contain("shutdown_server");
        }

        /// <summary>
        /// Resource list uses odata URIs.
        /// </summary>
        [TestMethod]
        public async Task ListResourcesAsync_ReturnsOdataUris()
        {
            var result = await ODataMcpHandlerExtensions.ListResourcesAsync(
                new ServiceCollection().BuildServiceProvider(),
                _ => Session(),
                CancellationToken.None);

            result.Resources.Should().Contain(resource => resource.Uri.Contains("$metadata", StringComparison.Ordinal));
            result.Resources.Should().Contain(resource => resource.Name == "People");
        }

        /// <summary>
        /// Template list includes entity set and key templates.
        /// </summary>
        [TestMethod]
        public async Task ListTemplatesAsync_ReturnsTemplates()
        {
            var result = await ODataMcpHandlerExtensions.ListTemplatesAsync(
                new ServiceCollection().BuildServiceProvider(),
                _ => Session(),
                CancellationToken.None);

            result.ResourceTemplates.Select(template => template.Name).Should().Contain(["entitySet", "entityByKey"]);
        }

        /// <summary>
        /// Completions for entitySet return declared names.
        /// </summary>
        [TestMethod]
        public async Task CompleteAsync_EntitySet_ReturnsPeople()
        {
            var result = await ODataMcpHandlerExtensions.CompleteAsync(
                Context(new CompleteRequestParams
                {
                    Argument = new Argument
                    {
                        Name = "entitySet",
                        Value = "Pe"
                    },
                    Ref = new ResourceTemplateReference { Uri = "odata://odata/{entitySet}" }
                }),
                _ => Session(),
                CancellationToken.None);

            result.Completion.Values.Should().Contain("People");
        }

        /// <summary>
        /// Completions for other arguments are empty.
        /// </summary>
        [TestMethod]
        public async Task CompleteAsync_OtherArgument_IsEmpty()
        {
            var result = await ODataMcpHandlerExtensions.CompleteAsync(
                Context(new CompleteRequestParams
                {
                    Argument = new Argument
                    {
                        Name = "key",
                        Value = "a"
                    },
                    Ref = new ResourceTemplateReference { Uri = "odata://odata/{entitySet}({key})" }
                }),
                _ => Session(),
                CancellationToken.None);

            result.Completion.Values.Should().BeEmpty();
        }

        /// <summary>
        /// Metadata reads return the cached CSDL.
        /// </summary>
        [TestMethod]
        public async Task ReadResourceAsync_Metadata_ReturnsXml()
        {
            var result = await ODataMcpHandlerExtensions.ReadResourceAsync(
                Context(new ReadResourceRequestParams
                {
                    Uri = "odata://remote/$metadata"
                }),
                _ => Session(),
                CancellationToken.None);

            result.Contents.Should().ContainSingle();
            ((TextResourceContents)result.Contents[0]).Text.Should().Contain("EntityType");
        }

        /// <summary>
        /// Entity set resource reads return the type card.
        /// </summary>
        [TestMethod]
        public async Task ReadResourceAsync_People_ReturnsTypeCard()
        {
            var result = await ODataMcpHandlerExtensions.ReadResourceAsync(
                Context(new ReadResourceRequestParams
                {
                    Uri = "odata://remote/People"
                }),
                _ => Session(),
                CancellationToken.None);

            ((TextResourceContents)result.Contents[0]).Text.Should().Contain("People");
        }

        /// <summary>
        /// Missing URI returns empty contents.
        /// </summary>
        [TestMethod]
        public async Task ReadResourceAsync_MissingUri_IsEmpty()
        {
            var result = await ODataMcpHandlerExtensions.ReadResourceAsync(
                Context(new ReadResourceRequestParams { Uri = " " }),
                _ => Session(),
                CancellationToken.None);

            result.Contents.Should().BeEmpty();
        }

        /// <summary>
        /// Extra call handlers run first.
        /// </summary>
        [TestMethod]
        public async Task CallToolAsync_ExtraHandler_Wins()
        {
            var extra = new CallToolResult
            {
                Content = [new TextContentBlock { Text = "extra" }]
            };
            var result = await ODataMcpHandlerExtensions.CallToolAsync(
                Context(new CallToolRequestParams { Name = "shutdown_server" }),
                _ => Session(),
                (_, _) => ValueTask.FromResult<CallToolResult?>(extra),
                CancellationToken.None);

            result.Should().BeSameAs(extra);
        }

        /// <summary>
        /// Missing tool names are errors.
        /// </summary>
        [TestMethod]
        public async Task CallToolAsync_MissingName_IsError()
        {
            var result = await ODataMcpHandlerExtensions.CallToolAsync(
                Context(new CallToolRequestParams { Name = " " }),
                _ => Session(),
                null,
                CancellationToken.None);

            result.IsError.Should().BeTrue();
        }

        /// <summary>
        /// Catalog tools dispatch through the runtime.
        /// </summary>
        [TestMethod]
        public async Task CallToolAsync_ListEntitySets_ReturnsJson()
        {
            var result = await ODataMcpHandlerExtensions.CallToolAsync(
                Context(new CallToolRequestParams
                {
                    Name = "odata_list_entity_sets",
                    Arguments = new Dictionary<string, JsonElement>()
                }),
                _ => Session(),
                (_, _) => ValueTask.FromResult<CallToolResult?>(null),
                CancellationToken.None);

            result.IsError.Should().BeFalse();
            result.StructuredContent.Should().NotBeNull();
        }

        /// <summary>
        /// Null request services throw.
        /// </summary>
        [TestMethod]
        public void RequireServices_Null_Throws()
        {
            var act = () => ODataMcpHandlerExtensions.RequireServices(null);
            act.Should().Throw<InvalidOperationException>();
        }

        /// <summary>
        /// Unknown resource URIs return empty JSON contents.
        /// </summary>
        [TestMethod]
        public async Task ReadResourceAsync_UnknownUri_ReturnsEmptyText()
        {
            var result = await ODataMcpHandlerExtensions.ReadResourceAsync(
                Context(new ReadResourceRequestParams
                {
                    Uri = "odata://remote/DoesNotExist"
                }),
                _ => Session(),
                CancellationToken.None);

            result.Contents.Should().ContainSingle();
            ((TextResourceContents)result.Contents[0]).Text.Should().BeEmpty();
        }

        /// <summary>
        /// Unknown catalog tools are errors.
        /// </summary>
        [TestMethod]
        public async Task CallToolAsync_UnknownTool_IsError()
        {
            var result = await ODataMcpHandlerExtensions.CallToolAsync(
                Context(new CallToolRequestParams { Name = "nope" }),
                _ => Session(),
                null,
                CancellationToken.None);

            result.IsError.Should().BeTrue();
        }

        /// <summary>
        /// Mapping helpers reject null descriptors.
        /// </summary>
        [TestMethod]
        public void Mapping_NullDescriptors_Throw()
        {
            var tool = () => ODataMcpHandlerExtensions.ToTool(null!);
            var resource = () => ODataMcpHandlerExtensions.ToResource(null!);

            tool.Should().Throw<ArgumentNullException>();
            resource.Should().Throw<ArgumentNullException>();
        }

        /// <summary>
        /// Registered handlers run against the session.
        /// </summary>
        [TestMethod]
        public async Task WithODataCatalogHandlers_InvokesRegisteredHandlers()
        {
            var session = Session();
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton(session);
            services.AddMcpServer().WithODataCatalogHandlers(provider => provider.GetRequiredService<ODataMcpSession>());
            using var provider = services.BuildServiceProvider();
            var handlers = provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<McpServerOptions>>().Value.Handlers;

            (await handlers.ListToolsHandler!(Context(new ListToolsRequestParams(), provider), CancellationToken.None)).Tools.Should().NotBeEmpty();
            (await handlers.ListResourcesHandler!(Context(new ListResourcesRequestParams(), provider), CancellationToken.None)).Resources.Should().NotBeEmpty();
            (await handlers.ListResourceTemplatesHandler!(Context(new ListResourceTemplatesRequestParams(), provider), CancellationToken.None)).ResourceTemplates.Should().NotBeEmpty();
            (await handlers.CallToolHandler!(Context(new CallToolRequestParams { Name = "odata_list_entity_sets" }, provider), CancellationToken.None)).IsError.Should().BeFalse();
            (await handlers.ReadResourceHandler!(Context(new ReadResourceRequestParams { Uri = "odata://remote/$metadata" }, provider), CancellationToken.None)).Contents.Should().NotBeEmpty();
            (await handlers.CompleteHandler!(Context(new CompleteRequestParams
            {
                Argument = new Argument { Name = "entitySet", Value = "P" },
                Ref = new ResourceTemplateReference { Uri = "odata://remote/{entitySet}" }
            }, provider), CancellationToken.None)).Completion.Values.Should().Contain("People");
        }

        #endregion

        #region Internal Methods

        /// <summary>
        /// Builds an uninitialized request context with params and services.
        /// </summary>
        /// <typeparam name="T">The parameter type.</typeparam>
        /// <param name="parameters">Request parameters.</param>
        /// <returns>
        /// The context.
        /// </returns>
        internal static RequestContext<T> Context<T>(T parameters, IServiceProvider? services = null)
        {
            var request = (RequestContext<T>)RuntimeHelpers.GetUninitializedObject(typeof(RequestContext<T>));
            request.Params = parameters;
            request.Services = services ?? new ServiceCollection().BuildServiceProvider();

            return request;
        }

        /// <summary>
        /// Builds a session over the documented CSDL.
        /// </summary>
        /// <returns>
        /// The session.
        /// </returns>
        internal static ODataMcpSession Session()
        {
            var catalog = ODataMcpCatalogMoreTests.Catalog();
            var runtime = new ODataToolRuntime(catalog, new RecordingODataExecutor());

            return new ODataMcpSession(catalog, runtime, CsdlParserDocumentationTests.DocumentedCsdl);
        }

        #endregion

    }

}
