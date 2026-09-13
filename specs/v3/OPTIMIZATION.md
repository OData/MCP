# Optimization

**Status:** Living  
**Revised:** 2026-09-09  
**Protocol:** MCP `2026-07-28` / **ModelContextProtocol C# SDK 2.x**  
**Companion:** [TYPE-SHAPES.md](./TYPE-SHAPES.md)  
**Plan:** [OPTIMIZATION-PLAN.md](./OPTIMIZATION-PLAN.md)

---

## Goals

1. **The calling AI must understand the service without the EDMX.** `$metadata` is the full CSDL document. This product exists so the model gets a context-sized map — types, required fields, enums, operations — instead of that XML.
2. **The end user must not wait while the AI retries a wrong call.** Instructions, descriptions, schemas, and fail-before-HTTP exist so the first `tools/call` is the right one.

Token cuts that make the model less able to use the service are a regression.

---

## How

| Lever | Rule |
|---|---|
| `ServerInstructions` | Same default on both hosts. Non-obvious rules only. Developer may **prepend**, never replace. |
| Tool descriptions | Short, first-call. Schema carries structure; description carries the one rule the schema cannot say. |
| Typed `inputSchema` | JSON Schema wherever the EDM knows the shape. No opaque `body` string when we have properties. |
| EdmType validation | Fail **before** OData HTTP, with the declared signature in the error. |
| Enums | Members always in the shape. JSON wire: string members by default. |
| Type cache | Fill-once for static models. |
| Breakdance baselines | `Before/` + `Current/` payload files. Tokenizer report after a round, not in this file. |

---

## Non-goals

| Non-goal | Why |
|---|---|
| `$metadata` as the exploration surface | CSDL stays the escape hatch. |
| Replacing default instructions with developer copy | Preface only. |
| Dual-emitting text + JSON in one tool result | Many harnesses dump both. |
| Changing advertised schemas mid-session | No `listChanged` for enum wire observation. |
| Guessing store-generated keys from Int32/Guid | Only `Core.Computed` / `Core.ComputedDefaultValue`. |
| CLI `--instructions-preface` in this work | Options API is enough. |
| Named `list_{set}_{nav}` | Never registered. Navigation is `odata_navigate` plus navs on the type. |

---

## 1. `initialize.instructions`

`ODataMcpInstructions.Default` — one string, both hosts. `McpServerOptions.ServerInstructions`.

```
Tool parameter names omit $ (filter, not $filter); inside expand options write $top, $select as usual. Prefer named tools when listed; otherwise odata_describe_type then generic odata_*. odata_describe_model summary is the map; complete dumps every type in one call. Bound operations are listed on the type; unbound on odata_list_operations. odata_call: pass arguments in parameters (a JSON object) using the declared parameter names — do not stringify, do not wrap, do not guess a body. Do not read the $metadata resource to explore; it is there when the user asks for raw CSDL.
```

Why this wording (from two independent no-context model reviews of the surface):

- "Query options have no $ prefix" over-generalized: a model reading it literally would strip `$` inside `expand=Orders($top=5)`. The rule is about tool parameter names only.
- "Do not read $metadata" read as a ban. The CSDL resource exists for the user who asks for raw EDMX; the model must not use it to *explore*.
- The PATCH rule is not here because it lives on `odata_update` (and `update_*`), which are always in context too. Each rule appears once.

`ODataMcpCatalogOptions.InstructionsPreface` (`string?`, default empty):

```
ServerInstructions = preface is whitespace ? Default : $"{preface.Trim()}\n\n{Default}"
```

AspNetCore: set at `AddODataMcp` from catalog options. Tools: holder registered **before** `Build()`, same pattern as `ToolsMcpSessionHolder` — do not assign `ServerInstructions` after `Build()`.

Ship this string **after** `odata_describe_model` is registered. Tests: initialize contains Default; preface appears first; empty preface → Default only.

---

## 2. Tool descriptions

Normative copy. CSDL summary is appended on named tools via `ComposeToolDescription` when present.

| Tool | Description |
|---|---|
| `odata_list_entity_sets` | `Names, types, and keys of declared entity sets.` |
| `odata_describe_type` | `Declared properties, keys, navigations, bound operations, and enums for a type or set. format=text (default) or json. No ? = required on create; Name?: = optional; -> = navigation; [] = collection. // key, store-generated = omit on create; plain // key = send it unless the service generates it.` |
| `odata_describe_model` | `summary (default): sets, keys, navigations, then unbound operations. complete: every type with properties, enums, and bound operations, then unbound operations. Prefer complete instead of calling odata_describe_type once per type. Pass sets to scope a large model. format=text (default), json, or mermaid. Do not read $metadata to explore.` |
| `odata_query` | Keep: parameter names have no `$`; executor adds them. |
| `odata_get` | `Gets an entity by key. Optional select and expand shape the result.` Named: `Gets a {Type} by key.` + CSDL summary if any. |
| `odata_delete` | `Deletes an entity by key.` Named: `Deletes a {Type}.` + CSDL summary if any. |
| `odata_navigate` | `Follows a navigation property from a key. Accepts the same query options as odata_query.` |
| `odata_create` | `JSON object in body. Include every property the type lists as required on create (odata_describe_type). Client-assigned keys are required; omit store-generated keys.` |
| `odata_update` | `PATCH object in body. Send only fields to change. Omitted fields keep their values. JSON null clears an optional property; never send it for a required one.` |
| `list_{x}` | Set sentence + CSDL summary if any + `Query parameter names do not include $.` When the type exposes enum properties, append `Filter enums as NS.Enum'{value}', ...` (one pattern per distinct enum, first-use order; same list as the type shape header, [TYPE-SHAPES.md](./TYPE-SHAPES.md) §1.1). A model told to prefer named tools never calls `odata_describe_type`, so the hint has to travel with the list tool. |
| `create_{x}` | `Creates a {Type}.` + CSDL summary if any. Schema `required` is the create signal. |
| `update_{x}` | `PATCH a {Type}. Send only fields to change; omit to keep. Do not send JSON null for required properties.` + CSDL summary if any. |
| `odata_list_operations` | `Unbound operations on the service.` |
| `odata_call` | See §3. |

Do not invent `"description": "CompanyName"`. Omit CSDL docs when absent ([TYPE-SHAPES.md](./TYPE-SHAPES.md) §4).

Each rule lives in exactly one place. The bound/unbound routing sentence is in the instructions and on `odata_call`; the PATCH null rule is on `odata_update` / `update_*`; neither is repeated on `odata_describe_type` or `odata_list_operations`.

---

## 3. Schemas and `odata_call`

### inputSchema / outputSchema

| Tool | inputSchema | outputSchema |
|---|---|---|
| Named `create_*` | Declared properties. `"required"` = required-on-create. Enum `enum` array. `description` only when CSDL has it. `maxLength` only when ≤ 16. | Omit (OData body). |
| Named `update_*` | `key` + same property map. `"required": ["key"]` only. Non-nullable types do not include `null`. | Omit. |
| `odata_create` / `odata_update` | `entitySet` + `body` **object** (and `key` on update). `body` is `{"type":"object","description":"... as a JSON object, not a string."}`; the runtime still accepts a string, but the schema must not contradict "do not stringify" on `odata_call`. | Omit. |
| `odata_get` / named `get_*` | `entitySet` (generic) + `key` + optional `select`, `expand`. | Omit. |
| `odata_navigate` | `entitySet`, `key`, `navigation` + the full `odata_query` option set (`filter`, `select`, `orderby`, `expand`, `top`, `skip`, `count`). The runtime always honored them; the schema now says so. | Omit. |
| `odata_call` | `{ name, parameters (object), entitySet, key }`, required `name`. `parameters.description` ends `Omit when the operation takes none.` **No `body`.** **No undeclared top-level args.** | Omit. |
| `odata_query` / named `list_*` | Today’s query object (no `$`). | Omit. |

Every `key` argument carries a description that states the literal form, because no model can infer it from `"type":"string"`:

| Where | `key.description` |
|---|---|
| Generic `odata_get`, `odata_update`, `odata_delete`, `odata_navigate`, `odata_call` (`ODataMcpCatalog.KeySchema`) | `Key as text: ALFKI or 10248; strings are quoted for you. Composite: OrderID=10248,ProductID=11.` |
| Named `get_*`, single key (`BuildNamedKeySchema`) | `{KeyName} as text; strings are quoted for you.` |
| Named `get_*`, composite key | `Name=value pairs: OrderID=...,ProductID=...; strings are quoted for you.` |
| Named `get_*`, type without key | Falls back to `KeySchema`. |

The named form is shorter than the generic one because it repeats once per entity set, and it names the key properties the model must use. The runtime contract behind both is `FormatKey`: integers, GUIDs, and booleans pass through bare; other scalars are single-quoted (with `'` doubled) unless already quoted; `Name=value` pairs are formatted per value.
| `odata_describe_type` | `name` required; `format`: `text` \| `json`. | Omit (text vs json). |
| `odata_describe_model` | `detail`, `format`, `sets`. | Omit (text vs json vs mermaid). |
| `odata_list_entity_sets` | `{}` `additionalProperties: false`. | Yes — `{ entitySets: [...] }`. |
| `odata_list_operations` | `{}` `additionalProperties: false`. | Yes — `{ operations: [...] }` or empty object. |

`ODataToolDescriptor.OutputSchema` (`string?`). `SchemaSerializerOptions`: `WhenWritingNull`. Do not insert null keys.

### `odata_call`

```json
{
  "name": "ShareTrip",
  "parameters": { "userName": "scottketchum", "tripId": 0 },
  "entitySet": "People",
  "key": "scottketchum"
}
```

Unbound: `name` + `parameters`. Collection-bound: `name` + `parameters` + `entitySet`, no `key`. Instance-bound: those plus `key`.

`parameters` is a JSON **object**. Keys = listing names, same spelling, no fuzzy match. Delete `body`. Delete extra top-level argument scraping. Runtime maps `parameters` onto the URL (function) or POST body (action). `readOnlyHint` stays off.

Description:

> Call a declared operation by `name` as listed on `odata_describe_type` (bound) or `odata_list_operations` (unbound). Put every argument in `parameters` using the declared parameter names; do not stringify; do not add extra top-level fields; do not wrap under the operation name. Instance-bound (the default for bound): also `entitySet` and `key`. Collection-bound (`// collection` on the listing): `entitySet` only. Unbound: neither. The server sends GET or POST as declared — do not pass a method. `// writes` means it mutates. If arguments are wrong the tool errors with the signature; do not guess a different payload shape.

"the declared parameter names" replaced "those names", whose antecedent was ambiguous. "(the default for bound)" is there because instance binding was otherwise only inferable from the *absence* of `// collection`.

---

## 4. Validate against EdmType, fail before HTTP

Before any OData HTTP on create, update, and `odata_call`:

1. Resolve type / operation from the catalog EDM.
2. Required-on-create / required parameters / bound vs collection vs unbound (`entitySet` / `key`).
3. JSON kind vs EdmType:
   - `Edm.Boolean` → JSON boolean
   - `Edm.Byte` / `SByte` / `Int16` / `Int32` → JSON number
   - `Edm.Int64` / `Edm.Decimal` → JSON number **or** JSON string (IEEE754Compatible services)
   - `Edm.Double` / `Single` → JSON number
   - strings, guid, date, datetime, duration, time, enums-as-string → JSON string
4. Enum membership (§5).
5. `MaxLength` when the EDM has it, including lengths > 16.
6. Open types: extra properties allowed; declared properties still validated.
7. Failure: `isError`, text includes the declared signature or `required` list. **Do not** send OData.

Do not validate `$filter` AST. Do enforce existing `filter` / `select` / `expand` length caps.

`ODataMcpCatalogOptions.EnforceRequiredOnCreate` (default `true`) switches off step 2's required-on-create check **only**, for a service whose metadata declares non-nullable properties that its POST handler defaults or rejects. Live TripPin is the known case: `Person.Gender` and `FavoriteFeature` are non-nullable with no default, yet POST `People` returns 500 when either is sent and 201 with server defaults when both are omitted. The live TripPin test fixture sets the option to `false`; every other check stays on. Undeclared entity sets and bodies that are not JSON objects are forwarded untouched so the service answers.

Function arguments that are complex, collection, or entity typed travel as OData parameter aliases in the request path (`Find(at=@at)?@at=<escaped json>`); aliases are part of the operation call and are never `$`-prefixed query options. The executors always emit `$` on system query options regardless of the server's `EnableNoDollarQueryOptions`; see [TOOL-SURFACE.md](./TOOL-SURFACE.md) §6.1 for the measured behavior behind that rule.

| `odata_call` mistake | Error |
|---|---|
| Unknown `name` | `Operation 'X' is not declared.` |
| Bound instance, missing `entitySet` or `key` | `{Name} is bound to {Type}. Pass entitySet and key. Signature: {signature}` |
| Collection-bound, missing `entitySet` or extra `key` | `{Name} is collection-bound. Pass entitySet; omit key.` |
| Unbound with `entitySet`/`key` | `{Name} is unbound. Omit entitySet and key.` |
| Missing required parameter | `Missing parameter '{p}'. Signature: {signature}` |
| Unknown parameter key (including the binding parameter) | `Unknown parameter '{p}'. Declared: {list}.` |
| `parameters` is a string | `parameters must be a JSON object, not a string.` |

---

## 5. Enums

Parse `EnumType` into `EdmEnumType` / `EdmEnumMember` (`Name`, `Namespace`, `UnderlyingType`, `IsFlags`, `Members[{Name, Value}]`, docs). `EdmModel.EnumTypes`. `CsdlParser` and `EdmModelAdapter` fill them. Zero members → fail-first at parse.

EDM underlying type is always integer. That is not the JSON wire.

`ODataMcpCatalogOptions.EnumJsonFormat`:

| Value | Advertised schema | Validation |
|---|---|---|
| `String` | `"enum": ["Red","Green"]` | Member name or `NS.Color'Red'`. |
| `Integer` | `"enum": [0,1,2]` plus names in `description` | JSON number or numeric string. |
| `Auto` (**default**) | Same as `String` for the **catalog lifetime** | Member name, `NS.Color'Red'`, **or** JSON number. |

Do **not** rewrite `inputSchema` or fire `listChanged` because a GET returned a number. Observation must not change what `tools/list` already advertised.

`$filter` literals stay `NS.Color'Red'`. Describe prints that form once per shape. `IsFlags`: `// flags, comma-separated`.

---

## 6. Type cache

Fill-once, catalog lifetime, drop on rebuild. Eager at catalog build.

- EDM shapes (declaration, compact JSON, create schema, bound ops, required-on-create names): `ConcurrentDictionary<string, EdmTypeShape>` keyed by EDM full name.
- CLR `Type` maps (primitives, serializer): `Ben.Collections.TypeDictionary<T>`. PackageReference `Ben.TypeDictionary`. If AOT/trim warns, vendor the sources (Apache-2.0) into this repo and mark AOT. Do not invent a dummy `Type` for EDM names. **Not needed yet:** the first implementation keeps no CLR `Type` map, so the package is not referenced.

`ODataMcpCatalogOptions.IsDynamicModel` default `false`. When `true`, skip the EDM shape cache. Open extra properties are not cached; declared members still are when the model is static.

`EdmProperty.Computed`: `true` when CSDL/IEdmModel has `Org.OData.Core.V1.Computed` or `Core.ComputedDefaultValue`. Parser and adapter must set it. Store-generated = `Computed`. Do **not** infer Computed from Int32/Guid keys.

Do not cache forwarded OData HTTP.

---

## 7. Breakdance baselines

`src/Microsoft.OData.Mcp.Tests.Core/Baselines/TypeShapes/`

| Folder | Role |
|---|---|
| `Before/` | Snapshot of payloads **before** this work. Write-once (`File.Exists` guard). |
| `Current/` | What tests assert. Regenerated after each intended change. |

`[BreakdanceManifestGenerator]` writes files. Tests `result.Should().Be(File.ReadAllText(baseline))`. `projectPath = "..//..//..//"`. Live Northwind, live TripPin, convention-rich host. No mocks.

When a round is done, tokenize Before vs Current with SharpToken `cl100k_base` into `OPTIMIZATION-REPORT.md` (append dated sections later). That file is a report, not a merge gate for the first baseline PR.

The tokenizer lives in two places with two jobs:

- **Guard** — `TokenBaselineTests` (Tests.Core) asserts that no paired **tool result** costs more `cl100k_base` tokens in `Current/` than in `Before/`. Input schemas (typed on purpose), the `tools.list` aggregate (the Before snapshot never captured descriptions or instructions), and the `describe_type` text stubs (the old text was the bare type name) are excluded. `Baselines/TypeShapes/README.md` records where every `Before/` file came from.
- **Reports** — `src/Microsoft.OData.Mcp.Benchmarks` (`dotnet run -c Release -- --tokens`) links the same `Before/` and `Current/` folders into its output and writes `Reports/TOKENS.md` (every baseline, paired, plus the `tools.list` section breakdown) and `Reports/FORMATS.md` (the same model as CSDL XML, CSDL JSON, and our shapes, per type and whole service, with every counted artifact under `Reports/Formats/`). The same project carries the BenchmarkDotNet compute suites: CSDL parse, catalog build, describe/list tools, and the pre-HTTP write validation.

`Before/` counterparts for payloads that did not exist before the optimization (`describe_model`, the text shapes, `update_*` schemas, TripPin `list_entity_sets`) were produced by running the `288ccd6` build against the live services. The `describe_model` Before payload is what a model had to fetch to get the same map: `odata_list_entity_sets` plus `odata_describe_type` for every set.

---

## API

```csharp
public sealed class ODataMcpCatalogOptions
{
    public bool EnforceRequiredOnCreate { get; set; } = true;
    public string? InstructionsPreface { get; set; }
    public bool IsDynamicModel { get; set; }
    public ODataEnumJsonFormat EnumJsonFormat { get; set; } = ODataEnumJsonFormat.Auto;
}

public enum ODataEnumJsonFormat
{
    Auto,
    String,
    Integer
}

public static class ODataMcpInstructions
{
    public const string Default = """…§1 block…""";

    public static string Compose(string? preface) { /* whitespace preface → Default; else preface + blank line + Default */ }
}
```

Both hosts call `Compose`. Tests assert `Default`, not copies in Tools and AspNetCore.
