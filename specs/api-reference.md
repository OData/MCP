# API Reference

This document provides detailed API documentation for the Microsoft OData MCP Server.

## MCP Endpoints

### GET /mcp/info

Returns information about the MCP server.

**Response:**
```json
{
  "name": "OData MCP Server",
  "version": "1.0.0",
  "description": "MCP server for OData services",
  "capabilities": {
    "tools": true,
    "resources": true,
    "events": true
  },
  "metadata": {
    "serviceUrl": "https://api.example.com/odata",
    "lastUpdated": "2024-01-15T10:30:00Z"
  }
}
```

### GET /mcp/tools

Lists all available MCP tools.

**Query Parameters:**
- `category` (optional): Filter by tool category
- `search` (optional): Search tools by name or description

**Response:**
```json
{
  "tools": [
    {
      "name": "queryCustomers",
      "description": "Query the Customers entity set",
      "category": "query",
      "parameters": {
        "type": "object",
        "properties": {
          "filter": {
            "type": "string",
            "description": "OData filter expression"
          },
          "select": {
            "type": "string",
            "description": "Comma-separated list of properties"
          },
          "orderby": {
            "type": "string",
            "description": "OData orderby expression"
          },
          "top": {
            "type": "integer",
            "description": "Maximum number of results"
          },
          "skip": {
            "type": "integer",
            "description": "Number of results to skip"
          },
          "expand": {
            "type": "string",
            "description": "OData expand expression"
          }
        }
      },
      "examples": [
        {
          "description": "Get US customers",
          "parameters": {
            "filter": "Country eq 'USA'",
            "select": "CustomerId,CompanyName",
            "top": 10
          }
        }
      ]
    }
  ]
}
```

### POST /mcp/tools/execute

Executes a specific MCP tool.

**Request Body:**
```json
{
  "tool": "queryCustomers",
  "parameters": {
    "filter": "Country eq 'USA'",
    "select": "CustomerId,CompanyName,ContactName",
    "orderby": "CompanyName",
    "top": 20
  }
}
```

**Response:**
```json
{
  "success": true,
  "data": {
    "@odata.context": "https://api.example.com/odata/$metadata#Customers",
    "value": [
      {
        "CustomerId": "GREAL",
        "CompanyName": "Great Lakes Food Market",
        "ContactName": "Howard Snyder"
      }
    ]
  },
  "metadata": {
    "executionTime": 145,
    "recordCount": 20,
    "hasMore": true
  }
}
```

### GET /mcp/tools/{toolName}

Gets detailed information about a specific tool.

**Response:**
```json
{
  "name": "queryCustomers",
  "description": "Query the Customers entity set",
  "category": "query",
  "entityType": "Customer",
  "entitySet": "Customers",
  "httpMethod": "GET",
  "odataPath": "/Customers",
  "parameters": {
    "type": "object",
    "properties": {
      "filter": {
        "type": "string",
        "description": "OData filter expression",
        "examples": [
          "Country eq 'USA'",
          "startswith(CompanyName, 'A')"
        ]
      }
    }
  },
  "authorization": {
    "required": true,
    "scopes": ["read:customers"],
    "roles": ["User", "Admin"]
  }
}
```

### GET /mcp/metadata

Returns the OData metadata document.

**Response:**
```xml
<?xml version="1.0" encoding="utf-8"?>
<edmx:Edmx Version="4.0" xmlns:edmx="http://docs.oasis-open.org/odata/ns/edmx">
  <edmx:DataServices>
    <Schema Namespace="Example.Model" xmlns="http://docs.oasis-open.org/odata/ns/edm">
      <EntityType Name="Customer">
        <Key>
          <PropertyRef Name="CustomerId"/>
        </Key>
        <Property Name="CustomerId" Type="Edm.String" Nullable="false"/>
        <Property Name="CompanyName" Type="Edm.String"/>
        <Property Name="ContactName" Type="Edm.String"/>
      </EntityType>
    </Schema>
  </edmx:DataServices>
</edmx:Edmx>
```

### GET /mcp/health

Health check endpoint.

**Response:**
```json
{
  "status": "Healthy",
  "checks": {
    "odata_service": {
      "status": "Healthy",
      "description": "OData service is reachable",
      "data": {
        "responseTime": 45,
        "lastChecked": "2024-01-15T10:30:00Z"
      }
    },
    "authentication": {
      "status": "Healthy",
      "description": "Authentication service is configured"
    },
    "mcp_server": {
      "status": "Healthy",
      "description": "MCP server is operational",
      "data": {
        "toolCount": 42,
        "uptime": "2.14:30:45"
      }
    }
  }
}
```

## Core Library API

### IODataMcpServer Interface

```csharp
public interface IODataMcpServer
{
    /// <summary>
    /// Gets server information.
    /// </summary>
    Task<McpServerInfo> GetInfoAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Lists available tools.
    /// </summary>
    Task<IEnumerable<McpTool>> ListToolsAsync(
        ToolFilter filter = null, 
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Executes a tool.
    /// </summary>
    Task<McpToolResult> ExecuteToolAsync(
        string toolName, 
        JsonDocument parameters,
        ToolExecutionContext context = null,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Gets tool details.
    /// </summary>
    Task<McpToolDetails> GetToolDetailsAsync(
        string toolName,
        CancellationToken cancellationToken = default);
}
```

### IMcpToolFactory Interface

```csharp
public interface IMcpToolFactory
{
    /// <summary>
    /// Generates MCP tools from an EDM model.
    /// </summary>
    Task<IEnumerable<McpTool>> GenerateToolsAsync(
        EdmModel model,
        ToolGenerationOptions options = null);
    
    /// <summary>
    /// Generates CRUD tools for an entity type.
    /// </summary>
    Task<IEnumerable<McpTool>> GenerateCrudToolsAsync(
        EdmEntityType entityType,
        CrudToolGenerationOptions options = null);
    
    /// <summary>
    /// Generates query tools for an entity set.
    /// </summary>
    Task<IEnumerable<McpTool>> GenerateQueryToolsAsync(
        EdmEntitySet entitySet,
        QueryToolGenerationOptions options = null);
    
    /// <summary>
    /// Filters tools based on user context.
    /// </summary>
    Task<IEnumerable<McpTool>> FilterToolsForUserAsync(
        IEnumerable<McpTool> tools,
        ClaimsPrincipal user);
}
```

### ICsdlMetadataParser Interface

```csharp
public interface ICsdlMetadataParser
{
    /// <summary>
    /// Parses CSDL metadata from a string.
    /// </summary>
    Task<EdmModel> ParseAsync(string csdlContent);
    
    /// <summary>
    /// Parses CSDL metadata from a stream.
    /// </summary>
    Task<EdmModel> ParseAsync(Stream csdlStream);
    
    /// <summary>
    /// Validates CSDL metadata.
    /// </summary>
    Task<ValidationResult> ValidateAsync(string csdlContent);
}
```

## Configuration API

### ODataMcpOptions Class

```csharp
public class ODataMcpOptions
{
    /// <summary>
    /// Gets or sets the OData service URL.
    /// </summary>
    public string ServiceUrl { get; set; }
    
    /// <summary>
    /// Gets or sets whether to use local metadata.
    /// </summary>
    public bool UseLocalMetadata { get; set; }
    
    /// <summary>
    /// Gets or sets whether authentication is enabled.
    /// </summary>
    public bool EnableAuthentication { get; set; }
    
    /// <summary>
    /// Gets or sets the base path for MCP endpoints.
    /// </summary>
    public string BasePath { get; set; } = "/mcp";
    
    /// <summary>
    /// Gets or sets server information.
    /// </summary>
    public McpServerInfo ServerInfo { get; set; }
    
    /// <summary>
    /// Gets or sets tool generation options.
    /// </summary>
    public ToolGenerationOptions ToolGeneration { get; set; }
    
    /// <summary>
    /// Gets or sets caching configuration.
    /// </summary>
    public CachingConfiguration Caching { get; set; }
    
    /// <summary>
    /// Gets or sets security configuration.
    /// </summary>
    public SecurityConfiguration Security { get; set; }
}
```

### Extension Methods

```csharp
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds OData MCP Server services.
    /// </summary>
    public static IServiceCollection AddODataMcpServer(
        this IServiceCollection services,
        Action<ODataMcpOptions> configureOptions = null);
    
    /// <summary>
    /// Adds OData MCP Server with configuration.
    /// </summary>
    public static IServiceCollection AddODataMcpServer(
        this IServiceCollection services,
        IConfiguration configuration);
}

public static class ApplicationBuilderExtensions
{
    /// <summary>
    /// Adds OData MCP middleware.
    /// </summary>
    public static IApplicationBuilder UseODataMcp(
        this IApplicationBuilder app,
        string basePath = null);
}
```

## Model Classes

### McpTool Class

```csharp
public class McpTool
{
    /// <summary>
    /// Gets or sets the tool name.
    /// </summary>
    public string Name { get; set; }
    
    /// <summary>
    /// Gets or sets the tool description.
    /// </summary>
    public string Description { get; set; }
    
    /// <summary>
    /// Gets or sets the tool category.
    /// </summary>
    public string Category { get; set; }
    
    /// <summary>
    /// Gets or sets the JSON schema for parameters.
    /// </summary>
    public JsonDocument Parameters { get; set; }
    
    /// <summary>
    /// Gets or sets tool examples.
    /// </summary>
    public IList<McpToolExample> Examples { get; set; }
    
    /// <summary>
    /// Gets or sets authorization requirements.
    /// </summary>
    public AuthorizationRequirements Authorization { get; set; }
}
```

### McpToolResult Class

```csharp
public class McpToolResult
{
    /// <summary>
    /// Gets or sets whether the execution was successful.
    /// </summary>
    public bool Success { get; set; }
    
    /// <summary>
    /// Gets or sets the result data.
    /// </summary>
    public JsonDocument Data { get; set; }
    
    /// <summary>
    /// Gets or sets error information.
    /// </summary>
    public McpError Error { get; set; }
    
    /// <summary>
    /// Gets or sets execution metadata.
    /// </summary>
    public Dictionary<string, object> Metadata { get; set; }
}
```

### EdmModel Class

```csharp
public class EdmModel
{
    /// <summary>
    /// Gets the entity types in the model.
    /// </summary>
    public IList<EdmEntityType> EntityTypes { get; }
    
    /// <summary>
    /// Gets the complex types in the model.
    /// </summary>
    public IList<EdmComplexType> ComplexTypes { get; }
    
    /// <summary>
    /// Gets the entity containers in the model.
    /// </summary>
    public IList<EdmEntityContainer> EntityContainers { get; }
    
    /// <summary>
    /// Gets an entity type by name.
    /// </summary>
    public EdmEntityType GetEntityType(string name);
    
    /// <summary>
    /// Gets an entity set by name.
    /// </summary>
    public EdmEntitySet GetEntitySet(string name);
}
```

## Events and Hooks

### Tool Execution Events

```csharp
public interface IToolExecutionEvents
{
    /// <summary>
    /// Called before tool execution.
    /// </summary>
    Task OnExecutingAsync(ToolExecutingContext context);
    
    /// <summary>
    /// Called after tool execution.
    /// </summary>
    Task OnExecutedAsync(ToolExecutedContext context);
    
    /// <summary>
    /// Called when tool execution fails.
    /// </summary>
    Task OnErrorAsync(ToolErrorContext context);
}
```

### Metadata Change Events

```csharp
public interface IMetadataChangeEvents
{
    /// <summary>
    /// Called when metadata is updated.
    /// </summary>
    Task OnMetadataUpdatedAsync(MetadataUpdatedContext context);
    
    /// <summary>
    /// Called when tools are regenerated.
    /// </summary>
    Task OnToolsRegeneratedAsync(ToolsRegeneratedContext context);
}
```

## Error Codes

| Code | Description | HTTP Status |
|------|-------------|-------------|
| MCP-001 | Service URL not configured | 500 |
| MCP-002 | Invalid metadata | 502 |
| MCP-003 | Authentication required | 401 |
| MCP-004 | Tool not found | 404 |
| MCP-005 | Rate limit exceeded | 429 |
| MCP-006 | Invalid parameters | 400 |
| MCP-007 | Execution timeout | 504 |
| MCP-008 | Insufficient permissions | 403 |
| MCP-009 | Service unavailable | 503 |
| MCP-010 | Internal server error | 500 |

## HTTP Status Codes

- **200 OK** - Successful request
- **201 Created** - Resource created successfully
- **204 No Content** - Successful request with no content
- **400 Bad Request** - Invalid request parameters
- **401 Unauthorized** - Authentication required
- **403 Forbidden** - Insufficient permissions
- **404 Not Found** - Resource not found
- **429 Too Many Requests** - Rate limit exceeded
- **500 Internal Server Error** - Server error
- **502 Bad Gateway** - OData service error
- **503 Service Unavailable** - Service temporarily unavailable

## Next Steps

- [Getting Started](getting-started.md) - Quick start guide
- [Configuration](configuration.md) - Configuration options
- [Examples](examples.md) - Code examples
- [Troubleshooting](troubleshooting.md) - Debug issues