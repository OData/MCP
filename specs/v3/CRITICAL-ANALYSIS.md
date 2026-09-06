# Critical Analysis — Dual Prototypes

**Verdict:** The product shape is correct. The tree implements it two or three times. Concepts survive; implementations do not have to.

---

## 1. Eras

| Era | Theme | Keep as concept | Discard as code |
|-----|--------|-----------------|-----------------|
| E0 | Enterprise docs | Dual host story | Sidecar, winget, invented APIs |
| E1 | Magical AspNetCore | `AddODataMcp()` for every route; sibling of `$metadata` | Custom REST `/tools/execute` |
| E2 | Working CLI | stdio + SDK + metadata → tools | Dual pipeline registration, `0.*-*` SDK |
| E3 | Fix-it journals | Dual registration is poison | Incomplete cleanups |
| E4 | `NEW/Purpose.md` | Don’t reimplement MCP or ODL; be fast; no mocks | Overbuilt config as “already implemented” |

---

## 2. What is live today (2026-09-06)

**CLI stdio** against remote `$metadata` works:

```
start <url> → GET $metadata → CsdlParser → EdmModel
  → McpToolFactory → WithTools(...)
  → WithODataTools()   // second pipeline
  → stdio transport
```

SDK resolved: **`ModelContextProtocol` 0.9.0-preview.2**. Not 2.x.

**AspNetCore does not speak MCP.** `ModelContextProtocol.AspNetCore` is referenced; `MapMcp` is never called. Middleware invents REST. Route convention maps 501s. Sample builds official `IEdmModel`; Core expects custom `EdmModel`. No adapter.

---

## 3. Dual implementations

| Layer | Competitors |
|-------|-------------|
| Protocol | SDK stdio (live) vs custom REST (fake) vs unused SDK HTTP |
| Tools | `ODataMcpTools` + `DynamicODataMcpTools` attributes vs `McpToolFactory` vs `Legacy/*` |
| Config | `McpServerConfiguration` empire vs `ODataMcpOptions` |
| Model | Custom `EdmModel` vs official `IEdmModel` with no bridge |

CLI registers factory tools **and** attribute tools. That is the agent-poison catalog.

---

## 4. Lessons locked for v3

- Official MCP only. REST façade dies.
- Pin SDK 2.x.
- Custom EDM stays **as a job**. Parser/types may be rewritten from scratch.
- Resources/templates are the OData graph (not a consolation prize).
- Generic + named tools from **one** builder.
- `AddODataMcp()` / `.WithMcp()` is the public host API; `MapMcp` is private.
- `shutdown_server` is a local Tools development tool.
- AOT for the CLI host.
- Breakdance + real OData. No mocks.
- Unused config types do not ship.
