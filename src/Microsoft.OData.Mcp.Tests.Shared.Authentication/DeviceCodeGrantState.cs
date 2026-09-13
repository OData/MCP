// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

namespace Microsoft.OData.Mcp.Tests.Shared.Authentication
{

    /// <summary>
    /// Tracks a single RFC 8628 device authorization grant from the device code request through to token issuance.
    /// </summary>
    /// <remarks>
    /// The state is mutated by the token endpoint (poll counting) and by
    /// <see cref="LocalAuthorizationServer.Approve(string)"/>, so an instance is never reused across grants.
    /// </remarks>
    public sealed class DeviceCodeGrantState
    {

        #region Properties

        /// <summary>
        /// Gets or sets a value indicating whether the end user has approved the grant.
        /// </summary>
        public bool Approved { get; set; }

        /// <summary>
        /// Gets or sets the client identifier that started the grant.
        /// </summary>
        public string ClientId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the opaque device code the client polls the token endpoint with.
        /// </summary>
        public string DeviceCode { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the number of token polls this grant has received.
        /// </summary>
        public int Polls { get; set; }

        /// <summary>
        /// Gets or sets the space-delimited scope the client requested.
        /// </summary>
        public string Scope { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the short code the end user types at the verification URI.
        /// </summary>
        public string UserCode { get; set; } = string.Empty;

        #endregion

    }

}
