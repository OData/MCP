// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using Microsoft.OData.Mcp.Core.Vocabularies;
using Microsoft.OData.Mcp.Tests.Shared.Entities;

namespace Microsoft.OData.Mcp.Tests.AspNetCore.Vocabularies
{

    /// <summary>
    /// Stand-in for a Restier API or OData controller whose methods match EDM operations by name.
    /// </summary>
    public sealed class TaskedOperations
    {

        #region Public Methods

        /// <summary>
        /// Bound task action with an MCP description.
        /// </summary>
        /// <param name="customer">The bound customer.</param>
        [McpTask(Description = "Starts provider authorization.")]
        public void Authorize(Customer customer)
        {
        }

        /// <summary>
        /// Unbound function; <c>[McpTask]</c> must not stamp a function.
        /// </summary>
        /// <returns>
        /// 1.
        /// </returns>
        [McpTask]
        public int MostValuable()
        {
            return 1;
        }

        /// <summary>
        /// Bound task action with elicit property names.
        /// </summary>
        /// <param name="customer">The bound customer.</param>
        [McpTask("connectUri")]
        public void Pay(Customer customer)
        {
        }

        /// <summary>
        /// Bound task action whose elicit names come from <see cref="McpElicitUrlAttribute"/>.
        /// </summary>
        /// <param name="customer">The bound customer.</param>
        [McpTask]
        [McpElicitUrl("connectUri")]
        public void Refund(Customer customer)
        {
        }

        /// <summary>
        /// Unbound one-shot action.
        /// </summary>
        public void Reset()
        {
        }

        #endregion

    }

}
