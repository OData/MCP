# Getting Started with OData MCP Server

This guide will help you get the OData MCP Server up and running in 5 minutes!

## Prerequisites

- .NET 8.0 or later
- An existing OData service (or use our sample)
- Basic familiarity with ASP.NET Core

## Quick Start

### 1. Install the NuGet Package

```bash
dotnet add package Microsoft.OData.Mcp.AspNetCore
```

### 2. Add to Your Application

```csharp
using Microsoft.OData.Mcp.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// Add OData MCP Server with minimal configuration
builder.Services.AddODataMcpServer(options =>
{
    options.ServiceUrl = "https://services.odata.org/V4/Northwind/Northwind.svc";
});

var app = builder.Build();

// Enable MCP endpoint
app.UseODataMcp();

app.Run();
```

### 3. Test Your Setup

The MCP server is now running! Test it with:

```bash
# Get server information
curl http://localhost:5000/mcp/info

# List available tools
curl http://localhost:5000/mcp/tools

# Get OData metadata
curl http://localhost:5000/mcp/metadata
```

## Understanding the Response

When you query `/mcp/tools`, you'll see automatically generated tools like:

```json
{
  "tools": [
    {
      "name": "queryCustomers",
      "description": "Query the Customers entity set",
      "parameters": {
        "filter": "OData filter expression (e.g., 'Country eq USA')",
        "select": "Properties to include",
        "orderby": "Sort order",
        "top": "Maximum number of results",
        "skip": "Number of results to skip"
      }
    },
    {
      "name": "getCustomer",
      "description": "Get a specific Customer by ID",
      "parameters": {
        "id": "The Customer ID"
      }
    },
    {
      "name": "createCustomer",
      "description": "Create a new Customer",
      "parameters": {
        "data": "Customer data as JSON"
      }
    }
    // ... more tools
  ]
}
```

## Connecting an AI Model

### Using Claude Desktop

1. Add to your Claude Desktop configuration:

```json
{
  "mcpServers": {
    "odata-northwind": {
      "url": "http://localhost:5000/mcp"
    }
  }
}
```

2. Claude can now use commands like:
   - "Show me all customers from Germany"
   - "Create a new product called 'AI Assistant'"
   - "What orders has customer ALFKI placed?"

### Using the MCP SDK

```typescript
import { Client } from '@modelcontextprotocol/sdk';

const client = new Client({
  serverUrl: 'http://localhost:5000/mcp'
});

// List available tools
const tools = await client.listTools();

// Execute a tool
const result = await client.executeTool({
  name: 'queryCustomers',
  parameters: {
    filter: "Country eq 'Germany'",
    top: 10
  }
});
```

## Next Steps

### Add Authentication

```csharp
builder.Services.AddODataMcpServer(options =>
{
    options.ServiceUrl = "https://api.example.com/odata";
    options.EnableAuthentication = true;
    options.AuthenticationScheme = "Bearer";
});

// Add authentication middleware
builder.Services.AddAuthentication("Bearer")
    .AddJwtBearer("Bearer", options =>
    {
        options.Authority = "https://login.microsoftonline.com/your-tenant";
        options.Audience = "your-api-audience";
    });
```

### Configure Tool Generation

```csharp
builder.Services.AddODataMcpServer(options =>
{
    options.ServiceUrl = "https://api.example.com/odata";
    
    // Customize tool generation
    options.ToolGeneration = new ToolGenerationOptions
    {
        IncludeNavigationTools = true,
        IncludeBatchOperations = true,
        MaxResultsPerQuery = 100,
        GenerateCrudTools = true
    };
});
```

### Use with Existing OData Endpoint

If you already have an OData controller:

```csharp
// Your existing OData configuration
builder.Services.AddControllers()
    .AddOData(options => options
        .Select().Filter().OrderBy().Expand().Count()
        .SetMaxTop(100));

// Add MCP on top
builder.Services.AddODataMcpServer(options =>
{
    options.UseLocalMetadata = true; // Use metadata from your controllers
});
```

## Common Patterns

### 1. Filtering Sensitive Data

```csharp
options.EntityFilter = (entityType) =>
{
    // Don't expose internal entities
    return !entityType.Name.StartsWith("Internal");
};
```

### 2. Custom Tool Names

```csharp
options.ToolNamingConvention = ToolNamingConvention.CamelCase;
options.ToolPrefix = "northwind_";
```

### 3. Rate Limiting

```csharp
builder.Services.AddRateLimiter(options =>
{
    options.AddPolicy("mcp", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: context.User?.Identity?.Name ?? "anonymous",
            factory: partition => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 100,
                Window = TimeSpan.FromMinute(1)
            }));
});

app.UseRateLimiter();
```

## Troubleshooting Quick Fixes

**Tools not appearing?**
- Check the OData service is accessible
- Verify metadata endpoint: `{ServiceUrl}/$metadata`
- Check logs for parsing errors

**Authentication failing?**
- Ensure authentication middleware is before `UseODataMcp()`
- Check token has required scopes
- Verify CORS settings for browser-based clients

**Performance issues?**
- Enable metadata caching: `options.EnableMetadataCaching = true`
- Use pagination: Always specify `$top` in queries
- Consider implementing result caching

## Learn More

- [Detailed Installation Guide](installation.md)
- [Configuration Reference](configuration.md)
- [Integration Guide](integration-guide.md)
- [Security Best Practices](security.md)