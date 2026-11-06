// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

namespace Microsoft.OData.Mcp.Core.Models
{

    /// <summary>
    /// One allowed value on an enum or multi form field.
    /// </summary>
    public sealed class EdmElicitFormOption
    {

        #region Properties

        /// <summary>
        /// Gets or sets whether this option is a default.
        /// </summary>
        public bool Selected { get; set; }

        /// <summary>
        /// Gets or sets the display title.
        /// </summary>
        public string? Title { get; set; }

        /// <summary>
        /// Gets or sets the wire value.
        /// </summary>
        public string Value { get; set; } = string.Empty;

        #endregion

    }

}
