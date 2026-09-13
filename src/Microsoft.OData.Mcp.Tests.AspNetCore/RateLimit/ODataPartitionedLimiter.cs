// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;

namespace Microsoft.OData.Mcp.Tests.AspNetCore.RateLimit
{

    /// <summary>
    /// Independent fixed-window limiters for MCP, entity sets, and functions.
    /// </summary>
    public static class ODataPartitionedLimiter
    {

        #region Public Methods

        /// <summary>
        /// Applies <paramref name="budget"/> as a global limiter partitioned by path.
        /// </summary>
        /// <param name="options">The rate limiter options.</param>
        /// <param name="budget">Per-path permits. Zero means unlimited.</param>
        public static void Apply(RateLimiterOptions options, RateLimitBudget budget)
        {
            ArgumentNullException.ThrowIfNull(options);
            ArgumentNullException.ThrowIfNull(budget);

            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
            {
                var path = context.Request.Path.Value ?? string.Empty;
                if (path.Contains("/shop/mcp", StringComparison.OrdinalIgnoreCase))
                {
                    return Partition("shop-mcp", budget.ShopMcp);
                }

                if (path.Contains("/mcp", StringComparison.OrdinalIgnoreCase))
                {
                    return Partition("mcp", budget.Mcp);
                }

                if (path.Contains("MostValuable", StringComparison.OrdinalIgnoreCase))
                {
                    return Partition("function", budget.Function);
                }

                if (path.Contains("Customers", StringComparison.OrdinalIgnoreCase))
                {
                    return Partition("customers", budget.Customers);
                }

                if (path.Contains("Products", StringComparison.OrdinalIgnoreCase))
                {
                    return Partition("products", budget.Products);
                }

                return RateLimitPartition.GetNoLimiter("other");
            });
        }

        #endregion

        #region Internal Methods

        /// <summary>
        /// Returns an unlimited partition or a one-hour window with <paramref name="permit"/>.
        /// </summary>
        /// <param name="key">The partition key.</param>
        /// <param name="permit">Permits per window. Zero or negative is unlimited.</param>
        /// <returns>
        /// The partition.
        /// </returns>
        internal static RateLimitPartition<string> Partition(string key, int permit)
        {
            if (permit <= 0)
            {
                return RateLimitPartition.GetNoLimiter(key);
            }

            return RateLimitPartition.GetFixedWindowLimiter(key, _ => new FixedWindowRateLimiterOptions
            {
                AutoReplenishment = false,
                PermitLimit = permit,
                QueueLimit = 0,
                Window = TimeSpan.FromHours(1)
            });
        }

        #endregion

    }

}
