// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OData.Mcp.Core.Constants;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace Microsoft.OData.Mcp.Core.Catalog
{

    /// <summary>
    /// Registers catalog-backed MCP 2 handlers without assembly scanning.
    /// </summary>
    public static class ODataMcpHandlerExtensions
    {

        #region Public Methods

        /// <summary>
        /// Adds list/call/resource/complete handlers that resolve an <see cref="ODataMcpSession"/> per request.
        /// </summary>
        /// <param name="builder">The MCP server builder.</param>
        /// <param name="resolveSession">Resolves the session for the current request.</param>
        /// <param name="listExtraTools">Optional extra tools (for example, <c>shutdown_server</c>).</param>
        /// <param name="tryHandleExtra">Optional extra call handler that runs before catalog tools.</param>
        /// <returns>
        /// The builder.
        /// </returns>
        public static IMcpServerBuilder WithODataCatalogHandlers(
            this IMcpServerBuilder builder,
            Func<IServiceProvider, ODataMcpSession> resolveSession,
            Func<IServiceProvider, IReadOnlyList<Tool>>? listExtraTools = null,
            Func<RequestContext<CallToolRequestParams>, CancellationToken, ValueTask<CallToolResult?>>? tryHandleExtra = null)
        {
            ArgumentNullException.ThrowIfNull(builder);
            ArgumentNullException.ThrowIfNull(resolveSession);

            return builder
                .WithListToolsHandler((request, cancellationToken) => ListToolsAsync(RequireServices(request.Services), resolveSession, listExtraTools, cancellationToken))
                .WithCallToolHandler((request, cancellationToken) => CallToolAsync(request, resolveSession, tryHandleExtra, cancellationToken))
                .WithListResourcesHandler((request, cancellationToken) => ListResourcesAsync(RequireServices(request.Services), resolveSession, cancellationToken))
                .WithReadResourceHandler((request, cancellationToken) => ReadResourceAsync(request, resolveSession, cancellationToken))
                .WithListResourceTemplatesHandler((request, cancellationToken) => ListTemplatesAsync(RequireServices(request.Services), resolveSession, cancellationToken))
                .WithCompleteHandler((request, cancellationToken) => CompleteAsync(request, resolveSession, cancellationToken));
        }

        #endregion

        #region Internal Methods

        /// <summary>
        /// Executes a catalog tool.
        /// </summary>
        /// <param name="request">The call request.</param>
        /// <param name="resolveSession">Session resolver.</param>
        /// <param name="tryHandleExtra">Optional extra call handler that runs before catalog tools.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>
        /// The call result.
        /// </returns>
        internal static async ValueTask<CallToolResult> CallToolAsync(
            RequestContext<CallToolRequestParams> request,
            Func<IServiceProvider, ODataMcpSession> resolveSession,
            Func<RequestContext<CallToolRequestParams>, CancellationToken, ValueTask<CallToolResult?>>? tryHandleExtra,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);
            ArgumentNullException.ThrowIfNull(resolveSession);

            if (tryHandleExtra is not null)
            {
                var extra = await tryHandleExtra(request, cancellationToken).ConfigureAwait(false);
                if (extra is not null)
                {
                    return extra;
                }
            }

            var name = request.Params?.Name;
            if (string.IsNullOrWhiteSpace(name))
            {
                return new CallToolResult
                {
                    IsError = true,
                    Content = [new TextContentBlock { Text = "Tool name is required." }]
                };
            }

            var session = resolveSession(RequireServices(request.Services));
            var arguments = request.Params?.Arguments;
            var result = await session.Runtime.InvokeAsync(name, arguments, cancellationToken).ConfigureAwait(false);

            return new CallToolResult
            {
                Content = [new TextContentBlock { Text = result.Text }],
                IsError = result.IsError,
                StructuredContent = string.IsNullOrWhiteSpace(result.StructuredContent)
                    ? null
                    : JsonSerializer.Deserialize<JsonElement>(result.StructuredContent)
            };
        }

        /// <summary>
        /// Completes entity set names for resource templates.
        /// </summary>
        /// <param name="request">The complete request.</param>
        /// <param name="resolveSession">Session resolver.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>
        /// Completion values.
        /// </returns>
        internal static ValueTask<CompleteResult> CompleteAsync(
            RequestContext<CompleteRequestParams> request,
            Func<IServiceProvider, ODataMcpSession> resolveSession,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);
            ArgumentNullException.ThrowIfNull(resolveSession);

            cancellationToken.ThrowIfCancellationRequested();

            var argumentName = request.Params?.Argument?.Name;
            var prefix = request.Params?.Argument?.Value ?? string.Empty;
            IReadOnlyList<string> values = [];

            if (string.Equals(argumentName, "entitySet", StringComparison.Ordinal))
            {
                values = resolveSession(RequireServices(request.Services)).Catalog.CompleteEntitySetNames(prefix);
            }

            return ValueTask.FromResult(new CompleteResult
            {
                Completion = new Completion
                {
                    HasMore = false,
                    Values = [.. values]
                }
            });
        }

        /// <summary>
        /// Lists catalog resources.
        /// </summary>
        /// <param name="services">Request services.</param>
        /// <param name="resolveSession">Session resolver.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>
        /// Resource list.
        /// </returns>
        internal static ValueTask<ListResourcesResult> ListResourcesAsync(
            IServiceProvider services,
            Func<IServiceProvider, ODataMcpSession> resolveSession,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(resolveSession);

            cancellationToken.ThrowIfCancellationRequested();

            var resources = resolveSession(services).Catalog.Resources.Select(ToResource).ToList();

            return ValueTask.FromResult(new ListResourcesResult
            {
                Resources = resources
            });
        }

        /// <summary>
        /// Lists catalog tools plus optional extra tools.
        /// </summary>
        /// <param name="services">Request services.</param>
        /// <param name="resolveSession">Session resolver.</param>
        /// <param name="listExtraTools">Optional extra tools.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>
        /// Tool list.
        /// </returns>
        internal static ValueTask<ListToolsResult> ListToolsAsync(
            IServiceProvider services,
            Func<IServiceProvider, ODataMcpSession> resolveSession,
            Func<IServiceProvider, IReadOnlyList<Tool>>? listExtraTools,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(resolveSession);

            cancellationToken.ThrowIfCancellationRequested();

            var tools = resolveSession(services).Catalog.Tools.Select(ToTool).ToList();
            if (listExtraTools is not null)
            {
                tools.AddRange(listExtraTools(services));
            }

            return ValueTask.FromResult(new ListToolsResult
            {
                Tools = tools
            });
        }

        /// <summary>
        /// Lists resource templates.
        /// </summary>
        /// <param name="services">Request services.</param>
        /// <param name="resolveSession">Session resolver.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>
        /// Template list.
        /// </returns>
        internal static ValueTask<ListResourceTemplatesResult> ListTemplatesAsync(
            IServiceProvider services,
            Func<IServiceProvider, ODataMcpSession> resolveSession,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentNullException.ThrowIfNull(resolveSession);

            cancellationToken.ThrowIfCancellationRequested();

            var templates = resolveSession(services).Catalog.ResourceTemplates.Select(template => new ResourceTemplate
            {
                Description = template.Description,
                Name = template.Name,
                Title = template.Title,
                UriTemplate = template.UriTemplate
            }).ToList();

            return ValueTask.FromResult(new ListResourceTemplatesResult
            {
                ResourceTemplates = templates
            });
        }

        /// <summary>
        /// Reads a catalog resource.
        /// </summary>
        /// <param name="request">The read request.</param>
        /// <param name="resolveSession">Session resolver.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>
        /// Resource contents.
        /// </returns>
        internal static ValueTask<ReadResourceResult> ReadResourceAsync(
            RequestContext<ReadResourceRequestParams> request,
            Func<IServiceProvider, ODataMcpSession> resolveSession,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);
            ArgumentNullException.ThrowIfNull(resolveSession);

            cancellationToken.ThrowIfCancellationRequested();

            var uri = request.Params?.Uri;
            var session = resolveSession(RequireServices(request.Services));
            if (string.IsNullOrWhiteSpace(uri))
            {
                return ValueTask.FromResult(new ReadResourceResult
                {
                    Contents = []
                });
            }

            if (uri.EndsWith("/" + ODataMcpCatalogConstants.Metadata, StringComparison.Ordinal))
            {
                return ValueTask.FromResult(new ReadResourceResult
                {
                    Contents =
                    [
                        new TextResourceContents
                        {
                            MimeType = ODataMcpCatalogConstants.ApplicationXml,
                            Text = session.MetadataXml ?? string.Empty,
                            Uri = uri
                        }
                    ]
                });
            }

            var resource = session.Catalog.Resources.FirstOrDefault(item => item.Uri.Equals(uri, StringComparison.OrdinalIgnoreCase));
            var text = resource?.ReadContents ?? string.Empty;

            return ValueTask.FromResult(new ReadResourceResult
            {
                Contents =
                [
                    new TextResourceContents
                    {
                        MimeType = resource?.MimeType ?? ODataMcpCatalogConstants.ApplicationJson,
                        Text = text,
                        Uri = uri
                    }
                ]
            });
        }

        /// <summary>
        /// Returns request services or throws.
        /// </summary>
        /// <param name="services">Request services.</param>
        /// <returns>
        /// The service provider.
        /// </returns>
        internal static IServiceProvider RequireServices(IServiceProvider? services)
        {
            return services ?? throw new InvalidOperationException("MCP request services are required.");
        }

        /// <summary>
        /// Maps a catalog resource to an MCP resource.
        /// </summary>
        /// <param name="descriptor">The catalog resource.</param>
        /// <returns>
        /// The MCP resource.
        /// </returns>
        internal static Resource ToResource(ODataResourceDescriptor descriptor)
        {
            ArgumentNullException.ThrowIfNull(descriptor);

            return new Resource
            {
                Description = descriptor.Description,
                MimeType = descriptor.MimeType,
                Name = descriptor.Name,
                Title = descriptor.Title,
                Uri = descriptor.Uri
            };
        }

        /// <summary>
        /// Maps a catalog tool to an MCP tool.
        /// </summary>
        /// <param name="descriptor">The catalog tool.</param>
        /// <returns>
        /// The MCP tool.
        /// </returns>
        internal static Tool ToTool(ODataToolDescriptor descriptor)
        {
            ArgumentNullException.ThrowIfNull(descriptor);

            return new Tool
            {
                Annotations = new ToolAnnotations
                {
                    DestructiveHint = descriptor.DestructiveHint,
                    IdempotentHint = descriptor.IdempotentHint,
                    OpenWorldHint = descriptor.OpenWorldHint,
                    ReadOnlyHint = descriptor.ReadOnlyHint
                },
                Description = descriptor.Description,
                InputSchema = JsonSerializer.Deserialize<JsonElement>(descriptor.InputSchema),
                Name = descriptor.Name,
                Title = descriptor.Title
            };
        }

        #endregion

    }

}
