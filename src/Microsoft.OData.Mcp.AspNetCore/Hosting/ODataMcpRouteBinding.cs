// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using Microsoft.OData.Edm;

namespace Microsoft.OData.Mcp.AspNetCore.Hosting
{

    /// <summary>
    /// An OData route prefix bound to an <see cref="IEdmModel"/>.
    /// </summary>
    public sealed class ODataMcpRouteBinding
    {

        #region Properties

        /// <summary>
        /// Gets the EdmLib model for <see cref="Prefix"/>.
        /// </summary>
        public required IEdmModel Model { get; init; }

        /// <summary>
        /// Gets the OData route prefix, for example <c>odata</c>. Empty is the application root.
        /// </summary>
        public required string Prefix { get; init; }

        #endregion

    }

}
