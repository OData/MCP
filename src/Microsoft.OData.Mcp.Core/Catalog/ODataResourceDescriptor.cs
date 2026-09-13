// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using Microsoft.OData.Mcp.Core.Constants;

namespace Microsoft.OData.Mcp.Core.Catalog
{

    /// <summary>
    /// A resource advertised on <c>resources/list</c>.
    /// </summary>
    public sealed class ODataResourceDescriptor
    {

        #region Properties

        /// <summary>
        /// Gets or sets the description.
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the MIME type.
        /// </summary>
        public string MimeType { get; set; } = ODataMcpCatalogConstants.ApplicationJson;

        /// <summary>
        /// Gets or sets the resource name.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the type-card JSON for entity sets, or empty for metadata.
        /// </summary>
        public string ReadContents { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the title.
        /// </summary>
        public string Title { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the <c>odata://</c> URI.
        /// </summary>
        public string Uri { get; set; } = string.Empty;

        #endregion

    }

}
