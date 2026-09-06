# Architecture — OData MCP Platform v3

---

## 1. Purpose

Agents run real OData operations through MCP 2.

- **Resources / templates** describe what exists (the OData graph).
- **Tools** perform operations on that graph.
- **Two hosts** share one Core: a local AOT server for any remote API, and an ASP.NET Core package for apps that already have OData models.

---

## 2. Non-goals

| Non-goal | Why |
|----------|-----|
| Reimplement MCP JSON-RPC | SDK 2.x |
| Custom REST tool-execute | Not MCP |
| In-process OData query engine | Forward to OData |
| `Microsoft.OData.Core` | Not on our path; not AOT-friendly |
| EdmLib in Core or Tools | AOT + historical perf |
| Third schema type (`IOdataSchema`) | Core model is the projection |
| DotNetDocs scraper | Consume annotations only |
| Redis / feature flags / IP allowlists | No engine, no type |
| `shutdown_server` on AspNetCore | Local Tools only |
| Public `MapMcp` as the product API | SDK primitive, internal |

---

## 3. Packages

```
Microsoft.OData.Mcp.Core            # Models/ (IEdmModel-shaped EDM), CSDL, catalogs, IOdataExecutor
Microsoft.OData.Mcp.AspNetCore      # AddODataMcp / WithMcp; IEdmModel adapter; in-app routing
Microsoft.OData.Mcp.Tools           # AOT / dotnet tool; stdio; shutdown_server
Microsoft.OData.Mcp.Authentication  # Optional inbound JWT / outbound helpers
```

```
Tools ──────────► Core
AspNetCore ─────► Core
AspNetCore ─────► ModelContextProtocol.AspNetCore 2.x
AspNetCore ─────► Microsoft.OData.Edm          (adapter only)
AspNetCore ─────► Microsoft.AspNetCore.OData   (peer)
Core / Tools ───► ModelContextProtocol 2.x
Core ───────────► no Edm, no OData.Core, no ASP.NET, no Authentication
Tools ─optional─► Authentication
AspNetCore ─opt─► Authentication
```

Pin MCP 2.x. Tools: `IsAotCompatible`, `PublishAot` profile, JSON source-gen, explicit tool registration.

---

## 4. Components

```
Hosts:  Tools (stdio / optional HTTP, AOT)     AspNetCore (internal MapMcp per prefix)
                              │
                              ▼  SDK 2.x
              ToolCatalog + ResourceCatalog
                              │
              ┌───────────────┴───────────────┐
              ▼                               ▼
        MetadataSource                  IOdataExecutor
        $metadata → CsdlParser          Tools: HttpClient → remote
        IEdmModel → adapter             AspNetCore: route into app OData
              │
              ▼
        Core EdmModel
```

### 4.1 Public AspNetCore API

**All registered OData routes:**

```csharp
builder.Services.AddControllers()
    .AddOData(options =>
    {
        options.AddRouteComponents("api/v1", v1Model);
        options.AddRouteComponents("odata", mainModel);
    });

builder.Services.AddODataMcp();

app.MapControllers();
```

MCP appears at `/api/v1/mcp` and `/odata/mcp` (sibling of `$metadata`). XML docs on `AddODataMcp` must say: *enables MCP for every OData route component; prefer this unless a route must stay hidden from agents.*

**One route only:**

```csharp
builder.Services.AddControllers()
    .AddOData(options =>
    {
        options.AddRouteComponents("odata", mainModel).WithMcp();
        options.AddRouteComponents("internal", adminModel);
    });

app.MapControllers();
```

`WithMcp()` is an extension on the `ODataOptions` chain. It enables MCP for the **most recently added** route component. XML docs must say: *Call immediately after `AddRouteComponents` for the route that should speak MCP. Do not also call `AddODataMcp()`.*

**Mutual exclusion:** `AddODataMcp()` = all current and future route components. `.WithMcp()` = opt-in list. Calling both is invalid and must throw at startup with a message that names both APIs.

`ExcludeRoutes` on `AddODataMcp` covers “all except these” without switching to per-route opt-in.

Internally each enabled prefix calls SDK `MapMcp("{prefix}/mcp")` with a catalog bound to that prefix’s model. Developers never call `MapMcp`.

### 4.2 `IOdataExecutor`

```csharp
public interface IOdataExecutor
{

    Task<ODataExecuteResult> ExecuteAsync(ODataExecuteRequest request, CancellationToken cancellationToken);

}
```

| Host | Implementation |
|------|----------------|
| Tools | Named `HttpClient` to the remote service |
| AspNetCore | HTTP to the **same app** OData route (forwarder / loopback that preserves user) |

No `$filter` evaluation in this repo.

### 4.3 Tools CLI

```
odata-mcp start <serviceUrl> [options]
odata-mcp test  <serviceUrl>
```

Public command name `odata-mcp` (align `ToolCommandName`; today it is `dotnet-odata-mcp`). Stdio default. Optional stateless HTTP. AOT publish is a first-class artifact. Always registers `shutdown_server`.

---

## 5. Configuration

Ship options that drive a code path:

| Area | Options |
|------|---------|
| Remote service | Base URL, metadata path |
| Catalog | Max named tools, include/exclude sets |
| Outbound auth | None / Bearer / API key / Basic / Forward |
| AspNetCore | ExcludeRoutes (with `AddODataMcp` only) |
| Logging | stderr for stdio |

Delete unused config types until a path needs them.

---

## 6. Performance and AOT

Parse metadata once; cache model + catalogs. Deterministic list order. `ttlMs` / `cacheScope`. Span-minded CSDL. Shared JSON options + source-gen. No pretty-print on hot paths. `IHttpClientFactory`.

Tools AOT: no `WithToolsFromAssembly` on the native path; no EdmLib in the graph.

---

## 7. Security

Inbound MCP HTTP: ASP.NET schemes. Outbound: configured credentials or forward `Authorization`. Destructive tools: `destructiveHint`; MRTR confirm later. Exclude binary/stream properties from generated schemas.

---

## 8. Testing

Breakdance for DI. Official Northwind (read) and TripPin (read/write). In-process Restier OData via `Microsoft.Restier.Breakdance.RestierBreakdanceTestBase<TApi>` (subclass of `AspNetCoreBreakdanceTestBase`). Convention-model host tests may use `AspNetCoreBreakdanceTestBase` directly. MCP SDK client for protocol. PublishAot smoke for Tools.

**Never mock `HttpClient`, OData, MCP, or metadata.**

---

## 9. Forbidden public names

`AddODataMcpServer`, `/mcp/tools/execute`, `Microsoft.OData.Mcp.Sidecar`, `IOdataSchema` as a shipped type, `queryCustomers`.
