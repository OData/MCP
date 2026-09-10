# Microsoft.OData.Mcp.Core

Shared library for **Microsoft OData for AI**. It turns an OData model into MCP tools and resources.

You almost never reference this package directly.

- Talking to an OData API from your machine → [Local MCP](../Microsoft.OData.Mcp.Tools/readme.md) (`Microsoft.OData.Mcp.Tools`)
- Exposing your ASP.NET Core OData API to assistants → [Remote MCP](../Microsoft.OData.Mcp.AspNetCore/readme.md) (`Microsoft.OData.Mcp.AspNetCore`)

Take a dependency on Core only if you are building a custom host. Public types live in `Microsoft.OData.Mcp.Core` (catalog, CSDL parser, models). There is no `Microsoft.OData.Edm` reference here.

## License

MIT.
