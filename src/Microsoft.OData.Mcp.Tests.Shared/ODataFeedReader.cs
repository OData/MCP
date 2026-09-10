// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Text.Json;

namespace Microsoft.OData.Mcp.Tests.Shared
{

    /// <summary>
    /// Reads OData JSON feeds for paging assertions.
    /// </summary>
    public static class ODataFeedReader
    {

        #region Public Methods

        /// <summary>
        /// Reads <c>CompanyName</c> values from the feed, accepting camelCase payloads.
        /// </summary>
        /// <param name="json">The OData JSON body.</param>
        /// <returns>
        /// Company names in payload order.
        /// </returns>
        public static IReadOnlyList<string> ReadCompanyNames(string json)
        {
            return ReadStrings(json, "CompanyName", "companyName");
        }

        /// <summary>
        /// Reads <c>@odata.count</c> when present.
        /// </summary>
        /// <param name="json">The OData JSON body.</param>
        /// <returns>
        /// The count, or <c>null</c>.
        /// </returns>
        public static long? ReadCount(string json)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(json);

            using var document = JsonDocument.Parse(json);
            foreach (var property in document.RootElement.EnumerateObject())
            {
                if (!property.Name.Equals("@odata.count", StringComparison.OrdinalIgnoreCase)
                    && !property.Name.Equals("odata.count", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (property.Value.ValueKind == JsonValueKind.Number && property.Value.TryGetInt64(out var count))
                {
                    return count;
                }
            }

            return null;
        }

        /// <summary>
        /// Reads integer keys from <c>CustomerId</c> or <c>Id</c>.
        /// </summary>
        /// <param name="json">The OData JSON body.</param>
        /// <returns>
        /// Keys in payload order.
        /// </returns>
        public static IReadOnlyList<int> ReadKeys(string json)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(json);

            using var document = JsonDocument.Parse(json);
            if (!document.RootElement.TryGetProperty("value", out var value) || value.ValueKind != JsonValueKind.Array)
            {
                return [];
            }

            var keys = new List<int>();
            foreach (var item in value.EnumerateArray())
            {
                var key = ReadInt(item, "CustomerId", "customerId", "Id", "id");
                if (key is not null)
                {
                    keys.Add(key.Value);
                }
            }

            return keys;
        }

        /// <summary>
        /// Reads <c>@odata.nextLink</c> when present.
        /// </summary>
        /// <param name="json">The OData JSON body.</param>
        /// <returns>
        /// The next link, or <c>null</c>.
        /// </returns>
        public static string? ReadNextLink(string json)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(json);

            using var document = JsonDocument.Parse(json);
            foreach (var property in document.RootElement.EnumerateObject())
            {
                if (property.Name.EndsWith("nextLink", StringComparison.OrdinalIgnoreCase)
                    && property.Value.ValueKind == JsonValueKind.String)
                {
                    return property.Value.GetString();
                }
            }

            return null;
        }

        /// <summary>
        /// Reads string properties from each <c>value</c> object.
        /// </summary>
        /// <param name="json">The OData JSON body.</param>
        /// <param name="propertyNames">Property names to try on each item.</param>
        /// <returns>
        /// Values in payload order.
        /// </returns>
        public static IReadOnlyList<string> ReadStrings(string json, params string[] propertyNames)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(json);
            ArgumentNullException.ThrowIfNull(propertyNames);

            using var document = JsonDocument.Parse(json);
            if (!document.RootElement.TryGetProperty("value", out var value) || value.ValueKind != JsonValueKind.Array)
            {
                return [];
            }

            var names = new List<string>();
            foreach (var item in value.EnumerateArray())
            {
                var name = ReadString(item, propertyNames);
                if (!string.IsNullOrWhiteSpace(name))
                {
                    names.Add(name);
                }
            }

            return names;
        }

        /// <summary>
        /// Reads the number of objects in <c>value</c>.
        /// </summary>
        /// <param name="json">The OData JSON body.</param>
        /// <returns>
        /// The page length.
        /// </returns>
        public static int ReadValueCount(string json)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(json);

            using var document = JsonDocument.Parse(json);
            if (!document.RootElement.TryGetProperty("value", out var value) || value.ValueKind != JsonValueKind.Array)
            {
                return 0;
            }

            return value.GetArrayLength();
        }

        #endregion

        #region Internal Methods

        /// <summary>
        /// Reads the first matching integer property.
        /// </summary>
        /// <param name="item">The JSON object.</param>
        /// <param name="names">Property names to try.</param>
        /// <returns>
        /// The integer, or <c>null</c>.
        /// </returns>
        internal static int? ReadInt(JsonElement item, params string[] names)
        {
            foreach (var name in names)
            {
                if (item.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number))
                {
                    return number;
                }
            }

            return null;
        }

        /// <summary>
        /// Reads the first matching string property.
        /// </summary>
        /// <param name="item">The JSON object.</param>
        /// <param name="names">Property names to try.</param>
        /// <returns>
        /// The string, or <c>null</c>.
        /// </returns>
        internal static string? ReadString(JsonElement item, params string[] names)
        {
            foreach (var name in names)
            {
                if (item.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String)
                {
                    return value.GetString();
                }
            }

            return null;
        }

        #endregion

    }

}
