# Installation Guide

This guide provides detailed instructions for installing and setting up the Microsoft OData MCP Server in various environments.

## System Requirements

### Minimum Requirements
- **.NET Runtime**: .NET 8.0 or later
- **Operating System**: Windows, Linux, or macOS
- **Memory**: 512 MB RAM (2 GB recommended)
- **Disk Space**: 100 MB for binaries

### Development Requirements
- **.NET SDK**: .NET 8.0 SDK or later
- **IDE**: Visual Studio 2022, VS Code, or JetBrains Rider
- **Git**: For cloning the repository

## Installation Methods

### Method 1: NuGet Package (Recommended)

#### For ASP.NET Core Integration

```bash
# Install the ASP.NET Core integration package
dotnet add package Microsoft.OData.Mcp.AspNetCore

# Install authentication package if needed
dotnet add package Microsoft.OData.Mcp.Authentication
```

#### For Standalone Server

```bash
# Install the sidecar package
dotnet add package Microsoft.OData.Mcp.Sidecar
```

#### For Core Library Only

```bash
# Install core library for custom implementations
dotnet add package Microsoft.OData.Mcp.Core
```

### Method 2: Build from Source

```bash
# Clone the repository
git clone https://github.com/microsoft/odata-mcp-server.git
cd odata-mcp-server

# Restore dependencies
dotnet restore

# Build the solution
dotnet build --configuration Release

# Run tests (optional)
dotnet test --configuration Release

# Pack NuGet packages locally
dotnet pack --configuration Release --output ./packages
```

### Method 3: Docker Container

```dockerfile
# Dockerfile for OData MCP Server
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["src/Microsoft.OData.Mcp.Sidecar/Microsoft.OData.Mcp.Sidecar.csproj", "src/Microsoft.OData.Mcp.Sidecar/"]
RUN dotnet restore "src/Microsoft.OData.Mcp.Sidecar/Microsoft.OData.Mcp.Sidecar.csproj"
COPY . .
WORKDIR "/src/src/Microsoft.OData.Mcp.Sidecar"
RUN dotnet build "Microsoft.OData.Mcp.Sidecar.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Microsoft.OData.Mcp.Sidecar.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Microsoft.OData.Mcp.Sidecar.dll"]
```

Build and run:

```bash
# Build the Docker image
docker build -t odata-mcp-server .

# Run the container
docker run -d -p 8080:80 \
  -e ODataMcp__ServiceUrl="https://services.odata.org/V4/Northwind/Northwind.svc" \
  odata-mcp-server
```

## Platform-Specific Installation

### Windows

#### Using Windows Package Manager
```powershell
# Coming soon
winget install Microsoft.ODataMcpServer
```

#### Manual Installation
1. Download the latest release from [GitHub Releases](https://github.com/microsoft/odata-mcp-server/releases)
2. Extract to your preferred location (e.g., `C:\Program Files\ODataMcpServer`)
3. Add to PATH environment variable
4. Run `odata-mcp-server --version` to verify

### Linux

#### Ubuntu/Debian
```bash
# Add Microsoft package repository
wget https://packages.microsoft.com/config/ubuntu/22.04/packages-microsoft-prod.deb -O packages-microsoft-prod.deb
sudo dpkg -i packages-microsoft-prod.deb
rm packages-microsoft-prod.deb

# Install .NET Runtime
sudo apt-get update
sudo apt-get install -y dotnet-runtime-8.0

# Install OData MCP Server
dotnet tool install --global Microsoft.OData.Mcp.CLI
```

#### Red Hat/CentOS
```bash
# Add Microsoft package repository
sudo rpm -Uvh https://packages.microsoft.com/config/rhel/8/packages-microsoft-prod.rpm

# Install .NET Runtime
sudo dnf install dotnet-runtime-8.0

# Install OData MCP Server
dotnet tool install --global Microsoft.OData.Mcp.CLI
```

### macOS

#### Using Homebrew
```bash
# Install .NET
brew install --cask dotnet

# Install OData MCP Server
dotnet tool install --global Microsoft.OData.Mcp.CLI
```

#### Manual Installation
1. Install .NET from [Microsoft's website](https://dotnet.microsoft.com/download)
2. Run `dotnet tool install --global Microsoft.OData.Mcp.CLI`

## Post-Installation Setup

### 1. Verify Installation

```bash
# Check version
odata-mcp-server --version

# Run health check
odata-mcp-server health
```

### 2. Configure Environment

Create an `appsettings.json` file:

```json
{
  "ODataMcp": {
    "ServiceUrl": "https://your-odata-service.com/odata",
    "Authentication": {
      "Enabled": true,
      "Type": "OAuth2",
      "Authority": "https://login.microsoftonline.com/your-tenant"
    },
    "Caching": {
      "MetadataCacheDuration": "01:00:00",
      "ResultCacheDuration": "00:05:00"
    }
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.OData.Mcp": "Debug"
    }
  }
}
```

### 3. Set Environment Variables

```bash
# Linux/macOS
export ODATAMCP_SERVICEURL="https://your-odata-service.com/odata"
export ODATAMCP_AUTHENTICATION__ENABLED="true"

# Windows
set ODATAMCP_SERVICEURL=https://your-odata-service.com/odata
set ODATAMCP_AUTHENTICATION__ENABLED=true
```

### 4. Configure Logging

For production environments, configure structured logging:

```json
{
  "Serilog": {
    "Using": ["Serilog.Sinks.Console", "Serilog.Sinks.File"],
    "MinimumLevel": "Information",
    "WriteTo": [
      {
        "Name": "Console",
        "Args": {
          "theme": "Serilog.Sinks.SystemConsole.Themes.AnsiConsoleTheme::Code, Serilog.Sinks.Console"
        }
      },
      {
        "Name": "File",
        "Args": {
          "path": "logs/odata-mcp-.log",
          "rollingInterval": "Day",
          "retainedFileCountLimit": 7
        }
      }
    ]
  }
}
```

## Integration with Existing Applications

### ASP.NET Core Application

1. Install the package:
```bash
dotnet add package Microsoft.OData.Mcp.AspNetCore
```

2. Update `Program.cs`:
```csharp
var builder = WebApplication.CreateBuilder(args);

// Add OData MCP services
builder.Services.AddODataMcpServer(builder.Configuration);

var app = builder.Build();

// Use OData MCP middleware
app.UseODataMcp();

app.Run();
```

### Console Application

1. Install the package:
```bash
dotnet add package Microsoft.OData.Mcp.Core
```

2. Create a host:
```csharp
using Microsoft.Extensions.Hosting;
using Microsoft.OData.Mcp.Core;

var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices((context, services) =>
    {
        services.AddODataMcpServer(context.Configuration);
    })
    .Build();

await host.RunAsync();
```

## Deployment Considerations

### Production Checklist

- [ ] Configure proper authentication
- [ ] Set up SSL/TLS certificates
- [ ] Configure rate limiting
- [ ] Enable health checks
- [ ] Set up monitoring and logging
- [ ] Configure backup and recovery
- [ ] Review security settings
- [ ] Test disaster recovery procedures

### Performance Optimization

1. **Enable Caching**:
```json
{
  "ODataMcp": {
    "Caching": {
      "Provider": "Redis",
      "ConnectionString": "localhost:6379",
      "MetadataCacheDuration": "24:00:00"
    }
  }
}
```

2. **Configure Connection Pooling**:
```json
{
  "ODataMcp": {
    "HttpClient": {
      "MaxConnectionsPerServer": 100,
      "Timeout": "00:00:30"
    }
  }
}
```

3. **Enable Response Compression**:
```csharp
builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
});
```

## Troubleshooting Installation

### Common Issues

**Issue**: "The specified framework 'Microsoft.NETCore.App', version '8.0.0' was not found"
```bash
# Solution: Install .NET Runtime
# Windows
winget install Microsoft.DotNet.Runtime.8

# Linux
sudo apt-get install dotnet-runtime-8.0

# macOS
brew install --cask dotnet
```

**Issue**: "Unable to load DLL 'Microsoft.OData.Mcp.Native'"
```bash
# Solution: Install Visual C++ Redistributable (Windows)
# Download from: https://aka.ms/vs/17/release/vc_redist.x64.exe
```

**Issue**: "Permission denied" when running on Linux
```bash
# Solution: Make executable
chmod +x odata-mcp-server

# Or run with dotnet
dotnet odata-mcp-server.dll
```

## Next Steps

- [Configuration Reference](configuration.md) - Detailed configuration options
- [Integration Guide](integration-guide.md) - Integrate with your OData APIs
- [Security Setup](security.md) - Configure authentication and authorization
- [Getting Started](getting-started.md) - Quick start tutorial