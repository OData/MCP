// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

namespace Microsoft.OData.Mcp.Core.Models
{

    /// <summary>
    /// Compact XML <c>Type</c> spellings for an <see cref="EdmElicitFormField"/>.
    /// </summary>
    public enum EdmElicitFormFieldType
    {

        /// <summary>
        /// A string field.
        /// </summary>
        String = 0,

        /// <summary>
        /// An integer field.
        /// </summary>
        Integer = 1,

        /// <summary>
        /// A floating-point field.
        /// </summary>
        Number = 2,

        /// <summary>
        /// A boolean field.
        /// </summary>
        Boolean = 3,

        /// <summary>
        /// A single-select enumeration field.
        /// </summary>
        Enum = 4,

        /// <summary>
        /// A multi-select enumeration field.
        /// </summary>
        Multi = 5

    }

}
