// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Text.Json;

namespace Microsoft.OData.Mcp.Tests.Shared
{

    /// <summary>
    /// Builds JSON argument dictionaries for catalog tool invocations.
    /// </summary>
    public static class ToolArguments
    {

        #region Public Methods

        /// <summary>
        /// Serializes a value as a <see cref="JsonElement"/> argument.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns>
        /// The element.
        /// </returns>
        public static JsonElement Json(object? value)
        {
            return JsonSerializer.SerializeToElement(value);
        }

        /// <summary>
        /// Builds an argument dictionary from name/value pairs.
        /// </summary>
        /// <param name="pairs">Alternating names and values.</param>
        /// <returns>
        /// The argument dictionary.
        /// </returns>
        public static Dictionary<string, JsonElement> Of(params object?[] pairs)
        {
            var arguments = new Dictionary<string, JsonElement>();
            for (var index = 0; index + 1 < pairs.Length; index += 2)
            {
                var name = pairs[index] as string;
                if (string.IsNullOrWhiteSpace(name))
                {
                    continue;
                }

                arguments[name] = Json(pairs[index + 1]);
            }

            return arguments;
        }

        #endregion

    }

}
