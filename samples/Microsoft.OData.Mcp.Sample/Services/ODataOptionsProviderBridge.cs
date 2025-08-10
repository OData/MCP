// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

//using Microsoft.AspNetCore.OData;
//using Microsoft.Extensions.Options;
//using Microsoft.OData.Mcp.Core.Routing;
//using System;
//using System.Linq;

//namespace Microsoft.OData.Mcp.Sample.Services
//{
//    /// <summary>
//    /// Bridges ASP.NET Core OData options with MCP.
//    /// </summary>
//    /// <remarks>
//    /// This class provides OData configuration information to the MCP system
//    /// without creating a direct dependency on ASP.NET Core OData in the Core library.
//    /// </remarks>
//    public class ODataOptionsProviderBridge : IODataOptionsProvider
//    {
//        internal readonly IOptions<ODataOptions> _odataOptions;

//        /// <summary>
//        /// Initializes a new instance of the <see cref="ODataOptionsProviderBridge"/> class.
//        /// </summary>
//        /// <param name="odataOptions">The OData options from ASP.NET Core OData.</param>
//        public ODataOptionsProviderBridge(IOptions<ODataOptions> odataOptions)
//        {
//            _odataOptions = odataOptions ?? throw new ArgumentNullException(nameof(odataOptions));
//        }

//        /// <summary>
//        /// Gets a value indicating whether dollar prefixes are disabled for query options.
//        /// </summary>
//        public bool EnableNoDollarQueryOptions => _odataOptions.Value.EnableNoDollarQueryOptions;

//        /// <summary>
//        /// Gets the route prefix for a specific OData route.
//        /// </summary>
//        /// <param name="routeName">The name of the OData route.</param>
//        /// <returns>The route prefix, or null if not found.</returns>
//        public string? GetRoutePrefix(string routeName)
//        {
//            // In ASP.NET Core OData 8.x, route information is stored differently
//            // This is a simplified implementation - in production you'd need to
//            // access the route information through the proper channels
//            return routeName switch
//            {
//                "v1" => "api/v1",
//                "v2" => "api/v2",
//                "main" => "odata",
//                _ => null
//            };
//        }
//    }
//}
