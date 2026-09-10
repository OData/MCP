// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OData.Routing.Controllers;

namespace Microsoft.OData.Mcp.Tests.AspNetCore.RateLimit
{

    /// <summary>
    /// Unbound function used to rate-limit operations separately from entity sets.
    /// </summary>
    public sealed class MostValuableController : ODataController
    {

        #region Public Methods

        /// <summary>
        /// Returns a constant function result.
        /// </summary>
        /// <returns>
        /// 42.
        /// </returns>
        [HttpGet("odata/MostValuable")]
        [HttpGet("odata/MostValuable()")]
        public IActionResult Get()
        {
            return Ok(42);
        }

        #endregion

    }

}
