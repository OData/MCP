using Microsoft.AspNetCore.Routing;

namespace Microsoft.OData.Mcp.AspNetCore.Routing
{
    /// <summary>
    /// Defines a contract for applying MCP conventions to OData routes.
    /// </summary>
    public interface IMcpRouteConvention
    {
        /// <summary>
        /// Applies MCP conventions to the endpoint route builder.
        /// </summary>
        /// <param name="endpointRouteBuilder">The endpoint route builder.</param>
        /// <param name="routePrefix">The OData route prefix.</param>
        /// <param name="routeName">The OData route name.</param>
        void ApplyConvention(IEndpointRouteBuilder endpointRouteBuilder, string routePrefix, string routeName);
    }
}