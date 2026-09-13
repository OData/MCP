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
using Microsoft.OData.Mcp.Tools.Hosting;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace Microsoft.OData.Mcp.Tests.Tools
{

    /// <summary>
    /// Tests for <see cref="ShutdownServerTool"/> and the Tools-host extra-tool handler.
    /// </summary>
    [TestClass]
    public class ShutdownServerToolTests
    {

        #region Public Methods

        /// <summary>
        /// Omitting <c>delay_seconds</c> uses the documented default of 2 seconds.
        /// </summary>
        [TestMethod]
        public async Task HandleShutdown_OmittedDelaySeconds_UsesDefaultTwo()
        {
            using var cts = new CancellationTokenSource();
            var request = UninitializedCall("shutdown_server", null);

            var result = await ToolsMcpHost.HandleShutdownAsync(request, new ShutdownServerTool(cts), CancellationToken.None);

            result.Should().NotBeNull();
            var text = result!.Content.OfType<TextContentBlock>().Single().Text;
            text.Should().Contain("2 second(s)");
            text.Should().Contain("User requested shutdown");
            await Task.Delay(200);
            cts.IsCancellationRequested.Should().BeFalse();
            await WaitForCancellationAsync(cts, TimeSpan.FromSeconds(3));
            cts.IsCancellationRequested.Should().BeTrue();
        }

        /// <summary>
        /// Delay 11 is outside the allowed 0–10 range.
        /// </summary>
        [TestMethod]
        public async Task ShutdownServerTool_Delay11_ThrowsArgumentOutOfRange()
        {
            using var cts = new CancellationTokenSource();
            var act = async () => await new ShutdownServerTool(cts).InvokeAsync("x", 11, CancellationToken.None);

            await act.Should().ThrowAsync<ArgumentOutOfRangeException>()
                .WithParameterName("delaySeconds");
        }

        /// <summary>
        /// Delay -1 is outside the allowed 0–10 range.
        /// </summary>
        [TestMethod]
        public async Task ShutdownServerTool_DelayNegativeOne_ThrowsArgumentOutOfRange()
        {
            using var cts = new CancellationTokenSource();
            var act = async () => await new ShutdownServerTool(cts).InvokeAsync("x", -1, CancellationToken.None);

            await act.Should().ThrowAsync<ArgumentOutOfRangeException>()
                .WithParameterName("delaySeconds");
        }

        /// <summary>
        /// Null and whitespace reasons fall back to the default message.
        /// </summary>
        [TestMethod]
        public async Task ShutdownServerTool_NullReason_UsesDefaultMessage()
        {
            using var nullCts = new CancellationTokenSource();
            using var whitespaceCts = new CancellationTokenSource();

            var nullPayload = await new ShutdownServerTool(nullCts).InvokeAsync(null, 0, CancellationToken.None);
            var whitespacePayload = await new ShutdownServerTool(whitespaceCts).InvokeAsync("  ", 0, CancellationToken.None);

            AssertDefaultAck(nullPayload, 0);
            AssertDefaultAck(whitespacePayload, 0);
            nullCts.IsCancellationRequested.Should().BeTrue();
            whitespaceCts.IsCancellationRequested.Should().BeTrue();
        }

        /// <summary>
        /// A supplied reason is optional and appears in the JSON acknowledgement.
        /// </summary>
        [TestMethod]
        public async Task ShutdownServerTool_ReasonProvided_AppearsInAck()
        {
            using var cts = new CancellationTokenSource();
            var payload = await new ShutdownServerTool(cts).InvokeAsync("test", 0, CancellationToken.None);

            using var document = JsonDocument.Parse(payload);
            var message = document.RootElement.GetProperty("message").GetString();
            message.Should().NotBeNullOrWhiteSpace();
            message.Should().Contain("test");
            message.Should().Contain("0 second(s)");
            cts.IsCancellationRequested.Should().BeTrue();
        }

        #endregion

        #region Internal Methods

        /// <summary>
        /// Asserts the default shutdown acknowledgement JSON.
        /// </summary>
        /// <param name="payload">The tool payload.</param>
        /// <param name="delaySeconds">The expected delay in the message.</param>
        internal static void AssertDefaultAck(string payload, int delaySeconds)
        {
            using var document = JsonDocument.Parse(payload);
            var message = document.RootElement.GetProperty("message").GetString();
            message.Should().NotBeNullOrWhiteSpace();
            message.Should().Contain("User requested shutdown");
            message.Should().Contain($"{delaySeconds} second(s)");
        }

        /// <summary>
        /// Builds an uninitialized call-tool request.
        /// </summary>
        /// <param name="name">The tool name.</param>
        /// <param name="arguments">Tool arguments.</param>
        /// <returns>
        /// The request context.
        /// </returns>
        internal static RequestContext<CallToolRequestParams> UninitializedCall(string? name, Dictionary<string, JsonElement>? arguments)
        {
            var request = (RequestContext<CallToolRequestParams>)RuntimeHelpers.GetUninitializedObject(typeof(RequestContext<CallToolRequestParams>));
            if (name is not null)
            {
                request.Params = new CallToolRequestParams
                {
                    Arguments = arguments,
                    Name = name
                };
            }

            return request;
        }

        /// <summary>
        /// Waits until <paramref name="cts"/> is cancelled or <paramref name="timeout"/> elapses.
        /// </summary>
        /// <param name="cts">The lifetime source.</param>
        /// <param name="timeout">The maximum wait.</param>
        /// <returns>
        /// A task that completes when cancelled or timed out.
        /// </returns>
        internal static async Task WaitForCancellationAsync(CancellationTokenSource cts, TimeSpan timeout)
        {
            var deadline = DateTime.UtcNow + timeout;
            while (!cts.IsCancellationRequested && DateTime.UtcNow < deadline)
            {
                await Task.Delay(50);
            }
        }

        #endregion

    }

}
