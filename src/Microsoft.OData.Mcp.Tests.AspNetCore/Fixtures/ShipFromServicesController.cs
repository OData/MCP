// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OData.Formatter;
using Microsoft.AspNetCore.OData.Routing.Controllers;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace Microsoft.OData.Mcp.Tests.AspNetCore.Fixtures
{

    /// <summary>
    /// Unbound <c>ShipFromServices</c> action that optionally takes the MCP <c>tools/call</c>
    /// from DI. Used to see whether <c>[FromServices]</c> stays out of CSDL and injects on MCP.
    /// </summary>
    public sealed class ShipFromServicesController : ODataController
    {

        #region Public Methods

        /// <summary>
        /// Returns whether an MCP call was injected, plus the tracking number from the body.
        /// </summary>
        /// <param name="parameters">OData action arguments.</param>
        /// <param name="call">The current <c>tools/call</c> when this invoke came from MCP.</param>
        /// <returns>
        /// JSON with <c>injected</c>, <c>tool</c>, and <c>trackingNumber</c>.
        /// </returns>
        [HttpPost("odata/ShipFromServices")]
        [HttpPost("odata/ShipFromServices()")]
        public IActionResult Post(
            [FromBody] ODataActionParameters parameters,
            [FromServices] RequestContext<CallToolRequestParams>? call = null)
        {
            string? tracking = null;
            if (parameters is not null
                && parameters.TryGetValue("trackingNumber", out var value)
                && value is string text)
            {
                tracking = text;
            }

            return Ok(new Dictionary<string, object?>
            {
                ["injected"] = call is not null,
                ["tool"] = call?.Params?.Name,
                ["trackingNumber"] = tracking
            });
        }

        #endregion

    }

}
