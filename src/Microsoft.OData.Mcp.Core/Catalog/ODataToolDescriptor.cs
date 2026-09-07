// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

namespace Microsoft.OData.Mcp.Core.Catalog
{

    /// <summary>
    /// A tool advertised on <c>tools/list</c>.
    /// </summary>
    public sealed class ODataToolDescriptor
    {

        #region Properties

        /// <summary>
        /// Gets or sets the description.
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets a value indicating whether the tool is destructive.
        /// </summary>
        public bool DestructiveHint { get; set; }

        /// <summary>
        /// Gets or sets the entity set this named tool targets, if any.
        /// </summary>
        public string? EntitySetName { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the tool is idempotent.
        /// </summary>
        public bool IdempotentHint { get; set; }

        /// <summary>
        /// Gets or sets the JSON Schema for arguments. Query properties must not include '$'.
        /// </summary>
        public string InputSchema { get; set; } = "{}";

        /// <summary>
        /// Gets or sets the tool name.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets a value indicating whether the tool reaches an external world.
        /// </summary>
        public bool OpenWorldHint { get; set; } = true;

        /// <summary>
        /// Gets or sets a value indicating whether the tool is read-only.
        /// </summary>
        public bool ReadOnlyHint { get; set; }

        /// <summary>
        /// Gets or sets the title.
        /// </summary>
        public string Title { get; set; } = string.Empty;

        #endregion

    }

}
