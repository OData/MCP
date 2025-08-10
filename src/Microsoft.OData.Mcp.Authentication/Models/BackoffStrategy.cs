// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

namespace Microsoft.OData.Mcp.Authentication.Models
{

    /// <summary>
    /// Defines the backoff strategies for retry delays.
    /// </summary>
    public enum BackoffStrategy
    {

        /// <summary>
        /// Use a fixed delay between all retry attempts.
        /// </summary>
        Fixed,

        /// <summary>
        /// Increase delay linearly with each retry attempt.
        /// </summary>
        Linear,

        /// <summary>
        /// Increase delay exponentially with each retry attempt.
        /// </summary>
        Exponential,

        /// <summary>
        /// Use exponential backoff with random jitter to prevent thundering herd.
        /// </summary>
        ExponentialWithJitter

    }

}
