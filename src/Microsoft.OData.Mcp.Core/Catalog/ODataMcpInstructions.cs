// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

namespace Microsoft.OData.Mcp.Core.Catalog
{

    /// <summary>
    /// The MCP <c>initialize.instructions</c> text every OData MCP host advertises.
    /// </summary>
    /// <remarks>
    /// The default carries only the rules a calling model cannot infer from tool schemas. Developers may prepend
    /// service-specific guidance through <see cref="ODataMcpCatalogOptions.InstructionsPreface"/>; the default
    /// is never replaced.
    /// </remarks>
    public static class ODataMcpInstructions
    {

        #region Fields

        /// <summary>
        /// The default server instructions shared by the ASP.NET Core and command-line hosts.
        /// </summary>
        public const string Default =
            "Query options have no $ prefix. Prefer named tools when listed; otherwise odata_describe_type then generic odata_*. " +
            "odata_describe_model summary is the map; complete dumps every type in one call. " +
            "Bound operations are listed on the type; unbound on odata_list_operations. " +
            "odata_call: pass arguments in parameters (a JSON object) using those names — do not stringify, do not wrap, do not guess a body. " +
            "Do not read $metadata to explore. PATCH: omit a field to keep it; never send JSON null for a required property.";

        #endregion

        #region Public Methods

        /// <summary>
        /// Composes the server instructions from an optional developer preface and <see cref="Default"/>.
        /// </summary>
        /// <param name="preface">Service-specific guidance to place before the default text, or <c>null</c>.</param>
        /// <returns>
        /// <see cref="Default"/> alone when <paramref name="preface"/> is null or whitespace; otherwise the trimmed
        /// preface, one blank line, then <see cref="Default"/>.
        /// </returns>
        /// <example>
        /// <code>
        /// options.ServerInstructions = ODataMcpInstructions.Compose("Contoso sales data. Amounts are USD.");
        /// </code>
        /// </example>
        public static string Compose(string? preface)
        {
            if (string.IsNullOrWhiteSpace(preface))
            {
                return Default;
            }

            return $"{preface.Trim()}\n\n{Default}";
        }

        #endregion

    }

}
