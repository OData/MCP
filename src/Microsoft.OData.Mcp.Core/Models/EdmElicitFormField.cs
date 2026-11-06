// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System.Collections.Generic;

namespace Microsoft.OData.Mcp.Core.Models
{

    /// <summary>
    /// One field on an <see cref="EdmElicitForm"/>.
    /// </summary>
    public sealed class EdmElicitFormField
    {

        #region Properties

        /// <summary>
        /// Gets or sets the default boolean value.
        /// </summary>
        public bool? DefaultBool { get; set; }

        /// <summary>
        /// Gets or sets the default numeric value.
        /// </summary>
        public double? DefaultNumber { get; set; }

        /// <summary>
        /// Gets or sets the default string or single-select value.
        /// </summary>
        public string? DefaultString { get; set; }

        /// <summary>
        /// Gets or sets the default multi-select values.
        /// </summary>
        public IReadOnlyList<string>? DefaultStrings { get; set; }

        /// <summary>
        /// Gets or sets the description shown with the field.
        /// </summary>
        public string? Description { get; set; }

        /// <summary>
        /// Gets or sets the string format: <c>email</c>, <c>uri</c>, <c>date</c>, or <c>date-time</c>.
        /// </summary>
        public string? Format { get; set; }

        /// <summary>
        /// Gets or sets the maximum number of selected items on a multi-select field.
        /// </summary>
        public int? MaxItems { get; set; }

        /// <summary>
        /// Gets or sets the maximum string length.
        /// </summary>
        public int? MaxLength { get; set; }

        /// <summary>
        /// Gets or sets the maximum numeric value.
        /// </summary>
        public double? Maximum { get; set; }

        /// <summary>
        /// Gets or sets the minimum number of selected items on a multi-select field.
        /// </summary>
        public int? MinItems { get; set; }

        /// <summary>
        /// Gets or sets the minimum string length.
        /// </summary>
        public int? MinLength { get; set; }

        /// <summary>
        /// Gets or sets the minimum numeric value.
        /// </summary>
        public double? Minimum { get; set; }

        /// <summary>
        /// Gets or sets the camelCase wire name.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the allowed values for enum and multi fields.
        /// </summary>
        public List<EdmElicitFormOption> Options { get; set; } = [];

        /// <summary>
        /// Gets or sets whether the field is listed in <c>required</c>.
        /// </summary>
        public bool Required { get; set; }

        /// <summary>
        /// Gets or sets the display title.
        /// </summary>
        public string? Title { get; set; }

        /// <summary>
        /// Gets or sets the primitive kind.
        /// </summary>
        public EdmElicitFormFieldType Type { get; set; }

        #endregion

    }

}
