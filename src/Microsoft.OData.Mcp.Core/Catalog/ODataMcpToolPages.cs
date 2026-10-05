// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;

namespace Microsoft.OData.Mcp.Core.Catalog
{

    /// <summary>
    /// Pages a catalog's tool list for <c>tools/list</c>.
    /// </summary>
    /// <remarks>
    /// The page size is <see cref="ODataMcpCatalogOptions.ToolsPageSize"/>. Clients must not assume it.
    /// Cursors are opaque. A missing cursor starts at the beginning. An empty string is not a start or an
    /// end cursor; this server never issues one, so it is rejected. A cursor from another catalog instance,
    /// one that is not aligned to the page size, or one past the end of the list, is rejected with JSON-RPC -32602.
    /// </remarks>
    internal static class ODataMcpToolPages
    {

        #region Fields

        /// <summary>
        /// Freshness hint applied to every tools/list page, in milliseconds.
        /// </summary>
        internal const int TimeToLiveMilliseconds = 300_000;

        /// <summary>
        /// Freshness hint applied to every tools/list page.
        /// </summary>
        internal static readonly TimeSpan TimeToLive = TimeSpan.FromMilliseconds(TimeToLiveMilliseconds);

        #endregion

        #region Internal Methods

        /// <summary>
        /// Returns one page of the catalog tools followed by <paramref name="extraTools"/>.
        /// </summary>
        /// <param name="catalog">The catalog whose tool list was built at startup.</param>
        /// <param name="extraTools">Host tools appended after the catalog, in a stable order.</param>
        /// <param name="cursor">The opaque cursor from the previous page, or <see langword="null"/> to start.</param>
        /// <returns>
        /// The page, with <c>nextCursor</c> set only when another page remains.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="catalog"/> or <paramref name="extraTools"/> is <see langword="null"/>.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <see cref="ODataMcpCatalogOptions.ToolsPageSize"/> is not greater than zero.</exception>
        /// <exception cref="McpProtocolException">Thrown with <see cref="McpErrorCode.InvalidParams"/> when <paramref name="cursor"/> is not a cursor this catalog issued.</exception>
        internal static ListToolsResult Page(ODataMcpCatalog catalog, IReadOnlyList<Tool> extraTools, string? cursor)
        {
            ArgumentNullException.ThrowIfNull(catalog);
            ArgumentNullException.ThrowIfNull(extraTools);

            var pageSize = catalog._options.ToolsPageSize;
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(pageSize, nameof(ODataMcpCatalogOptions.ToolsPageSize));

            var total = catalog.Tools.Count + extraTools.Count;
            var start = ResolveStart(catalog, extraTools, cursor, total, pageSize);
            var count = Math.Min(pageSize, total - start);
            var tools = new List<Tool>(count);
            for (var index = start; index < start + count; index++)
            {
                tools.Add(index < catalog.Tools.Count
                    ? ODataMcpHandlers.ToTool(catalog.Tools[index])
                    : extraTools[index - catalog.Tools.Count]);
            }

            var nextStart = start + count;

            return new ListToolsResult
            {
                CacheScope = CacheScope.Public,
                NextCursor = nextStart < total ? Encode(catalog, extraTools, nextStart) : null,
                ResultType = "complete",
                TimeToLive = TimeToLive,
                Tools = tools
            };
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Encodes a continuation cursor for this catalog and extra-tool list.
        /// </summary>
        /// <param name="catalog">The catalog.</param>
        /// <param name="extraTools">The extra tools.</param>
        /// <param name="offset">The index of the first tool on the next page.</param>
        /// <returns>
        /// The opaque cursor.
        /// </returns>
        private static string Encode(ODataMcpCatalog catalog, IReadOnlyList<Tool> extraTools, int offset)
        {
            var text = catalog.ToolListStamp + "." + ExtraHash(extraTools) + "." + offset.ToString(CultureInfo.InvariantCulture);

            return ToBase64Url(text);
        }

        /// <summary>
        /// Hashes extra-tool names so a cursor cannot be replayed against a different extra list.
        /// </summary>
        /// <param name="extraTools">The extra tools, in list order.</param>
        /// <returns>
        /// <c>none</c> when there are no extra tools; otherwise a hex SHA-256 of their names.
        /// </returns>
        private static string ExtraHash(IReadOnlyList<Tool> extraTools)
        {
            if (extraTools.Count == 0)
            {
                return "none";
            }

            var names = string.Join('\n', extraTools.Select(tool => tool.Name));

            return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(names)));
        }

        /// <summary>
        /// Resolves the first index of the requested page.
        /// </summary>
        /// <param name="catalog">The catalog.</param>
        /// <param name="extraTools">The extra tools.</param>
        /// <param name="cursor">The request cursor.</param>
        /// <param name="total">The combined tool count.</param>
        /// <param name="pageSize">The catalog page size. Greater than zero.</param>
        /// <returns>
        /// The start index. <c>0</c> when <paramref name="cursor"/> is <see langword="null"/>.
        /// </returns>
        private static int ResolveStart(ODataMcpCatalog catalog, IReadOnlyList<Tool> extraTools, string? cursor, int total, int pageSize)
        {
            if (cursor is null)
            {
                return 0;
            }

            if (string.IsNullOrWhiteSpace(cursor) || !TryDecode(cursor, out var text))
            {
                throw new McpProtocolException("Invalid tools/list cursor.", McpErrorCode.InvalidParams);
            }

            var parts = text.Split('.');
            if (parts.Length != 3
                || !string.Equals(parts[0], catalog.ToolListStamp, StringComparison.Ordinal)
                || !string.Equals(parts[1], ExtraHash(extraTools), StringComparison.Ordinal)
                || !int.TryParse(parts[2], NumberStyles.None, CultureInfo.InvariantCulture, out var offset)
                || offset < 0
                || offset >= total
                || offset % pageSize != 0)
            {
                throw new McpProtocolException("Invalid tools/list cursor.", McpErrorCode.InvalidParams);
            }

            return offset;
        }

        /// <summary>
        /// Encodes cursor text as base64url without padding.
        /// </summary>
        /// <param name="text">The cursor text.</param>
        /// <returns>
        /// The encoded cursor.
        /// </returns>
        private static string ToBase64Url(string text)
        {
            return Convert.ToBase64String(Encoding.UTF8.GetBytes(text)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        }

        /// <summary>
        /// Decodes a base64url cursor.
        /// </summary>
        /// <param name="cursor">The cursor.</param>
        /// <param name="text">The decoded text when this method returns <see langword="true"/>.</param>
        /// <returns>
        /// <see langword="true"/> when <paramref name="cursor"/> is valid base64url.
        /// </returns>
        private static bool TryDecode(string cursor, out string text)
        {
            text = string.Empty;
            var padded = cursor.Replace('-', '+').Replace('_', '/');
            switch (padded.Length % 4)
            {
                case 0:
                    break;
                case 2:
                    padded += "==";
                    break;
                case 3:
                    padded += "=";
                    break;
                default:
                    return false;
            }

            try
            {
                text = Encoding.UTF8.GetString(Convert.FromBase64String(padded));

                return true;
            }
            catch (FormatException)
            {
                return false;
            }
        }

        #endregion

    }

}
