# Microsoft.OData.Mcp

[![NuGet](https://img.shields.io/nuget/v/Microsoft.OData.Mcp.Core.svg)](https://www.nuget.org/packages/Microsoft.OData.Mcp.Core/)
[![Build Status](https://img.shields.io/azure-devops/build/microsoft/odata-mcp/main.svg)](https://dev.azure.com/microsoft/odata-mcp)
[![License](https://img.shields.io/github/license/microsoft/odata-mcp.svg)](LICENSE)

Enable AI assistants to interact with your OData services through the Model Context Protocol (MCP). 

## ✨ Features

- **Dynamic Tool Generation** - Automatically generates MCP tools from OData metadata
- **Two Deployment Models** - Embedded (AspNetCore) or Standalone (Console)
- **Real HTTP Operations** - All tools execute actual OData HTTP requests
- **Comprehensive Coverage** - CRUD, Query, Navigation, and Advanced OData operations
- **Claude Code Ready** - Works immediately with Claude Code and other MCP clients
- **High Performance** - Efficient tool generation with caching and optimizations

## 🚀 Quick Start

### Option 1: Standalone Console (Connect to ANY OData Service)

```bash
# Install the tool globally
dotnet tool install -g Microsoft.OData.Mcp.Tools

# Start the MCP server for Northwind service
odata-mcp start --url https://services.odata.org/V4/Northwind/Northwind.svc

# Or with authentication
odata-mcp start --url https://your-api.com/odata --auth-token YOUR_TOKEN
```

### Option 2: Embedded in ASP.NET Core

```csharp
// 1. Install the package
// dotnet add package Microsoft.OData.Mcp.AspNetCore

// 2. Add to your Program.cs
builder.Services.AddControllers()
    .AddOData(options => options
        .AddRouteComponents("odata", GetEdmModel()));

// 3. Enable MCP - that's it!
builder.Services.AddODataMcp();

var app = builder.Build();
app.UseODataMcp();
app.UseRouting();
app.MapControllers();
```

Your OData service now has MCP endpoints:
- `/odata/mcp` - Server information
- `/odata/mcp/tools` - Available AI tools
- `/odata/mcp/tools/execute` - Execute operations

## 📖 What is MCP?

The Model Context Protocol (MCP) is an open standard that enables AI assistants to interact with external systems. With OData MCP, AI assistants can:

- Query your data using OData syntax
- Create, update, and delete entities
- Navigate relationships
- Execute batch operations
- Use advanced OData features ($filter, $orderby, $expand, etc.)

## 🤖 Using with Claude Code

### 1. Register the MCP Server

Add to your Claude Code configuration:

**Windows** (`%APPDATA%\Claude\claude.conf.json`):
```json
{
  "mcpServers": {
    "odata-northwind": {
      "command": "odata-mcp",
      "args": ["start", "--url", "https://services.odata.org/V4/Northwind/Northwind.svc"]
    }
  }
}
```

**macOS/Linux** (`~/.config/claude/claude.conf.json`):
```json
{
  "mcpServers": {
    "odata-northwind": {
      "command": "odata-mcp",
      "args": ["start", "--url", "https://services.odata.org/V4/Northwind/Northwind.svc"]
    }
  }
}
```

### 2. Use in Claude

Once registered, you can ask Claude to:

- "List all products from Northwind"
- "Get customer ALFKI details"
- "Show me orders from 2024 sorted by date"
- "Find products with price > $50"
- "Create a new product category"

## 🔧 Generated Tools

The MCP server dynamically generates tools based on your OData metadata:

### CRUD Operations
- `create_[entity]` - Create new entities
- `get_[entity]` - Retrieve entities by key
- `update_[entity]` - Update existing entities
- `delete_[entity]` - Delete entities

### Query Operations
- `list_[entityset]` - List entities with filtering and pagination
- `odata_query` - Execute advanced OData queries

### Examples

```javascript
// Get a specific product
{
  "tool": "get_product",
  "parameters": {
    "id": 1
  }
}

// List products with filtering
{
  "tool": "list_products",
  "parameters": {
    "filter": "UnitPrice gt 20",
    "orderby": "ProductName",
    "top": 10
  }
}

// Advanced OData query
{
  "tool": "odata_query",
  "parameters": {
    "query": "Products?$filter=Category/CategoryName eq 'Beverages'&$expand=Category"
  }
}
```

## 🔐 Authentication

### Bearer Token
```bash
odata-mcp start --url https://api.example.com/odata --auth-token YOUR_TOKEN
```

### Configuration File
```json
{
  "McpServer": {
    "ODataService": {
      "BaseUrl": "https://api.example.com/odata",
      "AuthToken": "YOUR_TOKEN"
    }
  }
}
```

```bash
odata-mcp start --config settings.json
```

## 📚 Advanced Configuration

### Tool Generation Options

Control which tools are generated:

```csharp
services.AddODataMcp(options =>
{
    options.GenerateCrudTools = true;      // CRUD operations
    options.GenerateQueryTools = true;     // List and query operations  
    options.GenerateNavigationTools = true; // Relationship navigation
    options.MaxToolCount = 100;            // Limit total tools
    options.IncludeExamples = true;        // Add usage examples
});
```

### Multiple OData Routes

```csharp
builder.Services
    .AddOData(options => options
        .AddRouteComponents("v1", GetV1Model())
        .AddRouteComponents("v2", GetV2Model()))
    .AddODataMcp(); // Automatically handles all routes
```

## 🧪 Testing

The project includes comprehensive tests using real OData services (no mocking):

```bash
# Run all tests
dotnet test

# Run with coverage
dotnet test /p:CollectCoverage=true /p:CoverageOutputFormat=opencover
```

## 🛠️ Troubleshooting

### "Failed to reconnect to odata-server"
- Ensure the OData service URL is accessible
- Check authentication tokens are valid
- Verify network connectivity

### "Tool not found"
- Tools are generated from metadata - ensure your OData service exposes metadata
- Check the tool name matches the generated names (use `list_tools` to see all)

### Performance Issues
- Adjust `MaxToolCount` to limit tool generation
- Use specific entity queries instead of full collection queries
- Enable response caching in your OData service

## 📦 NuGet Packages

- `Microsoft.OData.Mcp.Core` - Core functionality and abstractions
- `Microsoft.OData.Mcp.AspNetCore` - ASP.NET Core integration
- `Microsoft.OData.Mcp.Tools` - Standalone CLI tool
- `Microsoft.OData.Mcp.Authentication` - Authentication providers

## 🤝 Contributing

We welcome contributions! Please see [CONTRIBUTING.md](CONTRIBUTING.md) for details.

## 📄 License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## 🔗 Related Projects

- [Model Context Protocol](https://github.com/modelcontextprotocol)
- [OData](https://www.odata.org/)
- [Claude Code](https://claude.ai/code)

## 📞 Support

- **Issues**: [GitHub Issues](https://github.com/microsoft/odata-mcp/issues)
- **Discussions**: [GitHub Discussions](https://github.com/microsoft/odata-mcp/discussions)
- **Email**: odata@microsoft.com