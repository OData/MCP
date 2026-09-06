// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

namespace Microsoft.OData.Mcp.Core.Execution
{

    /// <summary>
    /// The HTTP result of an OData call.
    /// </summary>
    public sealed class ODataExecuteResult
    {

        #region Properties

        /// <summary>
        /// Gets or sets the response body.
        /// </summary>
        public string Body { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets a value indicating whether the status code is success.
        /// </summary>
        public bool IsSuccess { get; set; }

        /// <summary>
        /// Gets or sets the response media type.
        /// </summary>
        public string? MediaType { get; set; }

        /// <summary>
        /// Gets or sets the HTTP status code.
        /// </summary>
        public int StatusCode { get; set; }

        #endregion

    }

}
