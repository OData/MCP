// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.OData.Mcp.Tools.Hosting
{

    /// <summary>
    /// Local-only control-plane tool that cancels the host lifetime after an optional delay.
    /// </summary>
    public sealed class ShutdownServerTool
    {

        #region Fields

        internal readonly CancellationTokenSource _lifetime;

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="ShutdownServerTool"/> class.
        /// </summary>
        /// <param name="lifetime">The host lifetime source to cancel.</param>
        public ShutdownServerTool(CancellationTokenSource lifetime)
        {
            ArgumentNullException.ThrowIfNull(lifetime);

            _lifetime = lifetime;
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Acknowledges shutdown and cancels the host after <paramref name="delaySeconds"/>.
        /// </summary>
        /// <param name="reason">The reason recorded in the acknowledgement.</param>
        /// <param name="delaySeconds">Delay between 0 and 10 seconds. Zero cancels immediately.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>
        /// JSON acknowledgement text.
        /// </returns>
        public async Task<string> InvokeAsync(string? reason, int delaySeconds, CancellationToken cancellationToken)
        {
            if (delaySeconds < 0 || delaySeconds > 10)
            {
                throw new ArgumentOutOfRangeException(nameof(delaySeconds), "Delay must be between 0 and 10 seconds.");
            }

            var text = string.IsNullOrWhiteSpace(reason) ? "User requested shutdown" : reason.Trim();
            var payload = $$"""{"message":"Server shutdown initiated. Reason: {{JsonEncodedText.Encode(text)}}. Shutting down in {{delaySeconds}} second(s)."}""";

            if (delaySeconds == 0)
            {
                _lifetime.Cancel();

                return payload;
            }

            _ = Task.Run(async () =>
            {
                await Task.Delay(TimeSpan.FromSeconds(delaySeconds), CancellationToken.None).ConfigureAwait(false);
                _lifetime.Cancel();
            }, CancellationToken.None);

            await Task.CompletedTask.ConfigureAwait(false);

            return payload;
        }

        #endregion

    }

}
