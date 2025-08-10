// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

namespace Microsoft.OData.Mcp.Authentication.Models
{
    /// <summary>
    /// Defines the sources from which client certificates can be loaded.
    /// </summary>
    public enum CertificateSource
    {
        /// <summary>
        /// Load certificate from the Windows certificate store.
        /// </summary>
        Store,

        /// <summary>
        /// Load certificate from a file on disk.
        /// </summary>
        File,

        /// <summary>
        /// Load certificate from Base64-encoded data in configuration.
        /// </summary>
        Base64
    }
}
