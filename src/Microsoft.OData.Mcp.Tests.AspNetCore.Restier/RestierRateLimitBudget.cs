// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

namespace Microsoft.OData.Mcp.Tests.AspNetCore.Restier
{

    /// <summary>
    /// Per-path permit counts for Restier rate-limit tests. Zero means unlimited.
    /// </summary>
    public sealed class RestierRateLimitBudget
    {

        #region Properties

        /// <summary>
        /// Gets permits for <c>/odata/Customers</c>.
        /// </summary>
        public int Customers { get; init; }

        /// <summary>
        /// Gets permits for <c>/odata/mcp</c>.
        /// </summary>
        public int OdataMcp { get; init; }

        /// <summary>
        /// Gets permits for <c>/shop/Products</c>.
        /// </summary>
        public int Products { get; init; }

        /// <summary>
        /// Gets permits for <c>/shop/mcp</c>.
        /// </summary>
        public int ShopMcp { get; init; }

        #endregion

    }

}
