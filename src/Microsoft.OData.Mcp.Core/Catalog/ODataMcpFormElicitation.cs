// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.OData.Mcp.Core.Constants;
using Microsoft.OData.Mcp.Core.Models;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using static Microsoft.OData.Mcp.Core.Constants.ODataMcpCatalogConstants;

namespace Microsoft.OData.Mcp.Core.Catalog
{

    /// <summary>
    /// Before-invoke form elicitation: client gate, MRTR, merge, decline.
    /// </summary>
    /// <remarks>
    /// Tools (remote <c>$metadata</c>) uses this before invoke with <see cref="ElicitFormMapper"/>.
    /// In-process AspNetCore actions call <c>RequireForm</c>. Schema <c>default</c> is the form
    /// author's; the harness may pre-fill the widget from tool arguments.
    /// </remarks>
    public static class ODataMcpFormElicitation
    {

        #region Fields

        /// <summary>
        /// Shared error result for decline and cancel. Callers must not mutate it.
        /// </summary>
        internal static readonly CallToolResult DeclinedForm = new()
        {
            Content = [new TextContentBlock { Text = "The operator declined the form." }],
            IsError = true
        };

        /// <summary>
        /// MRTR <c>inputRequests</c> key for the annotated-action form elicitation.
        /// </summary>
        internal const string InputKey = "odata.elicitForm";

        #endregion

        #region Internal Methods

        /// <summary>
        /// Collects operator fields before OData invoke when the action has
        /// <c>Microsoft.OData.Mcp.V1.ElicitForm</c> and the client advertised form elicitation.
        /// </summary>
        /// <param name="request">The call request.</param>
        /// <param name="form">The catalog form, or <see langword="null"/> when the term is omitted.</param>
        /// <param name="buildLive">
        /// Optional live schema factory for session <c>ElicitAsync</c>. When omitted,
        /// <see cref="ElicitFormMapper"/> projects <paramref name="form"/>.
        /// </param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <param name="toInputRequest">
        /// Optional cached MRTR payload. When omitted, the schema is serialized with
        /// <see cref="InputRequest.ForElicitation"/>.
        /// </param>
        /// <returns>
        /// Merged call params after accept; <see langword="null"/> when elicitation does not apply.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="request"/> is <see langword="null"/>.</exception>
        /// <exception cref="InputRequiredException">Thrown for MRTR on stateless Streamable HTTP.</exception>
        /// <exception cref="ODataFormElicitationDeclinedException">Thrown when the operator declines or cancels.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the form message is empty.</exception>
        public static async ValueTask<CallToolRequestParams?> BeforeInvokeAsync(
            RequestContext<CallToolRequestParams> request,
            EdmElicitForm? form,
            Func<ElicitRequestParams>? buildLive,
            CancellationToken cancellationToken,
            Func<InputRequest>? toInputRequest = null)
        {
            ArgumentNullException.ThrowIfNull(request);

            if (form is null || request.Params is null)
            {
                return null;
            }

            if (TryReadResponse(request, out var elicit))
            {
                if (!elicit.IsAccepted)
                {
                    throw new ODataFormElicitationDeclinedException(DeclinedResult());
                }

                return Merge(request.Params, elicit);
            }

            if (!ClientHasFormElicitation(request))
            {
                return null;
            }

            var server = request.Server;
            if (server is null)
            {
                return null;
            }

            var sessionElicit = !string.IsNullOrWhiteSpace(ReadTaskId(request))
                || HasTasksExtension(request.Params.Meta);
            if (sessionElicit || !server.IsMrtrSupported)
            {
                try
                {
                    return await ElicitOnSessionAsync(request, server, RequireParams(form, buildLive), cancellationToken).ConfigureAwait(false);
                }
                catch (InvalidOperationException exception) when (exception.Message.Contains("stateless", StringComparison.OrdinalIgnoreCase))
                {
                    return null;
                }
            }

            var input = toInputRequest is not null
                ? toInputRequest()
                : InputRequest.ForElicitation(RequireParams(form, buildLive));
            ArgumentNullException.ThrowIfNull(input);

            throw FormInputRequired(input, request.Params.Arguments);
        }

        /// <summary>
        /// Returns whether this request advertised form elicitation.
        /// </summary>
        /// <param name="request">The call request.</param>
        /// <returns>
        /// <c>true</c> when request <c>_meta</c> or client capabilities include <c>elicitation.form</c>.
        /// </returns>
        internal static bool ClientHasFormElicitation(RequestContext<CallToolRequestParams> request)
        {
            ArgumentNullException.ThrowIfNull(request);

            if (HasFormElicitation(request.Params?.Meta))
            {
                return true;
            }

            return request.Server?.ClientCapabilities?.Elicitation?.Form is not null;
        }

        /// <summary>
        /// Copies call arguments, falling back to JSON <c>requestState</c> on an MRTR retry.
        /// </summary>
        /// <param name="parameters">Call parameters.</param>
        /// <returns>A mutable argument dictionary.</returns>
        internal static Dictionary<string, JsonElement> CopyArguments(CallToolRequestParams parameters)
        {
            ArgumentNullException.ThrowIfNull(parameters);

            if (parameters.Arguments is { Count: > 0 })
            {
                return new Dictionary<string, JsonElement>(parameters.Arguments, StringComparer.Ordinal);
            }

            if (!string.IsNullOrWhiteSpace(parameters.RequestState))
            {
                try
                {
                    using var document = JsonDocument.Parse(parameters.RequestState);
                    if (document.RootElement.ValueKind == JsonValueKind.Object)
                    {
                        var restored = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
                        foreach (var property in document.RootElement.EnumerateObject())
                        {
                            restored[property.Name] = property.Value.Clone();
                        }

                        return restored;
                    }
                }
                catch (JsonException)
                {
                    return new Dictionary<string, JsonElement>(StringComparer.Ordinal);
                }
            }

            return new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        }

        /// <summary>
        /// Returns the error tool result used when the operator declines or cancels the form.
        /// </summary>
        /// <returns>An error <see cref="CallToolResult"/>.</returns>
        internal static CallToolResult DeclinedResult()
        {
            return DeclinedForm;
        }

        /// <summary>
        /// Sends <c>elicitation/create</c> on the live session and merges on accept.
        /// </summary>
        /// <param name="request">The call request.</param>
        /// <param name="server">The MCP server.</param>
        /// <param name="elicit">The form params.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>Merged params after accept.</returns>
        internal static async ValueTask<CallToolRequestParams> ElicitOnSessionAsync(
            RequestContext<CallToolRequestParams> request,
            McpServer server,
            ElicitRequestParams elicit,
            CancellationToken cancellationToken)
        {
            var result = await server.ElicitAsync(elicit, cancellationToken).ConfigureAwait(false);
            if (!result.IsAccepted)
            {
                throw new ODataFormElicitationDeclinedException(DeclinedResult());
            }

            return Merge(request.Params!, result);
        }

        /// <summary>
        /// Builds the MRTR exception for a form elicitation.
        /// </summary>
        /// <param name="elicit">The form params.</param>
        /// <param name="arguments">Current tool arguments stored as <c>requestState</c>.</param>
        /// <returns>The exception the SDK maps to <c>inputRequests</c>.</returns>
        internal static InputRequiredException FormInputRequired(ElicitRequestParams elicit, IDictionary<string, JsonElement>? arguments)
        {
            ArgumentNullException.ThrowIfNull(elicit);

            return FormInputRequired(InputRequest.ForElicitation(elicit), arguments);
        }

        /// <summary>
        /// Builds the MRTR exception from a prepared <see cref="InputRequest"/>.
        /// </summary>
        /// <param name="input">The form elicitation request, typically a cached <c>ToInputRequest</c>.</param>
        /// <param name="arguments">Current tool arguments stored as <c>requestState</c>.</param>
        /// <returns>The exception the SDK maps to <c>inputRequests</c>.</returns>
        internal static InputRequiredException FormInputRequired(InputRequest input, IDictionary<string, JsonElement>? arguments)
        {
            ArgumentNullException.ThrowIfNull(input);

            return new InputRequiredException(
                inputRequests: new Dictionary<string, InputRequest>
                {
                    [InputKey] = input
                },
                requestState: SerializeArguments(arguments));
        }

        /// <summary>
        /// Returns whether request <c>_meta</c> advertises form elicitation.
        /// </summary>
        /// <param name="meta">Request metadata.</param>
        /// <returns>
        /// <c>true</c> when <c>elicitation.form</c> is present.
        /// </returns>
        internal static bool HasFormElicitation(JsonObject? meta)
        {
            if (meta is null
                || !meta.TryGetPropertyValue("io.modelcontextprotocol/clientCapabilities", out var capabilitiesNode)
                || capabilitiesNode is not JsonObject capabilities
                || !capabilities.TryGetPropertyValue("elicitation", out var elicitationNode)
                || elicitationNode is not JsonObject elicitation)
            {
                return false;
            }

            return elicitation.ContainsKey("form");
        }

        /// <summary>
        /// Returns whether this request is an MRTR retry that already carries form content.
        /// </summary>
        /// <param name="request">The call request.</param>
        /// <returns>
        /// <c>true</c> when <see cref="InputKey"/> is in <c>inputResponses</c>.
        /// </returns>
        internal static bool IsFormRetry(RequestContext<CallToolRequestParams> request)
        {
            ArgumentNullException.ThrowIfNull(request);

            return request.Params?.InputResponses is { } responses && responses.ContainsKey(InputKey);
        }

        /// <summary>
        /// Merges accepted form content into tool arguments. Field names overwrite or fill.
        /// </summary>
        /// <param name="parameters">The original call parameters.</param>
        /// <param name="elicit">The accepted elicitation result.</param>
        /// <returns>Call parameters with merged arguments.</returns>
        internal static CallToolRequestParams Merge(CallToolRequestParams parameters, ElicitResult elicit)
        {
            ArgumentNullException.ThrowIfNull(parameters);
            ArgumentNullException.ThrowIfNull(elicit);

            if (elicit.Content is not { Count: > 0 })
            {
                return parameters;
            }

            var arguments = CopyArguments(parameters);
            foreach (var pair in elicit.Content)
            {
                arguments[pair.Key] = pair.Value;
                MergeIntoParameters(arguments, pair.Key, pair.Value);
            }

            return new CallToolRequestParams
            {
                Name = parameters.Name,
                Arguments = arguments,
                Meta = parameters.Meta,
                InputResponses = parameters.InputResponses,
                RequestState = parameters.RequestState
            };
        }

        /// <summary>
        /// Serializes tool arguments as a JSON object for MRTR <c>requestState</c>.
        /// </summary>
        /// <param name="arguments">Call arguments.</param>
        /// <returns>A JSON object, or <c>{}</c> when empty.</returns>
        internal static string SerializeArguments(IDictionary<string, JsonElement>? arguments)
        {
            if (arguments is null || arguments.Count == 0)
            {
                return "{}";
            }

            var buffer = new ArrayBufferWriter<byte>(64 + (arguments.Count * 24));
            using (var json = new Utf8JsonWriter(buffer))
            {
                json.WriteStartObject();
                foreach (var pair in arguments)
                {
                    json.WritePropertyName(pair.Key);
                    pair.Value.WriteTo(json);
                }

                json.WriteEndObject();
            }

            return Encoding.UTF8.GetString(buffer.WrittenSpan);
        }

        /// <summary>
        /// Throws <see cref="InputRequiredException"/> so the SDK returns MRTR <c>inputRequests</c>
        /// for the form elicitation.
        /// </summary>
        /// <param name="elicit">The form params.</param>
        /// <param name="arguments">Current tool arguments stored as <c>requestState</c>.</param>
        [DoesNotReturn]
        internal static void ThrowFormElicitation(ElicitRequestParams elicit, IDictionary<string, JsonElement>? arguments)
        {
            throw FormInputRequired(elicit, arguments);
        }

        /// <summary>
        /// Reads the form elicitation response from an MRTR retry.
        /// </summary>
        /// <param name="request">The call request.</param>
        /// <param name="elicit">The deserialized result when present.</param>
        /// <returns>
        /// <c>true</c> when <see cref="InputKey"/> is in <c>inputResponses</c>.
        /// </returns>
        internal static bool TryReadResponse(RequestContext<CallToolRequestParams> request, out ElicitResult elicit)
        {
            ArgumentNullException.ThrowIfNull(request);

            elicit = new ElicitResult();
            if (request.Params?.InputResponses is not { } responses
                || !responses.TryGetValue(InputKey, out var response))
            {
                return false;
            }

            elicit = response.Deserialize(InputResponse.ElicitResultJsonTypeInfo) ?? new ElicitResult();
            return true;
        }

        /// <summary>
        /// Projects the catalog form, or invokes the live schema factory.
        /// </summary>
        /// <param name="form">The catalog form.</param>
        /// <param name="buildLive">Optional live schema factory.</param>
        /// <returns>The elicitation params.</returns>
        internal static ElicitRequestParams RequireParams(EdmElicitForm form, Func<ElicitRequestParams>? buildLive)
        {
            var elicitParams = buildLive is not null
                ? buildLive()
                : ElicitFormMapper.ToRequestParams(form);
            ArgumentNullException.ThrowIfNull(elicitParams);

            return elicitParams;
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Returns whether request <c>_meta</c> advertises the Tasks extension.
        /// </summary>
        /// <param name="meta">Request metadata.</param>
        /// <returns>
        /// <c>true</c> when the nested extensions map contains the Tasks id.
        /// </returns>
        private static bool HasTasksExtension(JsonObject? meta)
        {
            if (meta is null
                || !meta.TryGetPropertyValue("io.modelcontextprotocol/clientCapabilities", out var capabilitiesNode)
                || capabilitiesNode is not JsonObject capabilities
                || !capabilities.TryGetPropertyValue("extensions", out var extensionsNode)
                || extensionsNode is not JsonObject extensions)
            {
                return false;
            }

            return extensions.ContainsKey("io.modelcontextprotocol/tasks");
        }

        /// <summary>
        /// Writes a form field into a nested <c>parameters</c> object when present.
        /// </summary>
        /// <param name="arguments">Mutable tool arguments.</param>
        /// <param name="name">The field name.</param>
        /// <param name="value">The field value.</param>
        private static void MergeIntoParameters(Dictionary<string, JsonElement> arguments, string name, JsonElement value)
        {
            if (!arguments.TryGetValue(Parameters, out var nested) || nested.ValueKind != JsonValueKind.Object)
            {
                return;
            }

            var buffer = new ArrayBufferWriter<byte>(256);
            using (var writer = new Utf8JsonWriter(buffer))
            {
                writer.WriteStartObject();
                var written = false;
                foreach (var property in nested.EnumerateObject())
                {
                    writer.WritePropertyName(property.Name);
                    if (string.Equals(property.Name, name, StringComparison.Ordinal))
                    {
                        value.WriteTo(writer);
                        written = true;
                    }
                    else
                    {
                        property.Value.WriteTo(writer);
                    }
                }

                if (!written)
                {
                    writer.WritePropertyName(name);
                    value.WriteTo(writer);
                }

                writer.WriteEndObject();
            }

            using var document = JsonDocument.Parse(buffer.WrittenMemory);
            arguments[Parameters] = document.RootElement.Clone();
        }

        /// <summary>
        /// Reads the related task id from request metadata.
        /// </summary>
        /// <param name="request">The call request.</param>
        /// <returns>The task id, or empty when absent.</returns>
        private static string ReadTaskId(RequestContext<CallToolRequestParams> request)
        {
            if (request.Params?.Meta is not { } meta
                || !meta.TryGetPropertyValue("io.modelcontextprotocol/related-task", out var node)
                || node is null)
            {
                return string.Empty;
            }

            if (node is JsonValue value)
            {
                return value.ToString();
            }

            if (node is JsonObject related && related.TryGetPropertyValue("taskId", out var taskIdNode) && taskIdNode is not null)
            {
                return taskIdNode.ToString();
            }

            return string.Empty;
        }

        #endregion

    }

}
