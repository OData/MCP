# Microsoft OData for AI

[![License](https://img.shields.io/github/license/odata/mcp.svg)](LICENSE)

Ask your AI about your business data — customers, orders, projects, accounts — and get answers from the live OData APIs you already run.

Microsoft OData for AI connects an assistant to those APIs through [Model Context Protocol 2.0](https://modelcontextprotocol.io). The assistant can look up a record, filter a list, follow a relationship, and make a change, without you pasting spreadsheets into the chat.

Two products, one idea:

| | **Local MCP** | **Remote MCP** |
|---|---|---|
| Who | You, using an AI on your machine | You, shipping an OData API |
| What | A small app that talks to any OData service on your behalf | A NuGet package that puts MCP on the API you already host |
| Where | Windows, macOS, and Linux | Your ASP.NET Core host |
| Package | `Microsoft.OData.Mcp.Tools` (`dotnet odata-mcp`) | `Microsoft.OData.Mcp.AspNetCore` |

Namespaces and project names stay `Microsoft.OData.Mcp.*`.

## What you can ask

Once it is connected, questions like these go against **your** data:

- Tell me about customer ALFKI.
- When was the last order for that account?
- Find customers who have not created a new project in 45 days.
- Give me ten non-obvious insights about this account.
- Who are the German customers, and what are they waiting on?

The assistant is steered toward the right call the first time. It does not chew through `$metadata` (EDMX) to guess how your API works.

## Before OData for AI

- AI will connect to your service, download the $metadata document, and start flopping around trying to use it. 
    - What YOU pay for: wasted connections, wasted bandwidth, wasted compute.
    - What THEY pay for: wasted tokens, wasted usage, wasted time.

### Nobody wins.

| Service Name | $metadata size | Tokens to read |
| ------------ | -------------- | -------------- |
| Northwind    | 19,406 bytes   | 4,802          |
| TripPin      | 7,297 bytes    | 1,859          |

## After OData for AI

- Your MCP server starts up, logs in, processes the remote $metadata, and builds a compact picture of the service.
- It provides your or your customer's AI with specific tools and instructions to help maximize success while minimizing wasted tokens & bandwidth.
    - What YOU get: happier customers and smaller bills.
    - What THEY get: faster answer and smaller bills.

### EVERYBODY WINS!

| Service Name | Compact payload size | % size reduction | Tokens to read | % token reduction |
| ------------ | -------------------- | ---------------- | -------------- | ----------------- |
| Northwind    | 2,419 bytes          | 87.5%            | 654            | 86.4%             |
| TripPin      | 320 bytes            | 95.6%            | 91             | 95.1%             |

# What We Ship

## NuGet packages

| Package | What it is | Version |
|---|---|---|
| [`Microsoft.OData.Mcp.Tools`](https://www.nuget.org/packages/Microsoft.OData.Mcp.Tools/) | Local MCP (`dotnet odata-mcp`) | [![NuGet](https://img.shields.io/nuget/vpre/Microsoft.OData.Mcp.Tools.svg)](https://www.nuget.org/packages/Microsoft.OData.Mcp.Tools/) |
| [`Microsoft.OData.Mcp.AspNetCore`](https://www.nuget.org/packages/Microsoft.OData.Mcp.AspNetCore/) | Remote MCP | [![NuGet](https://img.shields.io/nuget/vpre/Microsoft.OData.Mcp.AspNetCore.svg)](https://www.nuget.org/packages/Microsoft.OData.Mcp.AspNetCore/) |
| [`Microsoft.OData.Mcp.Core`](https://www.nuget.org/packages/Microsoft.OData.Mcp.Core/) | Shared catalog and models | [![NuGet](https://img.shields.io/nuget/vpre/Microsoft.OData.Mcp.Core.svg)](https://www.nuget.org/packages/Microsoft.OData.Mcp.Core/) |
| [`Microsoft.OData.Mcp.Authentication`](https://www.nuget.org/packages/Microsoft.OData.Mcp.Authentication/) | Outbound OAuth for Local MCP | [![NuGet](https://img.shields.io/nuget/vpre/Microsoft.OData.Mcp.Authentication.svg)](https://www.nuget.org/packages/Microsoft.OData.Mcp.Authentication/) |

## Build pipelines

| Stream | Pipeline |
|---|---|
| CI | [![Build status](https://dev.azure.com/dotnet/OData/_apis/build/status/MCP/MCP-CI)](https://dev.azure.com/dotnet/OData/_build/latest?definitionId=199) |
| RC | [![Build status](https://dev.azure.com/dotnet/OData/_apis/build/status/MCP/MCP-Beta)](https://dev.azure.com/dotnet/OData/_build/latest?definitionId=200) |
| RTM | [![Build status](https://dev.azure.com/dotnet/OData/_apis/build/status/MCP/MCP-Prod)](https://dev.azure.com/dotnet/OData/_build/latest?definitionId=201) |

# How It Works

## Local MCP — use it on your machine

Install the tool, point it at a service, add it to your assistant.

```bash
dotnet tool install -g Microsoft.OData.Mcp.Tools

dotnet odata-mcp try https://services.odata.org/TripPinRESTierService
dotnet odata-mcp add
```

`try` checks that the service answers. `add` walks you through registering it with Claude Code so you never paste a secret into shell history. Then ask the questions above.

Full happy path, signing in, and what to do when something is off: [Local MCP](src/Microsoft.OData.Mcp.Tools/readme.md).

## Remote MCP — put it on your API

This is what you offer **your customers**: they connect an assistant to your product and ask the same kind of questions, against **their** data on **your** API, without waiting while it guesses at `$metadata`.

If you already host OData in ASP.NET Core (OData 7, OData 8, or Restier):

```csharp
builder.Services.AddODataMcp();

var app = builder.Build();
app.MapControllers();   // or MapODataRoute / MapApiRoute
app.UseODataMcp();      // MCP at {prefix}/mcp, for example /odata/mcp
```

Assistants (and Local MCP) then talk to `https://your-api.example/odata/mcp`.

Install, getting started, and securing the endpoint: [Remote MCP](src/Microsoft.OData.Mcp.AspNetCore/readme.md).
