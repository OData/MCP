// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;

namespace Microsoft.OData.Mcp.Core.Catalog
{

    /// <summary>
    /// Per-endpoint MCP state: catalog, runtime, and optional CSDL cache.
    /// </summary>
    public sealed class ODataMcpSession
    {

        #region Properties

        /// <summary>
        /// Gets the catalog.
        /// </summary>
        public ODataMcpCatalog Catalog { get; }

        /// <summary>
        /// Gets the cached CSDL document, if any.
        /// </summary>
        public string? MetadataXml { get; }

        /// <summary>
        /// Gets the tool runtime.
        /// </summary>
        public ODataToolRuntime Runtime { get; }

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="ODataMcpSession"/> class.
        /// </summary>
        /// <param name="catalog">The catalog.</param>
        /// <param name="runtime">The runtime.</param>
        /// <param name="metadataXml">The CSDL document for <c>$metadata</c> reads.</param>
        public ODataMcpSession(ODataMcpCatalog catalog, ODataToolRuntime runtime, string? metadataXml)
        {
            ArgumentNullException.ThrowIfNull(catalog);
            ArgumentNullException.ThrowIfNull(runtime);

            Catalog = catalog;
            Runtime = runtime;
            MetadataXml = metadataXml;
        }

        #endregion

    }

}
