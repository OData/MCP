# Microsoft.OData.Mcp.Tools

A command-line tool that bridges external OData APIs to the Model Context Protocol (MCP), enabling AI assistants to interact with any OData service.

## Overview

This tool acts as an MCP server that:
1. Connects to any external OData API
2. Automatically discovers the API's data model from its metadata
3. Exposes OData operations as MCP tools that AI assistants can use
4. Translates MCP tool calls into OData queries and forwards them to the external API

## Installation

### As a .NET Tool

```bash
dotnet tool install -g Microsoft.OData.Mcp.Tools
```

### From Source

```bash
dotnet build
dotnet pack
dotnet tool install --global --add-source ./bin/Debug Microsoft.OData.Mcp.Tools
```

## Usage

### Basic Usage (STDIO Mode - Default)

The tool runs in STDIO mode by default, which is the standard transport for MCP servers:

```bash
# Connect to a public OData service
odata-mcp start https://services.odata.org/V4/Northwind/Northwind.svc/$metadata

# With authentication
odata-mcp start https://api.example.com/odata/$metadata --auth-token "Bearer YOUR_TOKEN"

# With verbose logging (logs to stderr, doesn't interfere with protocol)
odata-mcp start https://api.example.com/odata/$metadata --verbose
```

### HTTP Mode (Optional for Debugging)

For debugging or special scenarios, you can run in HTTP mode by specifying a port:

```bash
# Run on port 3000
odata-mcp start https://api.example.com/odata/$metadata --port 3000
```

### Test Command

Test that an OData service is accessible and parse its metadata:

```bash
odata-mcp test https://services.odata.org/V4/Northwind/Northwind.svc/$metadata
```

## How It Works

### 1. Metadata Discovery
When started, the tool:
- Fetches the OData metadata document from the specified URL
- Parses the CSDL (Common Schema Definition Language) to understand:
  - Entity types and their properties
  - Entity sets (collections)
  - Relationships between entities
  - Available operations

### 2. Tool Generation
Based on the metadata, it automatically generates MCP tools:
- **QueryEntitySet** - Query collections with OData filters, sorting, pagination
- **GetEntity** - Retrieve a single entity by key
- **CreateEntity** - Create new entities
- **GetMetadata** - Retrieve the raw metadata document
- **DiscoverEntitySets** - List all available entity sets
- **DescribeEntityType** - Get detailed schema information

### 3. MCP Protocol Communication
In STDIO mode (default):
- Reads JSON-RPC messages from stdin
- Processes MCP protocol methods:
  - `initialize` - Protocol handshake
  - `tools/list` - Returns available tools
  - `tools/call` - Executes tool with parameters
  - `ping` - Health check
- Writes JSON-RPC responses to stdout

### 4. OData API Bridge
When a tool is called:
1. Receives parameters from the MCP client
2. Constructs the appropriate OData query URL
3. Makes HTTP request to the external OData API
4. Returns the response to the MCP client

## Configuration for AI Assistants

### Claude Desktop

Add to your Claude Desktop configuration:

```json
{
  "mcpServers": {
    "northwind": {
      "command": "odata-mcp",
      "args": ["start", "https://services.odata.org/V4/Northwind/Northwind.svc/$metadata"]
    },
    "my-api": {
      "command": "odata-mcp",
      "args": [
        "start",
        "https://api.example.com/odata/$metadata",
        "--auth-token",
        "Bearer YOUR_TOKEN"
      ]
    }
  }
}
```

### Other MCP Clients

Any MCP-compatible client can connect using the standard STDIO transport:

```python
import subprocess
import json

# Start the MCP server
process = subprocess.Popen(
    ["odata-mcp", "start", "https://api.example.com/odata/$metadata"],
    stdin=subprocess.PIPE,
    stdout=subprocess.PIPE,
    stderr=subprocess.PIPE,
    text=True
)

# Send initialize request
request = {
    "jsonrpc": "2.0",
    "id": 1,
    "method": "initialize",
    "params": {
        "protocolVersion": "2024-11-05",
        "capabilities": {"tools": {}},
        "clientInfo": {"name": "MyClient", "version": "1.0.0"}
    }
}
process.stdin.write(json.dumps(request) + "\n")
process.stdin.flush()

# Read response
response = json.loads(process.stdout.readline())
```

## Available Tools

### QueryEntitySet
Query an OData entity set with filtering, sorting, and pagination:
```json
{
  "name": "QueryEntitySet",
  "arguments": {
    "entitySet": "Customers",
    "filter": "Country eq 'USA'",
    "orderby": "CompanyName",
    "select": "CustomerID,CompanyName,Country",
    "top": 10,
    "skip": 0,
    "count": true
  }
}
```

### GetEntity
Get a single entity by its key:
```json
{
  "name": "GetEntity",
  "arguments": {
    "entitySet": "Customers",
    "key": "ALFKI",
    "select": "CustomerID,CompanyName,ContactName"
  }
}
```

### CreateEntity
Create a new entity:
```json
{
  "name": "CreateEntity",
  "arguments": {
    "entitySet": "Customers",
    "entity": "{\"CustomerID\": \"NEWCO\", \"CompanyName\": \"New Company\"}"
  }
}
```

### DiscoverEntitySets
List all available entity sets and their properties:
```json
{
  "name": "DiscoverEntitySets",
  "arguments": {}
}
```

### DescribeEntityType
Get detailed schema information for an entity type:
```json
{
  "name": "DescribeEntityType",
  "arguments": {
    "entityTypeName": "Customer"
  }
}
```

## Command-Line Options

### start Command
- `url` (required) - The OData metadata URL
- `--port, -p` - Port for HTTP mode (omit for STDIO mode)
- `--auth-token, -t` - Authentication token for the OData service
- `--config, -c` - Path to configuration file
- `--verbose, -v` - Enable verbose logging

### test Command
- `url` (required) - The OData metadata URL to test

### version Command
Shows the tool version

## Architecture

```
┌─────────────────┐         ┌──────────────────┐         ┌─────────────────┐
│   AI Assistant  │ <-----> │  OData MCP Tools │ <-----> │  External OData │
│  (MCP Client)   │  STDIO  │   (This Tool)    │  HTTP   │      API        │
└─────────────────┘         └──────────────────┘         └─────────────────┘
        │                            │                            │
        │ 1. tools/call              │ 2. HTTP GET/POST          │
        │    "QueryEntitySet"        │    /odata/Customers        │
        │                            │                            │
        │ 4. JSON response           │ 3. OData response          │
        │<───────────────────────────│<───────────────────────────│
```

## Supported OData Features

- ✅ Entity sets and entity types
- ✅ Complex types
- ✅ Navigation properties
- ✅ OData query options ($filter, $select, $orderby, $top, $skip, $count)
- ✅ Key-based entity access
- ✅ Entity creation (POST)
- ✅ Authentication (Bearer tokens)
- ⚠️  Entity updates (PATCH) - planned
- ⚠️  Entity deletion (DELETE) - planned
- ⚠️  Functions and Actions - planned
- ⚠️  Batch operations - planned

## Troubleshooting

### Tool doesn't start
- Ensure the OData URL is accessible and ends with `/$metadata` or `/metadata`
- Check network connectivity
- Verify authentication token if required

### No tools available
- Verify the OData service has entity sets defined
- Check the metadata document is valid CSDL
- Use the `test` command to validate the service

### Protocol errors in STDIO mode
- Ensure no other output is sent to stdout (only JSON-RPC messages)
- Use `--verbose` flag to see debug logs (sent to stderr)
- Check that the MCP client supports protocol version 2024-11-05

## Development

### Building from Source

```bash
# Clone the repository
git clone https://github.com/microsoft/odata-mcp-server.git
cd odata-mcp-server/src/Microsoft.OData.Mcp.Tools

# Build
dotnet build

# Run locally
dotnet run -- start https://services.odata.org/V4/Northwind/Northwind.svc/$metadata
```

### Testing

```bash
# Run unit tests
dotnet test

# Test with a real OData service
dotnet run -- test https://services.odata.org/V4/Northwind/Northwind.svc/$metadata
```

### Contributing

Contributions are welcome! Please see the main repository's contributing guidelines.

## License

MIT License - see LICENSE file in the repository root.

## See Also

- [Model Context Protocol Specification](https://modelcontextprotocol.io)
- [OData Specification](https://www.odata.org)
- [ASP.NET Core OData](https://github.com/OData/AspNetCoreOData)