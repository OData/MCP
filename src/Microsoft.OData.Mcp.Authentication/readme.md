# Microsoft.OData.Mcp.Authentication

Outbound OAuth client used by **Local MCP** (`dotnet odata-mcp`) to sign in to an OData API you do not host.

You almost never reference this package. Install [Local MCP](../Microsoft.OData.Mcp.Tools/readme.md) and pass `--client-id` / `--auth-token` as `dotnet odata-mcp try` tells you.

This is **not** inbound MCP OAuth (agent → MCP HTTP). That stays with the MCP host. This package authenticates **OData HTTP** (`$metadata` and data).

If you **own** the API and want Local MCP to discover sign-in with no flags, use `AddODataProtectedResource` in [Remote MCP](../Microsoft.OData.Mcp.AspNetCore/readme.md).

## License

MIT.
