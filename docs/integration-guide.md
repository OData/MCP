# Integration Guide

This guide explains how to integrate the Microsoft OData MCP Server with your existing OData APIs and applications.

## Integration Scenarios

### 1. Adding MCP to an Existing OData Service

If you already have an OData service running, you can add MCP capabilities with minimal changes.

#### ASP.NET Core OData Service

```csharp
// Existing OData configuration
var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddControllers()
    .AddOData(options => options
        .Select().Filter().OrderBy().Expand().Count().SetMaxTop(100)
        .AddRouteComponents("odata", GetEdmModel()));

// Add MCP Server - it will use your existing EDM model
builder.Services.AddODataMcpServer(options =>
{
    options.UseLocalMetadata = true; // Use EDM model from your controllers
    options.BasePath = "/mcp";       // MCP endpoints at /mcp/*
});

var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();

// Add MCP middleware
app.UseODataMcp();

app.MapControllers();
app.Run();

// Your existing EDM model
static IEdmModel GetEdmModel()
{
    var builder = new ODataConventionModelBuilder();
    builder.EntitySet<Customer>("Customers");
    builder.EntitySet<Order>("Orders");
    return builder.GetEdmModel();
}
```

#### Web API with OData

```csharp
public class Startup
{
    public void ConfigureServices(IServiceCollection services)
    {
        services.AddControllers();
        services.AddOData();
        
        // Add MCP Server
        services.AddODataMcpServer(Configuration);
    }
    
    public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
    {
        app.UseRouting();
        app.UseAuthentication();
        app.UseAuthorization();
        
        // Add MCP middleware
        app.UseODataMcp();
        
        app.UseEndpoints(endpoints =>
        {
            endpoints.MapODataRoute("odata", "odata", GetEdmModel());
            endpoints.MapControllers();
        });
    }
}
```

### 2. Connecting to a Remote OData Service

To expose a remote OData service through MCP:

```csharp
var builder = WebApplication.CreateBuilder(args);

// Configure MCP to connect to remote OData service
builder.Services.AddODataMcpServer(options =>
{
    options.ServiceUrl = "https://services.odata.org/V4/Northwind/Northwind.svc";
    
    // Configure authentication for the remote service
    options.RemoteAuthentication = new RemoteAuthenticationOptions
    {
        Type = "Bearer",
        TokenProvider = async (serviceProvider) =>
        {
            // Get token for remote service
            var tokenService = serviceProvider.GetRequiredService<ITokenService>();
            return await tokenService.GetTokenAsync();
        }
    };
});

// Optional: Add authentication for MCP endpoints
builder.Services.AddAuthentication("Bearer")
    .AddJwtBearer(options =>
    {
        options.Authority = "https://your-auth-server.com";
        options.Audience = "your-mcp-api";
    });

var app = builder.Build();

app.UseAuthentication();
app.UseODataMcp();
app.Run();
```

### 3. Sidecar Deployment

Deploy as a separate service alongside your OData API:

```yaml
# docker-compose.yml
version: '3.8'

services:
  odata-api:
    image: mycompany/odata-api:latest
    ports:
      - "5000:80"
    environment:
      - ConnectionStrings__Default=Server=db;Database=MyApp;...
  
  mcp-sidecar:
    image: microsoft/odata-mcp-sidecar:latest
    ports:
      - "5001:80"
    environment:
      - ODataMcp__ServiceUrl=http://odata-api/odata
      - ODataMcp__Authentication__Enabled=true
    depends_on:
      - odata-api
  
  db:
    image: mcr.microsoft.com/mssql/server:2022-latest
    environment:
      - ACCEPT_EULA=Y
      - SA_PASSWORD=YourStrong@Password
```

### 4. Kubernetes Integration

Deploy with a Kubernetes sidecar pattern:

```yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: odata-app
spec:
  replicas: 3
  selector:
    matchLabels:
      app: odata-app
  template:
    metadata:
      labels:
        app: odata-app
    spec:
      containers:
      # Main OData API container
      - name: odata-api
        image: mycompany/odata-api:latest
        ports:
        - containerPort: 80
          name: http
        
      # MCP sidecar container
      - name: mcp-sidecar
        image: microsoft/odata-mcp-sidecar:latest
        ports:
        - containerPort: 8080
          name: mcp
        env:
        - name: ODataMcp__ServiceUrl
          value: "http://localhost/odata"
        - name: ODataMcp__BasePath
          value: "/mcp"
```

## Advanced Integration Patterns

### 1. Multi-Tenant Configuration

Support multiple OData services from a single MCP server:

```csharp
public class TenantAwareODataMcpServer : IODataMcpServer
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ITenantResolver _tenantResolver;
    
    public async Task<McpToolResult> ExecuteToolAsync(string toolName, JsonDocument parameters)
    {
        // Resolve tenant from request
        var tenant = await _tenantResolver.ResolveAsync(_httpContextAccessor.HttpContext);
        
        // Get tenant-specific OData service URL
        var serviceUrl = GetServiceUrlForTenant(tenant);
        
        // Execute tool against tenant's service
        return await ExecuteAgainstServiceAsync(serviceUrl, toolName, parameters);
    }
}

// Registration
builder.Services.AddScoped<IODataMcpServer, TenantAwareODataMcpServer>();
```

### 2. Custom Tool Generation

Extend or customize the generated tools:

```csharp
public class CustomToolFactory : IMcpToolFactory
{
    private readonly IMcpToolFactory _defaultFactory;
    
    public CustomToolFactory(IMcpToolFactory defaultFactory)
    {
        _defaultFactory = defaultFactory;
    }
    
    public async Task<IEnumerable<McpTool>> GenerateToolsAsync(EdmModel model)
    {
        // Get default tools
        var tools = await _defaultFactory.GenerateToolsAsync(model);
        
        // Add custom tools
        var customTools = new List<McpTool>
        {
            new McpTool
            {
                Name = "bulkImportCustomers",
                Description = "Import customers from CSV",
                Parameters = new
                {
                    csvData = new { type = "string", description = "CSV data" }
                }
            }
        };
        
        return tools.Concat(customTools);
    }
}

// Registration
builder.Services.AddSingleton<IMcpToolFactory, CustomToolFactory>();
```

### 3. Caching Strategy

Implement intelligent caching for better performance:

```csharp
public class SmartCachingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IMemoryCache _cache;
    
    public async Task InvokeAsync(HttpContext context)
    {
        // Cache metadata indefinitely
        if (context.Request.Path.StartsWithSegments("/mcp/metadata"))
        {
            var cacheKey = "metadata";
            if (!_cache.TryGetValue(cacheKey, out string cachedResponse))
            {
                // Generate response
                await _next(context);
                
                // Cache for 24 hours
                _cache.Set(cacheKey, context.Response, TimeSpan.FromHours(24));
            }
        }
        // Cache query results briefly
        else if (context.Request.Path.StartsWithSegments("/mcp/tools/execute"))
        {
            var cacheKey = GenerateCacheKey(context.Request);
            if (_cache.TryGetValue(cacheKey, out string cachedResponse))
            {
                await context.Response.WriteAsync(cachedResponse);
                return;
            }
            
            await _next(context);
            
            // Cache for 5 minutes
            _cache.Set(cacheKey, context.Response, TimeSpan.FromMinutes(5));
        }
        else
        {
            await _next(context);
        }
    }
}
```

### 4. Event-Driven Updates

React to OData model changes:

```csharp
public class MetadataChangeNotifier : BackgroundService
{
    private readonly IMetadataDiscoveryService _discoveryService;
    private readonly IMcpToolFactory _toolFactory;
    private readonly IHubContext<MetadataHub> _hubContext;
    
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            // Check for metadata changes
            var hasChanged = await _discoveryService.HasMetadataChangedAsync();
            
            if (hasChanged)
            {
                // Regenerate tools
                var newModel = await _discoveryService.GetLatestModelAsync();
                var newTools = await _toolFactory.GenerateToolsAsync(newModel);
                
                // Notify connected clients
                await _hubContext.Clients.All.SendAsync(
                    "MetadataUpdated", 
                    newTools, 
                    stoppingToken);
            }
            
            await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
        }
    }
}
```

## Integration Testing

### Testing MCP Endpoints

```csharp
[TestClass]
public class McpIntegrationTests
{
    private WebApplicationFactory<Program> _factory;
    
    [TestInitialize]
    public void Setup()
    {
        _factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureServices(services =>
                {
                    // Override with test configuration
                    services.Configure<ODataMcpOptions>(options =>
                    {
                        options.ServiceUrl = "https://test-service.com/odata";
                    });
                });
            });
    }
    
    [TestMethod]
    public async Task Tools_ShouldReturnExpectedTools()
    {
        // Arrange
        var client = _factory.CreateClient();
        
        // Act
        var response = await client.GetAsync("/mcp/tools");
        
        // Assert
        response.EnsureSuccessStatusCode();
        var tools = await response.Content.ReadFromJsonAsync<ToolsResponse>();
        Assert.IsTrue(tools.Tools.Any(t => t.Name == "queryCustomers"));
    }
}
```

### Mocking OData Services

```csharp
public class MockODataService
{
    public static IEdmModel GetMockModel()
    {
        var model = new EdmModel();
        
        // Define Customer entity
        var customer = new EdmEntityType("Test", "Customer");
        customer.AddKeys(customer.AddStructuralProperty("Id", EdmPrimitiveTypeKind.Int32));
        customer.AddStructuralProperty("Name", EdmPrimitiveTypeKind.String);
        model.AddElement(customer);
        
        // Define container
        var container = new EdmEntityContainer("Test", "Container");
        container.AddEntitySet("Customers", customer);
        model.AddElement(container);
        
        return model;
    }
}

// Use in tests
builder.Services.AddSingleton<IEdmModel>(MockODataService.GetMockModel());
```

## Performance Optimization

### 1. Connection Pooling

```csharp
builder.Services.AddHttpClient("ODataService", client =>
{
    client.BaseAddress = new Uri("https://api.example.com/odata");
    client.Timeout = TimeSpan.FromSeconds(30);
})
.ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
{
    MaxConnectionsPerServer = 100,
    UseProxy = false
})
.AddPolicyHandler(GetRetryPolicy())
.AddPolicyHandler(GetCircuitBreakerPolicy());
```

### 2. Batch Operations

Enable batch operations for better performance:

```csharp
public class BatchToolExecutor
{
    public async Task<IEnumerable<McpToolResult>> ExecuteBatchAsync(
        IEnumerable<McpToolRequest> requests)
    {
        // Group by operation type
        var groups = requests.GroupBy(r => r.ToolName);
        
        var results = new List<McpToolResult>();
        
        foreach (var group in groups)
        {
            // Execute same operations in parallel
            var tasks = group.Select(request => 
                ExecuteToolAsync(request.ToolName, request.Parameters));
            
            results.AddRange(await Task.WhenAll(tasks));
        }
        
        return results;
    }
}
```

## Monitoring Integration

### 1. Application Insights

```csharp
builder.Services.AddApplicationInsightsTelemetry();
builder.Services.Configure<TelemetryConfiguration>(config =>
{
    config.TelemetryProcessorChainBuilder
        .Use(next => new ODataMcpTelemetryProcessor(next))
        .Build();
});

public class ODataMcpTelemetryProcessor : ITelemetryProcessor
{
    public void Process(ITelemetry item)
    {
        if (item is RequestTelemetry request)
        {
            // Add custom properties
            request.Properties["McpTool"] = ExtractToolName(request.Url);
            request.Properties["ODataEntity"] = ExtractEntityName(request.Url);
        }
        
        _next.Process(item);
    }
}
```

### 2. Custom Metrics

```csharp
public class MetricsMiddleware
{
    private readonly IMetrics _metrics;
    
    public async Task InvokeAsync(HttpContext context)
    {
        using var timer = _metrics.Measure.Timer.Time("mcp.request.duration");
        
        try
        {
            await _next(context);
            
            _metrics.Measure.Counter.Increment("mcp.request.success", 
                new MetricTags("tool", ExtractToolName(context)));
        }
        catch (Exception ex)
        {
            _metrics.Measure.Counter.Increment("mcp.request.error",
                new MetricTags("error", ex.GetType().Name));
            throw;
        }
    }
}
```

## Next Steps

- [Security Setup](security.md) - Configure authentication and authorization
- [Examples](examples.md) - See real-world integration examples
- [Troubleshooting](troubleshooting.md) - Solve common integration issues
- [API Reference](api-reference.md) - Detailed API documentation