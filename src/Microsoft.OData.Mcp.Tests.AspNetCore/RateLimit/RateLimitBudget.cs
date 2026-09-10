// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

namespace Microsoft.OData.Mcp.Tests.AspNetCore.RateLimit
{

    /// <summary>
    /// Per-path permit counts for a test host. Zero or negative means unlimited.
    /// </summary>
    public sealed class RateLimitBudget
    {

        #region Properties

        /// <summary>
        /// Gets the permit count for Customers entity-set requests.
        /// </summary>
        public int Customers { get; init; }

        /// <summary>
        /// Gets the permit count for the MostValuable function.
        /// </summary>
        public int Function { get; init; }

        /// <summary>
        /// Gets the permit count for the <c>/odata/mcp</c> transport.
        /// </summary>
        public int Mcp { get; init; }

        /// <summary>
        /// Gets the permit count for Products entity-set requests.
        /// </summary>
        public int Products { get; init; }

        /// <summary>
        /// Gets the permit count for <c>/shop/mcp</c> when a second prefix exists.
        /// </summary>
        public int ShopMcp { get; init; }

        #endregion

    }

}
