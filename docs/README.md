# Microsoft OData MCP Server Documentation

Welcome to the Microsoft OData MCP Server documentation! This system enables AI models to interact with OData APIs through the Model Context Protocol (MCP), automatically generating tools from OData metadata.

## What is OData MCP Server?

The OData MCP Server is a bridge that:
- **Discovers** OData services and their metadata
- **Generates** MCP tools automatically from OData CSDL
- **Exposes** these tools to AI models through the MCP protocol
- **Handles** authentication, authorization, and secure API access

## Documentation Overview

### Getting Started
- [Quick Start Guide](getting-started.md) - Get up and running in 5 minutes
- [Installation](installation.md) - Detailed installation instructions
- [Configuration](configuration.md) - Configuration options and settings

### Integration
- [Integration Guide](integration-guide.md) - Integrate with existing OData APIs
- [Security Setup](security.md) - Configure authentication and authorization
- [Examples](examples.md) - Real-world examples and use cases

### Reference
- [API Reference](api-reference.md) - Detailed API documentation
- [Troubleshooting](troubleshooting.md) - Common issues and solutions

## Key Features

✨ **Automatic Tool Generation** - No manual tool definition needed  
🔒 **Built-in Security** - OAuth2/JWT authentication support  
🚀 **High Performance** - Caching and optimized tool generation  
🔧 **Flexible Deployment** - Run as middleware or standalone server  
📊 **Full OData Support** - Queries, relationships, functions, actions  
🤖 **AI-Ready** - Designed for LLM interaction patterns

## Architecture Overview

```
┌─────────────────┐     ┌─────────────────┐     ┌─────────────────┐
│   AI/LLM Model  │────▶│  MCP Protocol   │────▶│  OData Service  │
└─────────────────┘     └─────────────────┘     └─────────────────┘
                               │
                               ▼
                        ┌─────────────────┐
                        │  OData MCP      │
                        │  Server         │
                        │                 │
                        │ • Tool Gen      │
                        │ • Auth          │
                        │ • Routing       │
                        └─────────────────┘
```

## Quick Example

```csharp
// Add to your ASP.NET Core application
var builder = WebApplication.CreateBuilder(args);

// Add OData MCP Server
builder.Services.AddODataMcpServer(options =>
{
    options.ServiceUrl = "https://api.example.com/odata";
    options.EnableAuthentication = true;
});

var app = builder.Build();

// Use OData MCP middleware
app.UseODataMcp("/mcp");

app.Run();
```

This automatically exposes your OData service to AI models through MCP!

## Use Cases

- **Enterprise Data Access** - Enable AI to query and update business data
- **API Integration** - Connect AI models to existing OData APIs
- **Automation** - Build AI-powered automation workflows
- **Data Analysis** - Let AI analyze and report on OData sources
- **Customer Service** - AI agents accessing customer data via OData

## Getting Help

- 📖 Read the [Getting Started Guide](getting-started.md)
- 💬 Check [Troubleshooting](troubleshooting.md) for common issues
- 🐛 Report issues on [GitHub](https://github.com/microsoft/odata-mcp-server)
- 📧 Contact support at odata-mcp@microsoft.com

## Contributing

We welcome contributions! Please see our [Contributing Guide](../CONTRIBUTING.md) for details.

## License

This project is licensed under the MIT License - see the [LICENSE](../LICENSE) file for details.