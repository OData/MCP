// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;

namespace Microsoft.OData.Mcp.Core.Models
{

    /// <summary>
    /// Helpers for composing CSDL and vocabulary documentation into MCP text.
    /// </summary>
    public static class EdmDocumentation
    {

        #region Public Methods

        /// <summary>
        /// Adds a documentation string to a payload only when it carries information.
        /// </summary>
        /// <param name="target">The payload dictionary.</param>
        /// <param name="key">The key to add.</param>
        /// <param name="value">The documentation string, or <c>null</c>.</param>
        /// <param name="skipIfEqualTo">A value already emitted elsewhere; an equal string is not repeated.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="target"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown when <paramref name="key"/> is null or whitespace.</exception>
        /// <remarks>
        /// Whitespace values never produce a key. This is how shapes avoid <c>"description": null</c> noise.
        /// </remarks>
        public static void Add(IDictionary<string, object?> target, string key, string? value, string? skipIfEqualTo = null)
        {
            ArgumentNullException.ThrowIfNull(target);
            ArgumentException.ThrowIfNullOrWhiteSpace(key);

            if (ShouldAdd(value, skipIfEqualTo))
            {
                target[key] = value!.Trim();
            }
        }

        /// <summary>
        /// Adds a documentation string to a string-valued payload only when it carries information.
        /// </summary>
        /// <param name="target">The payload dictionary.</param>
        /// <param name="key">The key to add.</param>
        /// <param name="value">The documentation string, or <c>null</c>.</param>
        /// <param name="skipIfEqualTo">A value already emitted elsewhere; an equal string is not repeated.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="target"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown when <paramref name="key"/> is null or whitespace.</exception>
        public static void Add(IDictionary<string, string> target, string key, string? value, string? skipIfEqualTo = null)
        {
            ArgumentNullException.ThrowIfNull(target);
            ArgumentException.ThrowIfNullOrWhiteSpace(key);

            if (ShouldAdd(value, skipIfEqualTo))
            {
                target[key] = value!.Trim();
            }
        }

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

        #region Internal Methods

        /// <summary>
        /// Determines whether a documentation value should be emitted.
        /// </summary>
        /// <param name="value">The candidate value.</param>
        /// <param name="skipIfEqualTo">A value already emitted elsewhere.</param>
        /// <returns>
        /// <c>true</c> when the value is non-whitespace and differs from <paramref name="skipIfEqualTo"/>.
        /// </returns>
        internal static bool ShouldAdd(string? value, string? skipIfEqualTo)
        {
            return !string.IsNullOrWhiteSpace(value)
                && !string.Equals(value.Trim(), skipIfEqualTo?.Trim(), StringComparison.Ordinal);
        }

        #endregion

    }

}
