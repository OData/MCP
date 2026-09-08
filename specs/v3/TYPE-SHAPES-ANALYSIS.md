# Type Shapes — Analysis (draft, not normative)

**Status:** brainstorm. Nothing here is implemented or approved. It exists so the discussion about how the
MCP surface describes OData types can start from what the model actually receives today, with token
costs attached, rather than from taste.

**Question asked:** are we exposing too much OData (navigations, `IsBound`, facets), should there be a
shorter format, and should string/number length restrictions be exposed at all?

**Short answer:** the describe format is the *visible* cost but not the *dominant* one. The dominant
cost is `tools/list`, which every MCP client re-sends on every model turn. Fix that first, then make
`describe` a compact declaration-style "shape", expose *semantic* constraints (enums, keys, required,
literal syntax) and skip *storage* constraints (length, precision, unicode, SRID) except where they are
short codes the model is likely to get wrong.

---

## 1. What the model receives today

Four surfaces describe types. Only the first is paid on every turn.

| Surface | When paid | Shape today | Source |
|---|---|---|---|
| `tools/list` | **Every request** (clients resend the full tool list) | 8 generic tools + up to 150 named tools. `create_{x}` carries a JSON Schema with every property (`type` + `description`); `list_{set}` repeats the 7-parameter query schema per set; `update_{x}` takes an untyped `body: string`. | `ODataMcpCatalog.BuildNamedFamily`, `BuildPropertySchema` |
| `odata_describe_type` | On demand | JSON: `description`, `entitySet`, `entityTypeDescription`, `keys[]`, `longDescription`, `name`, `namespace`, `navigations[{description,name,type}]`, `properties[{description,name,nullable,type}]` | `ODataToolRuntime.DescribeType` |
| Entity-set resource (type card) | On demand (`resources/read`) | Same as above minus `nullable`, plus set-level fields | `ODataMcpCatalog.BuildTypeCard` |
| `odata_list_operations` | On demand | `{description, isBound, kind, name, parameters[names], returnType}` per function/action | `ODataToolRuntime.ListOperations` |

Facts worth knowing before redesigning:

- **Length/precision facets are not exposed anywhere today** except inside the raw CSDL resource. `EdmProperty` carries `MaxLength`, `Precision`, `Scale`, `Unicode`, `DefaultValue`, `SRID`, but no surface reads them. So "should we expose them" is a question about *adding*, not removing.
- **Enum members are not exposed anywhere.** A property typed `NS.Color` shows up as the string `"NS.Color"`. The model cannot write `Color eq NS.Color'Red'` without guessing. This is a bigger correctness gap than any facet.
- **Property descriptions fall back to the property name.** `BuildPropertySchema` emits `"description": "CompanyName"` for `CompanyName` when the CSDL has no documentation — pure duplication, paid on every turn for every property of every `create_*` tool.
- **Descriptions are tripled.** `describe` returns `description`, `entityTypeDescription`, and `longDescription`, which are usually the same string or null.
- **Types are fully qualified.** `Edm.String`, `Collection(NorthwindModel.Order)`. The namespace carries no information the model can act on unless the model has several namespaces.

---

## 2. Where the tokens go (estimates, 1 token ≈ 4 chars, Northwind `Customer`: 11 properties, 2 navigations)

### 2.1 `tools/list` — the always-on cost

| Item | Approx. tokens | Notes |
|---|---|---|
| `list_{set}` input schema | ~90 | identical for every set; 150 sets → ~13,500 tokens of repetition |
| `create_{x}` input schema, 11 props | ~250 | `"CompanyName":{"type":"string","description":"CompanyName"}` ≈ 20 tokens × 11 |
| `create_{x}` for a 40-property type | ~850 | wide tables are common in line-of-business APIs |
| One CRUD family (list/get/create/update/delete), 11 props | ~550 | name + title + description + schema + hints |
| **150 named tools at the cap** | **~18,000–30,000** | paid on **every** model turn, before the model has said a word |

For comparison, one `odata_describe_type` result for `Customer` is ~420 tokens and is paid once, when asked.

**Conclusion:** the describe format could be made free and the session would still pay ~25k tokens a turn at the named-tool cap. Any spec that only changes `describe` is optimizing the wrong thing.

### 2.2 `odata_describe_type` today vs. a compact shape

Today (abridged, real key names):

```json
{"description":null,"entitySet":"Customers","entityTypeDescription":null,"keys":["CustomerID"],
 "longDescription":null,"name":"Customer","namespace":"NorthwindModel",
 "navigations":[{"description":null,"name":"Orders","type":"Collection(NorthwindModel.Order)"},
                {"description":null,"name":"CustomerDemographics","type":"Collection(NorthwindModel.CustomerDemographic)"}],
 "properties":[{"description":null,"name":"CustomerID","nullable":false,"type":"Edm.String"},
               {"description":null,"name":"CompanyName","nullable":false,"type":"Edm.String"},
               ... 9 more ...]}
```
≈ 420 tokens. Per property ≈ 28 tokens, of which the information content is "name, string, required".

Compact declaration-style shape (the format the model has seen most of in training — TypeScript/C#
declarations):

```
Customer  (set: Customers, key: CustomerID)
  CustomerID: string(5)      // key
  CompanyName: string
  ContactName?: string
  ContactTitle?: string
  Address?: string
  City?: string
  Region?: string
  PostalCode?: string
  Country?: string
  Phone?: string
  Fax?: string
  Orders -> Order[]
  CustomerDemographics -> CustomerDemographic[]
```
≈ 110 tokens. Same information plus the one facet that matters (`CustomerID` is a 5-char code). **~74% fewer tokens**, and it reads the way the model writes code.

Compact JSON (if a machine-readable form is required — e.g. for resources):

```json
{"Customer":{"set":"Customers","key":["CustomerID"],
 "props":{"CustomerID":"string(5)!","CompanyName":"string!","ContactName":"string","City":"string", ...},
 "navs":{"Orders":"Order[]","CustomerDemographics":"CustomerDemographic[]"}}}
```
≈ 150 tokens. Still ~65% smaller than today; keeps `resources/read` JSON.

---

## 3. Answers to the specific questions

### 3.1 Navigations — keep, but make them one line each

Navigations *are* the graph. They are the only way the model learns that `expand=Orders` and
`odata_navigate(Customers, ALFKI, Orders)` are legal, and they are what makes multi-hop questions
("orders for German customers") answerable without a tour of the metadata. Removing them would push the
model back to guessing names. What should go is the ceremony: an object with a null `description` and a
`Collection(NS.Type)` string collapses to `Orders -> Order[]`. `Partner`, `ContainsTarget`, `OnDelete`,
and referential constraints are not exposed today and should stay that way; none change what the model
can *ask for*.

### 3.2 `IsBound` — replace the boolean with the binding target

A boolean tells the model *that* something is bound, not *what to*; it still has to guess the binding
parameter. The useful fact is the target: `Products/MostExpensive()` or
`boundTo: Product` with the call shape. Proposed operations listing:

```
functions
  GetProductsByRating(rating: int) -> Product[]                 // unbound
  Product.GetRelated(count?: int) -> Product[]                  // bound to Product
actions
  Product.Discount(percentage: int) -> Product                  // bound to Product
```
`kind` matters (GET vs POST, parameters in URL vs body) and is expressed by the section, not a field.
Parameter *types* matter and are missing today (only names are listed) — add them; they are cheap and
they are exactly what the model needs to write the call.

### 3.3 Length / precision / scale — expose almost nothing, and only where the model would fail

Split constraints into two kinds:

- **Semantic constraints** change *what the model writes*: key, required (non-nullable without default),
  enum members, type family (string/number/bool/date/datetime/guid/duration), collection-ness, and the
  OData literal syntax that goes with the type. These belong in every shape.
- **Storage constraints** change *whether the server accepts it*: `MaxLength`, `Precision`, `Scale`,
  `Unicode`, `SRID`, `DefaultValue`. The model almost never violates a `MaxLength(200)` on a name field,
  and when it does the server's 400 already names the property — that feedback loop costs nothing until
  it is needed. Exposing them on every property on every turn is paying insurance on a risk that is
  nearly zero.

Recommendation: expose `MaxLength` **only when it is small** (proposed threshold ≤ 16), because short
fixed-width codes (`CustomerID` nchar(5), `CountryCode` char(2), `Currency` char(3)) are precisely the
fields a model invents plausible-but-too-long values for. Render it inline as `string(5)` — two tokens.
Never expose `Precision`/`Scale`/`Unicode`/`SRID` in shapes; they stay in the CSDL resource for anyone
who wants them. `DefaultValue` is interesting only to mark a non-nullable property as *optional on
create*; that can be folded into the `?` marker rather than shown.

Related: JSON Schema on `create_*` tools *could* carry `maxLength`. Same rule: only for the short codes,
never as a blanket. It is worth more there than in `describe`, because the create schema is what the
model fills in.

### 3.4 Enums — the missing piece

This is the one *addition* the analysis argues for. `Color: enum(Red|Green|Blue)` (plus the literal form
once at the top of the shape: `enum literal: NS.Color'Red'`) costs a handful of tokens and removes an
entire class of failed `$filter`s. Today enum members are unreachable except by reading the CSDL.

### 3.5 Descriptions — one field, only when it exists

`description` / `longDescription` / `entityTypeDescription` collapse to a single trailing comment, only
when the CSDL actually has documentation. Never fall back to the name.

---

## 4. The bigger lever: the always-on tool list

Options, cheapest first. They are independent; the first two are near-free.

1. **Stop echoing names as descriptions** in `BuildPropertySchema`. Immediate ~40% cut on every
   `create_*` schema, zero information lost.
2. **Make `update_*` and `create_*` consistent.** Today create has a typed schema and update has
   `body: string`. Either both get the schema (doubles the cost) or both take a body and say "shape via
   `odata_describe_type`" (halves it). Given the numbers above, the second is the token-rational choice,
   and it makes `describe` the single place types are learned — which is what §2–3 optimize.
3. **Lower the default `MaxNamedTools`** (150 today). Named tools are convenience aliases over generics;
   a model with `odata_query` plus a good `describe` does not need `list_shippers`. Named families should
   default to `IncludeEntitySets` only, with a small alphabetical fill (e.g. 40 total), not 150.
4. **Progressive disclosure** (needs client support): MCP is growing tool-search / deferred-tool
   loading (this very session runs on it). Publishing generics eagerly and named tools as searchable
   would make the cap irrelevant. Worth tracking, not worth building until clients ship it.

---

## 5. What a spec would contain

Working title `TYPE-SHAPES.md`. Sections:

1. **Shape grammar** — the declaration format (§2.2), formally: header line, property lines
   (`Name?: type(facet) // doc`), navigation lines (`Name -> Type[]`), enum rendering, key marker,
   one-line literal-syntax legend at the top of every shape (string quotes, guid bare, date `2024-01-31`,
   datetime `2024-01-31T00:00:00Z`, enum `NS.E'Member'`, duration `duration'PT1H'`).
2. **Type mapping table** — EDM primitive → shape type name, and which get a literal hint.
3. **Facet policy** — the semantic/storage split (§3.3), the `MaxLength ≤ 16` rule, enum members always.
4. **Where each shape appears** — `odata_describe_type` (text), type-card resources (text or compact
   JSON — decide), `odata_list_entity_sets` (one line per set: `Customers: Customer, key CustomerID`),
   `odata_list_operations` (§3.2).
5. **Tool-list policy** — items §4.1–4.3 with new defaults, and the rule that named tools never embed
   per-property schemas beyond short-code `maxLength`.
6. **Measurement** — required before merging: token counts (real tokenizer, not chars/4) for
   `tools/list`, `describe`, and a 5-question task suite on Northwind, TripPin, and one wide line-of-
   business model, before/after; plus a correctness check that the model's generated `$filter`s and
   create bodies succeed at least as often as today. Token savings that cost accuracy are a regression.
7. **Compatibility** — the CSDL resource remains the full-fidelity escape hatch; `ODataMcpCatalogOptions`
   gets `ShapeFormat` (Compact | Json) so the JSON cards can be kept for anyone parsing them.

---

## 6. Open decisions (need a human)

| # | Decision | Recommendation |
|---|---|---|
| 1 | Text shape vs compact JSON as the default for `describe` | Text. Nothing client-side parses it; the model does. |
| 2 | Keep JSON for `resources/read` type cards? | Yes — resources are the one place a client might parse. Same content, compact keys. |
| 3 | `MaxLength` threshold | ≤ 16. Tune with data. |
| 4 | `update_*` gets a schema, or `create_*` loses it? | `create_*` loses the per-property schema; both say "see describe". |
| 5 | Default `MaxNamedTools` | 40 (generics + ~7 families), `IncludeEntitySets` first. |
| 6 | Show navigation cardinality only via `[]`, or also `1`/`0..1`? | `[]` only; the `?` on a single-valued nav covers optionality. |
| 7 | Namespace | Once, in the shape header, only when the model has more than one. |
| 8 | Bound operations listed under their type or in a flat list? | Flat list with `Type.Name(...)` prefix — searchable and short. |
