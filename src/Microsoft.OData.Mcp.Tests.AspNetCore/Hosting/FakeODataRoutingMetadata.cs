// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using Microsoft.OData.Edm;

namespace Microsoft.OData.Mcp.Tests.AspNetCore.Hosting
{

    /// <summary>
    /// Duck-typed stand-in for OData 8 routing metadata used by discovery tests.
    /// </summary>
    public sealed class FakeODataRoutingMetadata
    {

        #region Properties

        /// <summary>
        /// Gets or sets the EdmLib model.
        /// </summary>
        public IEdmModel Model { get; set; } = null!;

        /// <summary>
        /// Gets or sets the route prefix.
        /// </summary>
        public string Prefix { get; set; } = string.Empty;

        #endregion

    }

}
