// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

namespace Microsoft.OData.Mcp.Core.Catalog
{

    /// <summary>
    /// The result of invoking a catalog tool.
    /// </summary>
    public sealed class ODataToolInvocationResult
    {

        #region Properties

        /// <summary>
        /// Gets or sets a value indicating whether the tool failed.
        /// </summary>
        public bool IsError { get; set; }

        /// <summary>
        /// Gets or sets structured JSON content, when the result is JSON.
        /// </summary>
        public string? StructuredContent { get; set; }

        /// <summary>
        /// Gets or sets the text content returned to the client.
        /// </summary>
        public string Text { get; set; } = string.Empty;

        #endregion

    }

}
