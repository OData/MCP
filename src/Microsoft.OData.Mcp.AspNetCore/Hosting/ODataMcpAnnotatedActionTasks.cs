// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OData.Mcp.Core.Catalog;
using Microsoft.OData.Mcp.Core.Models;
using Microsoft.OData.Mcp.Core.Vocabularies;
using ModelContextProtocol.Extensions.Tasks;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace Microsoft.OData.Mcp.AspNetCore.Hosting
{

    /// <summary>
    /// Annotated-action URL elicitation, optional MCP Tasks wrapping, and the host waiter.
    /// </summary>
    /// <remarks>
    /// Elicitation follows the SDK: <c>InputRequiredException</c> (MRTR) on stateless
    /// <c>2026-07-28</c>, <c>ElicitAsync</c> inside a task scope or a stateful session.
    /// Protocol Tasks are optional. CRUD, functions, and unannotated actions stay one-shot.
    /// </remarks>
    internal static class ODataMcpAnnotatedActionTasks
    {

        #region Fields

        /// <summary>
        /// MRTR <c>inputRequests</c> key for the annotated-action URL elicitation.
        /// </summary>
        internal const string ElicitInputKey = "odata.elicitUrl";

        #endregion

        #region Internal Methods

        /// <summary>
        /// After a catalog <c>tools/call</c>, elicits an annotated-action URL when the client
        /// advertised URL elicitation, then waits for the host callback when a waiter is registered.
        /// </summary>
        /// <param name="request">The call request.</param>
        /// <param name="result">The one-shot catalog result, or the Started ticket echoed as <c>requestState</c>.</param>
        /// <param name="cancellationToken">Timeout, <c>tasks/cancel</c>, and process shutdown.</param>
        /// <returns>
        /// The terminal tool result.
        /// </returns>
        internal static async ValueTask<CallToolResult> AfterInvokeAsync(
            RequestContext<CallToolRequestParams> request,
            CallToolResult result,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);
            ArgumentNullException.ThrowIfNull(result);

            if (result.IsError == true || !TryGetTaskedAction(request, out var action) || action is null)
            {
                return result;
            }

            if (TryReadElicitResponse(request, out var elicit))
            {
                return elicit.IsAccepted
                    ? await WaitForHostAsync(request, action, result, cancellationToken).ConfigureAwait(false)
                    : DeclinedResult();
            }

            var url = FindElicitUrl(result, action.ElicitUrlProperties);
            if (string.IsNullOrWhiteSpace(url) || !ClientHasUrlElicitation(request))
            {
                return result;
            }

            return await ElicitUrlAsync(request, result, action, url, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Copies call arguments to a read-only dictionary when needed.
        /// </summary>
        /// <param name="arguments">Call arguments.</param>
        /// <returns>
        /// A read-only view, or <c>null</c>.
        /// </returns>
        internal static IReadOnlyDictionary<string, JsonElement>? AsReadOnly(IDictionary<string, JsonElement>? arguments)
        {
            return arguments switch
            {
                null => null,
                IReadOnlyDictionary<string, JsonElement> readOnly => readOnly,
                _ => new Dictionary<string, JsonElement>(arguments)
            };
        }

        /// <summary>
        /// Returns whether this request advertised the MCP Tasks extension.
        /// </summary>
        /// <param name="request">The call request.</param>
        /// <returns>
        /// <c>true</c> when the request <c>_meta</c> or initialize capabilities include
        /// <c>io.modelcontextprotocol/tasks</c>.
        /// </returns>
        internal static bool ClientOptedIntoTasks(RequestContext<CallToolRequestParams> request)
        {
            ArgumentNullException.ThrowIfNull(request);

            if (HasTasksExtension(request.Params?.Meta))
            {
                return true;
            }

            return request.Server?.ClientCapabilities?.Extensions?.ContainsKey(TasksProtocol.ExtensionId) == true;
        }

        /// <summary>
        /// Returns whether this request advertised URL elicitation.
        /// </summary>
        /// <param name="request">The call request.</param>
        /// <returns>
        /// <c>true</c> when request <c>_meta</c> or client capabilities include <c>elicitation.url</c>.
        /// </returns>
        internal static bool ClientHasUrlElicitation(RequestContext<CallToolRequestParams> request)
        {
            ArgumentNullException.ThrowIfNull(request);

            if (HasUrlElicitation(request.Params?.Meta))
            {
                return true;
            }

            return request.Server?.ClientCapabilities?.Elicitation?.Url is not null;
        }

        /// <summary>
        /// Builds URL-mode elicitation params from the Started ticket.
        /// </summary>
        /// <param name="result">The Started ticket.</param>
        /// <param name="url">The URL to elicit.</param>
        /// <returns>
        /// Params for <c>elicitation/create</c> or MRTR <c>InputRequest.ForElicitation</c>.
        /// </returns>
        internal static ElicitRequestParams CreateUrlElicitParams(CallToolResult result, string url)
        {
            ArgumentNullException.ThrowIfNull(result);
            ArgumentException.ThrowIfNullOrWhiteSpace(url);

            return new ElicitRequestParams
            {
                ElicitationId = Guid.NewGuid().ToString("N"),
                Message = ReadTicketMessage(result) ?? "Open the URL to finish this action.",
                Mode = "url",
                Url = url
            };
        }

        /// <summary>
        /// Returns the Failed ticket used when the operator declines the URL.
        /// </summary>
        /// <returns>
        /// An error <see cref="CallToolResult"/>.
        /// </returns>
        internal static CallToolResult DeclinedResult()
        {
            return new CallToolResult
            {
                Content = [new TextContentBlock { Text = "The operator declined to open the URL." }],
                IsError = true
            };
        }

        /// <summary>
        /// Sends <c>elicitation/create</c> on the live session, then waits for the host when accepted.
        /// </summary>
        /// <param name="request">The call request.</param>
        /// <param name="result">The Started ticket.</param>
        /// <param name="action">The tasked action.</param>
        /// <param name="url">The URL to elicit.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>
        /// The terminal tool result.
        /// </returns>
        internal static async ValueTask<CallToolResult> ElicitOnSessionAsync(
            RequestContext<CallToolRequestParams> request,
            CallToolResult result,
            EdmAction action,
            string url,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);
            ArgumentNullException.ThrowIfNull(result);
            ArgumentNullException.ThrowIfNull(action);
            ArgumentException.ThrowIfNullOrWhiteSpace(url);

            var server = request.Server;
            if (server is null)
            {
                return result;
            }

            var elicit = await server.ElicitAsync(CreateUrlElicitParams(result, url), cancellationToken).ConfigureAwait(false);

            return elicit.IsAccepted
                ? await WaitForHostAsync(request, action, result, cancellationToken).ConfigureAwait(false)
                : DeclinedResult();
        }

        /// <summary>
        /// Elicits <paramref name="url"/> via MRTR, task-scoped <c>ElicitAsync</c>, or a stateful session.
        /// </summary>
        /// <param name="request">The call request.</param>
        /// <param name="result">The Started ticket.</param>
        /// <param name="action">The tasked action.</param>
        /// <param name="url">The URL to elicit.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>
        /// The terminal tool result.
        /// </returns>
        internal static async ValueTask<CallToolResult> ElicitUrlAsync(
            RequestContext<CallToolRequestParams> request,
            CallToolResult result,
            EdmAction action,
            string url,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);
            ArgumentNullException.ThrowIfNull(result);
            ArgumentNullException.ThrowIfNull(action);
            ArgumentException.ThrowIfNullOrWhiteSpace(url);

            var server = request.Server;
            if (server is null)
            {
                return result;
            }

            if (!string.IsNullOrWhiteSpace(ReadTaskId(request)) || ClientOptedIntoTasks(request))
            {
                return await ElicitOnSessionAsync(request, result, action, url, cancellationToken).ConfigureAwait(false);
            }

            if (server.IsMrtrSupported)
            {
                ThrowUrlElicitation(result, url);
            }

            try
            {
                return await ElicitOnSessionAsync(request, result, action, url, cancellationToken).ConfigureAwait(false);
            }
            catch (InvalidOperationException exception) when (exception.Message.Contains("stateless", StringComparison.OrdinalIgnoreCase))
            {
                return result;
            }
        }

        /// <summary>
        /// Finds the first elicit URL on <paramref name="result"/> matching
        /// <paramref name="declared"/> or <see cref="McpVocabulary.DefaultElicitUrlProperties"/>.
        /// </summary>
        /// <param name="result">The one-shot tool result.</param>
        /// <param name="declared">Declared <c>ElicitUrlProperties</c>, or <c>null</c> for the default names.</param>
        /// <returns>
        /// An absolute http(s) URI, or <c>null</c> when none is present.
        /// </returns>
        internal static string? FindElicitUrl(CallToolResult result, IReadOnlyList<string>? declared)
        {
            ArgumentNullException.ThrowIfNull(result);

            var names = declared is { Count: > 0 }
                ? declared
                : McpVocabulary.DefaultElicitUrlProperties;
            if (!TryGetResultJson(result, out var json))
            {
                return null;
            }

            return FindElicitUrl(json, names);
        }

        /// <summary>
        /// Walks <paramref name="element"/> for a named elicit URL.
        /// </summary>
        /// <param name="element">Started JSON.</param>
        /// <param name="names">JSON property names to elicit, in order.</param>
        /// <returns>
        /// An absolute http(s) URI, or <c>null</c>.
        /// </returns>
        internal static string? FindElicitUrl(JsonElement element, IReadOnlyList<string> names)
        {
            ArgumentNullException.ThrowIfNull(names);

            if (element.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            foreach (var name in names)
            {
                if (string.IsNullOrWhiteSpace(name) || IsSecretPropertyName(name))
                {
                    continue;
                }

                foreach (var property in element.EnumerateObject())
                {
                    if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase)
                        && TryReadAbsoluteHttpUrl(property.Value, out var url))
                    {
                        return url;
                    }
                }
            }

            foreach (var property in element.EnumerateObject())
            {
                if (property.Value.ValueKind == JsonValueKind.Object)
                {
                    var nested = FindElicitUrl(property.Value, names);
                    if (nested is not null)
                    {
                        return nested;
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Returns whether request <c>_meta</c> advertises the Tasks extension.
        /// </summary>
        /// <param name="meta">Request metadata.</param>
        /// <returns>
        /// <c>true</c> when the nested extensions map contains the Tasks id.
        /// </returns>
        internal static bool HasTasksExtension(JsonObject? meta)
        {
            if (meta is null
                || !meta.TryGetPropertyValue("io.modelcontextprotocol/clientCapabilities", out var capabilitiesNode)
                || capabilitiesNode is not JsonObject capabilities
                || !capabilities.TryGetPropertyValue("extensions", out var extensionsNode)
                || extensionsNode is not JsonObject extensions)
            {
                return false;
            }

            return extensions.ContainsKey(TasksProtocol.ExtensionId);
        }

        /// <summary>
        /// Returns whether request <c>_meta</c> advertises URL elicitation.
        /// </summary>
        /// <param name="meta">Request metadata.</param>
        /// <returns>
        /// <c>true</c> when <c>elicitation.url</c> is present.
        /// </returns>
        internal static bool HasUrlElicitation(JsonObject? meta)
        {
            if (meta is null
                || !meta.TryGetPropertyValue("io.modelcontextprotocol/clientCapabilities", out var capabilitiesNode)
                || capabilitiesNode is not JsonObject capabilities
                || !capabilities.TryGetPropertyValue("elicitation", out var elicitationNode)
                || elicitationNode is not JsonObject elicitation)
            {
                return false;
            }

            return elicitation.ContainsKey("url");
        }

        /// <summary>
        /// Returns whether <paramref name="name"/> is a secret-bearing JSON property (AUTH-12).
        /// </summary>
        /// <param name="name">A JSON property name.</param>
        /// <returns>
        /// <c>true</c> when the name contains token, secret, password, or code.
        /// </returns>
        internal static bool IsSecretPropertyName(string name)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(name);

            return name.Contains("token", StringComparison.OrdinalIgnoreCase)
                || name.Contains("secret", StringComparison.OrdinalIgnoreCase)
                || name.Contains("password", StringComparison.OrdinalIgnoreCase)
                || name.Contains("code", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Returns whether this call is an annotated EDM action (named <c>action_*</c> or
        /// <c>odata_call</c> of the same action).
        /// </summary>
        /// <param name="context">The call request.</param>
        /// <returns>
        /// <c>true</c> when the resolved tool is a tasked action.
        /// </returns>
        internal static bool IsTaskedCall(RequestContext<CallToolRequestParams> context)
        {
            ArgumentNullException.ThrowIfNull(context);

            return TryGetTaskedAction(context, out _);
        }

        /// <summary>
        /// Reads the bound entity key from tool arguments.
        /// </summary>
        /// <param name="arguments">Call arguments.</param>
        /// <returns>
        /// The <c>key</c> argument, or <c>null</c>.
        /// </returns>
        internal static string? ReadBindingKey(IDictionary<string, JsonElement>? arguments)
        {
            if (arguments is null
                || !arguments.TryGetValue("key", out var key)
                || key.ValueKind != JsonValueKind.String)
            {
                return null;
            }

            var value = key.GetString();
            return string.IsNullOrWhiteSpace(value) ? null : value;
        }

        /// <summary>
        /// Reads the Started JSON from a catalog tool result.
        /// </summary>
        /// <param name="result">The one-shot result.</param>
        /// <returns>
        /// Structured JSON text, or the first text content block.
        /// </returns>
        internal static string ReadStartedJson(CallToolResult result)
        {
            ArgumentNullException.ThrowIfNull(result);

            if (result.StructuredContent is { } json)
            {
                return json.GetRawText();
            }

            foreach (var block in result.Content ?? [])
            {
                if (block is TextContentBlock text && !string.IsNullOrWhiteSpace(text.Text))
                {
                    return text.Text;
                }
            }

            return string.Empty;
        }

        /// <summary>
        /// Reads the related task id from request metadata.
        /// </summary>
        /// <param name="request">The call request.</param>
        /// <returns>
        /// The task id, or empty when absent.
        /// </returns>
        internal static string ReadTaskId(RequestContext<CallToolRequestParams> request)
        {
            ArgumentNullException.ThrowIfNull(request);

            if (request.Params?.Meta is not { } meta
                || !meta.TryGetPropertyValue(TasksProtocol.MetaRelatedTask, out var node)
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

        /// <summary>
        /// Reads a ticket <c>message</c> from the Started JSON when present.
        /// </summary>
        /// <param name="result">The one-shot result.</param>
        /// <returns>
        /// The message, or <c>null</c>.
        /// </returns>
        internal static string? ReadTicketMessage(CallToolResult result)
        {
            ArgumentNullException.ThrowIfNull(result);

            if (!TryGetResultJson(result, out var json) || json.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            foreach (var property in json.EnumerateObject())
            {
                if (string.Equals(property.Name, "message", StringComparison.OrdinalIgnoreCase)
                    && property.Value.ValueKind == JsonValueKind.String)
                {
                    var message = property.Value.GetString();
                    return string.IsNullOrWhiteSpace(message) ? null : message;
                }
            }

            if (json.TryGetProperty("value", out var nested) && nested.ValueKind == JsonValueKind.Object)
            {
                foreach (var property in nested.EnumerateObject())
                {
                    if (string.Equals(property.Name, "message", StringComparison.OrdinalIgnoreCase)
                        && property.Value.ValueKind == JsonValueKind.String)
                    {
                        var message = property.Value.GetString();
                        return string.IsNullOrWhiteSpace(message) ? null : message;
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Throws <see cref="InputRequiredException"/> so the SDK returns MRTR <c>inputRequests</c>
        /// for the URL elicitation.
        /// </summary>
        /// <param name="result">The Started ticket stored as <c>requestState</c>.</param>
        /// <param name="url">The URL to elicit.</param>
        [DoesNotReturn]
        internal static void ThrowUrlElicitation(CallToolResult result, string url)
        {
            ArgumentNullException.ThrowIfNull(result);
            ArgumentException.ThrowIfNullOrWhiteSpace(url);

            throw new InputRequiredException(
                inputRequests: new Dictionary<string, InputRequest>
                {
                    [ElicitInputKey] = InputRequest.ForElicitation(CreateUrlElicitParams(result, url))
                },
                requestState: ReadStartedJson(result));
        }

        /// <summary>
        /// Parses structured content or the first text block as JSON.
        /// </summary>
        /// <param name="result">The tool result.</param>
        /// <param name="json">The parsed JSON when successful.</param>
        /// <returns>
        /// <c>true</c> when JSON was parsed.
        /// </returns>
        internal static bool TryGetResultJson(CallToolResult result, out JsonElement json)
        {
            if (result.StructuredContent is { } structured)
            {
                json = structured;
                return true;
            }

            foreach (var block in result.Content ?? [])
            {
                if (block is TextContentBlock text && !string.IsNullOrWhiteSpace(text.Text))
                {
                    try
                    {
                        using var document = JsonDocument.Parse(text.Text);
                        json = document.RootElement.Clone();
                        return true;
                    }
                    catch (JsonException)
                    {
                        break;
                    }
                }
            }

            json = default;
            return false;
        }

        /// <summary>
        /// Resolves the tasked EDM action for this call.
        /// </summary>
        /// <param name="context">The call request.</param>
        /// <param name="action">The tasked action when found.</param>
        /// <returns>
        /// <c>true</c> when the catalog tool is a tasked action.
        /// </returns>
        internal static bool TryGetTaskedAction(RequestContext<CallToolRequestParams> context, out Microsoft.OData.Mcp.Core.Models.EdmAction? action)
        {
            action = null;
            var name = context.Params?.Name;
            if (string.IsNullOrWhiteSpace(name) || context.Services is null)
            {
                return false;
            }

            var factory = context.Services.GetService<ODataMcpSessionFactory>();
            if (factory is null)
            {
                return false;
            }

            try
            {
                var http = context.Services.GetService<IHttpContextAccessor>()?.HttpContext;
                var session = factory.Resolve(http);
                return session.Catalog.TryGetTaskedAction(name, AsReadOnly(context.Params?.Arguments), out action);
            }
            catch (InvalidOperationException)
            {
                return false;
            }
        }

        /// <summary>
        /// Returns whether <paramref name="value"/> is an absolute http or https URI.
        /// </summary>
        /// <param name="value">A JSON value.</param>
        /// <param name="url">The URI string when valid.</param>
        /// <returns>
        /// <c>true</c> when the value is an absolute http(s) URI.
        /// </returns>
        internal static bool TryReadAbsoluteHttpUrl(JsonElement value, out string? url)
        {
            url = null;
            if (value.ValueKind != JsonValueKind.String)
            {
                return false;
            }

            var text = value.GetString();
            if (string.IsNullOrWhiteSpace(text)
                || !Uri.TryCreate(text, UriKind.Absolute, out var uri)
                || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            {
                return false;
            }

            url = uri.AbsoluteUri;
            return true;
        }

        /// <summary>
        /// Reads the URL elicitation response from an MRTR retry.
        /// </summary>
        /// <param name="request">The call request.</param>
        /// <param name="elicit">The deserialized result when present.</param>
        /// <returns>
        /// <c>true</c> when <see cref="ElicitInputKey"/> is in <c>inputResponses</c>.
        /// </returns>
        internal static bool TryReadElicitResponse(RequestContext<CallToolRequestParams> request, out ElicitResult elicit)
        {
            ArgumentNullException.ThrowIfNull(request);

            elicit = new ElicitResult();
            if (request.Params?.InputResponses is not { } responses
                || !responses.TryGetValue(ElicitInputKey, out var response))
            {
                return false;
            }

            elicit = response.Deserialize(InputResponse.ElicitResultJsonTypeInfo) ?? new ElicitResult();
            return true;
        }

        /// <summary>
        /// Waits for the host callback after the operator accepted the URL, or returns
        /// <paramref name="result"/> when no waiter is registered.
        /// </summary>
        /// <param name="request">The call request.</param>
        /// <param name="action">The tasked action.</param>
        /// <param name="result">The Started ticket.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>
        /// The terminal tool result.
        /// </returns>
        internal static async ValueTask<CallToolResult> WaitForHostAsync(
            RequestContext<CallToolRequestParams> request,
            EdmAction action,
            CallToolResult result,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);
            ArgumentNullException.ThrowIfNull(action);
            ArgumentNullException.ThrowIfNull(result);

            var waiter = request.Services?.GetService<IMcpAnnotatedActionWaiter>();
            if (waiter is null)
            {
                return result;
            }

            var terminal = await waiter.WaitAsync(new McpAnnotatedActionContext
            {
                ActionName = action.Name,
                BindingKey = ReadBindingKey(request.Params?.Arguments),
                StartedJson = ReadStartedJson(result),
                TaskId = ReadTaskId(request)
            }, cancellationToken).ConfigureAwait(false);

            return terminal ?? result;
        }

        #endregion

    }

}
