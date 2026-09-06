# Tool Surface

Agents succeed or fail on the catalog. One builder. Two tool families. The graph itself is resources ([PROTOCOL.md](./PROTOCOL.md)).

---

## 1. Goals

1. Predictable names.
2. Bounded context: 200 entity sets must not emit 1,000 tools.
3. Self-describing: CSDL docs in `title`/`description`; no `$` on query params.
4. Every listed tool executes real OData HTTP (except `shutdown_server`, which executes host lifetime).
5. Single pipeline.

---

## 2. Generic tools (always)

| Name | Title | Verb hints | Purpose |
|------|-------|------------|---------|
| `odata_list_entity_sets` | List entity sets | readOnly | Names, types, keys from the model |
| `odata_describe_type` | Describe type | readOnly | Properties, navs, keys, docs |
| `odata_query` | Query entity set | readOnly | `filter`, `select`, `orderby`, `expand`, `top`, `skip`, `count` |
| `odata_get` | Get entity | readOnly | By key |
| `odata_create` | Create entity | | POST JSON |
| `odata_update` | Update entity | idempotent | PATCH JSON |
| `odata_delete` | Delete entity | destructive, idempotent | DELETE by key |
| `odata_navigate` | Navigate | readOnly | Follow a nav from a key |

Phase 2+: `odata_call_function`, `odata_call_action`.

Prefer a resource URI or `entitySet` name as the target. Executor resolves `odata://…` or a bare set name.

---

## 3. Named tools (capped)

Per entity set, snake_case:

| Operation | Pattern | Example |
|-----------|---------|---------|
| List/query | `list_{set}` | `list_products` |
| Get | `get_{singular}` | `get_product` |
| Create | `create_{singular}` | `create_product` |
| Update | `update_{singular}` | `update_product` |
| Delete | `delete_{singular}` | `delete_product` |
| Navigate | `list_{set}_{nav}` | `list_products_category` |

Default `MaxNamedTools` = 150 including generics. Fill `IncludeEntitySets` first, then remaining sets alphabetically. Never emit a partial CRUD family for a set (if `list_products` is in, `get_product` is in for the enabled verbs).

Sets that do not get named tools remain fully usable via resources + generic tools.

---

## 4. Forbidden

| Pattern | Why |
|---------|-----|
| Two pipelines (`WithODataTools` + factory) | Historical poison |
| Full generic CRUD **and** named CRUD for every set | Same poison |
| Multiple naming dialects | Models thrash |
| Metadata-only tools | Legacy `McpTool` |
| `queryCustomers` / `Customer.query` | Dead names |
| `shutdown_server` on AspNetCore | Local Tools only |

---

## 5. Local Tools: `shutdown_server`

Always registered by `odata-mcp`.

| | |
|--|--|
| Name | `shutdown_server` |
| Title | Shut down MCP server |
| Params | `reason` (string, optional), `delay_seconds` (int, 0–10, default 2) |
| Hints | `readOnlyHint: false`, `destructiveHint: true`, `idempotentHint: false` |
| Behavior | Return a JSON ack, then cancel the host after the delay so the MCP response flushes |

This is a development control-plane tool. It is part of the local server spec.

---

## 6. Query parameters

No `$` in tool args. Executor adds `$filter`, `$select`, `$orderby`, `$expand`, `$top`, `$skip`, `$count`.

Keys: simple `key` string/number; composite as a structured object on named get/delete.

Bodies: JSON object from **declared** model properties when named; JSON string acceptable on generic create/update. Never emit a property that is not on the EDM (OData `Ignore()`, absent from CSDL). Also exclude binary/stream properties from generated schemas by default.

---

## 7. Descriptions

1. CSDL / vocabulary documentation  
2. Structural fallback  
3. One short example  
4. Reminder: no `$` prefixes  

Keep them tight.

---

## 8. Execution

```
tools/call → catalog handler → IOdataExecutor → structuredContent + short text
```

Honor `CancellationToken`. Size-guard responses; suggest `$select` / `$top` when over limit.

---

## 9. Multi-route

One MCP endpoint per OData prefix. Tool names need no route prefix. CLI uses a single `remote` route.

---

## 10. Server instructions (required copy)

> Prefer entity-specific tools (`list_*`, `get_*`) when they appear in the tool list. If the set is not listed, read `odata://…/{entitySet}` and use generic `odata_*` tools. Query parameter names do not include `$`.
