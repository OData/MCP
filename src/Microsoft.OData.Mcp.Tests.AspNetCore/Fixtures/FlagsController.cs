// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OData.Query;
using Microsoft.AspNetCore.OData.Routing.Controllers;
using Microsoft.OData.Mcp.Tests.Shared.Entities;

namespace Microsoft.OData.Mcp.Tests.AspNetCore.Fixtures
{

    /// <summary>
    /// Boolean-keyed Flags entity set for unquoted <c>true</c>/<c>false</c> keys.
    /// </summary>
    public sealed class FlagsController : ODataController
    {

        #region Fields

        internal readonly List<Flag> _flags =
        [
            new Flag
            {
                FlagId = true,
                Name = "On"
            },
            new Flag
            {
                FlagId = false,
                Name = "Off"
            }
        ];

        #endregion

        #region Public Methods

        /// <summary>
        /// Returns the seeded flags.
        /// </summary>
        /// <returns>
        /// The flag query.
        /// </returns>
        [EnableQuery]
        [HttpGet]
        public IQueryable<Flag> Get()
        {
            return _flags.AsQueryable();
        }

        /// <summary>
        /// Returns a flag by boolean key.
        /// </summary>
        /// <param name="key">The flag key.</param>
        /// <returns>
        /// The flag, or not found.
        /// </returns>
        [EnableQuery]
        [HttpGet]
        public IActionResult Get(bool key)
        {
            var flag = _flags.FirstOrDefault(item => item.FlagId == key);

            return flag is null ? NotFound() : Ok(flag);
        }

        #endregion

    }

}
