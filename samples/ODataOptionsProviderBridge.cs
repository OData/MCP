using Microsoft.AspNetCore.OData;
using Microsoft.Extensions.Options;
using Microsoft.OData.Mcp.Core.Routing;

namespace Microsoft.OData.Mcp.Samples
{
    /// <summary>
    /// Sample implementation showing how to bridge ASP.NET Core OData options with MCP.
    /// </summary>
    /// <remarks>
    /// This class should be implemented in the host application to provide
    /// OData configuration information to the MCP system without creating
    /// a direct dependency on ASP.NET Core OData in the Core library.
    /// </remarks>
    public class ODataOptionsProviderBridge : IODataOptionsProvider
    {
        private readonly IOptions<ODataOptions> _odataOptions;

        /// <summary>
        /// Initializes a new instance of the <see cref="ODataOptionsProviderBridge"/> class.
        /// </summary>
        /// <param name="odataOptions">The OData options from ASP.NET Core OData.</param>
        public ODataOptionsProviderBridge(IOptions<ODataOptions> odataOptions)
        {
            _odataOptions = odataOptions ?? throw new ArgumentNullException(nameof(odataOptions));
        }

        /// <summary>
        /// Gets a value indicating whether dollar prefixes are disabled for query options.
        /// </summary>
        public bool EnableNoDollarQueryOptions => _odataOptions.Value.EnableNoDollarQueryOptions;

        /// <summary>
        /// Gets the route prefix for a specific OData route.
        /// </summary>
        /// <param name="routeName">The name of the OData route.</param>
        /// <returns>The route prefix, or null if not found.</returns>
        public string? GetRoutePrefix(string routeName)
        {
            // In ASP.NET Core OData, route prefixes are stored in RouteComponents
            if (_odataOptions.Value.RouteComponents.TryGetValue(routeName, out var routeComponent))
            {
                return routeComponent.RoutePrefix;
            }
            return null;
        }
    }
}

// Example registration in Program.cs:
/*
builder.Services.AddControllers()
    .AddOData(options => 
    {
        options.EnableNoDollarQueryOptions = true; // Disable $ prefixes
        options.AddRouteComponents("api/v1", GetV1Model());
        options.AddRouteComponents("api/v2", GetV2Model());
    });

// Register the bridge to provide OData options to MCP
builder.Services.AddSingleton<IODataOptionsProvider, ODataOptionsProviderBridge>();

// Enable MCP with automatic registration
builder.Services.AddODataMcp();

var app = builder.Build();
app.UseRouting();
app.UseODataMcp();
app.MapControllers();
*/