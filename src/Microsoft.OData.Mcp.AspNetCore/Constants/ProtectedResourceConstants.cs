// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

namespace Microsoft.OData.Mcp.AspNetCore.Constants
{

    /// <summary>
    /// The literal wire values <c>AddProtectedResourceMetadata</c> publishes: the RFC 9728 well-known path, the
    /// RFC 6750 <c>Bearer</c> scheme, and the auth-params an annotated challenge carries.
    /// </summary>
    /// <example>
    /// <code>
    /// var url = $"{origin}{ProtectedResourceConstants.WellKnownPath}";
    /// </code>
    /// </example>
    /// <remarks>
    /// These strings are deliberately duplicated from <c>Microsoft.OData.Mcp.Authentication</c> rather than
    /// shared with it. The dependency direction in <c>specs/v3/ARCHITECTURE.md</c> is one way: the CLI may
    /// reference an ASP.NET Core hosting concept, an ASP.NET Core host may never reference outbound
    /// authentication. Four constants is a cheaper price than a reference that inverts the graph.
    /// </remarks>
    public static class ProtectedResourceConstants
    {

        #region Fields

        /// <summary>
        /// The RFC 6750 authentication scheme an annotated <c>WWW-Authenticate</c> challenge uses.
        /// </summary>
        public const string BearerScheme = "Bearer";

        /// <summary>
        /// The <c>Cache-Control</c> value the protected resource metadata document is served with.
        /// </summary>
        public const string CacheControl = "public, max-age=300";

        /// <summary>
        /// The media type the protected resource metadata document is served as.
        /// </summary>
        public const string ContentType = "application/json";

        /// <summary>
        /// The only RFC 9728 <c>bearer_methods_supported</c> value an OData service accepts: the
        /// <c>Authorization</c> header.
        /// </summary>
        public const string HeaderBearerMethod = "header";

        /// <summary>
        /// The RFC 9728 auth-param that points a client at the protected resource metadata document.
        /// </summary>
        public const string ResourceMetadataParameter = "resource_metadata";

        /// <summary>
        /// The RFC 6750 auth-param that names the scopes a client should request.
        /// </summary>
        public const string ScopeParameter = "scope";

        /// <summary>
        /// The RFC 9728 well-known path the protected resource metadata document is published under, both in
        /// its origin form and, suffixed with <c>/{prefix}</c>, in its path-suffixed form.
        /// </summary>
        public const string WellKnownPath = "/.well-known/oauth-protected-resource";

        #endregion

    }

}
