# OData MCP Server Implementation Plan

## Overview

This document outlines the implementation plan for enhancing the Microsoft OData MCP Server to support multiple OData endpoints with a "magical" zero-configuration approach while maintaining flexibility for explicit control.

## Core Design Principles

1. **Magic by Default**: Zero configuration required for common scenarios
2. **Performance First**: Generate once at startup, serve from cache
3. **Natural Integration**: MCP endpoints as siblings to $metadata
4. **Flexible Control**: Support both automatic and explicit registration

## Architecture

### Route Structure

MCP endpoints will be registered as siblings to OData metadata endpoints:

```
{baseUrl}/{odataRoute}/$metadata
{baseUrl}/{odataRoute}/$batch
{baseUrl}/{odataRoute}/mcp         ← NEW
{baseUrl}/{odataRoute}/mcp/info
{baseUrl}/{odataRoute}/mcp/tools
{baseUrl}/{odataRoute}/mcp/tools/execute
```

Examples:
- `/api/v1/$metadata` → `/api/v1/mcp`
- `/odata/$metadata` → `/odata/mcp`
- `/$metadata` → `/mcp` (empty route prefix)

### Registration Approaches

#### 1. Automatic Registration (Magic)

```csharp
// Program.cs
builder.Services.AddControllers()
    .AddOData(options => options
        .AddRouteComponents("api/v1", GetV1Model())
        .AddRouteComponents("api/v2", GetV2Model()));

// This single line automatically enables MCP for ALL OData routes
builder.Services.AddODataMcp();

var app = builder.Build();
app.UseRouting();
app.UseODataMcp(); // Automatic discovery and registration
app.MapControllers();
```

Result:
- `/api/v1/mcp` - Automatically created
- `/api/v2/mcp` - Automatically created

#### 2. Explicit Registration (Fluent Control)

```csharp
app.UseEndpoints(endpoints =>
{
    // Explicit control over which routes get MCP
    endpoints.MapODataRoute("public", "api/public", GetPublicModel())
        .AddMcp(); // Uses default: /api/public/mcp
    
    endpoints.MapODataRoute("admin", "api/admin", GetAdminModel())
        .AddMcp("/custom/mcp/admin"); // Custom MCP path
    
    endpoints.MapODataRoute("internal", "internal", GetInternalModel());
    // No .AddMcp() - this route won't have MCP
});
```

#### 3. Mixed Mode Configuration

```csharp
builder.Services.AddODataMcp(options =>
{
    // Disable automatic registration
    options.AutoRegisterRoutes = false;
    
    // Or exclude specific routes
    options.ExcludeRoutes = new[] { "internal", "legacy" };
});
```

## Core Components

### 1. High-Performance Route Parser

```csharp
public ref struct SpanRouteParser
{
    private ReadOnlySpan<char> _path;
    
    public bool TryExtractODataRoute(out ReadOnlySpan<char> route, out ReadOnlySpan<char> mcpCommand)
    {
        // Zero-allocation parsing using spans
        // Handle: /{route}/mcp/{command}
    }
}
```

### 2. OData MCP Route Convention

```csharp
public class ODataMcpRouteConvention : IODataRouteConvention
{
    public void Apply(ODataRoute route)
    {
        // Automatically add /mcp endpoints to each OData route
        route.AddSubRoute("mcp", new McpRouteHandler(route));
    }
}
```

### 3. Tool Generation Cache

```csharp
public class McpToolCache
{
    private readonly FrozenDictionary<string, FrozenSet<McpTool>> _toolsByRoute;
    
    public McpToolCache(IEnumerable<ODataRoute> routes)
    {
        // Generate all tools at startup
        _toolsByRoute = GenerateAndFreeze(routes);
    }
    
    public IReadOnlyList<McpTool> GetTools(string routeName)
    {
        // O(1) lookup, zero allocations
        return _toolsByRoute[routeName];
    }
}
```

### 4. Fluent Extension Methods

```csharp
public static class ODataRouteBuilderExtensions
{
    public static ODataRoute AddMcp(this ODataRoute odataRoute, string mcpPath = null)
    {
        var routePrefix = odataRoute.RoutePrefix;
        var actualMcpPath = mcpPath ?? $"/{routePrefix}/mcp".TrimStart('/');
        
        // Register MCP endpoint for this specific route
        var registry = odataRoute.ServiceProvider.GetRequiredService<IMcpEndpointRegistry>();
        registry.RegisterEndpoint(odataRoute, actualMcpPath);
        
        return odataRoute;
    }
}
```

### 5. MCP Endpoint Registry

```csharp
public interface IMcpEndpointRegistry
{
    void RegisterEndpoint(ODataRoute route, string mcpPath);
    bool TryGetEndpoint(string path, out McpEndpoint endpoint);
    IEnumerable<McpEndpoint> GetAllEndpoints();
}
```

## Performance Optimizations

### 1. Startup-Time Tool Generation
- Generate all tools once during application startup
- Cache using `FrozenDictionary` and `FrozenSet` for optimal runtime performance
- No tool generation during request handling

### 2. Zero-Allocation Route Parsing
- Use `ReadOnlySpan<char>` for path parsing
- Avoid string allocations with pre-computed route patterns
- Stack-allocated buffers for temporary operations

### 3. Cached Response Bodies
- Pre-serialize common responses (tool lists, info)
- Use `IMemoryCache` with sliding expiration for dynamic content
- Implement ETags for client-side caching

### 4. Optimized JSON Serialization
- Use `System.Text.Json` source generators
- Pre-compile serialization contracts
- Stream directly to response body

## OData Integration Details

### 1. Dollar Sign Prefix Handling
```csharp
public class ODataOptionsResolver
{
    public bool UsesDollarPrefix(ODataOptions options)
    {
        // Check if routes use $ prefix for metadata
        return options.EnableDollarPrefix ?? true;
    }
    
    public string GetMetadataPath(ODataOptions options)
    {
        return UsesDollarPrefix(options) ? "/$metadata" : "/metadata";
    }
}
```

### 2. Model Discovery
```csharp
public class ODataModelDiscovery
{
    public IEdmModel GetModelForRoute(string routeName)
    {
        // Retrieve EDM model from OData route configuration
        var routeComponents = _odataOptions.Value.RouteComponents[routeName];
        return routeComponents.EdmModel;
    }
}
```

### 3. Tool Namespacing
```csharp
public class McpToolNamingStrategy
{
    public string GenerateToolName(string routeName, string entitySet, string operation)
    {
        if (string.IsNullOrEmpty(routeName))
            return $"{entitySet}.{operation}";
        
        return $"{routeName}.{entitySet}.{operation}";
    }
}
```

## Configuration Options

```csharp
public class ODataMcpOptions
{
    /// <summary>
    /// Automatically register MCP endpoints for all OData routes (default: true)
    /// </summary>
    public bool AutoRegisterRoutes { get; set; } = true;
    
    /// <summary>
    /// Routes to exclude from automatic registration
    /// </summary>
    public string[] ExcludeRoutes { get; set; } = Array.Empty<string>();
    
    /// <summary>
    /// Enable dynamic model updates (default: false for performance)
    /// </summary>
    public bool EnableDynamicModels { get; set; } = false;
    
    /// <summary>
    /// Tool naming pattern (default: "{route}.{entity}.{operation}")
    /// </summary>
    public string ToolNamingPattern { get; set; } = "{route}.{entity}.{operation}";
    
    /// <summary>
    /// Maximum number of tools to generate per entity (default: unlimited)
    /// </summary>
    public int? MaxToolsPerEntity { get; set; }
    
    /// <summary>
    /// Cache duration for dynamic content (default: 5 minutes)
    /// </summary>
    public TimeSpan CacheDuration { get; set; } = TimeSpan.FromMinutes(5);
}
```

## Implementation Timeline

### Phase 1: Core Infrastructure (Week 1)
- [ ] High-performance route parser with spans
- [ ] OData route discovery mechanism
- [ ] Tool generation and caching system
- [ ] Basic MCP endpoint registration

### Phase 2: Integration (Week 2)
- [ ] Automatic registration via `AddODataMcp()`
- [ ] Fluent `AddMcp()` extension method
- [ ] Dollar sign prefix handling
- [ ] Multi-route tool namespacing

### Phase 3: Optimization (Week 3)
- [ ] Startup-time tool generation
- [ ] Response caching strategies
- [ ] Performance benchmarking
- [ ] Memory allocation profiling

### Phase 4: Polish (Week 4)
- [ ] Configuration options
- [ ] Comprehensive tests
- [ ] Documentation updates
- [ ] Sample applications

## Testing Strategy

### Unit Tests
- Route parsing with various formats
- Tool generation for different EDM models
- Registration mechanisms (auto vs explicit)
- Configuration option behavior

### Integration Tests
- Multiple OData routes with MCP
- Empty route prefix scenarios
- Custom MCP path registration
- Dollar vs non-dollar prefix handling

### Performance Tests
- Startup time with various model sizes
- Request throughput benchmarks
- Memory allocation tracking
- Cache effectiveness metrics

## Success Criteria

1. **Zero Configuration**: Adding `AddODataMcp()` enables MCP for all routes
2. **Performance**: <1ms response time for cached tool requests
3. **Memory**: Zero heap allocations in hot paths
4. **Flexibility**: Support both automatic and explicit registration
5. **Compatibility**: Works with all OData configuration options

## Next Steps

1. Create branch for implementation
2. Set up performance benchmarking infrastructure
3. Implement core route parsing with spans
4. Build tool generation cache system
5. Create integration tests for validation