// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using ModelContextProtocol.Protocol;

namespace Microsoft.OData.Mcp.Core.Catalog
{

    /// <summary>
    /// Raised from before-invoke form elicitation when the operator declines or cancels.
    /// <see cref="ODataMcpHandlers.CallToolAsync"/> turns this into an error tool result
    /// without POSTing to OData.
    /// </summary>
    internal sealed class ODataFormElicitationDeclinedException : Exception
    {

        #region Properties

        /// <summary>
        /// Gets the error tool result.
        /// </summary>
        public CallToolResult Result { get; }

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes the exception with the declined tool result.
        /// </summary>
        /// <param name="result">The error result returned to the client.</param>
        public ODataFormElicitationDeclinedException(CallToolResult result)
            : base("The operator declined the form.")
        {
            ArgumentNullException.ThrowIfNull(result);
            Result = result;
        }

        #endregion

    }

}
