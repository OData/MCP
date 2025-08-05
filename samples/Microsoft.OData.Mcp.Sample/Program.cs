using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.OData.Mcp.Core.Configuration;
using Microsoft.OData.Mcp.Middleware.Extensions;
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
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
//builder.Services.AddSwaggerGen();

// Configure MCP Server
var mcpConfig = new McpServerConfiguration();
builder.Configuration.GetSection("McpServer").Bind(mcpConfig);

// Add MCP Server services based on deployment mode
if (mcpConfig.DeploymentMode == McpDeploymentMode.Middleware)
{
    builder.Services.AddMcpMiddleware(options =>
    {
        builder.Configuration.GetSection("McpServer").Bind(options);
    });
}
else
{
    builder.Services.AddMcpServer(options =>
    {
        builder.Configuration.GetSection("McpServer").Bind(options);
    });
}

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    //app.UseSwagger();
    //app.UseSwaggerUI();
}

app.UseHttpsRedirection();

// Use MCP Server middleware if configured
if (mcpConfig.DeploymentMode == McpDeploymentMode.Middleware)
{
    app.UseMcpServer();
}

app.UseAuthorization();
app.MapControllers();

try
{
    Log.Information("Starting Microsoft.OData.Mcp.Sample");
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
