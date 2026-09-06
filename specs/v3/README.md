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
2. **ASP.NET Core package** — `AddODataMcp()` for every registered OData route, or `.WithMcp()` on a specific route. Discovers `IEdmModel`, speaks official MCP, and turns calls into **routed OData requests** on those models.

Both speak **only** official MCP through the **official C# SDK 2.x**.

---

## Documents

| Document | Purpose |
|----------|---------|
| [CRITICAL-ANALYSIS.md](./CRITICAL-ANALYSIS.md) | What is live, what is theater |
| [ARCHITECTURE.md](./ARCHITECTURE.md) | Packages, hosts, public API, AOT |
| [PROTOCOL.md](./PROTOCOL.md) | MCP 2: resources = graph, tools = operations |
| [METADATA-STRATEGY.md](./METADATA-STRATEGY.md) | Custom EDM stays; AspNetCore adapts `IEdmModel` |
| [TOOL-SURFACE.md](./TOOL-SURFACE.md) | Generic + named tools; local `shutdown_server` |
| [INVENTORY.md](./INVENTORY.md) | Concept keep vs implementation delete |
| [EXECUTION-PLAN.md](./EXECUTION-PLAN.md) | Agent task list |

---

## Hard rules

1. **Official MCP only.** No custom `/mcp/tools/execute`.
2. **SDK 2.x pinned.** No `0.*-*`.
3. **Custom EDM in Core (`Models/`).** No `Microsoft.OData.Core`. No `Microsoft.OData.Edm` in Core or Tools. No third schema type and no second `Edm/` folder. The model is **IEdmModel-shaped**: undeclared / ignored properties never appear in tools, resources, schemas, or generated JSON.
4. **AspNetCore adapts** `Microsoft.OData.Edm.IEdmModel` into Core’s model.
5. **MCP 2 projection:** resources and resource templates **are** the OData graph; tools are operations on that graph.
6. **One builder** emits generic tools, named tools, and resources. Dual *pipelines* are forbidden.
7. **Forward OData.** Do not re-evaluate `$filter`. CLI HTTP-calls the remote service. AspNetCore routes into the app’s OData endpoints.
8. **Public AspNetCore API:** `AddODataMcp()` (all routes) or `.WithMcp()` (one route). XML docs must make the choice obvious. SDK `MapMcp` is internal.
9. **`shutdown_server`** is part of the **local Tools** spec. Never on AspNetCore endpoints.
10. **Tools host is AOT-first.** Explicit registration, JSON source-gen, no assembly scan on the native path.
11. **Core does not reference Authentication.**
12. **Tests:** Breakdance, real Northwind and TripPin (and in-process OData TestServer). **Never mock.**

---

## Spec governance

Treat v3 edits as code review. When implementation diverges, update the v3 doc in the same PR. Do not revive `AddODataMcpServer`, Sidecar, winget, or `queryCustomers`.
