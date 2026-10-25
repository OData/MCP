# OData MCP Sample

A real in-memory OData API with Remote MCP on it. Two lines in `Program.cs`, then point a harness at `/odata/mcp`.

## Run it

```bash
cd samples/Microsoft.OData.Mcp.Sample
dotnet run
```

- OData: `https://localhost:59982/odata`, `/api/v1`, `/api/v2`
- MCP: `https://localhost:59982/odata/mcp`, `/api/v1/mcp`, `/api/v2/mcp`

Paste `https://localhost:59982/odata/mcp` into Claude Desktop, Cursor, Claude Code, or VS Code. Then ask:

- Tell me about customer Contoso.
- Who has orders over 1000?
- Apply a 15% discount to product 1.

## What you get

Each OData prefix gets its own MCP endpoint. Named tools come from the model: `list_customers`, `get_customer`, `create_order`, `action_order_ship`, `function_gettopcustomers`. Generic `odata_*` tools cover everything else.

`Ship` is marked `[HasMcpTask]`, so a harness that opted into Tasks waits instead of treating the call as done the instant the action returns.

OData at `/odata` is unchanged.

## Add it (the whole host)

```csharp
builder.Services.AddControllers()
    .AddOData(options => options
        .AddRouteComponents("api/v1", SampleEdmModel.GetV1Model())
        .AddRouteComponents("api/v2", SampleEdmModel.GetV2Model())
        .AddRouteComponents("odata", SampleEdmModel.GetMainModel()));

builder.Services.AddODataMcp();

var app = builder.Build();
app.MapControllers();
app.UseODataMcp();
```

## Sample data

- 4 customers (Contoso, Adventure Works, Northwind, Wide World Importers)
- 5 products across 4 categories
- 3 orders with line items

## License

MIT. See [LICENSE](../../LICENSE).
