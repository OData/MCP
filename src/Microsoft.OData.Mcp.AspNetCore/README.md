# Microsoft OData for AI — Remote MCP for ASP.NET Core

NuGet package that exposes your existing ASP.NET Core OData API to AI assistants over the [Model Context Protocol](https://modelcontextprotocol.io).

This is what you offer **your customers** when they connect an assistant to your product: they stay in the chat and ask about *their* data on *your* API.

- Tell me about customer ALFKI.
- When was the last order on that account?
- Find customers who have not created a new project in 45 days.
- Give me ten non-obvious insights about this account.
- Who are we still waiting on in Germany?

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

## The Other Side of the Coin

This package is for helping developers with enabling MCP in OData APIs.
If you want an assistant on **your machine** to talk to someone else’s OData API, use [Microsoft OData for AI — Local MCP](../Microsoft.OData.Mcp.Tools/readme.md) (`dotnet odata-mcp`) instead.

Local MCP runs on Windows, macOS, and Linux.

# Getting Started

## Installation

```bash
dotnet add package Microsoft.OData.Mcp.AspNetCore
```

Target frameworks: `net8.0`, `net9.0`, `net10.0`, `net11.0`.

This package does **not** `PackageReference` `Microsoft.AspNetCore.OData`. Your app already brings OData 7 (`MapODataRoute` / Restier `MapApiRoute`) or OData 8 (`AddRouteComponents`). We discover prefix + `IEdmModel` from endpoint routing.

## Adding To Your API

```csharp
builder.Services
    .AddControllers()
    .AddOData(options => options.AddRouteComponents("odata", GetEdmModel()));

builder.Services.AddODataMcp();

var app = builder.Build();
app.MapControllers();

app.UseODataMcp();
```

MCP is at `/{yourRoute}/mcp`. `AddODataMcp` is DI only. `UseODataMcp` maps the endpoint **after** OData routes exist. Do not call the SDK `MapMcp` yourself.

Several OData prefixes become several MCP endpoints (`/odata/mcp`, `/reporting/mcp`, …). Hide a prefix with `IncludePrefixes` or `ExcludeRoutes`.

```csharp
builder.Services.AddODataMcp(options =>
{
    options.IncludePrefixes.Add("odata");
    options.Catalog.MaxNamedTools = 80;
    options.Catalog.InstructionsPreface = "Contoso ERP. Monetary amounts are USD.";
});
```

`InstructionsPreface` is prepended to the shared assistant instructions. It cannot replace them.

Explicit model when routing cannot see `IEdmModel`:

```csharp
builder.Services.AddODataMcp(options =>
{
    options.AddRoute("odata", GetEdmModel());
});
```

## Securing your MCP server

MCP here is a sibling of your OData API. Lock the MCP endpoint the same way you lock the API, then tell Local MCP how to sign in.

**Require an authenticated user on MCP**

```csharp
builder.Services.AddODataMcp(options =>
{
    options.RequireAuthorization = true;
});
```

That calls `RequireAuthorization` on the mapped MCP endpoints. Your existing authentication and authorization pipeline applies.

**Rate limit**

```csharp
builder.Services.AddRateLimiter(options =>
{
    options.AddFixedWindowLimiter("mcp", limiter =>
    {
        limiter.PermitLimit = 30;
        limiter.Window = TimeSpan.FromMinutes(1);
    });
});

builder.Services.AddODataMcp(options =>
{
    options.RateLimitingPolicyName = "mcp";
});
```

A global `UseRateLimiter` still applies when no policy name is set.

**Let Local MCP sign in with no extra flags**

Publish protected-resource metadata (RFC 9728) so `dotnet odata-mcp start` can discover the authorization server from a `401`. Independent of `AddODataMcp`.

```csharp
builder.Services.AddProtectedResourceMetadata(options =>
{
    options.AuthorizationServers.Add(new Uri("https://login.microsoftonline.com/contoso.com/v2.0"));
    options.ScopesSupported.Add("api://contoso-odata/Data.Read");
});
```

There is no `Use` call. The default protected resource is the application root. The document is served anonymously at `/.well-known/oauth-protected-resource`. A `401` on the host — OData HTTP or `{prefix}/mcp` — gains `resource_metadata="…"`. Same authorization server, same scopes, same Bearer.

Set `options.Prefixes` when the resource is not the whole host — any route base Endpoint Routing or Minimal APIs accept, including `""` and `"/"`. Each extra base is also published at `/.well-known/oauth-protected-resource/{prefix}`.

Token validation is your host’s authentication, or a gateway, in front of the API and MCP. This method publishes discovery; it does not issue or validate tokens.