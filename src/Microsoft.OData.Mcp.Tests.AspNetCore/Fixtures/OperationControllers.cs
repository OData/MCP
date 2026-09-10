// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OData.Routing.Controllers;

namespace Microsoft.OData.Mcp.Tests.AspNetCore.Fixtures
{

    /// <summary>
    /// Unbound function <c>GetStatus(code)</c>.
    /// </summary>
    public sealed class GetStatusController : ODataController
    {

        #region Public Methods

        /// <summary>
        /// Echoes the code argument.
        /// </summary>
        /// <param name="code">The status code.</param>
        /// <returns>
        /// The code.
        /// </returns>
        [HttpGet("odata/GetStatus(code={code})")]
        [HttpGet("odata/GetStatus()")]
        public IActionResult Get(string? code)
        {
            return Ok(string.IsNullOrWhiteSpace(code) ? "unknown" : code);
        }

        #endregion

    }

    /// <summary>
    /// Unbound action <c>Reset</c>.
    /// </summary>
    public sealed class ResetController : ODataController
    {

        #region Public Methods

        /// <summary>
        /// Accepts the unbound action.
        /// </summary>
        /// <returns>
        /// No content.
        /// </returns>
        [HttpPost("odata/Reset")]
        [HttpPost("odata/Reset()")]
        public IActionResult Post()
        {
            return NoContent();
        }

        #endregion

    }

}
