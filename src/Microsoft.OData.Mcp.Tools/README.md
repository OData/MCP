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
# Connect to a public OData service. Pass the service root; the tool fetches $metadata itself.
odata-mcp start https://services.odata.org/V4/Northwind/Northwind.svc

# With authentication
odata-mcp start https://api.example.com/odata --auth-token "Bearer YOUR_TOKEN"

# With verbose logging (logs to stderr, doesn't interfere with protocol)
odata-mcp start https://api.example.com/odata --verbose
```

### HTTP Mode (Optional for Debugging)

For debugging or special scenarios, you can run in HTTP mode by specifying a port:

```bash
# Run on port 3000
odata-mcp start https://api.example.com/odata --port 3000
```

### Test Command

Test that an OData service is accessible and parse its metadata:

```bash
odata-mcp test https://services.odata.org/V4/Northwind/Northwind.svc
```

## How It Works

### 1. Metadata Discovery
When started, the tool:
- Fetches `$metadata` from the service root you pass (a URL that already ends in `/$metadata` is trimmed to its root)
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
      "args": ["start", "https://services.odata.org/V4/Northwind/Northwind.svc"]
    },
    "my-api": {
      "command": "odata-mcp",
      "args": [
        "start",
        "https://api.example.com/odata",
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
    ["odata-mcp", "start", "https://api.example.com/odata"],
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
- `url` (required) - The OData service root, for example `https://host/odata`. The tool appends `$metadata` itself; a pasted `.../$metadata` URL is accepted and trimmed.
- `--port, -p` - Port for HTTP mode (omit for STDIO mode)
- `--config, -c` - Path to configuration file
- `--verbose, -v` - Enable verbose logging

#### Authentication (outbound OAuth to the OData service)

`odata-mcp` discovers how a protected OData service wants to be authenticated (RFC 9728 protected resource metadata, then RFC 8414 / OIDC authorization server metadata) and completes an OAuth grant on your behalf — no bearer token has to be pasted into the MCP host config. `--auth-token` remains a plain escape hatch when you already have a token.

| Flag | Required | Job |
|------|----------|-----|
| `-t\|--auth-token` | no | Escape hatch bearer. Skips OAuth discovery entirely. |
| `--client-id` | when no DCR | Public/confidential OAuth client id. |
| `--client-secret` | client_credentials / confidential | Never written as a value by `add`. Also read from env `ODATA_MCP_CLIENT_SECRET` when unset. A daemon must also pass `--grant client_credentials`: the CLI does no TTY detection, so without it an unattended process falls into an interactive grant nobody is there to complete. |
| `--scopes` | no | Fallback scope list (space-separated). |
| `--auth-server` | no | Authorization server issuer override — use this when the service's `WWW-Authenticate` header returns a bare `Bearer` challenge (no `resource_metadata`, no RFC 9728 document) so discovery has nowhere else to look. |
| `--resource` | no | OAuth resource/audience override (for example Graph: `https://graph.microsoft.com`). |
| `--grant` | no | `device_code` \| `authorization_code` \| `client_credentials` \| `identity_assertion`. Must be advertised by the authorization server. |
| `--redirect-uri` | no | Loopback override for the authorization-code + PKCE grant. |
| `--token-cache` | no | Optional directory for the file-based token cache backend. Default: OS credential store. |
| `--auth-timeout` | no | Seconds to wait for an interactive grant. Default 300. |
| `--api-key` | no | Raw API key, sent with `--api-key-header`. |
| `--api-key-header` | with `--api-key` | Header name for the API key. |
| `--basic-user` / `--basic-password` | together | HTTP Basic credentials. |

Access and refresh tokens are stored in the OS credential store (Latchkey) and never appear in tool results, logs, or `.mcp.json`. See [`specs/v3/AUTHENTICATION.md`](https://github.com/microsoft/odata-mcp-server/blob/main/specs/v3/AUTHENTICATION.md) for the full discovery algorithm and grant reference.

**Device code example** (default interactive grant for stdio; prints a code and URL to stderr):

```bash
odata-mcp start "https://graph.microsoft.com/v1.0" \
  --client-id "{your-app-registration-id}" \
  --scopes "https://graph.microsoft.com/.default" \
  --grant device_code
```

**`--auth-server` example** — some APIs return only `WWW-Authenticate: Bearer realm="..."` with no `resource_metadata` and no RFC 9728 well-known document, so discovery cannot find an authorization server on its own. Point it there explicitly:

```bash
odata-mcp start "https://api.example.com/odata" \
  --auth-server "https://login.example.com/oauth2" \
  --client-id "{your-app-registration-id}"
```

**Make your API discoverable instead** — if you own the OData service, `--auth-server` is a flag your users should never have to type. Add one line to the API and the CLI finds everything on its own:

```csharp
// In the OData API, using Microsoft.OData.Mcp.AspNetCore:
builder.Services.AddODataProtectedResource(options =>
{
    options.AuthorizationServers.Add(new Uri("https://login.example.com/oauth2/v2.0"));
    options.ScopesSupported.Add("api://example-odata/Data.Read");
});
```

That publishes RFC 9728 protected resource metadata at `/.well-known/oauth-protected-resource` and `/.well-known/oauth-protected-resource/{prefix}` for every OData route the app serves, anonymously, and adds `resource_metadata="…"` to the `WWW-Authenticate` header on a `401`. No MCP server, no `UseODataMcp`, and no pipeline call are required. `odata-mcp start "https://api.example.com/odata"` then signs itself in with no flags at all.

### test Command
- `url` (required) - The OData service root, for example `https://host/odata`. The tool appends `$metadata` itself; a pasted `.../$metadata` URL is accepted and trimmed. to test

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
- Ensure the service root is reachable and that `{url}/$metadata` returns a CSDL document (try `odata-mcp test <url>`)
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
dotnet run -- start https://services.odata.org/V4/Northwind/Northwind.svc
```

### Testing

```bash
# Run unit tests
dotnet test

# Test with a real OData service
dotnet run -- test https://services.odata.org/V4/Northwind/Northwind.svc
```

### Contributing

Contributions are welcome! Please see the main repository's contributing guidelines.

## License

MIT License - see LICENSE file in the repository root.

## See Also

- [Model Context Protocol Specification](https://modelcontextprotocol.io)
- [OData Specification](https://www.odata.org)
- [ASP.NET Core OData](https://github.com/OData/AspNetCoreOData)