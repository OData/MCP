using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.OData;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.OData.Mcp.Core.Routing;
using Microsoft.OData.Mcp.Sample.Data;
using Microsoft.OData.Mcp.Sample.Models;
using Microsoft.OData.Mcp.Sample.Services;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .CreateLogger();

builder.Host.UseSerilog();

// Add services to the container
builder.Services.AddControllers()
    .AddOData(options =>
    {
        // Enable query options without dollar prefix (optional)
        options.EnableQueryFeatures();
        options.SetMaxTop(1000);
        options.Select().Filter().OrderBy().Expand().Count().SetMaxTop(1000);
        
        // Add route components for multiple API versions
        options.AddRouteComponents("api/v1", SampleEdmModel.GetV1Model());
        options.AddRouteComponents("api/v2", SampleEdmModel.GetV2Model());
        options.AddRouteComponents("odata", SampleEdmModel.GetMainModel());
    });

// Register the in-memory data store as a singleton
builder.Services.AddSingleton<InMemoryDataStore>();

// Register the OData options provider bridge
builder.Services.AddSingleton<IODataOptionsProvider, ODataOptionsProviderBridge>();

// Enable the magical OData MCP integration!
builder.Services.AddODataMcp(options =>
{
    // Optional: Exclude any routes from MCP
    // options.ExcludeRoutes = new[] { "internal" };
    
    // Optional: Customize tool naming
    options.ToolNamingPattern = "{route}.{entity}.{operation}";
    
    // Optional: Enable dynamic models (for changing schemas)
    options.EnableDynamicModels = false;
    
    // Performance settings
    options.UseAggressiveCaching = true;
    options.DefaultPageSize = 50;
    options.MaxPageSize = 500;
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Add CORS for testing
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseCors();

// Enable OData routing
app.UseRouting();

// Enable OData MCP middleware
app.UseODataMcp();

app.UseAuthorization();

// Map controllers with OData conventions
app.MapControllers();

// Log startup information
Log.Information("OData MCP Sample started successfully!");
Log.Information("OData endpoints available at:");
Log.Information("  - https://localhost:5001/api/v1/$metadata");
Log.Information("  - https://localhost:5001/api/v2/$metadata");
Log.Information("  - https://localhost:5001/odata/$metadata");
Log.Information("MCP endpoints automatically available at:");
Log.Information("  - https://localhost:5001/api/v1/mcp");
Log.Information("  - https://localhost:5001/api/v2/mcp");
Log.Information("  - https://localhost:5001/odata/mcp");

try
{
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
