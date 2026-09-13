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
    /// Documents entity set with binary and stream properties that MCP must omit from schemas.
    /// </summary>
    public sealed class DocumentsController : ODataController
    {

        #region Fields

        internal readonly List<Document> _documents =
        [
            new Document
            {
                DocumentId = 1,
                Photo = [1, 2, 3],
                Title = "Spec"
            }
        ];

        #endregion

        #region Public Methods

        /// <summary>
        /// Returns the seeded documents.
        /// </summary>
        /// <returns>
        /// The document query.
        /// </returns>
        [EnableQuery]
        [HttpGet]
        public IQueryable<Document> Get()
        {
            return _documents.AsQueryable();
        }

        /// <summary>
        /// Returns a document by key.
        /// </summary>
        /// <param name="key">The document key.</param>
        /// <returns>
        /// The document, or not found.
        /// </returns>
        [EnableQuery]
        [HttpGet]
        public IActionResult Get(int key)
        {
            var document = _documents.FirstOrDefault(item => item.DocumentId == key);

            return document is null ? NotFound() : Ok(document);
        }

        #endregion

    }

}
