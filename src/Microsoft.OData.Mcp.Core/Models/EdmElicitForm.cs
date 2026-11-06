// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System.Collections.Generic;

namespace Microsoft.OData.Mcp.Core.Models
{

    /// <summary>
    /// Compact form elicited before invoking an annotated action.
    /// </summary>
    /// <remarks>
    /// Populated from the <c>Form</c> island nested under <c>Microsoft.OData.Mcp.V1.ElicitForm</c>.
    /// Core does not reference McpEssentials; this DTO is the catalog projection.
    /// </remarks>
    public sealed class EdmElicitForm
    {

        #region Properties

        /// <summary>
        /// Gets or sets the fields in insertion order.
        /// </summary>
        public List<EdmElicitFormField> Fields { get; set; } = [];

        /// <summary>
        /// Gets or sets the prompt presented with the form.
        /// </summary>
        public string Message { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the optional keyed-DI name published on the island.
        /// </summary>
        public string? Name { get; set; }

        #endregion

    }

}
