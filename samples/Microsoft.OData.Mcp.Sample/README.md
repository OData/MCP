# OData MCP Sample - Real In-Memory OData Service

This sample demonstrates the **magical zero-configuration** integration of MCP (Model Context Protocol) with a real, functioning OData service using in-memory data.

## 🚀 Quick Start

1. **Run the sample:**
   ```bash
   cd samples/Microsoft.OData.Mcp.Sample
   dotnet run
   ```

2. **Access the service:**
   - OData endpoints: `https://localhost:5001/odata`, `/api/v1`, `/api/v2`
   - MCP endpoints: `https://localhost:5001/odata/mcp`, `/api/v1/mcp`, `/api/v2/mcp`
   - Swagger UI: `https://localhost:5001/swagger`

## ✨ What This Sample Demonstrates

### AddODataMcp and UseODataMcp
- `services.AddODataMcp()` registers MCP services
- `app.UseODataMcp()` maps official MCP at `{prefix}/mcp` after OData routes exist
- Hide a prefix with `IncludePrefixes` or `ExcludeRoutes` on host options

### Multiple OData Routes
- **V1 API** (`/api/v1`) - Basic entities (Customers, Orders)
- **V2 API** (`/api/v2`) - Extended entities (+ Products, Categories)
- **Main API** (`/odata`) - Full feature set with all entities

### Real Data Operations
- In-memory database with sample data
- Full CRUD operations
- Navigation properties and relationships
- OData query support ($filter, $select, $expand, etc.)

## 📊 Sample Data

The service includes pre-populated sample data:
- 4 Customers (Contoso, Adventure Works, Northwind, Wide World Importers)
- 5 Products across 4 Categories
- 3 Orders with Order Items
- Full relationships between entities

## 🔧 Architecture

```
┌─────────────────────┐
│   ASP.NET Core      │
│   Application       │
├─────────────────────┤
│  OData Controllers  │ ← Standard OData implementation
├─────────────────────┤
│  OData Middleware   │ ← Handles OData routing
├─────────────────────┤
│  MCP at {prefix}/mcp│ ← SDK MapMcp from UseODataMcp()
├─────────────────────┤
│ In-Memory DataStore │ ← Thread-safe data storage
└─────────────────────┘
```

## 🎯 MCP Endpoints

Each OData route automatically gets MCP endpoints:

### `/odata/mcp`
```json
GET /odata/mcp
{
  "name": "Sample OData MCP Server",
  "version": "1.0.0",
  "description": "A real in-memory OData service..."
}
```

### `/odata/mcp/tools`
```json
GET /odata/mcp/tools
{
  "tools": [
    {
      "name": "odata.Customer.query",
      "description": "Query customers with filtering..."
    },
    {
      "name": "odata.Product.query",
      "description": "Query products with filtering..."
    }
    // ... many more tools
  ]
}
```

## 🛠️ Available Tools

Tools are automatically namespaced by route:

### Query Tools
- `{route}.Customer.query` - Query customers
- `{route}.Order.query` - Query orders  
- `{route}.Product.query` - Query products (V2 and main only)
- `{route}.Category.query` - Query categories (V2 and main only)

### CRUD Tools
- `{route}.{Entity}.create` - Create new entities
- `{route}.{Entity}.get` - Get single entity by key
- `{route}.{Entity}.update` - Update existing entity
- `{route}.{Entity}.delete` - Delete entity

### Navigation Tools
- `{route}.Customer.getOrders` - Get customer's orders
- `{route}.Order.getCustomer` - Get order's customer
- `{route}.Product.getCategory` - Get product's category

### Action Tools
- `{route}.Order.cancel` - Cancel an order
- `{route}.Order.process` - Process an order
- `{route}.Order.ship` - Ship an order
- `{route}.Product.discontinue` - Discontinue a product
- `{route}.Product.applyDiscount` - Apply discount to product

## 📝 Example MCP Interactions

### Query Customers
```http
POST /odata/mcp/tools/execute
Content-Type: application/json

{
  "tool": "odata.Customer.query",
  "parameters": {
    "filter": "Country eq 'USA'",
    "orderby": "Name",
    "top": 5,
    "select": "Id,Name,Email,Country"
  }
}
```

### Create Product
```http
POST /odata/mcp/tools/execute
Content-Type: application/json

{
  "tool": "odata.Product.create",
  "parameters": {
    "entity": "{\"Name\":\"New Product\",\"UnitPrice\":29.99,\"CategoryId\":1}"
  }
}
```

### Navigate Relationships
```http
POST /odata/mcp/tools/execute
Content-Type: application/json

{
  "tool": "odata.Customer.getOrders",
  "parameters": {
    "key": "1",
    "filter": "TotalAmount gt 1000"
  }
}
```

## 🧪 Testing the Sample

### Using the Included Examples
```bash
cd Examples
dotnet run --project McpClientExample.csproj
```

### Using cURL
```bash
# Get MCP info
curl https://localhost:5001/odata/mcp

# List tools
curl https://localhost:5001/odata/mcp/tools

# Query customers
curl -X POST https://localhost:5001/odata/mcp/tools/execute \
  -H "Content-Type: application/json" \
  -d '{"tool":"odata.Customer.query","parameters":{"top":5}}'
```

### Using Postman
Import the included `OData-MCP-Sample.postman_collection.json` for pre-configured requests.

## 🔍 Key Implementation Details

### Program.cs
```csharp
// Standard OData configuration
builder.Services.AddControllers()
    .AddOData(options => options
        .AddRouteComponents("api/v1", GetV1Model())
        .AddRouteComponents("api/v2", GetV2Model())
        .AddRouteComponents("odata", GetMainModel()));

builder.Services.AddODataMcp();

var app = builder.Build();
app.MapControllers();
app.UseODataMcp();
```

### How It Works
1. `AddODataMcp()` registers MCP services (DI only)
2. `MapControllers()` maps OData routes
3. `UseODataMcp()` discovers those prefixes and maps `{prefix}/mcp`
4. Tools are generated from the EDM model

## 🎨 Customization Options

```csharp
builder.Services.AddODataMcp(options =>
{
    // Exclude specific routes
    options.ExcludeRoutes = new[] { "internal" };
    
    // Custom tool naming
    options.ToolNamingPattern = "{route}.{entity}.{operation}";
    
    // Performance tuning
    options.UseAggressiveCaching = true;
    options.DefaultPageSize = 50;
});
```

## 📚 Learn More

- [ASP.NET Core OData](https://docs.microsoft.com/odata)
- [Model Context Protocol](https://modelcontextprotocol.com)

## 🤝 Contributing

This sample is part of the Microsoft.OData.Mcp project. Contributions are welcome!

## 📄 License

MIT License - see [LICENSE](../../LICENSE) file for details.