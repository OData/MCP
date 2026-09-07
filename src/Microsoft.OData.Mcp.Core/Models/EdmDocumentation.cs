// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;

namespace Microsoft.OData.Mcp.Core.Models
{

    /// <summary>
    /// Helpers for composing CSDL and vocabulary documentation into MCP text.
    /// </summary>
    public static class EdmDocumentation
    {

        #region Public Methods

        /// <summary>
        /// Returns the first non-whitespace documentation string.
        /// </summary>
        /// <param name="values">Candidate documentation strings, in preference order.</param>
        /// <returns>
        /// The first non-whitespace value, or <c>null</c> when none are documented.
        /// </returns>
        public static string? First(params string?[] values)
        {
            ArgumentNullException.ThrowIfNull(values);

            foreach (var value in values)
            {
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value.Trim();
                }
            }

            return null;
        }

        #endregion

    }

}
