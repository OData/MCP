// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System.Collections.Generic;

namespace Microsoft.OData.Mcp.Core.Catalog
{

    /// <summary>
    /// Options that control how an <see cref="ODataMcpCatalog"/> is built from an EDM.
    /// </summary>
    public sealed class ODataMcpCatalogOptions
    {

        #region Properties

        /// <summary>
        /// Gets or sets a value indicating whether create calls fail before HTTP when a required-on-create property is missing.
        /// </summary>
        /// <remarks>
        /// Defaults to <c>true</c>: the declared metadata is the contract. Turn it off only for a service whose metadata
        /// declares properties non-nullable that its POST handler actually rejects or defaults (live TripPin does this
        /// with <c>Gender</c>). JSON kind, unknown property, enum, and <c>MaxLength</c> checks stay on regardless.
        /// </remarks>
        public bool EnforceRequiredOnCreate { get; set; } = true;

        /// <summary>
        /// Gets or sets how enumeration values are advertised in JSON Schema and accepted on input.
        /// </summary>
        /// <remarks>
        /// Defaults to <see cref="ODataEnumJsonFormat.Auto"/>: member names are advertised and JSON numbers are
        /// also accepted. The advertised schema never changes during a catalog's lifetime.
        /// </remarks>
        public ODataEnumJsonFormat EnumJsonFormat { get; set; } = ODataEnumJsonFormat.Auto;

        /// <summary>
        /// Gets or sets entity set names to exclude from named tools and resources.
        /// </summary>
        public List<string> ExcludeEntitySets { get; set; } = [];

        /// <summary>
        /// Gets or sets a value indicating whether create tools are generated.
        /// </summary>
        public bool IncludeCreate { get; set; } = true;

        /// <summary>
        /// Gets or sets a value indicating whether delete tools are generated.
        /// </summary>
        public bool IncludeDelete { get; set; } = true;

        /// <summary>
        /// Gets or sets entity set names that receive named tools first.
        /// </summary>
        public List<string> IncludeEntitySets { get; set; } = [];

        /// <summary>
        /// Gets or sets a value indicating whether update tools are generated.
        /// </summary>
        public bool IncludeUpdate { get; set; } = true;

        /// <summary>
        /// Gets or sets service-specific guidance placed before the default MCP server instructions.
        /// </summary>
        /// <remarks>
        /// The default text in <see cref="ODataMcpInstructions.Default"/> is always included; this only prepends.
        /// See <see cref="ODataMcpInstructions.Compose(string?)"/>.
        /// </remarks>
        public string? InstructionsPreface { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the EDM can change while the catalog is alive.
        /// </summary>
        /// <remarks>
        /// When <c>false</c> (the default) type shapes are computed once at catalog construction and cached.
        /// Set to <c>true</c> for models whose declared members change at runtime so shapes are rebuilt per request.
        /// </remarks>
        public bool IsDynamicModel { get; set; }

        /// <summary>
        /// Gets or sets the maximum number of entity-set completion values returned for an empty prefix.
        /// </summary>
        public int MaxCompletionValues { get; set; } = 50;

        /// <summary>
        /// Gets or sets the maximum length of an <c>expand</c> argument.
        /// </summary>
        public int MaxExpandLength { get; set; } = 512;

        /// <summary>
        /// Gets or sets the maximum length of a <c>filter</c> argument.
        /// </summary>
        public int MaxFilterLength { get; set; } = 2_048;

        /// <summary>
        /// Gets or sets the maximum number of tools including generics.
        /// </summary>
        public int MaxNamedTools { get; set; } = 150;

        /// <summary>
        /// Gets or sets the maximum JSON body size in bytes for create, update, and operation calls.
        /// </summary>
        public int MaxRequestBodyBytes { get; set; } = 262_144;

        /// <summary>
        /// Gets or sets the maximum number of catalog resources, including <c>$metadata</c>.
        /// </summary>
        public int MaxResources { get; set; } = 50;

        /// <summary>
        /// Gets or sets the maximum response size in bytes before a tool result is rejected.
        /// </summary>
        public int MaxResponseBytes { get; set; } = 1_048_576;

        /// <summary>
        /// Gets or sets the maximum length of a <c>select</c> argument.
        /// </summary>
        public int MaxSelectLength { get; set; } = 1_024;

        /// <summary>
        /// Gets or sets the MCP resource route name (for example, "remote" or "odata").
        /// </summary>
        public string RouteName { get; set; } = "remote";

        #endregion

    }

}
