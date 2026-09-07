// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

namespace Microsoft.OData.Mcp.Tests.AspNetCore.Hosting
{

    /// <summary>
    /// Metadata that has Prefix but no Model, so duck-typing must reject it.
    /// </summary>
    public sealed class MissingModelMetadata
    {

        #region Properties

        /// <summary>
        /// Gets or sets a prefix without a companion model.
        /// </summary>
        public string Prefix { get; set; } = string.Empty;

        #endregion

    }

}
