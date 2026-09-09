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
Query options have no $ prefix. Prefer named tools when listed; otherwise odata_describe_type then generic odata_*. odata_describe_model summary is the map; complete dumps every type in one call. Bound operations are listed on the type; unbound on odata_list_operations. odata_call: pass arguments in parameters (a JSON object) using those names — do not stringify, do not wrap, do not guess a body. Do not read $metadata to explore. PATCH: omit a field to keep it; never send JSON null for a required property.
```

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
| `odata_describe_type` | `Declared properties, keys, navigations, bound operations, and enums for a type or set. format=text (default) or json. Bound operations are here; unbound are on odata_list_operations. No ? = required on create; Name?: = optional. PATCH may omit any field; JSON null is invalid for required fields.` |
| `odata_describe_model` | `summary (default): sets, keys, navigations, then unbound operations. complete: every type with properties, enums, and bound operations, then unbound operations. Prefer complete instead of calling odata_describe_type once per type. Pass sets to scope a large model. format=text (default), json, or mermaid. Do not read $metadata to explore.` |
| `odata_query` | Keep: parameter names have no `$`; executor adds them. |
| `odata_get` | `Gets an entity by key.` Named: `Gets a {Type} by key.` + CSDL summary if any. |
| `odata_delete` | `Deletes an entity by key.` Named: `Deletes a {Type}.` + CSDL summary if any. |
| `odata_navigate` | `Follows a navigation property from a key.` |
| `odata_create` | `JSON body in body. Include every property the type lists as required on create (odata_describe_type). Client-assigned keys are required; omit store-generated keys.` |
| `odata_update` | `PATCH in body. Send only fields to change. Omitted fields keep their values. Do not send JSON null for required properties.` |
| `create_{x}` | `Creates a {Type}.` + CSDL summary if any. Schema `required` is the create signal. |
| `update_{x}` | `PATCH a {Type}. Send only fields to change; omit to keep. Do not send JSON null for required properties.` + CSDL summary if any. |
| `odata_list_operations` | `Unbound operations on the service. Bound operations are on odata_describe_type.` |
| `odata_call` | See §3. |

Do not invent `"description": "CompanyName"`. Omit CSDL docs when absent ([TYPE-SHAPES.md](./TYPE-SHAPES.md) §4).

---

## 3. Schemas and `odata_call`

### inputSchema / outputSchema

| Tool | inputSchema | outputSchema |
|---|---|---|
| Named `create_*` | Declared properties. `"required"` = required-on-create. Enum `enum` array. `description` only when CSDL has it. `maxLength` only when ≤ 16. | Omit (OData body). |
| Named `update_*` | `key` + same property map. `"required": ["key"]` only. Non-nullable types do not include `null`. | Omit. |
| `odata_create` / `odata_update` | `entitySet` + `body` string (and `key` on update). | Omit. |
| `odata_call` | `{ name, parameters (object), entitySet, key }`, required `name`. **No `body`.** **No undeclared top-level args.** | Omit. |
| `odata_query` / named `list_*` | Today’s query object (no `$`). | Omit. |
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

> Call a declared operation by `name` as listed on `odata_describe_type` (bound) or `odata_list_operations` (unbound). Put every argument in `parameters` using those names; do not stringify; do not add extra top-level fields; do not wrap under the operation name. Instance-bound: also `entitySet` and `key`. Collection-bound (`// collection` on the listing): `entitySet` only. Unbound: neither. The server sends GET or POST as declared — do not pass a method. `// writes` means it mutates. If arguments are wrong the tool errors with the signature; do not guess a different payload shape.

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

Function arguments that are complex, collection, or entity typed travel as OData parameter aliases in the request path (`Find(at=@at)?@at=<escaped json>`); aliases are part of the operation call and are never `$`-prefixed query options.

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
