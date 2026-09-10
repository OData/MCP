// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OData.Query;
using Microsoft.AspNetCore.OData.Routing.Controllers;
using Microsoft.OData.Mcp.Tests.Shared.Entities;

namespace Microsoft.OData.Mcp.Tests.AspNetCore.Fixtures
{

    /// <summary>
    /// Guid-keyed Widgets entity set for <c>FormatKey</c> tests.
    /// </summary>
    public sealed class WidgetsController : ODataController
    {

        #region Fields

        internal readonly List<Widget> _widgets =
        [
            new Widget
            {
                Name = "Alpha",
                WidgetId = Widget.SeedId
            }
        ];

        #endregion

        #region Public Methods

        /// <summary>
        /// Returns the seeded widgets.
        /// </summary>
        /// <returns>
        /// The widget query.
        /// </returns>
        [EnableQuery]
        [HttpGet]
        public IQueryable<Widget> Get()
        {
            return _widgets.AsQueryable();
        }

        /// <summary>
        /// Returns a widget by guid key.
        /// </summary>
        /// <param name="key">The widget key.</param>
        /// <returns>
        /// The widget, or not found.
        /// </returns>
        [EnableQuery]
        [HttpGet]
        public IActionResult Get(Guid key)
        {
            var widget = _widgets.FirstOrDefault(item => item.WidgetId == key);

            return widget is null ? NotFound() : Ok(widget);
        }

        #endregion

    }

}
