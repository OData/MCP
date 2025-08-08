# Microsoft.OData.Mcp

[![NuGet](https://img.shields.io/nuget/v/Microsoft.OData.Mcp.Core.svg)](https://www.nuget.org/packages/Microsoft.OData.Mcp.Core/)
[![Build Status](https://img.shields.io/azure-devops/build/microsoft/odata-mcp/main.svg)](https://dev.azure.com/microsoft/odata-mcp)
[![License](https://img.shields.io/github/license/microsoft/odata-mcp.svg)](LICENSE)

Enable AI assistants to interact with your OData services through the Model Context Protocol (MCP). 

## ✨ Features

- **Zero Configuration** - Just add `services.AddODataMcp()` and it works!
- **Automatic Discovery** - MCP endpoints for all your OData routes
- **High Performance** - Zero-allocation routing, frozen collections, startup-time caching
- **Tool Generation** - Query, CRUD, navigation, and batch operations out of the box
- **Multi-Route Support** - Automatic namespacing for multiple OData endpoints
- **Flexible Control** - Opt-out of routes, customize naming, explicit configuration when needed

## 🚀 Quick Start

```csharp
// 1. Install the package
// dotnet add package Microsoft.OData.Mcp.Core

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
- Understand your data model through metadata

## 🎯 Use Cases

- **AI-Powered Analytics** - Let AI assistants analyze your business data
- **Automated Reporting** - Generate reports through natural language
- **Data Integration** - Connect AI workflows to your OData services
- **Customer Support** - Enable AI to look up customer information
- **Process Automation** - Automate CRUD operations through AI

## 📚 Documentation

- [Getting Started](docs/GETTING_STARTED.md) - Set up in 5 minutes
- [Magical Zero-Config](docs/MAGICAL_ZERO_CONFIG.md) - How automatic registration works
- [Configuration](docs/CONFIGURATION.md) - Customization options
- [Examples](docs/EXAMPLES.md) - Common scenarios
- [API Reference](docs/API.md) - Detailed API documentation

## 🔧 Advanced Configuration

```csharp
builder.Services.AddODataMcp(options =>
{
    // Exclude internal routes
    options.ExcludeRoutes = new[] { "admin", "system" };
    
    // Custom tool naming
    options.ToolNamingPattern = "{route}.{entity}.{operation}";
    
    // Performance tuning
    options.UseAggressiveCaching = true;
    options.DefaultPageSize = 100;
    
    // Enable dynamic models (off by default)
    options.EnableDynamicModels = true;
});
```

## 🏗️ Architecture Highlights

### Zero-Allocation Performance
- Route parsing with `ReadOnlySpan<char>`
- `FrozenDictionary` for O(1) lookups
- No regex, minimal allocations

### Automatic Integration
- Hooks into OData route registration
- Creates MCP endpoints as siblings to `$metadata`
- Respects OData configuration (e.g., `EnableNoDollarQueryOptions`)

### Tool Generation
- Analyzes EDM model at startup
- Generates strongly-typed tools
- Caches for optimal runtime performance

## 📦 Multi-Route Support

```csharp
// Multiple OData routes
builder.Services.AddOData(options => options
    .AddRouteComponents("api/v1", GetV1Model())
    .AddRouteComponents("api/v2", GetV2Model())
    .AddRouteComponents("odata", GetMainModel()));

// MCP enables for ALL automatically
builder.Services.AddODataMcp();
```

Tools are namespaced to prevent conflicts:
- `v1.Customer.query`
- `v2.Customer.query`
- `odata.Customer.query`

## 🔌 Integration with AI Assistants

### Claude Desktop
```json
{
  "servers": {
    "my-odata-api": {
      "command": "curl",
      "args": ["http://localhost:5000/odata/mcp"]
    }
  }
}
```

### Custom AI Applications
```python
import httpx

# Discover available tools
tools = httpx.get("http://api.example.com/odata/mcp/tools").json()

# Execute a query
result = httpx.post(
    "http://api.example.com/odata/mcp/tools/execute",
    json={
        "tool": "Customer.query",
        "parameters": {
            "filter": "Country eq 'USA'",
            "top": 10
        }
    }
).json()
```

## 🤝 Contributing

We welcome contributions! Please see our [Contributing Guide](CONTRIBUTING.md) for details.

## 📄 License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## 🙏 Acknowledgments

Built on top of:
- [ASP.NET Core OData](https://github.com/OData/AspNetCoreOData)
- [Model Context Protocol](https://modelcontextprotocol.com)

## 🔗 Links

- [NuGet Package](https://www.nuget.org/packages/Microsoft.OData.Mcp.Core/)
- [GitHub Repository](https://github.com/microsoft/odata-mcp)
- [Issue Tracker](https://github.com/microsoft/odata-mcp/issues)
- [OData Documentation](https://www.odata.org/)
- [MCP Specification](https://spec.modelcontextprotocol.com/)
