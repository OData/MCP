// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.OData.Mcp.Core.Models;
using Microsoft.OData.Mcp.Core.Parsing;
using Microsoft.OData.Mcp.Tests.Shared;

namespace Microsoft.OData.Mcp.Tests.Core
{

    /// <summary>
    /// Loads live OData metadata into Core EDM models.
    /// </summary>
    internal static class LiveMetadata
    {

        #region Public Methods

        /// <summary>
        /// Fetches Northwind <c>$metadata</c> and parses it.
        /// </summary>
        /// <returns>
        /// The parsed Northwind model.
        /// </returns>
        internal static async Task<EdmModel> LoadNorthwindModelAsync()
        {
            using var http = new HttpClient();
            var xml = await http.GetStringAsync($"{LiveOData.Northwind.TrimEnd('/')}/$metadata");

            return new CsdlParser().ParseFromString(xml);
        }

        /// <summary>
        /// Fetches TripPin <c>$metadata</c> and parses it.
        /// </summary>
        /// <returns>
        /// The parsed TripPin model.
        /// </returns>
        internal static async Task<EdmModel> LoadTripPinModelAsync()
        {
            using var http = new HttpClient();
            var xml = await http.GetStringAsync($"{LiveOData.TripPin.TrimEnd('/')}/$metadata");

            return new CsdlParser().ParseFromString(xml);
        }

        #endregion

    }

}
