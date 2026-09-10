// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

namespace Microsoft.OData.Mcp.Core.Catalog
{

    /// <summary>
    /// Controls how enumeration values are advertised in JSON Schema and accepted on input.
    /// </summary>
    /// <remarks>
    /// The EDM underlying type of an enumeration is always integral. That describes storage, not the JSON wire.
    /// The advertised schema is fixed for the lifetime of a catalog; observing a numeric value on a GET never
    /// rewrites what <c>tools/list</c> already returned.
    /// </remarks>
    public enum ODataEnumJsonFormat
    {

        /// <summary>
        /// Advertise member names, the same as <see cref="String"/>, but also accept a JSON number on input.
        /// </summary>
        Auto,

        /// <summary>
        /// Advertise and accept member names such as <c>"Red"</c> or the OData literal <c>NS.Color'Red'</c>.
        /// </summary>
        String,

        /// <summary>
        /// Advertise integral member values and accept a JSON number or numeric string.
        /// </summary>
        Integer

    }

}
