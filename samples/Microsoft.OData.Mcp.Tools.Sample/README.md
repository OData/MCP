# OData MCP Console Sample

This sample demonstrates how to use the standalone OData MCP Server to connect to external OData APIs and expose them through the Model Context Protocol for AI assistants like Claude.

## Overview

The console-based MCP server is designed for **Concept B** from our architecture:
- A standalone console application
- Connects to any external OData API
- Exposes OData operations as MCP tools
- Perfect for local development and AI integration

## Quick Start

### Method 1: Direct URL
```bash
# Connect to the Northwind sample service
dotnet run https://services.odata.org/V4/Northwind/Northwind.svc/$metadata

# Connect to your own OData API
dotnet run https://your-api.com/odata/$metadata
```

### Method 2: Configuration File
```bash
# Uses appsettings.json configuration
dotnet run
```

### Method 3: Command Line Tool
```bash
# Build and install as global tool
dotnet pack
dotnet tool install -g odata-mcp

# Run against any OData API
odata-mcp start "https://your-api.com/odata/$metadata"
```

## Configuration

Edit `appsettings.json` to configure the server:

```json
{
  "McpServer": {
    "ODataService": {
      "BaseUrl": "https://services.odata.org/northwind/northwind.svc",
      "MetadataPath": "/$metadata",
      "Authentication": {
        "Type": "Bearer",
        "Token": "your-token-here"
      }
    }
  }
}
```

## Features

- **Auto-discovery**: Automatically discovers all entities and operations from OData metadata
- **Dynamic tool generation**: Creates MCP tools for each entity set and operation
- **Caching**: Intelligent caching of metadata and query results
- **Health checks**: Built-in health monitoring for the OData service
- **Multiple authentication methods**: Supports Bearer tokens, API keys, and more

## Available Tools

Once connected to an OData service, the following tools are automatically generated:

- `{EntitySet}.Query` - Query entities with OData filters
- `{EntitySet}.Get` - Get a single entity by key
- `{EntitySet}.Create` - Create new entities
- `{EntitySet}.Update` - Update existing entities
- `{EntitySet}.Delete` - Delete entities
- `{EntitySet}.Navigate` - Navigate relationships

## Example Usage with Claude

1. Start the server:
   ```bash
   dotnet run https://services.odata.org/V4/Northwind/Northwind.svc/$metadata
   ```

2. In Claude, connect to the MCP server and use the tools:
   ```
   // Query customers from Germany
   Customers.Query({ filter: "Country eq 'Germany'" })
   
   // Get a specific order
   Orders.Get({ key: 10248 })
   
   // Navigate from order to customer
   Orders.Navigate({ key: 10248, navigation: "Customer" })
   ```

## Environment Variables

- `MCP_ODATA_URL` - Override the OData service URL
- `MCP_LOG_LEVEL` - Set logging level (Debug, Info, Warning, Error)
- `MCP_CACHE_ENABLED` - Enable/disable caching (true/false)

## Troubleshooting

1. **Connection Issues**: Check that the OData service is accessible and the URL is correct
2. **Authentication Errors**: Verify your authentication configuration in appsettings.json
3. **Tool Generation**: Use `--debug` flag to see detailed tool generation logs

## Advanced Scenarios

### Custom Authentication
```json
{
  "Authentication": {
    "Type": "Custom",
    "Headers": {
      "X-API-Key": "your-key",
      "X-Client-Id": "your-client"
    }
  }
}
```

### Proxy Configuration
```json
{
  "Network": {
    "Proxy": {
      "Address": "http://proxy.company.com:8080",
      "BypassOnLocal": true
    }
  }
}
```

### Performance Tuning
```json
{
  "ToolGeneration": {
    "MaxQueryDepth": 5,
    "EnableBatchOperations": true,
    "ParallelRequests": 4
  }
}
```