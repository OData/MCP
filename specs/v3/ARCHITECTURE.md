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
Microsoft.OData.Mcp.Core            # Models/ (IEdmModel-shaped EDM), CSDL, catalogs, IODataExecutor
Microsoft.OData.Mcp.AspNetCore      # AddODataMcp; EndpointDataSource discovery; IEdmModel adapter; in-app HTTP
Microsoft.OData.Mcp.Tools           # AOT / dotnet tool; stdio; shutdown_server
Microsoft.OData.Mcp.Authentication  # Outbound OAuth (Local MCP → OData HTTP)
```

```
Tools ──────────► Core
AspNetCore ─────► Core
AspNetCore ─────► ModelContextProtocol.AspNetCore 2.x
AspNetCore ─────► Microsoft.OData.Edm          (adapter only; IEdmModel, 7.x band)
AspNetCore ─────► Microsoft.AspNetCore.App     (EndpointDataSource, HTTP)
AspNetCore ─ ✗ ─► Microsoft.AspNetCore.OData   (neither 7 nor 8 — app brings one)
Core / Tools ───► ModelContextProtocol 2.x  (includes namespace ModelContextProtocol.Authentication)
Core ───────────► no Edm, no OData.Core, no ASP.NET, no Microsoft.OData.Mcp.Authentication
Tools ─optional─► Microsoft.OData.Mcp.Authentication
AspNetCore ─ ✗ ─► Microsoft.OData.Mcp.Authentication
```

OData 7 vs 8 hosting assemblies must not be a compile-time dependency of the MCP host. See [ODATA-HOSTING.md](./ODATA-HOSTING.md).

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
        MetadataSource                  IODataExecutor
        $metadata → CsdlParser          Tools: HttpClient → remote
        IEdmModel → adapter             AspNetCore: route into app OData
              │
              ▼
        Core EdmModel
```

### 4.1 Public AspNetCore API

Discovery is **ASP.NET Core Endpoint Routing**, not `ODataOptions`. After the app maps OData (OData 8 `MapControllers` / `AddRouteComponents`, OData 7 `MapODataRoute`, Restier `MapApiRoute`), `AddODataMcp()` walks `EndpointDataSource` for prefixes and `IEdmModel` values. It does not `PackageReference` `Microsoft.AspNetCore.OData`.

**All discovered OData routes:**

```csharp
builder.Services.AddControllers()
    .AddOData(options =>
    {
        options.AddRouteComponents("api/v1", v1Model);
        options.AddRouteComponents("odata", mainModel);
    });

builder.Services.AddODataMcp();

app.MapControllers();
app.UseODataMcp();
```

Restier / OData 7:

```csharp
app.MapRestier(builder => builder.MapApiRoute<SomeApi>("odata", "odata"));
// AddODataMcp() already registered — discovers prefix "odata" from the catch-all endpoint
```

MCP appears at `{prefix}/mcp` (sibling of `$metadata`). `AddODataMcp` registers services. `UseODataMcp` maps MCP after OData routes exist. XML docs on `AddODataMcp` must say: *registers MCP for every OData prefix on the endpoint data source (OData 7 and 8); call `UseODataMcp` after mapping OData. Prefer this unless a route must stay hidden from agents.*

**One prefix only (version-neutral):**

```csharp
builder.Services.AddODataMcp(options =>
{
    options.IncludePrefixes.Add("odata");
});
```

`ExcludeRoutes` / `IncludePrefixes` cover “all except these” / opt-in without touching `ODataOptions`. All-routes vs opt-in remain mutually exclusive; calling both throws at startup.

Fluent `ODataOptions.WithMcp()` after `AddRouteComponents` is OData **8** sugar only. It must not live in a package that pulls OData 8 into Restier apps. See [ODATA-HOSTING.md](./ODATA-HOSTING.md).

Internally each enabled prefix calls SDK `MapMcp("{prefix}/mcp")` with a catalog bound to that prefix’s model. Developers never call `MapMcp`.

`AddProtectedResourceMetadata` is independent of `AddODataMcp`. It publishes one RFC 9728 document for the host (default: the application root). When MCP HTTP and OData HTTP share that host, they are the same protected resource: same authorization server, same scopes, same Bearer. Token validation stays with the host’s `AddJwtBearer` (or a gateway). AspNetCore does not reference `Microsoft.OData.Mcp.Authentication`. See [AUTHENTICATION.md](./AUTHENTICATION.md).

### 4.2 `IODataExecutor`

```csharp
public interface IODataExecutor
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
odata-mcp start <serviceRoot> [auth options]
odata-mcp try   <serviceRoot> [auth options]
odata-mcp add   [serviceRoot]
```

Public command name `odata-mcp` (align `ToolCommandName`; today it is `dotnet-odata-mcp`). Stdio default. Optional stateless HTTP. AOT publish is a first-class artifact. Always registers `shutdown_server`.

`<serviceRoot>` is the service root; the host appends `$metadata` itself and trims a pasted `.../$metadata` URL (`ToolsMcpHost.NormalizeServiceRoot`).

**`try`** probes a service before anyone commits to it (`Core.Diagnostics.ServiceProbe`): (1) `GET $metadata` and parse; (2) `GET {firstSet}?$top=1`; (3) which of the two refused an anonymous caller, with the `WWW-Authenticate` challenge; (4) whether the row's properties are declared on the type. The first pass is always anonymous and never signs in. On a 401/403 with auth flags present it probes again through the authenticating `"OData"` client; with no flags it runs `OAuthDiscovery` and prints the authorization server, advertised grants, and the flags to pass. Exit codes: `0` ready, `2` sign-in required, `1` unreachable or unreadable.

**`start`** runs steps 2–4 of the same probe through the default (unauthenticated) client after the catalog is built. The verdict is logged; when data is secured and no credential was passed, `ODataMcpCatalogOptions.InstructionsPreface` tells the model so before its first call. Probe failures never block startup, and the interactive sign-in still happens lazily on the first real tool call.

---

## 5. Configuration

Ship options that drive a code path:

| Area | Options |
|------|---------|
| Remote service | Base URL, metadata path |
| Catalog | Max named tools, include/exclude sets |
| Outbound auth | None / discovered OAuth (device code, authorization code + PKCE, client credentials, identity assertion) via `Microsoft.OData.Mcp.Authentication` / `--auth-token` Bearer / API key / Basic escape hatches. See [AUTHENTICATION.md](./AUTHENTICATION.md) |
| Host discovery | `AddProtectedResourceMetadata` — one RFC 9728 document for the host (default: application root). Covers MCP HTTP and OData HTTP when they share the host. Independent of `AddODataMcp`. |
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

See [TESTING.md](./TESTING.md).

Breakdance for DI. Official Northwind (read) and TripPin (read/write). **OData 7 and OData 8 must not share a test process.** Restier is tested only with OData 7 (`Microsoft.OData.Mcp.Tests.AspNetCore.Restier`, `RestierBreakdanceTestBase<TApi>`, endpoint routing) until Restier hosts on OData 8. Convention OData 8 tests stay in `Microsoft.OData.Mcp.Tests.AspNetCore`. PublishAot smoke for Tools.

**Never mock `HttpClient`, OData, MCP, or metadata.** Do not `[Ignore]` Restier because of OData 8.

---

## 9. Forbidden public names

`AddODataMcpServer`, `/mcp/tools/execute`, `Microsoft.OData.Mcp.Sidecar`, `IOdataSchema` as a shipped type, `queryCustomers`.
