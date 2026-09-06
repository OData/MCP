# MCP Protocol — `2026-07-28` / SDK 2.x

Spec: [MCP 2026-07-28](https://modelcontextprotocol.io/specification/2026-07-28)  
SDK: [C# SDK 2.0](https://devblogs.microsoft.com/dotnet/announcing-v20-of-the-official-mcp-csharp-sdk/)

---

## 1. Projection

OData is a resource graph with operations. MCP 2 already has that split.

| OData | MCP |
|-------|-----|
| Entity sets, singletons, `$metadata` | `resources/list` |
| `Products({key})`, `{key}/{nav}` | `resources/templates/list` + `completion/complete` |
| Query, CUD, actions, functions | `tools/list` + `tools/call` |
| CSDL Documentation / `Core.Description` | `title`, `description`, parameter docs |
| HTTP verb | `ToolAnnotations` (`readOnlyHint`, `destructiveHint`, `idempotentHint`, `openWorldHint`) |

Do not invent `tools/search`. Do not dump a thousand `inputSchema`s into context.

---

## 2. URI scheme

Use **`odata://`**, not `https://`.

MCP says `https://` is for URIs the **client can fetch itself**. Authenticated APIs and in-app routes fail that test.

```
odata://{route}/$metadata
odata://{route}/{entitySet}
odata://{route}/{entitySet}({key})
odata://{route}/{entitySet}({key})/{navigation}
odata://{route}/{singleton}
```

`{route}` is `remote` for the CLI (single service) or the OData prefix (`odata`, `api/v1`) for AspNetCore.

The executor translates `odata://` into an OData HTTP request.

---

## 3. Resources

**List (catalog, not rows):** `$metadata`, each entity set (type card: keys, **declared** properties, navs, docs), singletons, later unbound operations. Type cards omit anything not on the EDM.

**Read:**

| URI | Contents |
|-----|----------|
| `$metadata` | CSDL XML (`application/xml`) |
| entity set | JSON type card, **not** the collection |
| `{entitySet}({key})` | Entity JSON via GET, size-guarded |
| collection URI as data | **Forbidden** — use `odata_query` |

`audience`: `["assistant"]` for type cards; `["user","assistant"]` for `$metadata` if a host shows it in UI.

Pagination + `ttlMs` + `cacheScope` on list results. Deterministic order: `$metadata`, then sets by name.

---

## 4. Resource templates

Always advertise templates matching OData path syntax. Completions on `{entitySet}`, `{navigation}`, and function names. `{key}` completion only if cheap.

---

## 5. Tools

Always-on **generic** set (see [TOOL-SURFACE.md](./TOOL-SURFACE.md)). Optional **named** tools up to `MaxNamedTools` (default 150, including generics). Remainder of the graph stays resources.

Generic tools first in `tools/list`, then named tools by entity set then operation.

Every tool: short `name`, human `title`, tight `description` (prefer CSDL docs), JSON Schema 2020-12 `inputSchema`, `outputSchema`, `structuredContent` plus a short text summary, verb annotations, `openWorldHint: true`.

Query parameter names have **no `$`**. The executor adds them.

---

## 6. Transports

| Host | Transport |
|------|-----------|
| Tools | stdio required; Streamable HTTP optional, stateless |
| AspNetCore | Streamable HTTP stateless at `{prefix}/mcp` via internal `MapMcp` |

No HTTP+SSE. No custom REST. Logging on stdio goes to **stderr only**.

---

## 7. Cache and change

| Catalog | `ttlMs` | `cacheScope` |
|---------|---------|--------------|
| Public remote metadata | 300000–3600000 | `public` |
| Authenticated / filtered | 60000–300000 | `private` |
| In-process model | long / until recycle | `private` if auth varies the catalog |

`listChanged` may be advertised on stdio. Stateless HTTP uses TTL; it cannot push.

---

## 8. Server identity and instructions

`serverInfo.name`: `odata-mcp` (Tools) or `Microsoft.OData.Mcp` (AspNetCore). Version from assembly.

Instructions:

> Prefer named tools when listed. If the entity set is not listed, read `odata://…/{entitySet}` then call generic `odata_*` tools. Query parameter names do not include `$`. Resource URIs use the `odata://` scheme.

---

## 9. Local-only: `shutdown_server`

Registered only by the Tools host. Not in AspNetCore catalogs.

Purpose: agent-driven process lifetime during development (stop the stdio server so the next install/run is clean).

`destructiveHint: true`, `readOnlyHint: false`. Delay 0–10 seconds so the MCP response can flush.

---

## 10. Errors

Unknown tool / bad URI: JSON-RPC `-32602`. OData 4xx/5xx: tool `isError` with status + trimmed OData `error`. Metadata failure: CLI fails start; AspNetCore fails catalog build loudly. Protocol mismatch: SDK.

Log stacks server-side. Do not send demystified traces to agents by default.

---

## 11. Out of phase 1

MRTR confirm-on-delete, Tasks, MCP Apps, `subscriptions/listen` on HTTP.

---

## 12. Checklist

- [ ] SDK 2.x pinned
- [ ] `odata://` resources + templates + completions
- [ ] Generic tools always; named tools capped
- [ ] `ttlMs` / `cacheScope` / deterministic order
- [ ] CSDL docs → descriptions; verb → `ToolAnnotations`
- [ ] `structuredContent` + `outputSchema`
- [ ] Tools: `shutdown_server`; AspNetCore: absent
- [ ] No custom JSON-RPC
