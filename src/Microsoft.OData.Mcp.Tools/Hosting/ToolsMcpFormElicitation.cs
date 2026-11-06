// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OData.Mcp.Core.Catalog;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace Microsoft.OData.Mcp.Tools.Hosting
{

    /// <summary>
    /// Tools-host before-invoke form elicitation. Projects the Core DTO; does not reference McpEssentials.
    /// </summary>
    internal static class ToolsMcpFormElicitation
    {

        #region Internal Methods

        /// <summary>
        /// Elicits a form from <c>EdmAction.ElicitForm</c> before the remote OData invoke.
        /// </summary>
        /// <param name="request">The call request.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>
        /// Merged call params, or <see langword="null"/> when elicitation does not apply.
        /// </returns>
        internal static async ValueTask<CallToolRequestParams?> BeforeInvokeAsync(
            RequestContext<CallToolRequestParams> request,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            var parameters = request.Params;
            if (parameters is null || string.IsNullOrWhiteSpace(parameters.Name) || request.Services is null)
            {
                return null;
            }

            var holder = request.Services.GetService<ToolsMcpSessionHolder>();
            var session = holder?.Session;
            if (session is null)
            {
                return null;
            }

            if (!session.Catalog.TryGetElicitFormAction(parameters.Name, AsReadOnly(parameters.Arguments), out var action))
            {
                return null;
            }

            return await ODataMcpFormElicitation.BeforeInvokeAsync(request, action.ElicitForm, buildLive: null, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Copies call arguments to a read-only dictionary when needed.
        /// </summary>
        /// <param name="arguments">Call arguments.</param>
        /// <returns>A read-only view, or <c>null</c>.</returns>
        internal static IReadOnlyDictionary<string, JsonElement>? AsReadOnly(IDictionary<string, JsonElement>? arguments)
        {
            return arguments switch
            {
                null => null,
                IReadOnlyDictionary<string, JsonElement> readOnly => readOnly,
                _ => new Dictionary<string, JsonElement>(arguments)
            };
        }

        #endregion

    }

}
