// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using Microsoft.OData.Mcp.Tests.AspNetCore.Restier;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.OData.Mcp.Tests.Authentication.Restier
{

    /// <summary>
    /// Disposes the per-class Breakdance + outbound hosts when the assembly finishes.
    /// </summary>
    [TestClass]
    public sealed class OutboundRestierHostLeaseCleanup
    {

        #region Public Methods

        /// <summary>
        /// Tears down every shared Restier host this assembly still holds.
        /// </summary>
        [AssemblyCleanup]
        public static void DisposeLeases()
        {
            RestierToolTestBase.DisposeHostLeases();
        }

        #endregion

    }

}
