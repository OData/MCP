// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System.Collections.Generic;

namespace Microsoft.OData.Mcp.Core.Catalog
{

    /// <summary>
    /// A tool advertised on <c>tools/list</c>.
    /// </summary>
    public sealed class ODataToolDescriptor
    {

        #region Properties

        /// <summary>
        /// Gets or sets the EDM binding parameter type for a named operation tool, or <c>null</c> when the tool is unbound or not an operation.
        /// </summary>
        public string? BindingParameterType { get; set; }

        /// <summary>
        /// Gets or sets the description.
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets a value indicating whether the tool is destructive.
        /// </summary>
        public bool DestructiveHint { get; set; }

        /// <summary>
        /// Gets or sets JSON property names on the action result that are URLs to elicit, or <c>null</c>
        /// when the term is omitted (the runtime then uses <see cref="Microsoft.OData.Mcp.Core.Vocabularies.McpVocabulary.DefaultElicitUrlProperties"/>).
        /// </summary>
        public IReadOnlyList<string>? ElicitUrlProperties { get; set; }

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
        /// Gets or sets a value indicating whether a named action tool is annotated <c>Microsoft.OData.Mcp.V1.Task</c>.
        /// </summary>
        /// <value>
        /// <c>true</c> when the EDM action is a task; otherwise, <c>false</c>.
        /// </value>
        public bool IsTask { get; set; }

        /// <summary>
        /// Gets or sets the tool name.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets a value indicating whether the tool reaches an external world.
        /// </summary>
        public bool OpenWorldHint { get; set; } = true;

        /// <summary>
        /// Gets or sets whether this tool is a named action, a named function, or neither.
        /// </summary>
        public ODataOperationKind OperationKind { get; set; }

        /// <summary>
        /// Gets or sets the EDM short name of the action or function, or <c>null</c> when the tool is not an operation.
        /// </summary>
        public string? OperationName { get; set; }

        /// <summary>
        /// Gets or sets the JSON Schema for <c>structuredContent</c>, or <c>null</c> when the tool's result is not a fixed JSON shape.
        /// </summary>
        /// <remarks>
        /// List tools and named <c>action_*</c> / <c>function_*</c> tools whose EDM return type is known declare one.
        /// Describe tools return text or JSON depending on <c>format</c>, and CRUD data tools forward OData bodies, so they
        /// leave this <c>null</c>. Operations with no return type also leave it <c>null</c>.
        /// </remarks>
        public string? OutputSchema { get; set; }

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
