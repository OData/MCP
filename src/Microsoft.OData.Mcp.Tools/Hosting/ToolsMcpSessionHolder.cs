// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using Microsoft.OData.Mcp.Core.Catalog;

namespace Microsoft.OData.Mcp.Tools.Hosting
{

    /// <summary>
    /// A mutable slot for the one <see cref="ODataMcpSession"/> a Tools host serves, registered before the host
    /// is built and filled once <c>$metadata</c> has been fetched and parsed.
    /// </summary>
    /// <example>
    /// <code>
    /// builder.Services.AddSingleton&lt;ToolsMcpSessionHolder&gt;();
    /// builder.Services.AddMcpServer().WithODataCatalogHandlers(sp =&gt;
    /// {
    ///     var session = sp.GetRequiredService&lt;ToolsMcpSessionHolder&gt;().Session;
    ///     ArgumentNullException.ThrowIfNull(session);
    ///
    ///     return session;
    /// });
    /// </code>
    /// </example>
    /// <remarks>
    /// The session cannot be registered directly because building it requires the very
    /// <see cref="System.Net.Http.IHttpClientFactory"/> the host owns: the metadata GET runs through the
    /// authenticating <c>"OData"</c> client, which only exists after <c>Build()</c>, and a service collection is
    /// frozen from that moment on. This holder is what lets one host serve both sides of that ordering.
    /// </remarks>
    public sealed class ToolsMcpSessionHolder
    {

        #region Properties

        /// <summary>
        /// Gets or sets the session the MCP handlers resolve, or <see langword="null"/> until <c>$metadata</c>
        /// has been fetched and parsed.
        /// </summary>
        public ODataMcpSession? Session { get; set; }

        #endregion

    }

}
