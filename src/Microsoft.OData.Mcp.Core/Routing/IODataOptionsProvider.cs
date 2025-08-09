namespace Microsoft.OData.Mcp.Core.Routing
{

    /// <summary>
    /// Provides access to OData configuration options.
    /// </summary>
    /// <remarks>
    /// This interface allows the MCP system to detect OData settings
    /// without directly depending on ASP.NET Core OData packages.
    /// </remarks>
    public interface IODataOptionsProvider
    {

        /// <summary>
        /// Gets a value indicating whether dollar prefixes are disabled for query options.
        /// </summary>
        /// <value>
        /// <c>true</c> if dollar prefixes are disabled (EnableNoDollarQueryOptions = true);
        /// otherwise, <c>false</c>.
        /// </value>
        bool EnableNoDollarQueryOptions { get; }

        /// <summary>
        /// Gets the route prefix for a specific OData route.
        /// </summary>
        /// <param name="routeName">The name of the OData route.</param>
        /// <returns>The route prefix, or null if not found.</returns>
        string? GetRoutePrefix(string routeName);

    }

}
