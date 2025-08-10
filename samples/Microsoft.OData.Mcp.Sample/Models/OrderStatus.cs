// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

namespace Microsoft.OData.Mcp.Sample.Models
{
    /// <summary>
    /// Order status constants.
    /// </summary>
    public static class OrderStatus
    {
        /// <summary>
        /// Order is pending processing.
        /// </summary>
        public const string Pending = "Pending";
        
        /// <summary>
        /// Order is currently being processed.
        /// </summary>
        public const string Processing = "Processing";
        
        /// <summary>
        /// Order has been shipped.
        /// </summary>
        public const string Shipped = "Shipped";
        
        /// <summary>
        /// Order has been delivered.
        /// </summary>
        public const string Delivered = "Delivered";
        
        /// <summary>
        /// Order has been cancelled.
        /// </summary>
        public const string Cancelled = "Cancelled";
    }
}
