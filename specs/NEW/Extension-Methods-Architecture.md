# Extension Methods Architecture

## Purpose
This document describes the separation of concerns between Core and AspNetCore extension methods to ensure proper service registration and avoid duplication.

## Microsoft.OData.Mcp.Core Extensions

### `AddODataMcpCore()`
**Purpose**: Registers ALL base services needed for ANY MCP server implementation (Console, AspNetCore, etc.)

**Two Overloads**:
1. `AddODataMcpCore(IConfiguration)` - Configures from appsettings.json
2. `AddODataMcpCore(Action<McpServerConfiguration>)` - Configures via code

**DRY Implementation**: Both overloads share common logic via private `AddODataMcpCoreServices()` method

**Registered Services**:
- **Configuration**: `McpServerConfiguration` from IConfiguration or Action<>
- **Parsing**: `ICsdlMetadataParser` → `CsdlParser`
- **Tool Factory**: `IMcpToolFactory` → `McpToolFactory`
- **Tool Generators**:
  - `IQueryToolGenerator` → `QueryToolGenerator`
  - `ICrudToolGenerator` → `CrudToolGenerator`
  - `INavigationToolGenerator` → `NavigationToolGenerator`
- **MCP Tools**:
  - `ODataMcpTools` (attribute-based SDK tools)
  - `DynamicODataMcpTools` (dynamic tool generation)
- **HTTP Client**: Via `AddODataHttpClient()` extension

**Usage**:
```csharp
// Used by Console/Tools projects
services.AddODataMcpCore(configuration);

// Or with custom configuration
services.AddODataMcpCore(config => {
    config.ODataService.BaseUrl = "https://services.odata.org/V4/Northwind/Northwind.svc";
    config.ODataService.Authentication.Type = ODataAuthenticationType.Bearer;
    config.ODataService.Authentication.BearerToken = "token";
});
```

### `AddODataHttpClient()`
**Purpose**: Separate extension for configuring HTTP clients for OData communication

**Features**:
- Configures standard OData headers (Accept, OData-Version, etc.)
- Sets base URL from configuration
- Configures timeout from configuration
- **Authentication Support**:
  - Bearer token authentication
  - API Key authentication (custom header)
  - Basic authentication (username/password)
  - OAuth2 (when configured)
- Can be called independently to add additional named HTTP clients

**Usage**:
```csharp
// Default client named "OData"
services.AddODataHttpClient();

// Custom named client
services.AddODataHttpClient("MyODataClient");
```

### `WithODataTools()` (IMcpServerBuilder extension)
**Purpose**: Registers OData tools with the official MCP SDK server builder

**What it does**:
- Calls `WithToolsFromAssembly()` to register all `[McpServerTool]` attributed methods

## Microsoft.OData.Mcp.AspNetCore Extensions

### `AddODataMcp()`
**Purpose**: Adds MCP support to an AspNetCore application that already has OData routes configured

**What it does**:
1. **Calls `AddODataMcpServerCore()`** to register all base services
2. Registers AspNetCore-specific services:
   - `ODataMcpOptions` configuration
   - `IMcpEndpointRegistry` → `McpEndpointRegistry` (tracks OData routes)
   - `IMcpRouteConvention` → `ODataMcpRouteConvention` (adds MCP endpoints)
   - `DynamicModelRefreshService` (if EnableDynamicModels is true)
   - `IMemoryCache` → `MemoryCache` (for caching)
   - `ODataMcpMarkerOptions` (marks MCP as enabled)

**Key Difference**: AspNetCore scenarios don't have a single OData service URL. Each route has its own EDM model, so BaseUrl is left empty.

**Usage**:
```csharp
// In Program.cs or Startup.cs
builder.Services.AddControllers()
    .AddOData(options => options
        .AddRouteComponents("api/v1", GetV1Model())
        .AddRouteComponents("api/v2", GetV2Model()));

// Add MCP support for all OData routes
builder.Services.AddODataMcp(options => {
    options.AutoRegisterRoutes = true;
    options.ExcludeRoutes = new[] { "internal" };
});
```

## Architecture Principles

1. **Core is Universal**: Core extensions register everything needed for ANY MCP implementation
2. **AspNetCore is Additive**: AspNetCore only adds web-specific routing and middleware services
3. **No Duplication**: AspNetCore MUST call Core extensions, not duplicate registrations
4. **Configuration Flexibility**: Both support IConfiguration and Action<> configuration patterns
5. **TryAdd Pattern**: Use TryAddSingleton/TryAddScoped to avoid duplicate registrations

## Common Issues and Solutions

### Issue: IMcpToolFactory not registered in AspNetCore
**Cause**: AspNetCore extension not calling Core extension
**Solution**: Ensure `AddODataMcp()` calls `AddODataMcpServerCore()`

### Issue: Multiple HTTP clients registered
**Cause**: Both Core and AspNetCore registering HTTP clients
**Solution**: Only Core should register the named "OData" HTTP client

### Issue: Configuration conflicts
**Cause**: Multiple configuration sources
**Solution**: AspNetCore should pass appropriate config to Core (e.g., empty BaseUrl for multi-route scenarios)

## Testing Considerations

- Core tests should verify all base services are registered
- AspNetCore tests should verify:
  - Core services are available (via AddODataMcpServerCore call)
  - AspNetCore-specific services are registered
  - Multiple OData routes are properly discovered
  - MCP endpoints are added to each route