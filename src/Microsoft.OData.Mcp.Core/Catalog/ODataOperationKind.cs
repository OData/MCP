// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

namespace Microsoft.OData.Mcp.Core.Catalog
{

    /// <summary>
    /// Distinguishes CRUD/generic catalog tools from named OData operations.
    /// </summary>
    public enum ODataOperationKind
    {

        /// <summary>
        /// A generic <c>odata_*</c> tool or a named CRUD family member.
        /// </summary>
        None = 0,

        /// <summary>
        /// A named <c>action_*</c> tool.
        /// </summary>
        Action,

        /// <summary>
        /// A named <c>function_*</c> tool.
        /// </summary>
        Function

    }

}
