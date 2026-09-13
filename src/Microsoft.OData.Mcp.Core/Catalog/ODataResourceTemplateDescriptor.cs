// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

namespace Microsoft.OData.Mcp.Core.Catalog
{

    /// <summary>
    /// A resource template advertised on <c>resources/templates/list</c>.
    /// </summary>
    public sealed class ODataResourceTemplateDescriptor
    {

        #region Properties

        /// <summary>
        /// Gets or sets the description.
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the template name.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the title.
        /// </summary>
        public string Title { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the URI template.
        /// </summary>
        public string UriTemplate { get; set; } = string.Empty;

        #endregion

    }

}
