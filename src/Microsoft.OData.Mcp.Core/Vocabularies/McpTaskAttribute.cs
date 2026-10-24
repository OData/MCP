// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;

namespace Microsoft.OData.Mcp.Core.Vocabularies
{

    /// <summary>
    /// Marks a CLR operation as an MCP task. After OData routes are mapped, the host stamps
    /// <c>Microsoft.OData.Mcp.V1.Task</c> on the matching EDM action.
    /// </summary>
    /// <example>
    /// <code>
    /// [BoundOperation(OperationType = OperationType.Action)]
    /// [McpTask]
    /// public Order Ship(Order order)
    /// {
    ///     return order;
    /// }
    /// </code>
    /// </example>
    /// <remarks>
    /// Pair with Restier <c>[BoundOperation]</c> / <c>[UnboundOperation]</c> or an OData controller action.
    /// Convention ModelBuilder hosts use <c>HasMcpTask()</c> instead. Functions are not tasked.
    /// </remarks>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
    public sealed class McpTaskAttribute : Attribute
    {

        #region Properties

        /// <summary>
        /// Gets JSON property names for <c>ElicitUrlProperties</c>. Empty when omitted.
        /// </summary>
        public string[] ElicitUrlProperties { get; }

        #endregion

        #region Constructors

        /// <summary>
        /// Marks the method as an MCP task.
        /// </summary>
        /// <param name="elicitUrlProperties">Optional JSON property names for <c>ElicitUrlProperties</c>.</param>
        public McpTaskAttribute(params string[] elicitUrlProperties)
        {
            ElicitUrlProperties = elicitUrlProperties ?? [];
        }

        #endregion

    }

}
