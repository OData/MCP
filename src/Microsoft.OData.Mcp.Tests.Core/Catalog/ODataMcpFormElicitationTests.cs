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
using Microsoft.OData.Mcp.Core.Catalog;
using Microsoft.OData.Mcp.Core.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace Microsoft.OData.Mcp.Tests.Core.Catalog
{

    /// <summary>
    /// Before-invoke form elicitation: client gate, merge, MRTR, decline.
    /// </summary>
    [TestClass]
    public class ODataMcpFormElicitationTests
    {

        #region Public Methods

        /// <summary>
        /// Request <c>_meta</c> with <c>elicitation.form</c> is form elicitation support.
        /// </summary>
        [TestMethod]
        public void ClientHasFormElicitation_RequestMeta_IsTrue()
        {
            var request = Context(new CallToolRequestParams
            {
                Name = "action_ship",
                Meta = FormMeta()
            });

            ODataMcpFormElicitation.ClientHasFormElicitation(request).Should().BeTrue();
            ODataMcpFormElicitation.HasFormElicitation(request.Params?.Meta).Should().BeTrue();
        }

        /// <summary>
        /// URL elicitation alone is not form elicitation.
        /// </summary>
        [TestMethod]
        public void ClientHasFormElicitation_UrlOnly_IsFalse()
        {
            var request = Context(new CallToolRequestParams
            {
                Name = "action_ship",
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

            ODataMcpFormElicitation.ClientHasFormElicitation(request).Should().BeFalse();
        }

        /// <summary>
        /// Null request is rejected.
        /// </summary>
        [TestMethod]
        public void ClientHasFormElicitation_Null_ThrowsArgumentNullException()
        {
            var act = () => ODataMcpFormElicitation.ClientHasFormElicitation(null!);
            act.Should().Throw<ArgumentNullException>().WithParameterName("request");
        }

        /// <summary>
        /// A missing form is a no-op.
        /// </summary>
        [TestMethod]
        public async Task BeforeInvokeAsync_NullForm_ReturnsNull()
        {
            var request = Context(new CallToolRequestParams { Name = "action_ship", Meta = FormMeta() });
            var result = await ODataMcpFormElicitation.BeforeInvokeAsync(request, form: null, buildLive: null, CancellationToken.None);
            result.Should().BeNull();
        }

        /// <summary>
        /// Without form capability the action invokes with the arguments as sent.
        /// </summary>
        [TestMethod]
        public async Task BeforeInvokeAsync_NoCapability_ReturnsNull()
        {
            var request = Context(new CallToolRequestParams { Name = "action_ship" });
            var result = await ODataMcpFormElicitation.BeforeInvokeAsync(request, ShipForm(), buildLive: null, CancellationToken.None);
            result.Should().BeNull();
        }

        /// <summary>
        /// An empty form message fails first at elicit time.
        /// </summary>
        [TestMethod]
        public async Task BeforeInvokeAsync_EmptyMessage_ThrowsInvalidOperationException()
        {
            await using var session = await InterceptedSessionAsync((_, _, _) => new ValueTask<JsonNode?>(JsonNode.Parse("{}")));
            var request = ContextWithServer(session.Server, new CallToolRequestParams { Name = "action_ship", Meta = FormMeta() });
            var act = async () => await ODataMcpFormElicitation.BeforeInvokeAsync(
                request,
                new EdmElicitForm(),
                buildLive: null,
                CancellationToken.None);

            await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*message cannot be empty*");
        }

        /// <summary>
        /// Accepted MRTR content merges into tool arguments.
        /// </summary>
        [TestMethod]
        public async Task BeforeInvokeAsync_AcceptedRetry_MergesArguments()
        {
            var request = Context(new CallToolRequestParams
            {
                Name = "action_ship",
                Arguments = new Dictionary<string, JsonElement>
                {
                    ["key"] = JsonSerializer.SerializeToElement("1")
                },
                InputResponses = new Dictionary<string, InputResponse>
                {
                    [ODataMcpFormElicitation.InputKey] = InputResponse.FromElicitResult(new ElicitResult
                    {
                        Action = "accept",
                        Content = new Dictionary<string, JsonElement>
                        {
                            ["trackingNumber"] = JsonSerializer.SerializeToElement("1Z999")
                        }
                    })
                }
            });

            var merged = await ODataMcpFormElicitation.BeforeInvokeAsync(request, ShipForm(), buildLive: null, CancellationToken.None);

            merged.Should().NotBeNull();
            merged!.Arguments!["trackingNumber"].GetString().Should().Be("1Z999");
            merged.Arguments["key"].GetString().Should().Be("1");
        }

        /// <summary>
        /// Decline does not POST.
        /// </summary>
        [TestMethod]
        public async Task BeforeInvokeAsync_Decline_ThrowsDeclinedException()
        {
            var request = Context(new CallToolRequestParams
            {
                Name = "action_ship",
                InputResponses = new Dictionary<string, InputResponse>
                {
                    [ODataMcpFormElicitation.InputKey] = InputResponse.FromElicitResult(new ElicitResult { Action = "decline" })
                }
            });

            var act = async () => await ODataMcpFormElicitation.BeforeInvokeAsync(request, ShipForm(), null, CancellationToken.None);
            var declined = await act.Should().ThrowAsync<ODataFormElicitationDeclinedException>();
            declined.Which.Result.IsError.Should().BeTrue();
            declined.Which.Result.Content[0].Should().BeOfType<TextContentBlock>()
                .Which.Text.Should().Be("The operator declined the form.");
        }

        /// <summary>
        /// Cancel is the same decline path.
        /// </summary>
        [TestMethod]
        public async Task BeforeInvokeAsync_Cancel_ThrowsDeclinedException()
        {
            var request = Context(new CallToolRequestParams
            {
                Name = "action_ship",
                InputResponses = new Dictionary<string, InputResponse>
                {
                    [ODataMcpFormElicitation.InputKey] = InputResponse.FromElicitResult(new ElicitResult { Action = "cancel" })
                }
            });

            var act = async () => await ODataMcpFormElicitation.BeforeInvokeAsync(request, ShipForm(), null, CancellationToken.None);
            await act.Should().ThrowAsync<ODataFormElicitationDeclinedException>();
        }

        /// <summary>
        /// Merge writes into nested <c>parameters</c> when present.
        /// </summary>
        [TestMethod]
        public void Merge_WritesNestedParameters()
        {
            var parameters = new CallToolRequestParams
            {
                Name = "odata_call",
                Arguments = new Dictionary<string, JsonElement>
                {
                    ["name"] = JsonSerializer.SerializeToElement("Ship"),
                    ["parameters"] = JsonDocument.Parse("""{"note":"keep"}""").RootElement.Clone()
                }
            };
            var elicit = new ElicitResult
            {
                Action = "accept",
                Content = new Dictionary<string, JsonElement>
                {
                    ["trackingNumber"] = JsonSerializer.SerializeToElement("1Z")
                }
            };

            var merged = ODataMcpFormElicitation.Merge(parameters, elicit);
            merged.Arguments!["trackingNumber"].GetString().Should().Be("1Z");
            merged.Arguments["parameters"].GetProperty("note").GetString().Should().Be("keep");
            merged.Arguments["parameters"].GetProperty("trackingNumber").GetString().Should().Be("1Z");
        }

        /// <summary>
        /// Merge restores arguments from <c>requestState</c> when Arguments is empty.
        /// </summary>
        [TestMethod]
        public void CopyArguments_RestoresRequestState()
        {
            var parameters = new CallToolRequestParams
            {
                Name = "action_ship",
                RequestState = """{"key":"1"}"""
            };

            var copy = ODataMcpFormElicitation.CopyArguments(parameters);
            copy["key"].GetString().Should().Be("1");
        }

        /// <summary>
        /// Malformed requestState yields empty arguments.
        /// </summary>
        [TestMethod]
        public void CopyArguments_MalformedRequestState_IsEmpty()
        {
            var parameters = new CallToolRequestParams
            {
                Name = "action_ship",
                RequestState = "not-json"
            };

            ODataMcpFormElicitation.CopyArguments(parameters).Should().BeEmpty();
        }

        /// <summary>
        /// Empty arguments serialize as an empty object.
        /// </summary>
        [TestMethod]
        public void SerializeArguments_Empty_IsObject()
        {
            ODataMcpFormElicitation.SerializeArguments(null).Should().Be("{}");
            ODataMcpFormElicitation.SerializeArguments(new Dictionary<string, JsonElement>()).Should().Be("{}");
        }

        /// <summary>
        /// Non-empty arguments round-trip through requestState JSON.
        /// </summary>
        [TestMethod]
        public void SerializeArguments_WritesProperties()
        {
            var json = ODataMcpFormElicitation.SerializeArguments(new Dictionary<string, JsonElement>
            {
                ["key"] = JsonSerializer.SerializeToElement("1")
            });

            json.Should().Contain("\"key\"");
            json.Should().Contain("1");
        }

        /// <summary>
        /// IsFormRetry is true only when the form key is present.
        /// </summary>
        [TestMethod]
        public void IsFormRetry_RequiresFormKey()
        {
            var retry = Context(new CallToolRequestParams
            {
                Name = "action_ship",
                InputResponses = new Dictionary<string, InputResponse>
                {
                    [ODataMcpFormElicitation.InputKey] = InputResponse.FromElicitResult(new ElicitResult { Action = "accept" })
                }
            });
            var other = Context(new CallToolRequestParams
            {
                Name = "action_ship",
                InputResponses = new Dictionary<string, InputResponse>
                {
                    ["odata.elicitUrl"] = InputResponse.FromElicitResult(new ElicitResult { Action = "accept" })
                }
            });

            ODataMcpFormElicitation.IsFormRetry(retry).Should().BeTrue();
            ODataMcpFormElicitation.IsFormRetry(other).Should().BeFalse();
        }

        /// <summary>
        /// TryReadResponse is false when the key is missing.
        /// </summary>
        [TestMethod]
        public void TryReadResponse_MissingKey_IsFalse()
        {
            var request = Context(new CallToolRequestParams { Name = "action_ship" });
            ODataMcpFormElicitation.TryReadResponse(request, out _).Should().BeFalse();
        }

        /// <summary>
        /// Merge rejects null inputs.
        /// </summary>
        [TestMethod]
        public void Merge_Null_Throw()
        {
            var merge = () => ODataMcpFormElicitation.Merge(null!, new ElicitResult());
            var copy = () => ODataMcpFormElicitation.CopyArguments(null!);
            var throwNull = () => ODataMcpFormElicitation.ThrowFormElicitation(null!, null);

            merge.Should().Throw<ArgumentNullException>();
            copy.Should().Throw<ArgumentNullException>();
            throwNull.Should().Throw<ArgumentNullException>();
        }

        /// <summary>
        /// HasFormElicitation is false for missing metadata.
        /// </summary>
        [TestMethod]
        public void HasFormElicitation_NullMeta_IsFalse()
        {
            ODataMcpFormElicitation.HasFormElicitation(null).Should().BeFalse();
            ODataMcpFormElicitation.HasFormElicitation(new JsonObject()).Should().BeFalse();
        }

        /// <summary>
        /// MRTR throw carries the form key and original arguments.
        /// </summary>
        [TestMethod]
        public void ThrowFormElicitation_IncludesKeyAndRequestState()
        {
            var elicit = ElicitFormMapper.ToRequestParams(ShipForm());
            var arguments = new Dictionary<string, JsonElement>
            {
                ["key"] = JsonSerializer.SerializeToElement("ALFKI")
            };

            var act = () => ODataMcpFormElicitation.ThrowFormElicitation(elicit, arguments);
            var thrown = act.Should().Throw<InputRequiredException>().Which;
            thrown.Result.InputRequests.Should().ContainKey(ODataMcpFormElicitation.InputKey);
            thrown.Result.RequestState.Should().Contain("ALFKI");
        }

        /// <summary>
        /// A live builder is used instead of the DTO mapper on the MRTR path.
        /// </summary>
        [TestMethod]
        public async Task BeforeInvokeAsync_LiveBuilder_IsPreferred()
        {
            var used = false;
            await using var session = await InterceptedSessionAsync((_, _, _) => new ValueTask<JsonNode?>(JsonNode.Parse("{}")));
            var request = ContextWithServer(session.Server, new CallToolRequestParams
            {
                Name = "action_ship",
                Meta = FormMeta()
            });

            var act = async () => await ODataMcpFormElicitation.BeforeInvokeAsync(
                request,
                ShipForm(),
                () =>
                {
                    used = true;
                    return ElicitFormMapper.ToRequestParams(ShipForm());
                },
                CancellationToken.None);

            await act.Should().ThrowAsync<InputRequiredException>();
            used.Should().BeTrue();
        }

        /// <summary>
        /// A cached <see cref="InputRequest"/> is thrown as-is on MRTR.
        /// </summary>
        [TestMethod]
        public async Task BeforeInvokeAsync_CachedInputRequest_IsThrownAsIs()
        {
            await using var session = await InterceptedSessionAsync((_, _, _) => new ValueTask<JsonNode?>(JsonNode.Parse("{}")));
            var request = ContextWithServer(session.Server, new CallToolRequestParams
            {
                Name = "action_ship",
                Meta = FormMeta()
            });
            var cached = InputRequest.ForElicitation(ElicitFormMapper.ToRequestParams(ShipForm()));

            var act = async () => await ODataMcpFormElicitation.BeforeInvokeAsync(
                request,
                ShipForm(),
                buildLive: () => throw new InvalidOperationException("session path should not Build"),
                CancellationToken.None,
                () => cached);

            var thrown = (await act.Should().ThrowAsync<InputRequiredException>()).Which;
            thrown.Result.InputRequests.Should().NotBeNull();
            thrown.Result.InputRequests![ODataMcpFormElicitation.InputKey].Should().BeSameAs(cached);
        }

        /// <summary>
        /// CallToolAsync maps a declined form to an error without invoking OData.
        /// </summary>
        [TestMethod]
        public async Task CallToolAsync_FormDeclined_IsErrorWithoutInvoke()
        {
            var executor = new RecordingODataExecutor();
            var catalog = ODataMcpCatalogMoreTests.Catalog();
            var session = new ODataMcpSession(catalog, new ODataToolRuntime(catalog, executor), "{}");
            var result = await ODataMcpHandlers.CallToolAsync(
                ODataMcpHandlerTests.Context(new CallToolRequestParams { Name = "odata_list_entity_sets" }),
                _ => session,
                null,
                null,
                CancellationToken.None,
                afterInvoke: null,
                beforeInvoke: (_, _) => throw new ODataFormElicitationDeclinedException(ODataMcpFormElicitation.DeclinedResult()));

            result.IsError.Should().BeTrue();
            executor.Last.Should().BeNull();
        }

        /// <summary>
        /// CallToolAsync uses merged arguments from before-invoke.
        /// </summary>
        [TestMethod]
        public async Task CallToolAsync_BeforeInvoke_ReplacesArguments()
        {
            CallToolRequestParams? seen = null;
            var result = await ODataMcpHandlers.CallToolAsync(
                ODataMcpHandlerTests.Context(new CallToolRequestParams
                {
                    Name = "odata_list_entity_sets",
                    Arguments = new Dictionary<string, JsonElement>()
                }),
                _ => ODataMcpHandlerTests.Session(),
                null,
                null,
                CancellationToken.None,
                afterInvoke: null,
                beforeInvoke: (request, _) =>
                {
                    seen = new CallToolRequestParams
                    {
                        Name = request.Params!.Name,
                        Arguments = new Dictionary<string, JsonElement>
                        {
                            ["trackingNumber"] = JsonSerializer.SerializeToElement("1Z")
                        }
                    };
                    return ValueTask.FromResult<CallToolRequestParams?>(seen);
                });

            result.IsError.Should().BeFalse();
            seen.Should().NotBeNull();
        }

        /// <summary>
        /// A form MRTR retry still invokes OData instead of taking the URL skip-invoke path.
        /// </summary>
        [TestMethod]
        public async Task CallToolAsync_FormRetry_StillInvokes()
        {
            var invoked = false;
            var result = await ODataMcpHandlers.CallToolAsync(
                ODataMcpHandlerTests.Context(new CallToolRequestParams
                {
                    Name = "odata_list_entity_sets",
                    RequestState = """{"key":"1"}""",
                    InputResponses = new Dictionary<string, InputResponse>
                    {
                        [ODataMcpFormElicitation.InputKey] = InputResponse.FromElicitResult(new ElicitResult { Action = "accept" })
                    }
                }),
                _ =>
                {
                    invoked = true;
                    return ODataMcpHandlerTests.Session();
                },
                null,
                null,
                CancellationToken.None,
                afterInvoke: (request, callResult, _) => ValueTask.FromResult(callResult),
                beforeInvoke: (request, _) => ValueTask.FromResult<CallToolRequestParams?>(request.Params));

            invoked.Should().BeTrue();
            result.IsError.Should().BeFalse();
        }

        /// <summary>
        /// URL requestState without a form key still skips invoke.
        /// </summary>
        [TestMethod]
        public async Task CallToolAsync_UrlRequestState_StillSkipsInvoke()
        {
            var invoked = false;
            const string started = """{"status":"Started"}""";
            await ODataMcpHandlers.CallToolAsync(
                ODataMcpHandlerTests.Context(new CallToolRequestParams
                {
                    Name = "ghost",
                    RequestState = started
                }),
                _ =>
                {
                    invoked = true;
                    return ODataMcpHandlerTests.Session();
                },
                null,
                null,
                CancellationToken.None,
                afterInvoke: (request, callResult, _) => ValueTask.FromResult(callResult));

            invoked.Should().BeFalse();
        }

        /// <summary>
        /// A missing <c>Params</c> is a no-op.
        /// </summary>
        [TestMethod]
        public async Task BeforeInvokeAsync_NullParams_ReturnsNull()
        {
            var request = (RequestContext<CallToolRequestParams>)RuntimeHelpers.GetUninitializedObject(typeof(RequestContext<CallToolRequestParams>));
            var result = await ODataMcpFormElicitation.BeforeInvokeAsync(request, ShipForm(), null, CancellationToken.None);
            result.Should().BeNull();
        }

        /// <summary>
        /// A live builder that returns null fails first.
        /// </summary>
        [TestMethod]
        public async Task BeforeInvokeAsync_LiveBuilderNull_ThrowsArgumentNullException()
        {
            await using var session = await InterceptedSessionAsync((_, _, _) => new ValueTask<JsonNode?>(JsonNode.Parse("{}")));
            var request = ContextWithServer(session.Server, new CallToolRequestParams { Name = "action_ship", Meta = FormMeta() });
            var act = async () => await ODataMcpFormElicitation.BeforeInvokeAsync(request, ShipForm(), () => null!, CancellationToken.None);
            await act.Should().ThrowAsync<ArgumentNullException>();
        }

        /// <summary>
        /// Non-object requestState does not restore arguments.
        /// </summary>
        [TestMethod]
        public void CopyArguments_ArrayRequestState_IsEmpty()
        {
            var parameters = new CallToolRequestParams { Name = "action_ship", RequestState = "[]" };
            ODataMcpFormElicitation.CopyArguments(parameters).Should().BeEmpty();
        }

        /// <summary>
        /// Merge overwrites a field already present in nested parameters.
        /// </summary>
        [TestMethod]
        public void Merge_OverwritesExistingNestedParameter()
        {
            var parameters = new CallToolRequestParams
            {
                Name = "odata_call",
                Arguments = new Dictionary<string, JsonElement>
                {
                    ["parameters"] = JsonDocument.Parse("""{"trackingNumber":"OLD","note":"keep"}""").RootElement.Clone()
                }
            };

            var merged = ODataMcpFormElicitation.Merge(parameters, new ElicitResult
            {
                Action = "accept",
                Content = new Dictionary<string, JsonElement>
                {
                    ["trackingNumber"] = JsonSerializer.SerializeToElement("NEW")
                }
            });

            merged.Arguments!["parameters"].GetProperty("trackingNumber").GetString().Should().Be("NEW");
            merged.Arguments["parameters"].GetProperty("note").GetString().Should().Be("keep");
        }

        /// <summary>
        /// A null elicitation payload on retry is treated as cancel.
        /// </summary>
        [TestMethod]
        public async Task BeforeInvokeAsync_NullElicitPayload_ThrowsDeclinedException()
        {
            var request = Context(new CallToolRequestParams
            {
                Name = "action_ship",
                InputResponses = new Dictionary<string, InputResponse>
                {
                    [ODataMcpFormElicitation.InputKey] = new InputResponse
                    {
                        RawValue = JsonDocument.Parse("null").RootElement.Clone()
                    }
                }
            });

            var act = async () => await ODataMcpFormElicitation.BeforeInvokeAsync(request, ShipForm(), null, CancellationToken.None);
            await act.Should().ThrowAsync<ODataFormElicitationDeclinedException>();
        }

        /// <summary>
        /// A connected stream server without a task id throws MRTR.
        /// </summary>
        [TestMethod]
        public async Task BeforeInvokeAsync_MrtrServer_ThrowsInputRequiredException()
        {
            await using var session = await InterceptedSessionAsync((_, _, _) => new ValueTask<JsonNode?>(JsonNode.Parse("{}")));
            var request = ContextWithServer(session.Server, new CallToolRequestParams
            {
                Name = "action_ship",
                Meta = FormMeta(),
                Arguments = new Dictionary<string, JsonElement>
                {
                    ["key"] = JsonSerializer.SerializeToElement("1")
                }
            });

            var act = async () => await ODataMcpFormElicitation.BeforeInvokeAsync(request, ShipForm(), null, CancellationToken.None);
            var thrown = (await act.Should().ThrowAsync<InputRequiredException>()).Which;
            thrown.Result.InputRequests.Should().ContainKey(ODataMcpFormElicitation.InputKey);
            thrown.Result.RequestState.Should().Contain("key");
        }

        /// <summary>
        /// Task-scoped elicitation merges accepted content from the live session.
        /// </summary>
        [TestMethod]
        public async Task BeforeInvokeAsync_TaskScope_Accept_MergesContent()
        {
            await using var session = await InterceptedSessionAsync((_, _, _) =>
            {
                var result = new ElicitResult
                {
                    Action = "accept",
                    Content = new Dictionary<string, JsonElement>
                    {
                        ["trackingNumber"] = JsonSerializer.SerializeToElement("1Z999")
                    }
                };
                return new ValueTask<JsonNode?>(JsonSerializer.SerializeToNode(result, McpJsonUtilities.DefaultOptions));
            });
            var request = ContextWithServer(session.Server, new CallToolRequestParams
            {
                Name = "action_ship",
                Meta = TaskFormMeta("task-1"),
                Arguments = new Dictionary<string, JsonElement>
                {
                    ["key"] = JsonSerializer.SerializeToElement("1")
                }
            });

            var merged = await ODataMcpFormElicitation.BeforeInvokeAsync(request, ShipForm(), null, CancellationToken.None);
            merged!.Arguments!["trackingNumber"].GetString().Should().Be("1Z999");
            merged.Arguments["key"].GetString().Should().Be("1");
        }

        /// <summary>
        /// Task-scoped elicitation decline does not POST.
        /// </summary>
        [TestMethod]
        public async Task BeforeInvokeAsync_TaskScope_Decline_ThrowsDeclinedException()
        {
            await using var session = await InterceptedSessionAsync((_, _, _) =>
            {
                var result = new ElicitResult { Action = "decline" };
                return new ValueTask<JsonNode?>(JsonSerializer.SerializeToNode(result, McpJsonUtilities.DefaultOptions));
            });
            var request = ContextWithServer(session.Server, new CallToolRequestParams
            {
                Name = "action_ship",
                Meta = TaskFormMeta("task-1")
            });

            var act = async () => await ODataMcpFormElicitation.BeforeInvokeAsync(request, ShipForm(), null, CancellationToken.None);
            await act.Should().ThrowAsync<ODataFormElicitationDeclinedException>();
        }

        /// <summary>
        /// A null interceptor result is treated as cancel.
        /// </summary>
        [TestMethod]
        public async Task BeforeInvokeAsync_TaskScope_NullResult_ThrowsDeclinedException()
        {
            await using var session = await InterceptedSessionAsync((_, _, _) => new ValueTask<JsonNode?>((JsonNode?)null));
            var request = ContextWithServer(session.Server, new CallToolRequestParams
            {
                Name = "action_ship",
                Meta = TaskFormMeta("task-1")
            });

            var act = async () => await ODataMcpFormElicitation.BeforeInvokeAsync(request, ShipForm(), null, CancellationToken.None);
            await act.Should().ThrowAsync<ODataFormElicitationDeclinedException>();
        }

        /// <summary>
        /// Tasks extension without a related-task id still uses session elicitation.
        /// </summary>
        [TestMethod]
        public async Task BeforeInvokeAsync_TasksExtension_Accept_MergesContent()
        {
            await using var session = await InterceptedSessionAsync((_, _, _) =>
            {
                var result = new ElicitResult
                {
                    Action = "accept",
                    Content = new Dictionary<string, JsonElement>
                    {
                        ["trackingNumber"] = JsonSerializer.SerializeToElement("EXT")
                    }
                };
                return new ValueTask<JsonNode?>(JsonSerializer.SerializeToNode(result, McpJsonUtilities.DefaultOptions));
            });
            var request = ContextWithServer(session.Server, new CallToolRequestParams
            {
                Name = "action_ship",
                Meta = new JsonObject
                {
                    ["io.modelcontextprotocol/clientCapabilities"] = new JsonObject
                    {
                        ["elicitation"] = new JsonObject { ["form"] = new JsonObject() },
                        ["extensions"] = new JsonObject { ["io.modelcontextprotocol/tasks"] = new JsonObject() }
                    }
                }
            });

            var merged = await ODataMcpFormElicitation.BeforeInvokeAsync(request, ShipForm(), null, CancellationToken.None);
            merged!.Arguments!["trackingNumber"].GetString().Should().Be("EXT");
        }

        /// <summary>
        /// A stateless transport error during session elicit skips elicitation.
        /// </summary>
        [TestMethod]
        public async Task BeforeInvokeAsync_TaskScope_Stateless_ReturnsNull()
        {
            await using var session = await InterceptedSessionAsync((_, _, _) => throw new InvalidOperationException("The server is stateless and cannot elicit."));
            var request = ContextWithServer(session.Server, new CallToolRequestParams
            {
                Name = "action_ship",
                Meta = TaskFormMeta("task-1")
            });

            var result = await ODataMcpFormElicitation.BeforeInvokeAsync(request, ShipForm(), null, CancellationToken.None);
            result.Should().BeNull();
        }

        /// <summary>
        /// Related-task metadata as a JSON object still identifies the task.
        /// </summary>
        [TestMethod]
        public async Task BeforeInvokeAsync_RelatedTaskObject_Accepts()
        {
            await using var session = await InterceptedSessionAsync((_, _, _) =>
            {
                var result = new ElicitResult { Action = "accept", Content = new Dictionary<string, JsonElement>() };
                return new ValueTask<JsonNode?>(JsonSerializer.SerializeToNode(result, McpJsonUtilities.DefaultOptions));
            });
            var request = ContextWithServer(session.Server, new CallToolRequestParams
            {
                Name = "action_ship",
                Meta = new JsonObject
                {
                    ["io.modelcontextprotocol/clientCapabilities"] = new JsonObject
                    {
                        ["elicitation"] = new JsonObject { ["form"] = new JsonObject() }
                    },
                    ["io.modelcontextprotocol/related-task"] = new JsonObject { ["taskId"] = "abc" }
                }
            });

            var merged = await ODataMcpFormElicitation.BeforeInvokeAsync(request, ShipForm(), null, CancellationToken.None);
            merged.Should().NotBeNull();
        }

        /// <summary>
        /// A null request is rejected.
        /// </summary>
        [TestMethod]
        public async Task BeforeInvokeAsync_NullRequest_ThrowsArgumentNullException()
        {
            var act = async () => await ODataMcpFormElicitation.BeforeInvokeAsync(null!, ShipForm(), null, CancellationToken.None);
            await act.Should().ThrowAsync<ArgumentNullException>().WithParameterName("request");
        }

        /// <summary>
        /// Accepted content that is omitted leaves original arguments intact.
        /// </summary>
        [TestMethod]
        public void Merge_NullContent_KeepsArguments()
        {
            var parameters = new CallToolRequestParams
            {
                Name = "action_ship",
                Arguments = new Dictionary<string, JsonElement>
                {
                    ["key"] = JsonSerializer.SerializeToElement("1")
                }
            };

            var merged = ODataMcpFormElicitation.Merge(parameters, new ElicitResult { Action = "accept" });
            merged.Arguments!["key"].GetString().Should().Be("1");
        }

        /// <summary>
        /// Whitespace requestState is treated as missing.
        /// </summary>
        [TestMethod]
        public void CopyArguments_WhitespaceRequestState_IsEmpty()
        {
            var parameters = new CallToolRequestParams { Name = "action_ship", RequestState = "  " };
            ODataMcpFormElicitation.CopyArguments(parameters).Should().BeEmpty();
        }

        /// <summary>
        /// IsFormRetry with missing params is false.
        /// </summary>
        [TestMethod]
        public void IsFormRetry_NullParams_IsFalse()
        {
            var request = (RequestContext<CallToolRequestParams>)RuntimeHelpers.GetUninitializedObject(typeof(RequestContext<CallToolRequestParams>));
            ODataMcpFormElicitation.IsFormRetry(request).Should().BeFalse();
        }

        /// <summary>
        /// TryReadResponse and IsFormRetry reject a null request.
        /// </summary>
        [TestMethod]
        public void IsFormRetryAndTryReadResponse_Null_Throw()
        {
            var retry = () => ODataMcpFormElicitation.IsFormRetry(null!);
            var read = () => ODataMcpFormElicitation.TryReadResponse(null!, out _);

            retry.Should().Throw<ArgumentNullException>().WithParameterName("request");
            read.Should().Throw<ArgumentNullException>().WithParameterName("request");
        }

        /// <summary>
        /// MRTR with no original arguments serializes an empty object.
        /// </summary>
        [TestMethod]
        public void ThrowFormElicitation_NullArguments_EmptyRequestState()
        {
            var elicit = ElicitFormMapper.ToRequestParams(ShipForm());
            var act = () => ODataMcpFormElicitation.ThrowFormElicitation(elicit, null);
            act.Should().Throw<InputRequiredException>().Which.Result.RequestState.Should().Be("{}");
        }

        /// <summary>
        /// Related-task metadata that is not a string or object is ignored, so MRTR is used.
        /// </summary>
        [TestMethod]
        public async Task BeforeInvokeAsync_RelatedTaskArray_ThrowsInputRequiredException()
        {
            await using var session = await InterceptedSessionAsync((_, _, _) => new ValueTask<JsonNode?>(JsonNode.Parse("{}")));
            var request = ContextWithServer(session.Server, new CallToolRequestParams
            {
                Name = "action_ship",
                Meta = new JsonObject
                {
                    ["io.modelcontextprotocol/clientCapabilities"] = new JsonObject
                    {
                        ["elicitation"] = new JsonObject { ["form"] = new JsonObject() }
                    },
                    ["io.modelcontextprotocol/related-task"] = new JsonArray("abc")
                }
            });

            var act = async () => await ODataMcpFormElicitation.BeforeInvokeAsync(request, ShipForm(), null, CancellationToken.None);
            await act.Should().ThrowAsync<InputRequiredException>();
        }

        /// <summary>
        /// Tasks extension advertised as a non-object is ignored, so MRTR is used.
        /// </summary>
        [TestMethod]
        public async Task BeforeInvokeAsync_TasksExtensionNotObject_ThrowsInputRequiredException()
        {
            await using var session = await InterceptedSessionAsync((_, _, _) => new ValueTask<JsonNode?>(JsonNode.Parse("{}")));
            var request = ContextWithServer(session.Server, new CallToolRequestParams
            {
                Name = "action_ship",
                Meta = new JsonObject
                {
                    ["io.modelcontextprotocol/clientCapabilities"] = new JsonObject
                    {
                        ["elicitation"] = new JsonObject { ["form"] = new JsonObject() },
                        ["extensions"] = "io.modelcontextprotocol/tasks"
                    }
                }
            });

            var act = async () => await ODataMcpFormElicitation.BeforeInvokeAsync(request, ShipForm(), null, CancellationToken.None);
            await act.Should().ThrowAsync<InputRequiredException>();
        }

        /// <summary>
        /// A connected server without form metadata is not form elicitation.
        /// </summary>
        [TestMethod]
        public async Task ClientHasFormElicitation_ServerWithoutCapabilities_IsFalse()
        {
            await using var session = await InterceptedSessionAsync((_, _, _) => new ValueTask<JsonNode?>(JsonNode.Parse("{}")));
            var request = ContextWithServer(session.Server, new CallToolRequestParams { Name = "action_ship" });
            ODataMcpFormElicitation.ClientHasFormElicitation(request).Should().BeFalse();
        }

        /// <summary>
        /// Missing params is not form elicitation.
        /// </summary>
        [TestMethod]
        public void ClientHasFormElicitation_NullParams_IsFalse()
        {
            var request = (RequestContext<CallToolRequestParams>)RuntimeHelpers.GetUninitializedObject(typeof(RequestContext<CallToolRequestParams>));
            ODataMcpFormElicitation.ClientHasFormElicitation(request).Should().BeFalse();
        }

        /// <summary>
        /// TryReadResponse with missing params is false.
        /// </summary>
        [TestMethod]
        public void TryReadResponse_NullParams_IsFalse()
        {
            var request = (RequestContext<CallToolRequestParams>)RuntimeHelpers.GetUninitializedObject(typeof(RequestContext<CallToolRequestParams>));
            ODataMcpFormElicitation.TryReadResponse(request, out _).Should().BeFalse();
        }

        /// <summary>
        /// A connected stream server with no arguments still throws MRTR.
        /// </summary>
        [TestMethod]
        public async Task BeforeInvokeAsync_MrtrServer_NullArguments_ThrowsInputRequiredException()
        {
            await using var session = await InterceptedSessionAsync((_, _, _) => new ValueTask<JsonNode?>(JsonNode.Parse("{}")));
            var request = ContextWithServer(session.Server, new CallToolRequestParams
            {
                Name = "action_ship",
                Meta = FormMeta()
            });

            var act = async () => await ODataMcpFormElicitation.BeforeInvokeAsync(request, ShipForm(), null, CancellationToken.None);
            (await act.Should().ThrowAsync<InputRequiredException>()).Which.Result.RequestState.Should().Be("{}");
        }

        /// <summary>
        /// Whitespace related-task ids are ignored, so MRTR is used.
        /// </summary>
        [TestMethod]
        public async Task BeforeInvokeAsync_WhitespaceTaskId_ThrowsInputRequiredException()
        {
            await using var session = await InterceptedSessionAsync((_, _, _) => new ValueTask<JsonNode?>(JsonNode.Parse("{}")));
            var request = ContextWithServer(session.Server, new CallToolRequestParams
            {
                Name = "action_ship",
                Meta = TaskFormMeta("   ")
            });

            var act = async () => await ODataMcpFormElicitation.BeforeInvokeAsync(request, ShipForm(), null, CancellationToken.None);
            await act.Should().ThrowAsync<InputRequiredException>();
        }

        /// <summary>
        /// Known client form capability is form elicitation without request <c>_meta</c>.
        /// </summary>
        [TestMethod]
        public async Task ClientHasFormElicitation_ServerFormCapability_IsTrue()
        {
            await using var session = await InterceptedSessionAsync(
                (_, _, _) => new ValueTask<JsonNode?>(JsonNode.Parse("{}")),
                new ClientCapabilities
                {
                    Elicitation = new ElicitationCapability { Form = new FormElicitationCapability() }
                });
            var request = ContextWithServer(session.Server, new CallToolRequestParams { Name = "action_ship" });
            ODataMcpFormElicitation.ClientHasFormElicitation(request).Should().BeTrue();
        }

        /// <summary>
        /// Known client capabilities without elicitation are not form elicitation.
        /// </summary>
        [TestMethod]
        public async Task ClientHasFormElicitation_ServerEmptyCapabilities_IsFalse()
        {
            await using var session = await InterceptedSessionAsync(
                (_, _, _) => new ValueTask<JsonNode?>(JsonNode.Parse("{}")),
                new ClientCapabilities());
            var request = ContextWithServer(session.Server, new CallToolRequestParams { Name = "action_ship" });
            ODataMcpFormElicitation.ClientHasFormElicitation(request).Should().BeFalse();
        }

        /// <summary>
        /// A null cached input request fails first on the MRTR path.
        /// </summary>
        [TestMethod]
        public async Task BeforeInvokeAsync_NullCachedInputRequest_ThrowsArgumentNullException()
        {
            await using var session = await InterceptedSessionAsync((_, _, _) => new ValueTask<JsonNode?>(JsonNode.Parse("{}")));
            var request = ContextWithServer(session.Server, new CallToolRequestParams
            {
                Name = "action_ship",
                Meta = FormMeta()
            });

            var act = async () => await ODataMcpFormElicitation.BeforeInvokeAsync(
                request,
                ShipForm(),
                buildLive: null,
                CancellationToken.None,
                () => null!);
            await act.Should().ThrowAsync<ArgumentNullException>();
        }

        /// <summary>
        /// Known client URL elicitation is not form elicitation.
        /// </summary>
        [TestMethod]
        public async Task ClientHasFormElicitation_ServerUrlCapability_IsFalse()
        {
            await using var session = await InterceptedSessionAsync(
                (_, _, _) => new ValueTask<JsonNode?>(JsonNode.Parse("{}")),
                new ClientCapabilities
                {
                    Elicitation = new ElicitationCapability { Url = new UrlElicitationCapability() }
                });
            var request = ContextWithServer(session.Server, new CallToolRequestParams { Name = "action_ship" });
            ODataMcpFormElicitation.ClientHasFormElicitation(request).Should().BeFalse();
        }

        /// <summary>
        /// Nested parameters that are not an object are left alone on merge.
        /// </summary>
        [TestMethod]
        public void Merge_NonObjectParameters_DoesNotRewrite()
        {
            var parameters = new CallToolRequestParams
            {
                Name = "odata_call",
                Arguments = new Dictionary<string, JsonElement>
                {
                    ["parameters"] = JsonSerializer.SerializeToElement(1)
                }
            };

            var merged = ODataMcpFormElicitation.Merge(parameters, new ElicitResult
            {
                Action = "accept",
                Content = new Dictionary<string, JsonElement>
                {
                    ["trackingNumber"] = JsonSerializer.SerializeToElement("1Z")
                }
            });

            merged.Arguments!["trackingNumber"].GetString().Should().Be("1Z");
            merged.Arguments["parameters"].GetInt32().Should().Be(1);
        }

        /// <summary>
        /// A non-stateless InvalidOperationException from session elicit still fails.
        /// </summary>
        [TestMethod]
        public async Task BeforeInvokeAsync_TaskScope_OtherInvalidOperation_Throws()
        {
            await using var session = await InterceptedSessionAsync((_, _, _) => throw new InvalidOperationException("The client does not support elicitation."));
            var request = ContextWithServer(session.Server, new CallToolRequestParams
            {
                Name = "action_ship",
                Meta = TaskFormMeta("task-1")
            });

            var act = async () => await ODataMcpFormElicitation.BeforeInvokeAsync(request, ShipForm(), null, CancellationToken.None);
            await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*does not support elicitation*");
        }

        #endregion

        #region Internal Methods

        /// <summary>
        /// Builds an uninitialized request context.
        /// </summary>
        /// <param name="parameters">Call parameters.</param>
        /// <returns>The context.</returns>
        internal static RequestContext<CallToolRequestParams> Context(CallToolRequestParams parameters)
        {
            var request = (RequestContext<CallToolRequestParams>)RuntimeHelpers.GetUninitializedObject(typeof(RequestContext<CallToolRequestParams>));
            request.Params = parameters;
            return request;
        }

        /// <summary>
        /// Builds a request context bound to a live interceptor server.
        /// </summary>
        /// <param name="server">The interceptor facade.</param>
        /// <param name="parameters">Call parameters.</param>
        /// <returns>The context.</returns>
        internal static RequestContext<CallToolRequestParams> ContextWithServer(McpServer server, CallToolRequestParams parameters)
        {
            return new RequestContext<CallToolRequestParams>(
                server,
                new JsonRpcRequest { Method = "tools/call", Id = new RequestId("1") },
                parameters);
        }

        /// <summary>
        /// Request metadata advertising form elicitation.
        /// </summary>
        /// <returns>The <c>_meta</c> object.</returns>
        internal static JsonObject FormMeta()
        {
            return new JsonObject
            {
                ["io.modelcontextprotocol/clientCapabilities"] = new JsonObject
                {
                    ["elicitation"] = new JsonObject
                    {
                        ["form"] = new JsonObject()
                    }
                }
            };
        }

        /// <summary>
        /// Creates a live interceptor session.
        /// </summary>
        /// <param name="interceptor">The outgoing-request interceptor.</param>
        /// <param name="clientCapabilities">Optional known client capabilities.</param>
        /// <returns>The disposable session.</returns>
        internal static Task<InterceptedFormSession> InterceptedSessionAsync(
            Func<string, JsonNode?, CancellationToken, ValueTask<JsonNode?>> interceptor,
            ClientCapabilities? clientCapabilities = null)
        {
            return Task.FromResult(InterceptedFormSession.Create(interceptor, clientCapabilities));
        }

        /// <summary>
        /// A kitchen-sink ship form.
        /// </summary>
        /// <returns>The catalog form.</returns>
        internal static EdmElicitForm ShipForm()
        {
            return new EdmElicitForm
            {
                Message = "Ship this order",
                Name = "ship",
                Fields =
                [
                    new EdmElicitFormField { Name = "trackingNumber", Type = EdmElicitFormFieldType.String, Required = true },
                    new EdmElicitFormField { Name = "age", Type = EdmElicitFormFieldType.Integer },
                    new EdmElicitFormField { Name = "on", Type = EdmElicitFormFieldType.Boolean },
                    new EdmElicitFormField
                    {
                        Name = "priority",
                        Type = EdmElicitFormFieldType.Enum,
                        Options = [new EdmElicitFormOption { Value = "low" }, new EdmElicitFormOption { Value = "high" }]
                    },
                    new EdmElicitFormField
                    {
                        Name = "roles",
                        Type = EdmElicitFormFieldType.Multi,
                        Options = [new EdmElicitFormOption { Value = "admin" }, new EdmElicitFormOption { Value = "user" }]
                    }
                ]
            };
        }

        /// <summary>
        /// Form elicitation metadata plus a related task id.
        /// </summary>
        /// <param name="taskId">The related task id.</param>
        /// <returns>The <c>_meta</c> object.</returns>
        internal static JsonObject TaskFormMeta(string taskId)
        {
            var meta = FormMeta();
            meta["io.modelcontextprotocol/related-task"] = taskId;
            return meta;
        }

        #endregion

    }

}
