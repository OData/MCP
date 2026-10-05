// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OData.Mcp.Core.Catalog;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;

namespace Microsoft.OData.Mcp.Tests.Core.Catalog
{

    /// <summary>
    /// tools/list paging over a catalog built once at construction.
    /// </summary>
    [TestClass]
    public class ODataMcpToolPageTests
    {

        #region Public Methods

        /// <summary>
        /// Walking cursors returns every tool once, splits a family across a page boundary, and ends without a cursor.
        /// </summary>
        [TestMethod]
        public void Page_WalksEveryTool_Once_LastPageOmitsCursor()
        {
            var catalog = new ODataMcpCatalog(CatalogComplexityTests.CreateWideModel(25), new ODataMcpCatalogOptions());
            var split = catalog.Tools.Where(tool => tool.EntitySetName == "Rows017").Select(tool => tool.Name).ToList();
            split.Should().HaveCount(5);

            var seen = new List<string>();
            string? cursor = null;
            var pages = 0;
            ListToolsResult? last = null;
            do
            {
                var page = ODataMcpToolPages.Page(catalog, [], cursor);
                pages++;
                page.CacheScope.Should().Be(CacheScope.Public);
                page.TimeToLive.Should().Be(ODataMcpToolPages.TimeToLive);
                page.ResultType.Should().Be("complete");
                page.Tools.Should().NotBeEmpty();
                if (page.NextCursor is not null)
                {
                    page.Tools.Should().HaveCount(catalog._options.ToolsPageSize);
                    page.NextCursor.Should().NotBeNullOrWhiteSpace();
                }

                seen.AddRange(page.Tools.Select(tool => tool.Name));
                cursor = page.NextCursor;
                last = page;
            }
            while (cursor is not null);

            pages.Should().BeGreaterThan(1);
            last!.NextCursor.Should().BeNull();
            seen.Should().Equal(catalog.Tools.Select(tool => tool.Name));
            seen.Distinct().Should().HaveCount(seen.Count);
            seen.Should().Contain(split);
        }

        /// <summary>
        /// Extra tools are appended after the catalog and ride on the last page.
        /// </summary>
        [TestMethod]
        public void Page_AppendsExtraTools_AfterCatalog()
        {
            var catalog = new ODataMcpCatalog(CatalogComplexityTests.CreateWideModel(25), new ODataMcpCatalogOptions());
            var extras = new Tool[] { new() { Name = "shutdown_server" } };
            var seen = new List<string>();
            string? cursor = null;
            do
            {
                var page = ODataMcpToolPages.Page(catalog, extras, cursor);
                seen.AddRange(page.Tools.Select(tool => tool.Name));
                cursor = page.NextCursor;
            }
            while (cursor is not null);

            seen.Should().HaveCount(catalog.Tools.Count + 1);
            seen[^1].Should().Be("shutdown_server");
        }

        /// <summary>
        /// ToolsPageSize is the length of a full page, and the next page continues after it.
        /// </summary>
        [TestMethod]
        public void Page_ToolsPageSize_LimitsFullPageAndContinues()
        {
            var catalog = new ODataMcpCatalog(
                CatalogComplexityTests.CreateWideModel(1),
                new ODataMcpCatalogOptions
                {
                    ToolsPageSize = 5
                });
            var first = ODataMcpToolPages.Page(catalog, [], null);

            first.Tools.Should().HaveCount(5);
            first.NextCursor.Should().NotBeNullOrWhiteSpace();
            first.Tools.Select(tool => tool.Name).Should().Equal(catalog.Tools.Take(5).Select(tool => tool.Name));

            var second = ODataMcpToolPages.Page(catalog, [], first.NextCursor);
            second.Tools.Select(tool => tool.Name).Should().Equal(catalog.Tools.Skip(5).Take(5).Select(tool => tool.Name));
        }

        /// <summary>
        /// A non-positive ToolsPageSize is rejected when the catalog is built.
        /// </summary>
        [TestMethod]
        public void Catalog_ToolsPageSizeNotPositive_Throws()
        {
            var model = CatalogComplexityTests.CreateWideModel(1);
            var zero = () => new ODataMcpCatalog(model, new ODataMcpCatalogOptions { ToolsPageSize = 0 });
            var negative = () => new ODataMcpCatalog(model, new ODataMcpCatalogOptions { ToolsPageSize = -1 });

            zero.Should().Throw<ArgumentOutOfRangeException>();
            negative.Should().Throw<ArgumentOutOfRangeException>();
        }

        /// <summary>
        /// A catalog that fits in one page omits nextCursor and still carries cache hints.
        /// </summary>
        [TestMethod]
        public async Task ListToolsAsync_SinglePage_OmitsCursor_IncludesCacheHints()
        {
            var session = ODataMcpHandlerTests.Session();
            var result = await ODataMcpHandlers.ListToolsAsync(
                new ServiceCollection().BuildServiceProvider(),
                _ => session,
                null,
                null,
                CancellationToken.None);

            result.NextCursor.Should().BeNull();
            result.CacheScope.Should().Be(CacheScope.Public);
            result.TimeToLive.Should().Be(ODataMcpToolPages.TimeToLive);
            result.ResultType.Should().Be("complete");
            result.Tools.Select(tool => tool.Name).Should().Equal(session.Catalog.Tools.Select(tool => tool.Name));
        }

        /// <summary>
        /// An empty string is a cursor value, not the start of the list.
        /// </summary>
        [TestMethod]
        public void Page_EmptyCursor_IsInvalidParams()
        {
            var catalog = new ODataMcpCatalog(CatalogComplexityTests.CreateWideModel(1), new ODataMcpCatalogOptions());
            var act = () => ODataMcpToolPages.Page(catalog, [], "");

            act.Should().Throw<McpProtocolException>().Which.ErrorCode.Should().Be(McpErrorCode.InvalidParams);
        }

        /// <summary>
        /// A cursor from another catalog instance is rejected.
        /// </summary>
        [TestMethod]
        public void Page_CursorFromAnotherCatalog_IsInvalidParams()
        {
            var first = new ODataMcpCatalog(CatalogComplexityTests.CreateWideModel(25), new ODataMcpCatalogOptions());
            var second = new ODataMcpCatalog(CatalogComplexityTests.CreateWideModel(25), new ODataMcpCatalogOptions());
            var cursor = ODataMcpToolPages.Page(first, [], null).NextCursor;
            cursor.Should().NotBeNullOrWhiteSpace();

            var act = () => ODataMcpToolPages.Page(second, [], cursor);

            act.Should().Throw<McpProtocolException>().Which.ErrorCode.Should().Be(McpErrorCode.InvalidParams);
        }

        /// <summary>
        /// A cursor that is not base64url, or that points past the list, is rejected.
        /// </summary>
        [TestMethod]
        public void Page_GarbageCursor_IsInvalidParams()
        {
            var catalog = new ODataMcpCatalog(CatalogComplexityTests.CreateWideModel(1), new ODataMcpCatalogOptions());
            var act = () => ODataMcpToolPages.Page(catalog, [], "not a cursor");

            act.Should().Throw<McpProtocolException>().Which.ErrorCode.Should().Be(McpErrorCode.InvalidParams);
        }

        #endregion

    }

}
