# OData MCP Platform — Specification v3

**Status:** Living (authoritative)  
**Revised:** 2026-09-06  
**Protocol:** MCP `2026-07-28` via **ModelContextProtocol C# SDK 2.x**  
**Stance:** Concepts are sacred. Every current class is a candidate for deletion.

---

## Product

**Microsoft.OData.Mcp** turns OData models into MCP servers so agents can query and mutate data using **real OData HTTP**.

One Core library. Two hosts:

1. **Local AOT MCP 2.x server** (`odata-mcp`) — parse any remote OData API and serve stdio (primary) or Streamable HTTP (optional). Includes `shutdown_server` for the development loop.
2. **ASP.NET Core package** — `AddODataMcp()` for every OData route on the **endpoint data source**, or prefix opt-in. Discovers `IEdmModel` from Endpoint Routing (OData 7 `MapODataRoute` / Restier `MapApiRoute` and OData 8 conventional endpoints). Speaks official MCP and turns calls into **routed OData HTTP** on those prefixes. Does **not** `PackageReference` `Microsoft.AspNetCore.OData`.

Both speak **only** official MCP through the **official C# SDK 2.x**.

---

## Documents

| Document | Purpose |
|----------|---------|
| [CRITICAL-ANALYSIS.md](./CRITICAL-ANALYSIS.md) | What is live, what is theater |
| [ARCHITECTURE.md](./ARCHITECTURE.md) | Packages, hosts, public API, AOT |
| [ODATA-HOSTING.md](./ODATA-HOSTING.md) | OData 7/8 via Endpoint Routing; no AspNetCore.OData PackageReference |
| [PROTOCOL.md](./PROTOCOL.md) | MCP 2: resources = graph, tools = operations |
| [METADATA-STRATEGY.md](./METADATA-STRATEGY.md) | Custom EDM stays; AspNetCore adapts `IEdmModel` |
| [TOOL-SURFACE.md](./TOOL-SURFACE.md) | Generic + named tools; local `shutdown_server` |
| [TYPE-SHAPES.md](./TYPE-SHAPES.md) | How MCP describes OData types: grammar, describe/describe_model, required-on-create, operations |
| [OPTIMIZATION.md](./OPTIMIZATION.md) | Hide EDMX from the calling AI; steer first-call success so the user is not waiting on retries |
| [OPTIMIZATION-PLAN.md](./OPTIMIZATION-PLAN.md) | Agent task list for that work. Do not use EXECUTION-PLAN.md. |
| [TOOL-TEST-MANIFEST.md](./TOOL-TEST-MANIFEST.md) | Exhaustive per-tool test cases (happy, malformed, overwhelm) |
| [TESTING.md](./TESTING.md) | Suites, OData 7 vs 8 process isolation, Restier on 7; §8 authenticated OData end to end (Local MCP → OData HTTP) |
| [AUTHENTICATION.md](./AUTHENTICATION.md) | Local MCP → OData HTTP OAuth (SDK 2.2). Colocated MCP HTTP and OData HTTP share one RFC 9728 document. |
| [INVENTORY.md](./INVENTORY.md) | Concept keep vs implementation delete |
| [EXECUTION-PLAN.md](./EXECUTION-PLAN.md) | **Complete.** Catalog + hosts + tools. Do not implement from it. |

---

## Hard rules

1. **Official MCP only.** No custom `/mcp/tools/execute`.
2. **SDK 2.x pinned.** No `0.*-*`.
3. **Custom EDM in Core (`Models/`).** No `Microsoft.OData.Core`. No `Microsoft.OData.Edm` in Core or Tools. No third schema type and no second `Edm/` folder. The model is **IEdmModel-shaped**: undeclared / ignored properties never appear in tools, resources, schemas, or generated JSON.
4. **AspNetCore adapts** `Microsoft.OData.Edm.IEdmModel` into Core’s model. EdmLib 7.x is shared by OData 7 and 8; that is not a host split.
5. **MCP 2 projection:** resources and resource templates **are** the OData graph; tools are operations on that graph.
6. **One builder** emits generic tools, named tools, and resources. Dual *pipelines* are forbidden.
7. **Forward OData.** Do not re-evaluate `$filter`. CLI HTTP-calls the remote service. AspNetCore HTTP-calls the app’s OData routes.
8. **Public AspNetCore API:** `AddODataMcp()` registers services. `UseODataMcp()` maps MCP at `{prefix}/mcp` after OData routes exist. Prefix include/exclude on host options. SDK `MapMcp` is internal. The host package **must not** reference `Microsoft.AspNetCore.OData` (7 or 8). See [ODATA-HOSTING.md](./ODATA-HOSTING.md).
9. **`shutdown_server`** is part of the **local Tools** spec. Never on AspNetCore endpoints.
10. **Tools host is AOT-first.** Explicit registration, JSON source-gen, no assembly scan on the native path.
11. **Core does not reference `Microsoft.OData.Mcp.Authentication`.** That package is Local MCP’s outbound OAuth client (OData HTTP). It is not the SDK namespace `ModelContextProtocol.Authentication`, which already ships in `ModelContextProtocol` 2.x — Core already PackageReferences that. AspNetCore publishes RFC 9728 with `AddProtectedResourceMetadata` and does not reference this package either.
12. **Tests:** Breakdance, real Northwind and TripPin, **never mock.** OData 7 and OData 8 **must not** share a test process. Restier is tested **only** with OData 7 until Restier hosts on 8. See [TESTING.md](./TESTING.md).

---

## Spec governance

Treat v3 edits as code review. When implementation diverges, update the v3 doc in the same PR. Do not revive `AddODataMcpServer`, Sidecar, winget, or `queryCustomers`.
