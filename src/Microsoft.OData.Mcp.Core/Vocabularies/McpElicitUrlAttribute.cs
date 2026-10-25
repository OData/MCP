// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;

namespace Microsoft.OData.Mcp.Core.Vocabularies
{

    /// <summary>
    /// Names JSON properties on an action result that are URLs to elicit. After OData routes
    /// are mapped, the host stamps <c>Microsoft.OData.Mcp.V1.ElicitUrlProperties</c> on the
    /// matching EDM action.
    /// </summary>
    /// <example>
    /// <code>
    /// [BoundOperation(OperationType = OperationType.Action)]
    /// [McpTask]
    /// [McpElicitUrl("connectUri")]
    /// public AuthorizationTicket Authorize(IntegrationConnection connection)
    /// {
    ///     return ticket;
    /// }
    /// </code>
    /// </example>
    /// <remarks>
    /// Pair with <see cref="McpTaskAttribute"/>. Convention ModelBuilder hosts use
    /// <c>HasMcpElicitUrl</c> instead. When this term is omitted, the runtime elicits the
    /// default names in <see cref="McpVocabulary.DefaultElicitUrlProperties"/> if they appear
    /// on the Started JSON. Functions are not elicited.
    /// </remarks>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
    public sealed class McpElicitUrlAttribute : Attribute
    {

        #region Properties

        /// <summary>
        /// Gets JSON property names for <c>ElicitUrlProperties</c>.
        /// </summary>
        public string[] PropertyNames { get; }

        #endregion

        #region Constructors

        /// <summary>
        /// Marks the method's result JSON properties as URLs to elicit.
        /// </summary>
        /// <param name="propertyNames">JSON property names that hold elicit URLs.</param>
        public McpElicitUrlAttribute(params string[] propertyNames)
        {
            PropertyNames = propertyNames ?? [];
        }

        #endregion

    }

}
