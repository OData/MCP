// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using Microsoft.Restier.AspNetCore.Model;
using Microsoft.Restier.EntityFrameworkCore;

namespace Microsoft.OData.Mcp.Tests.AspNetCore.Restier
{

    /// <summary>
    /// Restier API over <see cref="McpCustomerContext"/>. Entity sets come from DbSets;
    /// operations come from methods on this type. There is no OData controller.
    /// </summary>
    public sealed class McpCustomerApi : EntityFrameworkApi<McpCustomerContext>
    {

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="McpCustomerApi"/> class.
        /// </summary>
        /// <param name="serviceProvider">The Restier service provider.</param>
        public McpCustomerApi(IServiceProvider serviceProvider)
            : base(serviceProvider)
        {
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Unbound function with a string parameter.
        /// </summary>
        /// <param name="code">The status code to echo.</param>
        /// <returns>
        /// The code.
        /// </returns>
        [UnboundOperation]
        public string GetStatus(string code)
        {
            return string.IsNullOrWhiteSpace(code) ? "unknown" : code;
        }

        /// <summary>
        /// Unbound function used by <c>odata_call</c> tests.
        /// </summary>
        /// <returns>
        /// 42.
        /// </returns>
        [UnboundOperation]
        public int MostValuable()
        {
            return 42;
        }

        /// <summary>
        /// Unbound action with no parameters.
        /// </summary>
        [UnboundOperation(OperationType = OperationType.Action)]
        public void Reset()
        {
        }

        /// <summary>
        /// Bound action used by <c>odata_call</c> tests.
        /// </summary>
        /// <param name="customer">The bound customer.</param>
        /// <param name="userName">The share target.</param>
        /// <returns>
        /// The same customer.
        /// </returns>
        [BoundOperation(OperationType = OperationType.Action)]
        public McpCustomer Share(McpCustomer customer, string userName)
        {
            ArgumentNullException.ThrowIfNull(customer);
            ArgumentException.ThrowIfNullOrWhiteSpace(userName);

            return customer;
        }

        #endregion

    }

}
