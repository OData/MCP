// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

namespace Microsoft.OData.Mcp.Tests.AspNetCore.Restier
{

    /// <summary>
    /// Seeded Restier product used by multi-prefix tests.
    /// </summary>
    public sealed class McpProduct
    {

        #region Properties

        /// <summary>
        /// Gets or sets the key.
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// Gets or sets the product name.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        #endregion

    }

}
