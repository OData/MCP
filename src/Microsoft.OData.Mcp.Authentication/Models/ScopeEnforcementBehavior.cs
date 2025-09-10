// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

namespace Microsoft.OData.Mcp.Authentication.Models
{

    /// <summary>
    /// Defines the behavior when required scopes are missing.
    /// </summary>
    public enum ScopeEnforcementBehavior
    {

        /// <summary>
        /// Deny access to the operation or tool when required scopes are missing.
        /// </summary>
        DenyAccess,

        /// <summary>
        /// Filter out tools and operations that the user cannot access due to missing scopes.
        /// </summary>
        FilterTools,

        /// <summary>
        /// Log the authorization decision but allow access even when scopes are missing.
        /// </summary>
        LogOnly

    }

}
