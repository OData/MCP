# OData MCP: Magical Zero-Configuration Experience

This document describes the automatic MCP (Model Context Protocol) integration for OData services that requires zero configuration from developers while maintaining full control when needed.

## Overview

The OData MCP integration provides a "magical" experience where simply adding `services.AddODataMcp()` automatically enables MCP endpoints for all your OData routes. No manual registration, no complex configuration - it just works.

## Quick Start

```csharp
// Program.cs
var builder = WebApplication.CreateBuilder(args);

// Add OData with multiple routes
builder.Services.AddControllers()
    .AddOData(options => options
        .AddRouteComponents("api/v1", GetV1Model())
        .AddRouteComponents("api/v2", GetV2Model())
        .AddRouteComponents("odata", GetMainModel()));

// Enable MCP for ALL OData routes automatically!
builder.Services.AddODataMcp();

var app = builder.Build();
app.UseODataMcp();
app.UseRouting();
app.MapControllers();
app.Run();
```

That's it! MCP endpoints are now available for all your OData routes:
- `/api/v1/$metadata` → `/api/v1/mcp`
- `/api/v2/$metadata` → `/api/v2/mcp`
- `/odata/$metadata` → `/odata/mcp`

## How It Works

### Automatic Route Discovery

When you call `AddODataMcp()`, the system:

1. **Hooks into OData route registration** - Automatically detects when OData routes are registered
2. **Creates sibling MCP endpoints** - For each OData route, creates MCP endpoints at the same level as `$metadata`
3. **Generates tools at startup** - Pre-generates all MCP tools for optimal runtime performance
4. **Respects OData configuration** - Honors settings like `EnableNoDollarQueryOptions`

### MCP Endpoints

For each OData route, the following MCP endpoints are created:

| Endpoint | Method | Description |
|----------|--------|-------------|
| `/{route}/mcp` | GET | MCP server information |
| `/{route}/mcp/tools` | GET | List available tools |
| `/{route}/mcp/tools/{toolName}` | GET | Get tool details |
| `/{route}/mcp/tools/execute` | POST | Execute a tool |

### Tool Namespacing

When multiple OData routes exist, tools are automatically namespaced to prevent conflicts:

```json
{
  "tools": [
    {
      "name": "v1.Customer.query",
      "description": "Query customers from API v1"
    },
    {
      "name": "v2.Customer.query", 
      "description": "Query customers from API v2"
    }
  ]
}
```

## Configuration Options

While zero-config is the default, you can customize behavior:

```csharp
builder.Services.AddODataMcp(options =>
{
    // Exclude specific routes from MCP
    options.ExcludeRoutes = new[] { "internal", "legacy" };
    
    // Enable dynamic model updates (off by default for performance)
    options.EnableDynamicModels = true;
    options.CacheDuration = TimeSpan.FromMinutes(10);
    
    // Customize tool naming pattern
    options.ToolNamingPattern = "{route}.{entity}.{operation}";
    
    // Control caching behavior
    options.UseAggressiveCaching = true;
    
    // Set request limits
    options.MaxRequestSize = 2 * 1024 * 1024; // 2MB
    options.DefaultPageSize = 50;
    options.MaxPageSize = 500;
});
```

## Explicit Control with Fluent API

For scenarios requiring explicit control, use the fluent API:

```csharp
builder.Services.AddControllers()
    .AddOData(options => options
        .AddRouteComponents("api/v1", GetV1Model())
            .AddMcp() // Explicitly enable MCP for this route
        .AddRouteComponents("api/v2", GetV2Model())
            .AddMcp(mcp => mcp
                .WithToolPrefix("v2")
                .WithCustomHandler<MyV2McpHandler>())
        .AddRouteComponents("internal", GetInternalModel()));
        // No .AddMcp() - this route won't have MCP

// Don't call AddODataMcp() when using explicit control
```

## Respecting OData Configuration

The system automatically respects OData configuration options:

### Dollar Sign Prefix

```csharp
builder.Services.AddOData(options =>
{
    options.EnableNoDollarQueryOptions = true; // Removes $ from query options
    options.AddRouteComponents("api", model);
});
```

When `EnableNoDollarQueryOptions` is true:
- OData: `/api/metadata` (instead of `/api/$metadata`)
- MCP: `/api/mcp` (MCP never uses dollar prefixes)

### Bridge Pattern for OData Options

To make MCP aware of OData settings without creating a hard dependency:

```csharp
// In your host application
public class ODataOptionsProviderBridge : IODataOptionsProvider
{
    private readonly IOptions<ODataOptions> _odataOptions;
    
    public ODataOptionsProviderBridge(IOptions<ODataOptions> odataOptions)
    {
        _odataOptions = odataOptions;
    }
    
    public bool EnableNoDollarQueryOptions => 
        _odataOptions.Value.EnableNoDollarQueryOptions;
        
    public string? GetRoutePrefix(string routeName) =>
        _odataOptions.Value.RouteOptions
            .FirstOrDefault(r => r.RouteName == routeName)?.RoutePrefix;
}

// Register the bridge
builder.Services.AddSingleton<IODataOptionsProvider, ODataOptionsProviderBridge>();
```

## Performance Characteristics

The magical zero-config approach is designed for high performance:

### Startup-Time Generation
- All MCP tools are generated at application startup
- Tools are cached using `FrozenDictionary` for O(1) lookups
- No runtime generation overhead

### Zero-Allocation Routing
- Route parsing uses `ReadOnlySpan<char>` for zero heap allocations
- Pattern matching is optimized with aggressive inlining
- No regex or string allocations during request handling

### Efficient Caching
- ETags and long cache headers for tool listings
- In-memory caching of generated tools
- Aggressive caching enabled by default

## Dynamic Models (Opt-In)

For scenarios with changing OData models:

```csharp
builder.Services.AddODataMcp(options =>
{
    options.EnableDynamicModels = true;
    options.CacheDuration = TimeSpan.FromMinutes(5);
});
```

This enables a background service that:
- Periodically refreshes OData metadata
- Regenerates tools when models change
- Updates the tool cache automatically

## Troubleshooting

### MCP Endpoints Not Appearing

1. Ensure `UseODataMcp()` is called before `UseRouting()`
2. Check if the route is excluded in options
3. Verify OData routes are registered before MCP

### Tool Naming Conflicts

Tools are automatically namespaced by route. If conflicts still occur:
```csharp
options.ToolNamingPattern = "{route}.{entity}.{operation}.{guid}";
```

### Performance Issues

1. Ensure `EnableDynamicModels` is false (default) for static models
2. Check cache configuration is appropriate
3. Monitor startup time for large models

## Migration from Manual Registration

If migrating from manual MCP registration:

```csharp
// Old approach
app.MapODataRoute("odata", "odata", model)
    .MapMcpEndpoints(); // Manual registration

// New magical approach
builder.Services.AddODataMcp(); // That's all!
```

## Best Practices

1. **Use zero-config by default** - Let the magic work for you
2. **Exclude internal routes** - Use `ExcludeRoutes` for non-public APIs
3. **Keep models static** - Disable dynamic models unless truly needed
4. **Monitor startup performance** - Large models may impact startup time
5. **Use explicit control sparingly** - Only when custom behavior is required

## Examples

### Multi-Tenant SaaS Application

```csharp
builder.Services.AddODataMcp(options =>
{
    // Each tenant gets their own namespaced tools
    options.ToolNamingPattern = "{route}.{entity}.{operation}";
    
    // Exclude admin routes
    options.ExcludeRoutes = new[] { "admin", "system" };
    
    // Enable dynamic models for tenant-specific schemas
    options.EnableDynamicModels = true;
    options.CacheDuration = TimeSpan.FromMinutes(15);
});
```

### High-Performance API

```csharp
builder.Services.AddODataMcp(options =>
{
    // Aggressive caching for read-heavy workloads
    options.UseAggressiveCaching = true;
    
    // Larger page sizes for bulk operations
    options.DefaultPageSize = 200;
    options.MaxPageSize = 2000;
    
    // Static models only
    options.EnableDynamicModels = false;
});
```

### Development Environment

```csharp
builder.Services.AddODataMcp(options =>
{
    // Enable request logging for debugging
    options.EnableRequestLogging = true;
    
    // Shorter cache for rapid iteration
    options.CacheDuration = TimeSpan.FromSeconds(30);
    
    // Include detailed metadata
    options.IncludeMetadata = true;
});
```

## Conclusion

The magical zero-configuration approach makes OData MCP integration effortless while maintaining flexibility for advanced scenarios. By automatically discovering and configuring MCP endpoints for all OData routes, developers can focus on building their APIs rather than configuring infrastructure.