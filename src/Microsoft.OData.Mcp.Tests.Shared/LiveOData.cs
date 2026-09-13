// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

namespace Microsoft.OData.Mcp.Tests.Shared
{

    /// <summary>
    /// Official OData sample service URLs used by live tests.
    /// </summary>
    /// <remarks>
    /// Tests must call these services directly. Do not substitute mocks or local stubs
    /// for Northwind or TripPin.
    /// </remarks>
    public static class LiveOData
    {

        #region Fields

        /// <summary>
        /// Gets the Northwind V4 service root (read-only).
        /// </summary>
        public const string Northwind = "https://services.odata.org/V4/Northwind/Northwind.svc";

        /// <summary>
        /// Gets the TripPin RESTier service root (read/write).
        /// </summary>
        public const string TripPin = "https://services.odata.org/TripPinRESTierService";

        #endregion

    }

}
