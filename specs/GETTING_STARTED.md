# Getting Started with OData MCP

This guide will help you get up and running with OData MCP (Model Context Protocol) in just a few minutes.

## Prerequisites

- .NET 8.0 or later
- An existing ASP.NET Core application with OData

## Installation

Install the OData MCP package:

```bash
dotnet add package Microsoft.OData.Mcp.Core
```

## Basic Setup (Zero Configuration)

The easiest way to enable MCP for your OData services is using the automatic registration:

```csharp
using Microsoft.OData.Mcp.Core.Extensions;

var builder = WebApplication.CreateBuilder(args);

// Configure your OData services as usual
builder.Services.AddControllers()
    .AddOData(options => options
        .AddRouteComponents("odata", GetEdmModel()));

// Enable MCP with one line!
builder.Services.AddODataMcp();

var app = builder.Build();

// Add MCP middleware
app.UseODataMcp();
app.UseRouting();
app.MapControllers();

app.Run();
```

That's it! MCP endpoints are now available at:
- `/odata/mcp` - Server information
- `/odata/mcp/tools` - Available tools
- `/odata/mcp/tools/{toolName}` - Tool details
- `/odata/mcp/tools/execute` - Execute tools

## What You Get

With zero configuration, OData MCP automatically provides:

### Query Tools
- Query any entity set with filtering, sorting, and pagination
- Support for `$filter`, `$orderby`, `$top`, `$skip`, `$select`, `$expand`
- Automatic parameter generation based on your model

### CRUD Operations
- Create new entities
- Read individual entities by key
- Update existing entities (PUT and PATCH)
- Delete entities

### Navigation
- Follow navigation properties
- Expand related entities
- Query related collections

### Batch Operations
- Execute multiple operations in a single request
- Transactional batch support

## Example: Querying Data

Once MCP is enabled, AI assistants can query your data:

```http
POST /odata/mcp/tools/execute
Content-Type: application/json

{
  "tool": "Customer.query",
  "parameters": {
    "filter": "City eq 'Seattle'",
    "orderby": "CompanyName",
    "top": 10
  }
}
```

## Multiple OData Routes

If you have multiple OData routes, MCP handles them automatically:

```csharp
builder.Services.AddControllers()
    .AddOData(options => options
        .AddRouteComponents("v1", GetV1Model())
        .AddRouteComponents("v2", GetV2Model())
        .AddRouteComponents("admin", GetAdminModel()));

// This enables MCP for ALL routes!
builder.Services.AddODataMcp();
```

Tools are automatically namespaced:
- `v1.Customer.query`
- `v2.Customer.query`
- `admin.User.query`

## Configuration Options

Customize MCP behavior as needed:

```csharp
builder.Services.AddODataMcp(options =>
{
    // Exclude certain routes
    options.ExcludeRoutes = new[] { "admin" };
    
    // Change tool naming
    options.ToolNamingPattern = "{route}.{entity}.{operation}";
    
    // Adjust limits
    options.DefaultPageSize = 50;
    options.MaxPageSize = 500;
});
```

## Testing Your Setup

### 1. Check MCP Information
```bash
curl http://localhost:5000/odata/mcp
```

### 2. List Available Tools
```bash
curl http://localhost:5000/odata/mcp/tools
```

### 3. Execute a Query
```bash
curl -X POST http://localhost:5000/odata/mcp/tools/execute \
  -H "Content-Type: application/json" \
  -d '{
    "tool": "Customer.query",
    "parameters": {
      "top": 5
    }
  }'
```

## Next Steps

- Read the [Magical Zero-Config Guide](MAGICAL_ZERO_CONFIG.md) for advanced automatic configuration
- Explore [Configuration Options](CONFIGURATION.md) for customization
- Learn about [Performance Optimization](PERFORMANCE.md)
- See [Examples](EXAMPLES.md) for common scenarios

## Troubleshooting

### MCP endpoints return 404
Ensure `app.UseODataMcp()` is called before `app.UseRouting()`

### Tools list is empty
Check that your OData model has entity sets defined

### "Tool not found" errors
Verify the tool name matches the pattern (e.g., `Customer.query` not `customer.query`)

## Getting Help

- [GitHub Issues](https://github.com/microsoft/odata-mcp/issues)
- [Documentation](https://docs.microsoft.com/odata/mcp)
- [Samples](https://github.com/microsoft/odata-mcp/samples)