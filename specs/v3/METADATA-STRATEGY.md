# Metadata Strategy

**Lock:** Core ships a **custom EDM** in `Microsoft.OData.Mcp.Core.Models` and a **self-contained CSDL parser**. Official EdmLib is not a Core dependency. AspNetCore **adapts** the app’s `IEdmModel` into **those same types** — do not invent a parallel `Edm/` folder or `IOdataSchema`.

The types may be edited in place. They must stay **isomorphic with `IEdmModel`**: if a property, navigation, set, or operation is not on the EDM, it does not exist for MCP.

---

## 1. What tools and resources need

| Capability | Required |
|------------|----------|
| Entity sets, types, keys, nullability, primitives | Yes |
| Navigation properties + targets | Yes |
| Singletons | Yes |
| Functions / actions + parameters | Yes (parse now; tools in a later phase may follow) |
| Documentation / vocabulary annotations | Yes — this is how agents pick the right tool |
| Full OData URI AST / payload stack | **No** |
| `Microsoft.OData.Core` | **No** |

---

## 2. Dual source, one projection

```
                    Core EdmModel
                          ▲
              ┌───────────┴───────────┐
              │                       │
     CsdlParser ($metadata)    IEdmModelEdmAdapter
     Tools host                AspNetCore package only
```

Catalogs, tools, resources, JSON schemas, completions, and `structuredContent` field lists bind **only** to `Core.Models`. They never reflect over CLR entity classes.

### Declared surface (IEdmModel rules)

`IEdmModel` is the semantic contract even though Core does not reference EdmLib.

| If this is true on the EDM | MCP must |
|----------------------------|----------|
| Structural property not declared (`Ignore()`, not in CSDL, not on `DeclaredStructuralProperties`) | Omit from type cards, tool `inputSchema`/`outputSchema`, examples, completions, serialized payloads we generate |
| Navigation not declared | Same — omit |
| Entity set / singleton / operation not in the container | Do not list as a resource or named tool |
| Property is `Edm.Binary` / stream | Omit from default select schemas (already a product rule) **in addition to** declared-surface |

The adapter copies **declared** members only. The parser copies **CSDL-declared** members only. Never merge extra CLR properties back in.

A property that exists on the C# class but was excluded from the OData model **must not** appear anywhere in MCP output.

### Remote (Tools)

`GET {base}/$metadata` → streaming XML → Core model. No `XDocument` as the production parser if a `XmlReader` path is faster. Characterization tests against frozen Northwind and TripPin CSDL fixtures **and** live services.

### Local (AspNetCore)

The app already has `Microsoft.OData.Edm.IEdmModel` on each route component. **Do not re-parse `$metadata`.** Adapter lives in `Microsoft.OData.Mcp.AspNetCore` and maps sets/types/keys/navs/annotations into Core. `Microsoft.OData.Edm` is referenced **only** there.

---

## 3. Documentation annotations (in scope)

Read and surface, in this order:

1. CSDL `<Documentation><Summary>` / `<LongDescription>`
2. `Core.Description` / `Core.LongDescription` vocabulary annotations
3. Structural fallback: “Products entity set of type Northwind.Product; key ProductID.”

Write those strings onto:

- Resource `title` / `description`
- Named tool `title` / `description` / parameter docs
- Generic tool parameter descriptions for `entitySet` (enum-like list is **not** required; completions on templates cover discovery)

DotNetDocs enrichment (XmlDoc → EDM) is a **separate product**. When that plugin has already stamped annotations on `IEdmModel`, the adapter must pass them through. This repo does not scrape DotNetDocs.

---

## 4. Parser quality bar (rewrite allowed)

- Entity types, complex types, containers, sets, singletons, properties, navs, bindings, referential constraints
- Schema-level functions and actions **and** imports (today’s parser leaves `EdmFunction` / `EdmAction` empty — that gap must close)
- Documentation and `Core.Description`
- Ignore unknown vocabulary that we do not consume
- No full ODL validation graph
- Cache the model for the process; optional ETag refresh later

---

## 5. Package matrix

| Package | Microsoft.OData.Edm | Microsoft.OData.Core | Microsoft.AspNetCore.OData |
|---------|---------------------|----------------------|----------------------------|
| Core | No | No | No |
| Tools | No | No | No |
| AspNetCore | Yes (adapter) | Only if the OData peer forces it | Peer |
| Sample | Via OData stack | Via stack | Yes |

---

## 6. What this replaces in the first v3 draft

| First draft | Now |
|-------------|-----|
| Invent `IOdataSchema` | Core model is the projection |
| Delete custom EDM *or* keep it (contradiction) | Keep `Core/Models`; evolve in place |
| ODL benchmark gate as a live decision | Not a roadmap item |
| Docs as a future DotNetDocs-only story | Parse CSDL docs **now**; DotNetDocs still consume-only |
