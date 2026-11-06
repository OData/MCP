// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System.ComponentModel.DataAnnotations.Schema;

namespace Microsoft.OData.Mcp.Tests.AspNetCore.Restier.Scenarios
{

    /// <summary>
    /// Started-ticket payload returned by the Restier <c>Authorize</c> fixture action.
    /// Restier operation return types must already exist in the EDM; this complex type is
    /// declared on <see cref="McpCustomer"/> so <c>Authorize</c> can return it.
    /// </summary>
    [ComplexType]
    public sealed class McpAuthorizationTicket
    {

        #region Properties

        /// <summary>
        /// Gets or sets the URL the operator opens to finish authorization.
        /// </summary>
        public string ConnectUri { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the ticket lifetime in seconds.
        /// </summary>
        public int? ExpiresIn { get; set; }

        /// <summary>
        /// Gets or sets the operator-facing message.
        /// </summary>
        public string Message { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the ticket status.
        /// </summary>
        public string Status { get; set; } = "Started";

        #endregion

    }

}
