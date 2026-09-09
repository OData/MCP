# Type Shapes

**Status:** Living (authoritative for how MCP describes OData types)  
**Revised:** 2026-09-09  
**Protocol:** MCP `2026-07-28` via **ModelContextProtocol C# SDK 2.x**  
**Companion:** [OPTIMIZATION.md](./OPTIMIZATION.md)

This is the context-friendly stand-in for EDMX. The calling AI learns the service from these shapes, not from `$metadata`. Deferred tool loading is the default. Generics are eager. Named tools are searchable. Compact every payload that enters context. Semantic constraints (keys, required-on-create, enums, literals) are in the shape. Storage constraints (length, precision, unicode, SRID) are not, except `MaxLength` when it is ≤ 16.

---

## 1. Grammar

Declaration style is the default the model reads. It is TypeScript/C#-like on purpose.

### 1.1 One type

```
Customer  (set: Customers, key: CustomerID)
  // A customer of the store.                         ← type Description, only if CSDL has it
  CustomerID: string(5) // key
  CompanyName: string
  ContactName?: string
  Orders -> Order[]
  CustomerDemographics -> CustomerDemographic[]
  operations
    MostExpensive() -> Product // collection
    Discount(percentage: int) // writes
```

Required-on-create is carried by the absence of `?` alone; there is no `// required on create` comment. One space precedes every `//`; nothing is column-aligned.

Rules:

| Line | Form | Meaning |
|---|---|---|
| Header | `Type  (set: Set, key: K1, K2)` | Set omitted when describing a type with no set. Namespace on the header **only** when the model has more than one. |
| Property, required on create | `Name: type` | Must appear on POST. |
| Property, optional on create | `Name?: type` | Nullable, or has `DefaultValue`, or store-generated. |
| Key | trailing `// key` or `// key, store-generated` | Store-generated keys are optional on create. |
| Navigation | `Name -> Type` or `Name -> Type[]` | `[]` is collection. Single-valued optional nav keeps the arrow: `Name? -> Type` (the arrow is what tells `odata_navigate` targets apart from complex-typed properties). No `1` / `0..1`. |
| Short string | `string(n)` | Only when `MaxLength` is present **and** `n ≤ 16`. |
| Enum | `Name: enum(Red\|Green\|Blue)` | Members always. `// flags, comma-separated` when `IsFlags`. |
| Operation | `Name(arg: type, arg?: type) -> Return` | Bound ops only, on the type. Binding parameter omitted. `// collection` when bound to `Collection(...)`. `// writes` when it mutates (EDM action). |
| Docs | `// summary` | Only when CSDL/vocabulary has it **and** it is not the member name. Second `//` line only when LongDescription differs from Description. When a line has both markers and docs they share one comment: markers joined with `, `, then `; `, then the summary (`UserName: string // key; Unique person name.`). Type-level docs are one `//` line each for type Description, type LongDescription, set Description, set LongDescription, deduplicated. |

Do **not** emit empty `operations` / docs keys. Do **not** fall back to the property name as a description.

One-line legend is **not** in the result. The tool description carries required-on-create vs PATCH ([OPTIMIZATION.md](./OPTIMIZATION.md) §2). Some harnesses strip descriptions; JSON Schema `required` on `create_*` is still the primary create signal.

Enum filter literal, once per shape that contains an enum, as a header comment:

```
  // enum literal: NorthwindModel.Color'Red'
```

### 1.2 Compact JSON

Same facts, for `resources/read` type cards and `format=json`:

```json
{
  "Customer": {
    "set": "Customers",
    "key": ["CustomerID"],
    "description": "A customer of the store.",
    "props": {
      "CustomerID": "string(5)!",
      "CompanyName": "string!",
      "ContactName": "string"
    },
    "docs": { "CompanyName": "Legal registered name." },
    "navs": { "Orders": "Order[]", "CustomerDemographics": "CustomerDemographic[]" },
    "ops": {
      "MostExpensive": "() -> Product // collection",
      "Discount": "(percentage: int) // writes"
    }
  }
}
```

`!` mirrors required-on-create. It is **not** a substitute for JSON Schema `required` on `create_*`. `docs` / `description` / `longDescription` / `setDescription` / `enumLiteral` / `navs` / `ops` are omitted when empty. `setDescription` carries the entity set's own description when it differs from the type's. Flags enumerations render as `flags(A|B)` so the marker survives without a `docs` entry. `enumLiteral` is the once-per-shape filter literal (`NS.Color'Red'`). Do not turn `"CompanyName":"string!"` into an object because it is documented.

### 1.3 Type mapping

| EDM | Shape | Literal (once in the header if used) |
|---|---|---|
| `Edm.String` | `string` / `string(n)` | |
| `Edm.Boolean` | `bool` | |
| `Edm.Byte`, `SByte`, `Int16`, `Int32` | `int` | |
| `Edm.Int64` | `int64` | |
| `Edm.Decimal`, `Double`, `Single` | `number` | |
| `Edm.Date` | `date` | |
| `Edm.DateTimeOffset` | `datetime` | `datetimeoffset'…'` |
| `Edm.TimeOfDay` | `time` | |
| `Edm.Duration` | `duration` | `duration'P1D'` |
| `Edm.Guid` | `guid` | `guid'…'` |
| `Edm.Binary`, `Edm.Stream` | **omit** (already `IsExposedProperty`) | |
| Enum | `enum(A\|B\|C)` | `NS.Enum'Member'` |
| Complex / entity | short type name | |
| `Collection(T)` | `T[]` | |

Drop `Edm.` and the namespace on property types unless two types would collide.

---

## 2. Facets

**Semantic** (always in the shape): key, required-on-create, enum members, type family, collection-ness, literal form.

**Storage** (almost never): `Precision`, `Scale`, `Unicode`, `SRID`. Stay in CSDL. `DefaultValue` is not printed; it only flips the property to `Name?:`.

`MaxLength` is in the shape **only when ≤ 16**, rendered `string(n)`. Same rule on `create_*` / `update_*` JSON Schema (`maxLength`). Validation still enforces any `MaxLength` the EDM has ([OPTIMIZATION.md](./OPTIMIZATION.md) §4).

---

## 3. Required on create vs PATCH

Non-nullable in CSDL means the **stored** entity has a value. It does not mean every HTTP payload lists it.

**Required-on-create** =

`!property.Nullable && string.IsNullOrWhiteSpace(property.DefaultValue) && !property.Computed`

`EdmProperty.Computed` is `true` only when the EDM has `Org.OData.Core.V1.Computed` or `Core.ComputedDefaultValue`. Parser and adapter set it. Do **not** infer Computed from Int32 or Guid keys. Northwind `CustomerID` (string, not Computed) → required. TripPin `UserName` → required. A Computed integer identity → optional on create, marked `// key, store-generated`.

| Operation | Server | Shape | JSON Schema |
|---|---|---|---|
| POST | Must include every required-on-create property. Client-assigned keys required. Store-generated keys omitted. | `Name: type` (no `?`) | those names in `"required"` |
| PATCH | Omitted fields keep their values. Sent non-nullable fields must not be JSON `null`. | same `?` as create | `"required": ["key"]` only; non-nullable types do not include `null` |
| Nullable or default | Optional on create | `Name?: type` | omit from `"required"` |

The calling model will not infer PATCH-omit from `?` / `!`. Channels that must say it in English: create/update/describe **tool descriptions**, and Schema `required` on create. See [OPTIMIZATION.md](./OPTIMIZATION.md) §2.

---

## 4. Documentation

XmlDoc `<summary>` / `<remarks>` = CSDL `Documentation/Summary` + `Core.Description` / `LongDescription`. Useful. Northwind is empty because Northwind has no docs.

| When | Emit |
|---|---|
| Non-whitespace Description, not equal to the member name | `description` / trailing `//` |
| LongDescription non-whitespace **and** different from Description | `longDescription` / second `//` |
| Otherwise | **Do not put the key in the dictionary** |

Drop `entityTypeDescription`. Do not invent `"description": "CompanyName"`. Start with required keys; `EdmDocumentation.Add(...)` only when the value carries information. `WhenWritingNull` is defense in depth, not a substitute.

Do not cap individual strings. `MaxResponseBytes` on `complete` still fail-first.

Structural fallbacks (`Customers entity set of type Customer; key CustomerID.`) stay on **MCP tool descriptions** only, never inside the shape.

---

## 5. Operations

- **Bound** = what you can do with the **type**. Listed on `odata_describe_type`, each type in `odata_describe_model` `complete`, and type cards.
- **Unbound** = what you can do with the **service**. Listed on `odata_list_operations` (unbound only) and at the **end** of `odata_describe_model` (summary and complete).

One `operations` list. Do not split functions vs actions. Do not emit `isBound` or `kind`. Do not prefix `Type.Name`. Mutating (EDM action) gets `// writes`. Collection-bound gets `// collection`. Parameter **types** are required. Binding parameter is omitted from the argument list.

`odata_call` is one object map; listing names **are** `parameters` keys. Normative call contract: [OPTIMIZATION.md](./OPTIMIZATION.md) §3 and §4.

---

## 6. Surfaces

### 6.1 `odata_describe_type`

Eager generic. `name` **required** (type or set). Do not overload omitted `name` into a whole-model dump.

| Argument | Values | Default |
|---|---|---|
| `name` | set or type name | required |
| `format` | `text` \| `json` | `text` |

Unknown `format` → tool error. One representation per call. No mermaid. Includes bound operations and enums. Unbound operations are not here.

### 6.2 `odata_describe_model`

Eager generic (11th). Never EDMX.

| Argument | Values | Default |
|---|---|---|
| `detail` | `summary` \| `complete` | `summary` |
| `format` | `text` \| `json` \| `mermaid` | `text` |
| `sets` | string[] | all included sets |

**`summary`:** every included set, key, navigations; no property lists; unbound operations at the end.

**`complete`:** every in-scope type in §1.1 grammar (properties, enums, bound ops, docs), then unbound operations. Complex types used by those entities included.

**`mermaid`:** relationship-only `erDiagram`. No attribute compartments. Even with `detail=complete`. Cardinality from collection vs single (`||--o{` / `||--o|`). Quote names that are not mermaid-safe.

Over `MaxResponseBytes`: **error** (byte count, set count, “pass `sets` or use `summary`”). No silent trim. No CSDL fallback.

Default is `summary` because models omit optional args; complete-by-default would ingest Graph.

### 6.3 Type cards (`resources/read` of a set)

Compact JSON (§1.2). Always JSON. No `format` argument. Same bound ops and enums as describe.

### 6.4 `odata_list_entity_sets`

Unchanged JSON name index: `{ entitySets: [{ name, entityType, keys, description? }] }`. No navigations. `description` only when present.

### 6.5 `odata_list_operations`

Unbound only. Compact signatures, parameter types, `// writes`, docs-when-present. No `isBound`, no `kind`. Empty → omit the section.

### 6.6 `$metadata`

CSDL XML. Escape hatch. Do not use it to explore. `ServerInstructions` says so ([OPTIMIZATION.md](./OPTIMIZATION.md) §1).

---

## 7. Named-tool schemas

Keep typed `create_*` and `update_*` schemas. Do not collapse create to `body: string`.

- `create_*`: property map + `"required"` per §3. Enum `enum` array. `description` only when CSDL has it. `maxLength` only when ≤ 16.
- `update_*`: `key` + the same map; `"required": ["key"]` only.
- `MaxNamedTools` stays 150 (searchable ceiling, not a context-dump cap). `IncludeEntitySets` still wins ordering.
- Generics eager, named deferred. Do not wait on a future MCP revision.

`odata_create` / `odata_update` stay generic (`entitySet` + `body` string) with the create/update sentences on the description.

---

## 8. Options

No `ShapeFormat` host switch. Per-call `format` on describe tools is enough. Type cards are JSON.

---

## 9. Tests

Payloads are locked with Breakdance baselines, not token counts in this file. See [OPTIMIZATION.md](./OPTIMIZATION.md) §7.
